using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Data.Requests;
using DatabaseLoadBalancing.Data.Sqlite;
using Microsoft.Data.Sqlite;

namespace DatabaseLoadBalancing.Data.UnitTests;

public sealed class SqliteConcurrencyTests
{
    [Fact]
    public async Task ConcurrentMutations_AreSerializedAndAllCommitted()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"database-load-balancing-{Guid.NewGuid():N}.db");
        try
        {
            var factory = new SqliteConnectionFactory(databasePath);
            using (var connection = factory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE items (value INTEGER NOT NULL)";
                command.ExecuteNonQuery();
            }

            var service = new SqlExecutionService(factory);

            var operations = Enumerable.Range(0, 30).Select(index => service.ExecuteMutationAsync(new SqlRequest
            {
                RequestId = $"insert-{index}",
                Sql = "INSERT INTO items(value) VALUES (@value)",
                Parameters = new Dictionary<string, object?> { ["value"] = index }
            }));

            var results = await Task.WhenAll(operations);
            Assert.All(results, result => Assert.True(result.Success, result.Error?.Message));
            var query = await service.ExecuteQueryAsync(new SqlRequest { Sql = "SELECT COUNT(*) AS total FROM items" });
            Assert.Equal(30L, Convert.ToInt64(query.Rows[0]["total"]));
            service.Dispose();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }

            if (File.Exists($"{databasePath}-wal")) File.Delete($"{databasePath}-wal");
            if (File.Exists($"{databasePath}-shm")) File.Delete($"{databasePath}-shm");
        }
    }
}

