using DatabaseLoadBalancing.Data.Abstractions;

namespace DatabaseLoadBalancing.Data.Sqlite;

public sealed class SqliteDialect : IDatabaseDialect
{
    public string ProviderName => "Sqlite";

    public string RequestExecutionTableSql => """
        CREATE TABLE IF NOT EXISTS "__request_execution" (
            "request_id" TEXT NOT NULL PRIMARY KEY,
            "request_hash" TEXT NOT NULL,
            "operation" TEXT NOT NULL,
            "status" TEXT NOT NULL,
            "affected_rows" INTEGER NOT NULL DEFAULT 0,
            "error_code" TEXT NULL,
            "error_message" TEXT NULL,
            "created_at" TEXT NOT NULL,
            "completed_at" TEXT NOT NULL
        );
        """;

    public string TablesQuery => """
        SELECT name FROM sqlite_master
        WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND substr(name, 1, 2) <> '__'
        ORDER BY name;
        """;

    public string BuildCountQuery(string quotedTableName) => $"SELECT COUNT(*) FROM {quotedTableName};";

    public string BuildColumnsQuery(string quotedTableName) => $"PRAGMA table_info({quotedTableName});";

    public string BuildRowsQuery(string quotedTableName) => $"SELECT rowid AS \"__rowid\", * FROM {quotedTableName} LIMIT @limit OFFSET @offset;";

    public string BuildUpdateRecordQuery(string quotedTableName, string assignments) =>
        $"UPDATE {quotedTableName} SET {assignments} WHERE rowid = @rowid";

    public string BuildDeleteRecordQuery(string quotedTableName) =>
        $"DELETE FROM {quotedTableName} WHERE rowid = @rowid";

    public string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    public bool IsInfrastructureTable(string tableName) => tableName.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase) ||
                                                            tableName.StartsWith("__", StringComparison.Ordinal);

    public bool IsBusy(Exception exception) => exception.Message.Contains("busy", StringComparison.OrdinalIgnoreCase) ||
                                                exception.Message.Contains("locked", StringComparison.OrdinalIgnoreCase);
}

