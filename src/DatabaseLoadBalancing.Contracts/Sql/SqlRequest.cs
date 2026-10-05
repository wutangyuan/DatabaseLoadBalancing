namespace DatabaseLoadBalancing.Contracts.Sql;

public sealed class SqlRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

    public string Operation { get; set; } = "execute";

    public string Sql { get; set; } = string.Empty;

    public int TimeoutMs { get; set; } = 5000;

    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SqlCommandRequest
{
    public string Sql { get; set; } = string.Empty;

    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SqlTransactionRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

    public int TimeoutMs { get; set; } = 5000;

    public List<SqlCommandRequest> Commands { get; set; } = [];
}

public sealed class SqlExecutionResult
{
    public bool Success { get; set; }

    public string RequestId { get; set; } = string.Empty;

    public int RowsAffected { get; set; }

    public List<string> Columns { get; set; } = [];

    public List<Dictionary<string, object?>> Rows { get; set; } = [];

    public SqlError? Error { get; set; }

    public string? ErrorCode => Error?.Code;

    public string? ErrorMessage => Error?.Message;

    public bool Retryable => Error?.Retryable == true;

    public long DurationMs { get; set; }

    public static SqlExecutionResult Failed(string requestId, string code, string message, bool retryable = false) => new()
    {
        RequestId = requestId,
        Error = new SqlError { Code = code, Message = message, Retryable = retryable }
    };
}

public sealed class SqlError
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool Retryable { get; set; }
}

