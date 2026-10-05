using DatabaseLoadBalancing.Contracts.Sql;

namespace DatabaseLoadBalancing.Contracts.UnitTests;

public sealed class SqlRequestTests
{
    [Fact]
    public void RequestId_IsGeneratedAndParametersAreCaseInsensitive()
    {
        var request = new SqlRequest();
        request.Parameters["Id"] = 1;

        Assert.False(string.IsNullOrWhiteSpace(request.RequestId));
        Assert.Equal(1, request.Parameters["id"]);
    }

    [Fact]
    public void FailedResult_ContainsStructuredError()
    {
        var result = SqlExecutionResult.Failed("r1", "sql.policy", "DDL is not allowed");

        Assert.False(result.Success);
        Assert.Equal("sql.policy", result.Error!.Code);
    }
}

