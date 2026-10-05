using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Data.Requests;
using DatabaseLoadBalancing.Data.Sqlite;
using Microsoft.Data.Sqlite;

namespace DatabaseLoadBalancing.Data.UnitTests;

public sealed class TransactionRollbackTests
{
    [Fact]
    public async Task TransactionFailure_RollsBackEarlierCommands()
    {
        var path = Path.Combine(Path.GetTempPath(), $"database-load-balancing-rollback-{Guid.NewGuid():N}.db");
        try
        {
            var factory = new SqliteConnectionFactory(path);
            using (var connection = factory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE items (value TEXT NOT NULL)";
                command.ExecuteNonQuery();
            }

            using var service = new SqlExecutionService(factory);
            var result = await service.ExecuteTransactionAsync(new SqlTransactionRequest
            {
                Commands =
                [
                    new SqlCommandRequest { Sql = "INSERT INTO items(value) VALUES (@value)", Parameters = new Dictionary<string, object?> { ["value"] = "first" } },
                    new SqlCommandRequest { Sql = "INSERT INTO missing(value) VALUES (@value)", Parameters = new Dictionary<string, object?> { ["value"] = "second" } }
                ]
            });

            Assert.False(result.Success);
            var check = await service.ExecuteQueryAsync(new SqlRequest { Sql = "SELECT COUNT(*) AS total FROM items" });
            Assert.Equal(0L, Convert.ToInt64(check.Rows[0]["total"]));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in new[] { path, $"{path}-wal", $"{path}-shm" }) if (File.Exists(file)) File.Delete(file);
        }
    }
}

