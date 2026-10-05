namespace DatabaseLoadBalancing.Data.Sqlite;

public static class SqlCommandPolicy
{
    public static Requests.SqlValidationResult Validate(string? sql, bool mutation) => Requests.SqlCommandPolicy.Validate(sql, mutation);

    public static Requests.SqlValidationResult ValidateParameters(string sql, IReadOnlyDictionary<string, object?> parameters) =>
        Requests.SqlCommandPolicy.ValidateParameters(sql, parameters);

    public static bool IsValidIdentifier(string value) => Requests.SqlCommandPolicy.IsValidIdentifier(value);
}

