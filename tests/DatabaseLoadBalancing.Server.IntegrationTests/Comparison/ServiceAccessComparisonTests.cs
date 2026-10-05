using DatabaseLoadBalancing.Server.IntegrationTests.Support;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Comparison;

public sealed class ServiceAccessComparisonTests : IClassFixture<TestServiceFixture>
{
    private readonly TestServiceFixture _fixture;

    public ServiceAccessComparisonTests(TestServiceFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ServiceRunner_ProcessesOneHundredWrites()
    {
        var result = await new ServiceAccessScenarioRunner().RunAsync(_fixture.Client, 10, 100);

        Assert.Equal(100, result.Total);
        Assert.Equal(100, result.Success);
        Assert.True(result.DataAccurate);
    }
}

