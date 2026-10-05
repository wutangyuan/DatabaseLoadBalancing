using DatabaseLoadBalancing.Data.Sqlite;
using Microsoft.Data.Sqlite;

namespace DatabaseLoadBalancing.Server.IntegrationTests;

public sealed class ServiceHostTests
{
    [Fact]
    public void DatabaseSchema_InitializesInternalExecutionTable()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"database-load-balancing-server-{Guid.NewGuid():N}.db");
        try
        {
            var factory = new SqliteConnectionFactory(databasePath);
            new DatabaseSchemaInitializer(factory).Initialize();
            using (var connection = factory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT name FROM sqlite_master WHERE name = '__request_execution'";
                Assert.Equal("__request_execution", command.ExecuteScalar());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists($"{databasePath}-wal")) File.Delete($"{databasePath}-wal");
            if (File.Exists($"{databasePath}-shm")) File.Delete($"{databasePath}-shm");
        }
    }
}

