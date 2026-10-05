using DatabaseLoadBalancing.Client.Services;

namespace DatabaseLoadBalancing.Client.UnitTests;

public sealed class DatabaseServiceClientTests
{
    [Fact]
    public void Constructor_AcceptsServiceAddressWithoutTrailingSlash()
    {
        using var client = new DatabaseServiceClient("127.0.0.1:5080");

        Assert.NotNull(client);
    }
}

