namespace DatabaseLoadBalancing.Server.IntegrationTests.Comparison;

public sealed class ComparisonReportTests
{
    [Fact]
    public void ReportWriter_IncludesRequiredMetrics()
    {
        var report = new ComparisonReportWriter().Render(
            new ComparisonResult("direct", 100, 90, 10, 0, 10, TimeSpan.FromSeconds(1), 90),
            new ComparisonResult("service", 100, 100, 0, 0, 0, TimeSpan.FromSeconds(1), 100));

        Assert.Contains("success", report);
        Assert.Contains("lock/contention", report);
        Assert.Contains("final rows", report);
    }
}

