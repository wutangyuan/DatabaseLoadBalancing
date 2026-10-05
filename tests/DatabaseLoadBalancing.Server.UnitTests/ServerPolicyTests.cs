using DatabaseLoadBalancing.Data.Requests;

namespace DatabaseLoadBalancing.Server.UnitTests;

public sealed class ServerPolicyTests
{
    [Fact]
    public void TableIdentifiers_AcceptSafeNamesOnly()
    {
        Assert.True(SqlCommandPolicy.IsValidIdentifier("orders_2026"));
        Assert.False(SqlCommandPolicy.IsValidIdentifier("orders; DROP TABLE users"));
    }
}

