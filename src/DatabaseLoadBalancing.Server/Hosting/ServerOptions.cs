namespace DatabaseLoadBalancing.Server.Hosting;

public sealed class ServerOptions
{
    public string DatabaseProvider { get; set; } = "Sqlite";

    public string DatabasePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "data", "database.db");

    public string? ConnectionString { get; set; }

    public int Port { get; set; } = 5080;

    public int MaxSqlLength { get; set; } = 32 * 1024;

    public int MaxRows { get; set; } = 1000;

    public int TimeoutMs { get; set; } = 5000;

    public HashSet<string> AllowedTables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

