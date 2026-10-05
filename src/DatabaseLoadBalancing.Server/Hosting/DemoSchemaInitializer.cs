using DatabaseLoadBalancing.Data.Abstractions;
using log4net;

namespace DatabaseLoadBalancing.Server.Hosting;

public static class DemoSchemaInitializer
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(DemoSchemaInitializer));

    public static void Initialize(IDatabaseProvider provider)
    {
        using var connection = provider.OpenConnection();
        foreach (var sql in new[]
        {
            "CREATE TABLE IF NOT EXISTS items (id INTEGER PRIMARY KEY AUTOINCREMENT, value TEXT NOT NULL UNIQUE)",
            "CREATE TABLE IF NOT EXISTS audit (id INTEGER PRIMARY KEY AUTOINCREMENT, message TEXT NOT NULL)"
        })
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        Logger.Info("演示数据库表已初始化：items、audit。");
    }
}
