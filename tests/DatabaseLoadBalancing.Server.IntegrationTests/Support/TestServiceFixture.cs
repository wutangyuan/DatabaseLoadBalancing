using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Builder;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Support;

public sealed class TestServiceFixture : IAsyncLifetime
{
    private WebApplication? _application;

    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"database-load-balancing-fixture-{Guid.NewGuid():N}.db");

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        SeedDatabase();
        _application = ServerApplication.Build(["--database", DatabasePath, "--urls", "http://127.0.0.1:0"]);
        await _application.StartAsync();
        Client = new HttpClient { BaseAddress = new Uri(_application.Urls.First() + "/") };
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_application is not null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }

        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { DatabasePath, $"{DatabasePath}-wal", $"{DatabasePath}-shm" })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private void SeedDatabase()
    {
        var factory = new DatabaseLoadBalancing.Data.Sqlite.SqliteConnectionFactory(DatabasePath);
        using var connection = factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE items (id INTEGER PRIMARY KEY AUTOINCREMENT, value TEXT NOT NULL UNIQUE);
            CREATE TABLE audit (id INTEGER PRIMARY KEY AUTOINCREMENT, message TEXT NOT NULL);
            """;
        command.ExecuteNonQuery();
    }
}

