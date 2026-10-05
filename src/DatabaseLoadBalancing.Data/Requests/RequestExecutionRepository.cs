using System.Data.Common;
using DatabaseLoadBalancing.Data.Abstractions;

namespace DatabaseLoadBalancing.Data.Requests;

public sealed class RequestExecutionRepository
{
    private readonly IDatabaseDialect _dialect;

    public RequestExecutionRepository(IDatabaseDialect dialect)
    {
        _dialect = dialect;
    }

    public RequestExecution? Find(DbConnection connection, string requestId, DbTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {_dialect.QuoteIdentifier("request_id")}, {_dialect.QuoteIdentifier("request_hash")}, {_dialect.QuoteIdentifier("operation")}, {_dialect.QuoteIdentifier("status")}, {_dialect.QuoteIdentifier("affected_rows")} FROM {_dialect.QuoteIdentifier("__request_execution")} WHERE {_dialect.QuoteIdentifier("request_id")} = @request_id;";
        AddParameter(command, "@request_id", requestId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new RequestExecution(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            Convert.ToInt32(reader.GetValue(4)));
    }

    public void Add(
        DbConnection connection,
        DbTransaction transaction,
        string requestId,
        string requestHash,
        string operation,
        string status,
        int affectedRows)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO {_dialect.QuoteIdentifier("__request_execution")} ({_dialect.QuoteIdentifier("request_id")}, {_dialect.QuoteIdentifier("request_hash")}, {_dialect.QuoteIdentifier("operation")}, {_dialect.QuoteIdentifier("status")}, {_dialect.QuoteIdentifier("affected_rows")}, {_dialect.QuoteIdentifier("created_at")}, {_dialect.QuoteIdentifier("completed_at")}) VALUES (@request_id, @request_hash, @operation, @status, @affected_rows, @now, @now);";
        AddParameter(command, "@request_id", requestId);
        AddParameter(command, "@request_hash", requestHash);
        AddParameter(command, "@operation", operation);
        AddParameter(command, "@status", status);
        AddParameter(command, "@affected_rows", affectedRows);
        AddParameter(command, "@now", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

public sealed record RequestExecution(
    string RequestId,
    string RequestHash,
    string Operation,
    string Status,
    int AffectedRows);

