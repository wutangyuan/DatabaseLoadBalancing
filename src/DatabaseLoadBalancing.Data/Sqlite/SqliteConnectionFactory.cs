using System.Data.Common;
using DatabaseLoadBalancing.Data.Abstractions;
using Microsoft.Data.Sqlite;

namespace DatabaseLoadBalancing.Data.Sqlite;

public sealed class SqliteConnectionFactory : IDatabaseProvider
{
    public SqliteConnectionFactory(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public string ProviderName => "Sqlite";

    public IDatabaseDialect Dialect { get; } = new SqliteDialect();

    DbConnection IDatabaseProvider.OpenConnection() => OpenConnection();

    public SqliteConnection OpenConnection()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(DatabasePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5,
            Pooling = true
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        ExecutePragma(connection, "PRAGMA busy_timeout = 5000;");
        ExecutePragma(connection, "PRAGMA foreign_keys = ON;");
        ExecutePragma(connection, "PRAGMA journal_mode = WAL;");
        return connection;
    }

    private static void ExecutePragma(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

