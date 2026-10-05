using DatabaseLoadBalancing.Server.Execution;

namespace DatabaseLoadBalancing.Server.UnitTests.Execution;

public sealed class MutationCoordinatorTests
{
    [Fact]
    public async Task Coordinator_SerializesMutations()
    {
        using var coordinator = new MutationCoordinator();
        var active = 0;
        var maximum = 0;
        var tasks = Enumerable.Range(0, 20).Select(_ => coordinator.EnqueueAsync(async () =>
        {
            var now = Interlocked.Increment(ref active);
            Interlocked.Exchange(ref maximum, Math.Max(maximum, now));
            await Task.Yield();
            Interlocked.Decrement(ref active);
            return true;
        }));

        await Task.WhenAll(tasks);
        Assert.Equal(1, maximum);
    }
}

