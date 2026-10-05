using DatabaseLoadBalancing.Contracts.Health;

namespace DatabaseLoadBalancing.Client.Services;

public sealed class ServiceHealthClient
{
    private readonly string _serviceAddress;

    public ServiceHealthClient(string serviceAddress) => _serviceAddress = serviceAddress;

    public async Task<ServiceHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new DatabaseServiceClient(_serviceAddress);
            return await client.GetHealthAsync(cancellationToken)
                ?? new ServiceHealth { Status = "Unavailable", Database = "error", DatabaseAccessible = false, LastError = "服务端未返回健康状态。" };
        }
        catch (Exception exception)
        {
            return new ServiceHealth { Status = "Unavailable", Database = "error", DatabaseAccessible = false, LastError = exception.Message };
        }
    }
}
