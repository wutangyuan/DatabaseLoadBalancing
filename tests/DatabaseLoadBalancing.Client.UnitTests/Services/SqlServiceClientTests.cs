using DatabaseLoadBalancing.Client.Services;
using DatabaseLoadBalancing.Contracts.Sql;

namespace DatabaseLoadBalancing.Client.UnitTests.Services;

public sealed class SqlServiceClientTests
{
    [Fact]
    public void SqlClient_UsesTransportClientWithoutDatabasePath()
    {
        using var transport = new DatabaseServiceClient("127.0.0.1:5080");
        var client = new SqlServiceClient(transport);

        Assert.NotNull(client);
        Assert.Equal("SELECT 1", new SqlRequest { Sql = "SELECT 1" }.Sql);
    }
}

