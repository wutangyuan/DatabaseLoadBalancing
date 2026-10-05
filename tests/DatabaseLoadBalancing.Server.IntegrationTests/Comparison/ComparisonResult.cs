namespace DatabaseLoadBalancing.Server.IntegrationTests.Comparison;

public sealed record ComparisonResult(
    string Scenario,
    int Total,
    int Success,
    int Failure,
    int Timeout,
    int LockOrContention,
    TimeSpan Duration,
    long FinalRowCount)
{
    public bool DataAccurate => Success == FinalRowCount;
}

