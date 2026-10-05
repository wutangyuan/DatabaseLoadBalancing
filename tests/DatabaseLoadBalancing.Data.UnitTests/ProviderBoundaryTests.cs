using DatabaseLoadBalancing.Data.Abstractions;
using DatabaseLoadBalancing.Data.Sqlite;

namespace DatabaseLoadBalancing.Data.UnitTests;

public sealed class ProviderBoundaryTests
{
    [Fact]
    public void SqliteProvider_ImplementsProviderNeutralBoundary()
    {
        IDatabaseProvider provider = new SqliteConnectionFactory(Path.Combine(Path.GetTempPath(), $"provider-{Guid.NewGuid():N}.db"));

        Assert.Equal("Sqlite", provider.ProviderName);
        Assert.Equal(provider.ProviderName, provider.Dialect.ProviderName);
        Assert.Contains("CREATE TABLE", provider.Dialect.RequestExecutionTableSql, StringComparison.OrdinalIgnoreCase);
    }
}

