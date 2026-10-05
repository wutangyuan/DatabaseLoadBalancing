using DatabaseLoadBalancing.Data.Requests;

namespace DatabaseLoadBalancing.Data.UnitTests;

public sealed class SqlCommandPolicyTests
{
    [Theory]
    [InlineData("SELECT * FROM items")]
    [InlineData("WITH x AS (SELECT 1) SELECT * FROM x")]
    public void QueryPolicy_AllowsReadOnlyStatements(string sql)
    {
        var result = SqlCommandPolicy.Validate(sql, mutation: false);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("DROP TABLE items")]
    [InlineData("SELECT 1; DELETE FROM items")]
    public void Policy_RejectsDangerousOrMultipleStatements(string sql)
    {
        var result = SqlCommandPolicy.Validate(sql, mutation: false);

        Assert.False(result.IsValid);
    }
}

