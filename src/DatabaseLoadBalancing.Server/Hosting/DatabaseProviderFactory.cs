using DatabaseLoadBalancing.Data.Abstractions;
using DatabaseLoadBalancing.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace DatabaseLoadBalancing.Server.Hosting;

public static class DatabaseProviderFactory
{
    public static IDatabaseProvider Create(IConfiguration configuration)
    {
        var providerName = configuration["DatabaseProvider"] ?? configuration["databaseProvider"] ?? "Sqlite";
        return providerName.Trim().ToLowerInvariant() switch
        {
            "sqlite" => new SqliteConnectionFactory(
                configuration["database"] ??
                configuration["DatabasePath"] ??
                Path.Combine(AppContext.BaseDirectory, "data", "database.db")),
            "sqlserver" => throw new NotSupportedException(
                "SQL Server provider is reserved for the next adapter package; the service is currently configured for SQLite."),
            _ => throw new InvalidOperationException($"不支持的数据库提供程序：{providerName}。")
        };
    }
}

