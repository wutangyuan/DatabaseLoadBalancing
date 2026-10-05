using System.Data.Common;
using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Contracts.Tables;
using DatabaseLoadBalancing.Data.Abstractions;
using DatabaseLoadBalancing.Data.Requests;
using RequestPolicy = DatabaseLoadBalancing.Data.Requests.SqlCommandPolicy;

namespace DatabaseLoadBalancing.Data.Tables;

public sealed class TableMaintenanceService
{
    private readonly IDatabaseProvider _provider;
    private readonly SqlExecutionService _sqlExecutionService;

    public TableMaintenanceService(IDatabaseProvider provider, SqlExecutionService sqlExecutionService)
    {
        _provider = provider;
        _sqlExecutionService = sqlExecutionService;
    }

    public async Task<IReadOnlyList<TableMetadata>> ListTablesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = _provider.Dialect.TablesQuery;
        var tables = new List<TableMetadata>();
        var tableNames = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var tableName = reader.GetString(0);
                if (!_provider.Dialect.IsInfrastructureTable(tableName)) tableNames.Add(tableName);
            }
        }

        foreach (var tableName in tableNames)
        {
            tables.Add(await GetMetadataAsync(connection, tableName, cancellationToken));
        }

        return tables;
    }

    public async Task<TableDataResponse> GetTableAsync(string tableName, int limit = 200, int offset = 0, CancellationToken cancellationToken = default)
    {
        ValidateTableName(tableName);
        limit = Math.Clamp(limit, 1, 1000);
        offset = Math.Max(0, offset);
        await using var connection = _provider.OpenConnection();
        var metadata = await GetMetadataAsync(connection, tableName, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = _provider.Dialect.BuildRowsQuery(_provider.Dialect.QuoteIdentifier(tableName));
        DbParameterBinder.Add(command, "@limit", limit);
        DbParameterBinder.Add(command, "@offset", offset);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<TableRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var record = new TableRecord { RowId = reader.GetInt64(0) };
            for (var index = 1; index < reader.FieldCount; index++)
            {
                record.Values[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            records.Add(record);
        }

        return new TableDataResponse { Table = metadata, Records = records };
    }

    public Task<SqlExecutionResult> CreateRecordAsync(string tableName, TableRecordRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTableName(tableName);
        if (request.Values.Count == 0 || request.Values.Keys.Any(key => !RequestPolicy.IsValidIdentifier(key)))
        {
            return Task.FromResult(SqlExecutionResult.Failed(string.Empty, "record.invalid", "记录字段不能为空且必须是合法列名。"));
        }

        var columns = request.Values.Keys.ToArray();
        var parameters = columns.Select((column, index) => new { column, name = $"@p{index}" }).ToArray();
        var sql = $"INSERT INTO {_provider.Dialect.QuoteIdentifier(tableName)} ({string.Join(", ", columns.Select(_provider.Dialect.QuoteIdentifier))}) VALUES ({string.Join(", ", parameters.Select(item => item.name))})";
        return _sqlExecutionService.ExecuteMutationAsync(new SqlRequest
        {
            RequestId = request.RequestId,
            Sql = sql,
            Parameters = parameters.ToDictionary(item => item.name, item => request.Values[item.column])
        }, cancellationToken);
    }

    public Task<SqlExecutionResult> UpdateRecordAsync(string tableName, long rowId, TableRecordRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTableName(tableName);
        if (request.Values.Count == 0 || request.Values.Keys.Any(key => !RequestPolicy.IsValidIdentifier(key)))
        {
            return Task.FromResult(SqlExecutionResult.Failed(string.Empty, "record.invalid", "记录字段不能为空且必须是合法列名。"));
        }

        var columns = request.Values.Keys.ToArray();
        var parameters = columns.Select((column, index) => new { column, name = $"@p{index}" }).ToArray();
        var assignments = string.Join(", ", parameters.Select(item => $"{_provider.Dialect.QuoteIdentifier(item.column)} = {item.name}"));
        var sql = _provider.Dialect.BuildUpdateRecordQuery(_provider.Dialect.QuoteIdentifier(tableName), assignments);
        var values = parameters.ToDictionary(item => item.name, item => request.Values[item.column]);
        values["@rowid"] = rowId;
        return _sqlExecutionService.ExecuteMutationAsync(new SqlRequest { RequestId = request.RequestId, Sql = sql, Parameters = values }, cancellationToken);
    }

    public Task<SqlExecutionResult> DeleteRecordAsync(string tableName, long rowId, string? requestId = null, CancellationToken cancellationToken = default)
    {
        ValidateTableName(tableName);
        return _sqlExecutionService.ExecuteMutationAsync(new SqlRequest
        {
            RequestId = requestId ?? Guid.NewGuid().ToString("N"),
            Sql = _provider.Dialect.BuildDeleteRecordQuery(_provider.Dialect.QuoteIdentifier(tableName)),
            Parameters = new Dictionary<string, object?> { ["@rowid"] = rowId }
        }, cancellationToken);
    }

    public async Task<ClearTableResponse> ClearTableAsync(string tableName, string? requestId = null, CancellationToken cancellationToken = default)
    {
        ValidateTableName(tableName);
        var current = await GetTableAsync(tableName, 1, 0, cancellationToken);
        var before = current.Table.RecordCount;
        var result = await _sqlExecutionService.ExecuteMutationAsync(new SqlRequest
        {
            RequestId = requestId ?? Guid.NewGuid().ToString("N"),
            Sql = $"DELETE FROM {_provider.Dialect.QuoteIdentifier(tableName)}"
        }, cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Error?.Message ?? "清空表失败。");
        }

        return new ClearTableResponse { TableName = tableName, PreviousRecordCount = before, DeletedRecordCount = result.RowsAffected };
    }

    private async Task<TableMetadata> GetMetadataAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = _provider.Dialect.BuildCountQuery(_provider.Dialect.QuoteIdentifier(tableName));
        var count = Convert.ToInt64(await countCommand.ExecuteScalarAsync(cancellationToken));
        await using var columnCommand = connection.CreateCommand();
        columnCommand.CommandText = _provider.Dialect.BuildColumnsQuery(_provider.Dialect.QuoteIdentifier(tableName));
        await using var reader = await columnCommand.ExecuteReaderAsync(cancellationToken);
        var columns = new List<TableColumnMetadata>();
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new TableColumnMetadata
            {
                Name = reader.GetString(1),
                DisplayName = reader.GetString(1),
                DataType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                IsNullable = reader.GetInt32(3) == 0,
                IsPrimaryKey = reader.GetInt32(5) != 0,
                IsRequired = reader.GetInt32(3) != 0 || reader.GetInt32(5) != 0,
                IsEditable = reader.GetInt32(5) == 0
            });
        }

        return new TableMetadata
        {
            Name = tableName,
            TableId = tableName,
            DisplayName = tableName,
            RecordCount = count,
            Columns = columns,
            PrimaryKeyColumns = columns.Where(column => column.IsPrimaryKey).Select(column => column.Name).ToList()
        };
    }

    private static void ValidateTableName(string tableName)
    {
        if (!RequestPolicy.IsValidIdentifier(tableName) || tableName.StartsWith("__", StringComparison.Ordinal))
        {
            throw new ArgumentException("表名不合法。", nameof(tableName));
        }
    }

}

