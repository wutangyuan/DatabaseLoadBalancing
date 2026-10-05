using DatabaseLoadBalancing.Data.Requests;

namespace DatabaseLoadBalancing.Server.UnitTests.Execution;

public sealed class SqlExecutionDispatcherTests
{
    [Fact]
    public void DispatcherPolicy_RejectsUnboundParametersBeforeExecution()
    {
        var result = SqlCommandPolicy.ValidateParameters("SELECT * FROM items WHERE id = @id", new Dictionary<string, object?>());

        Assert.False(result.IsValid);
        Assert.Equal("sql.parameters", result.ErrorCode);
    }
}

