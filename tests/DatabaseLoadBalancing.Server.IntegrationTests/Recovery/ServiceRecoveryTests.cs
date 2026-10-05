using System.Net.Http.Json;
using System.Text.Json;
using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Data.Sqlite;
using Microsoft.AspNetCore.Builder;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Recovery;

public sealed class ServiceRecoveryTests
{
    [Fact]
    public async Task Restart_PreservesCommittedDataAndHealth()
    {
        var path = Path.Combine(Path.GetTempPath(), $"database-load-balancing-recovery-{Guid.NewGuid():N}.db");
        WebApplication? first = null;
        WebApplication? second = null;
        try
        {
            var factory = new SqliteConnectionFactory(path);
            using (var connection = factory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE items (value TEXT NOT NULL)";
                command.ExecuteNonQuery();
            }

            first = ServerApplication.Build(["--database", path, "--urls", "http://127.0.0.1:0"]);
            await first.StartAsync();
            using (var client = new HttpClient { BaseAddress = new Uri(first.Urls.First() + "/") })
            {
                var result = await (await client.PostAsJsonAsync("api/v1/sql/execute", new SqlRequest
                {
                    RequestId = "recovery-write-1",
                    Sql = "INSERT INTO items(value) VALUES (@value)",
                    Parameters = new Dictionary<string, object?> { ["value"] = "persisted" }
                })).Content.ReadFromJsonAsync<SqlExecutionResult>();
                Assert.True(result!.Success);
            }

            await first.StopAsync();
            await first.DisposeAsync();
            first = null;
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            second = ServerApplication.Build(["--database", path, "--urls", "http://127.0.0.1:0"]);
            await second.StartAsync();
            using var recoveredClient = new HttpClient { BaseAddress = new Uri(second.Urls.First() + "/") };
            var health = await recoveredClient.GetFromJsonAsync<DatabaseLoadBalancing.Contracts.Health.ServiceHealth>("health");
            var count = await (await recoveredClient.PostAsJsonAsync("api/v1/sql/scalar", new SqlRequest { Sql = "SELECT COUNT(*) FROM items" })).Content.ReadFromJsonAsync<SqlExecutionResult>();
            Assert.True(health!.DatabaseAccessible);
            Assert.Equal(1L, ((JsonElement)count!.Rows[0]["value"]!).GetInt64());
        }
        finally
        {
            if (first is not null) await first.DisposeAsync();
            if (second is not null) await second.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in new[] { path, $"{path}-wal", $"{path}-shm" }) if (File.Exists(file)) File.Delete(file);
        }
    }
}

