using DatabaseLoadBalancing.Client.Services;

namespace DatabaseLoadBalancing.Client.UnitTests.Services;

public sealed class ServiceHealthClientTests
{
    [Fact]
    public async Task UnavailableService_ReturnsExplicitState()
    {
        var health = await new ServiceHealthClient("127.0.0.1:1").GetHealthAsync();

        Assert.False(health.DatabaseAccessible);
        Assert.Equal("Unavailable", health.Status);
    }
}

