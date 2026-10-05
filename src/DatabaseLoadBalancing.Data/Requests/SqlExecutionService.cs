using System.Security.Cryptography;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Data.Abstractions;
using DatabaseLoadBalancing.Data.Sqlite;
using log4net;

namespace DatabaseLoadBalancing.Data.Requests;

public sealed class SqlExecutionService : IDisposable
{
    private readonly IDatabaseProvider _provider;
    private readonly RequestExecutionRepository _executionRepository;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly ILog _logger = LogManager.GetLogger(typeof(SqlExecutionService));
    private bool _disposed;

    public SqlExecutionService(IDatabaseProvider provider)
    {
        _provider = provider;
        _executionRepository = new RequestExecutionRepository(provider.Dialect);
        new DatabaseSchemaInitializer(provider).Initialize();
    }

    public async Task<SqlExecutionResult> ExecuteQueryAsync(SqlRequest request, CancellationToken cancellationToken = default)
    {
        var validation = SqlCommandPolicy.Validate(request.Sql, mutation: false);
        if (!validation.IsValid)
        {
            _logger.Warn($"SQL query request {request.RequestId} rejected: {validation.ErrorCode}.");
            return SqlExecutionResult.Failed(request.RequestId, validation.ErrorCode!, validation.ErrorMessage!);
        }
        var parameterValidation = SqlCommandPolicy.ValidateParameters(validation.Sql, request.Parameters);
        if (!parameterValidation.IsValid)
        {
            _logger.Warn($"SQL query request {request.RequestId} rejected: {parameterValidation.ErrorCode}.");
            return SqlExecutionResult.Failed(request.RequestId, parameterValidation.ErrorCode!, parameterValidation.ErrorMessage!);
        }

        try
        {
            await using var connection = _provider.OpenConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = validation.Sql;
            DbParameterBinder.Bind(command, request.Parameters);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = await ReadRowsAsync(request.RequestId, reader, cancellationToken);
            LogSuccess("query", request.RequestId, request.Sql, result.RowsAffected);
            return result;
        }
        catch (Exception exception)
        {
            LogFailure("query", request.RequestId, exception);
            return SqlExecutionResult.Failed(request.RequestId, "database.query", exception.Message, IsBusy(exception));
        }
    }

    public async Task<SqlExecutionResult> ExecuteScalarAsync(SqlRequest request, CancellationToken cancellationToken = default)
    {
        var validation = SqlCommandPolicy.Validate(request.Sql, mutation: false);
        if (!validation.IsValid)
        {
            _logger.Warn($"SQL scalar request {request.RequestId} rejected: {validation.ErrorCode}.");
            return SqlExecutionResult.Failed(request.RequestId, validation.ErrorCode!, validation.ErrorMessage!);
        }
        var parameterValidation = SqlCommandPolicy.ValidateParameters(validation.Sql, request.Parameters);
        if (!parameterValidation.IsValid)
        {
            _logger.Warn($"SQL scalar request {request.RequestId} rejected: {parameterValidation.ErrorCode}.");
            return SqlExecutionResult.Failed(request.RequestId, parameterValidation.ErrorCode!, parameterValidation.ErrorMessage!);
        }

        try
        {
            await using var connection = _provider.OpenConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = validation.Sql;
            DbParameterBinder.Bind(command, request.Parameters);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            var result = new SqlExecutionResult
            {
                Success = true,
                RequestId = request.RequestId,
                Columns = ["value"],
                Rows = [new Dictionary<string, object?> { ["value"] = value is DBNull ? null : value }],
                RowsAffected = 1
            };
            LogSuccess("scalar", request.RequestId, request.Sql, result.RowsAffected);
            return result;
        }
        catch (Exception exception)
        {
            LogFailure("scalar", request.RequestId, exception);
            return SqlExecutionResult.Failed(request.RequestId, "database.scalar", exception.Message, IsBusy(exception));
        }
    }

    public async Task<SqlExecutionResult> ExecuteMutationAsync(SqlRequest request, CancellationToken cancellationToken = default)
    {
        var validation = SqlCommandPolicy.Validate(request.Sql, mutation: true);
        if (!validation.IsValid)
        {
            _logger.Warn($"SQL mutation request {request.RequestId} rejected: {validation.ErrorCode}.");
            return SqlExecutionResult.Failed(request.RequestId, validation.ErrorCode!, validation.ErrorMessage!);
        }
        var parameterValidation = SqlCommandPolicy.ValidateParameters(validation.Sql, request.Parameters);
        if (!parameterValidation.IsValid)
        {
            _logger.Warn($"SQL mutation request {request.RequestId} rejected: {parameterValidation.ErrorCode}.");
            return SqlExecutionResult.Failed(request.RequestId, parameterValidation.ErrorCode!, parameterValidation.ErrorMessage!);
        }

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = _provider.OpenConnection();
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var requestHash = CreateHash(request.Sql, request.Parameters);
            var existing = _executionRepository.Find(connection, request.RequestId, transaction);
            if (existing is not null)
            {
                if (existing.RequestHash != requestHash)
                {
                    _logger.Warn($"SQL mutation request {request.RequestId} conflicted with an existing request hash.");
                    return SqlExecutionResult.Failed(request.RequestId, "request.reused", "RequestId conflicts with an existing request.");
                }

                return existing.RequestHash == requestHash
                    ? Successful(request.RequestId, existing.AffectedRows)
                    : SqlExecutionResult.Failed(request.RequestId, "request.reused", "RequestId 已被其他 SQL 请求使用。");
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = validation.Sql;
            DbParameterBinder.Bind(command, request.Parameters);
            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
            _executionRepository.Add(connection, transaction, request.RequestId, requestHash, "mutation", "succeeded", affectedRows);
            await transaction.CommitAsync(cancellationToken);
            var result = Successful(request.RequestId, affectedRows);
            LogSuccess("mutation", request.RequestId, request.Sql, affectedRows);
            return result;
        }
        catch (Exception exception)
        {
            LogFailure("mutation", request.RequestId, exception);
            return SqlExecutionResult.Failed(request.RequestId, "database.mutation", exception.Message, IsBusy(exception));
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<SqlExecutionResult> ExecuteTransactionAsync(SqlTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Commands.Count == 0)
        {
            _logger.Warn($"SQL transaction request {request.RequestId} rejected because it has no commands.");
            return SqlExecutionResult.Failed(request.RequestId, "transaction.empty", "事务至少需要一条 SQL。");
        }

        var validations = request.Commands.Select(command => SqlCommandPolicy.Validate(command.Sql, mutation: true)).ToArray();
        var invalid = validations.FirstOrDefault(result => !result.IsValid);
        if (invalid is not null)
        {
            _logger.Warn($"SQL transaction request {request.RequestId} rejected: {invalid.ErrorCode}.");
            return SqlExecutionResult.Failed(request.RequestId, invalid.ErrorCode!, invalid.ErrorMessage!);
        }
        var invalidParameters = request.Commands.Select((command, index) => SqlCommandPolicy.ValidateParameters(validations[index].Sql, command.Parameters))
            .FirstOrDefault(result => !result.IsValid);
        if (invalidParameters is not null)
        {
            _logger.Warn($"SQL transaction request {request.RequestId} rejected: {invalidParameters.ErrorCode}.");
            return SqlExecutionResult.Failed(request.RequestId, invalidParameters.ErrorCode!, invalidParameters.ErrorMessage!);
        }

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = _provider.OpenConnection();
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var requestHash = CreateTransactionHash(request);
            var existing = _executionRepository.Find(connection, request.RequestId, transaction);
            if (existing is not null)
            {
                if (existing.RequestHash != requestHash)
                {
                    _logger.Warn($"SQL transaction request {request.RequestId} conflicted with an existing request hash.");
                    return SqlExecutionResult.Failed(request.RequestId, "request.reused", "RequestId conflicts with an existing transaction.");
                }

                return existing.RequestHash == requestHash
                    ? Successful(request.RequestId, existing.AffectedRows)
                    : SqlExecutionResult.Failed(request.RequestId, "request.reused", "RequestId 已被其他事务使用。");
            }

            var affectedRows = 0;
            for (var index = 0; index < request.Commands.Count; index++)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = validations[index].Sql;
                DbParameterBinder.Bind(command, request.Commands[index].Parameters);
                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            _executionRepository.Add(connection, transaction, request.RequestId, requestHash, "transaction", "succeeded", affectedRows);
            await transaction.CommitAsync(cancellationToken);
            var result = Successful(request.RequestId, affectedRows);
            LogSuccess("transaction", request.RequestId, $"{request.Commands.Count} commands", affectedRows);
            return result;
        }
        catch (Exception exception)
        {
            LogFailure("transaction", request.RequestId, exception);
            return SqlExecutionResult.Failed(request.RequestId, "database.transaction", exception.Message, IsBusy(exception));
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<SqlExecutionResult> GetRequestOutcomeAsync(string requestId, CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.OpenConnection();
        var existing = _executionRepository.Find(connection, requestId);
        _logger.Info($"Request outcome lookup request={requestId} found={existing is not null}.");
        return existing is null
            ? SqlExecutionResult.Failed(requestId, "request.not_found", "未找到请求执行记录。")
            : Successful(requestId, existing.AffectedRows);
    }

    private static async Task<SqlExecutionResult> ReadRowsAsync(string requestId, DbDataReader reader, CancellationToken cancellationToken)
    {
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var name = columns[index];
                if (!row.TryAdd(name, reader.IsDBNull(index) ? null : reader.GetValue(index)))
                {
                    row[$"{name}_{index}"] = reader.IsDBNull(index) ? null : reader.GetValue(index);
                }
            }

            rows.Add(row);
        }

        return new SqlExecutionResult
        {
            Success = true,
            RequestId = requestId,
            Columns = columns,
            Rows = rows,
            RowsAffected = rows.Count
        };
    }

    private static SqlExecutionResult Successful(string requestId, int affectedRows) => new()
    {
        Success = true,
        RequestId = requestId,
        RowsAffected = affectedRows
    };

    private static string CreateHash(string sql, IReadOnlyDictionary<string, object?> parameters)
    {
        var payload = JsonSerializer.Serialize(new { Sql = sql.Trim(), Parameters = parameters });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static string CreateTransactionHash(SqlTransactionRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            Commands = request.Commands.Select(command => new { command.Sql, command.Parameters }).ToArray()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private bool IsBusy(Exception exception) => _provider.Dialect.IsBusy(exception);

    private void LogFailure(string operation, string requestId, Exception exception)
    {
        var message = $"Database {_provider.ProviderName} {operation} failed for request {requestId}.";
        if (IsBusy(exception)) _logger.Warn(message);
        else _logger.Error(message, exception);
    }

    private void LogSuccess(string operation, string requestId, string sqlShape, int affectedRows)
    {
        _logger.Info($"SQL {operation} request={requestId} affectedRows={affectedRows} sql=\"{PreviewSql(sqlShape)}\".");
    }

    private static string PreviewSql(string sql)
    {
        var preview = string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return preview.Length <= 240 ? preview : $"{preview[..240]}...";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writeGate.Dispose();
        _disposed = true;
    }
}

