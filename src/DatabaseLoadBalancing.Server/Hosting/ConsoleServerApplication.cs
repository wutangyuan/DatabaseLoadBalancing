using DatabaseLoadBalancing.Data.Requests;
using log4net;
using Microsoft.Extensions.Configuration;

namespace DatabaseLoadBalancing.Server.Hosting;

public static class ConsoleServerApplication
{
    public static TcpDatabaseServer Create(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables(prefix: "DATABASELOADBALANCING_")
            .AddCommandLine(args)
            .Build();

        Log4NetConsoleConfiguration.Configure(LogManager.GetRepository(typeof(Program).Assembly));
        var provider = DatabaseProviderFactory.Create(configuration);
        var sqlExecutionService = new SqlExecutionService(provider);
        if (configuration.GetValue<bool>("initialize-demo-schema"))
        {
            DemoSchemaInitializer.Initialize(provider);
        }

        var dataTableService = new DatabaseLoadBalancing.Data.Tables.TableMaintenanceService(provider, sqlExecutionService);
        var tableService = new DatabaseLoadBalancing.Server.Tables.TableMaintenanceService(dataTableService);
        var readinessMonitor = new ServiceReadinessMonitor(provider);
        var port = configuration.GetValue<int?>("port") ?? configuration.GetValue<int?>("Server:Port") ?? 5080;

        return new TcpDatabaseServer(port, provider.ProviderName, sqlExecutionService, tableService, readinessMonitor);
    }
}
