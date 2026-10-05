using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Server.UnitTests.Tables;

public sealed class TableMetadataServiceTests
{
    [Fact]
    public void MetadataOperations_AreWithinSupportedPolicy()
    {
        var metadata = new TableMetadata { Name = "items" };

        Assert.All(metadata.AllowedOperations, operation => Assert.Contains(operation, new[] { "read", "create", "update", "delete", "clear" }));
        Assert.DoesNotContain(metadata.AllowedOperations, operation => operation == "drop");
    }
}

