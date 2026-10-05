using DatabaseLoadBalancing.Data.Sqlite;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Comparison;

public sealed class DirectSqliteBaselineTests
{
    [Fact]
    public async Task DirectRunner_UsesOnlyDisposableDatabaseCopy()
    {
        var path = Path.Combine(Path.GetTempPath(), $"database-load-balancing-direct-{Guid.NewGuid():N}.db");
        try
        {
            var factory = new SqliteConnectionFactory(path);
            using (var connection = factory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE items (id INTEGER PRIMARY KEY AUTOINCREMENT, value TEXT UNIQUE NOT NULL)";
                command.ExecuteNonQuery();
            }

            var result = await new DirectSqliteBaselineRunner().RunAsync(path, 10, 100);
            Assert.Equal(100, result.Total);
            Assert.Equal(result.Success, result.FinalRowCount);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in new[] { path, $"{path}-wal", $"{path}-shm" }) if (File.Exists(file)) File.Delete(file);
        }
    }
}

