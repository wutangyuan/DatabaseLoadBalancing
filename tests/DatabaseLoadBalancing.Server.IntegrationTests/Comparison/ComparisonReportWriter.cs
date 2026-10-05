namespace DatabaseLoadBalancing.Server.IntegrationTests.Comparison;

public sealed class ComparisonReportWriter
{
    public string Render(ComparisonResult direct, ComparisonResult service)
    {
        return $"""
            # Direct vs service SQLite comparison

            | scenario | total | success | failure | timeout | lock/contention | duration(ms) | final rows | accurate |
            |---|---:|---:|---:|---:|---:|---:|---:|:---:|
            | {direct.Scenario} | {direct.Total} | {direct.Success} | {direct.Failure} | {direct.Timeout} | {direct.LockOrContention} | {direct.Duration.TotalMilliseconds:0} | {direct.FinalRowCount} | {direct.DataAccurate} |
            | {service.Scenario} | {service.Total} | {service.Success} | {service.Failure} | {service.Timeout} | {service.LockOrContention} | {service.Duration.TotalMilliseconds:0} | {service.FinalRowCount} | {service.DataAccurate} |

            The production WPF client uses only the service scenario. The direct scenario is isolated test code and is never referenced by the production client.
            """;
    }

    public void Write(string path, ComparisonResult direct, ComparisonResult service)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, Render(direct, service));
    }
}

