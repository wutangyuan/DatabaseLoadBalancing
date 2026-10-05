using DatabaseLoadBalancing.Contracts.Health;

namespace DatabaseLoadBalancing.Contracts.UnitTests;

public sealed class HealthContractTests
{
    [Fact]
    public void HealthContract_ReportsDatabaseAccessibility()
    {
        var health = new ServiceHealth { Status = "Ready", DatabaseAccessible = true };

        Assert.True(health.DatabaseAccessible);
        Assert.Equal("Ready", health.Status);
    }
}

