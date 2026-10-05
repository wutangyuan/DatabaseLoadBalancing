using DatabaseLoadBalancing.Contracts.Health;
using DatabaseLoadBalancing.Data.Abstractions;
using log4net;

namespace DatabaseLoadBalancing.Server.Hosting;

public sealed class ServiceReadinessMonitor
{
    private readonly IDatabaseProvider _provider;
    private readonly ILog _logger;

    public ServiceReadinessMonitor(IDatabaseProvider provider)
    {
        _provider = provider;
        _logger = LogManager.GetLogger(typeof(ServiceReadinessMonitor));
    }

    public ServiceHealth Check()
    {
        try
        {
            using var connection = _provider.OpenConnection();
            _logger.Info("数据库服务已就绪。");
            return new ServiceHealth { Status = "Ready", Database = "ok", DatabaseAccessible = true };
        }
        catch (Exception exception)
        {
            _logger.Error("数据库服务不可用。", exception);
            return new ServiceHealth { Status = "Unavailable", Database = "error", DatabaseAccessible = false, LastError = "数据库不可用。" };
        }
    }
}

