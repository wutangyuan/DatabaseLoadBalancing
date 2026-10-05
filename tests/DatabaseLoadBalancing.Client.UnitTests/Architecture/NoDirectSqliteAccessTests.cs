using DatabaseLoadBalancing.Client;

namespace DatabaseLoadBalancing.Client.UnitTests.Architecture;

public sealed class NoDirectSqliteAccessTests
{
    [Fact]
    public void ProductionClient_DoesNotReferenceSqliteAssembly()
    {
        var references = typeof(MainWindow).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name);

        Assert.DoesNotContain("Microsoft.Data.Sqlite", references);
    }
}

