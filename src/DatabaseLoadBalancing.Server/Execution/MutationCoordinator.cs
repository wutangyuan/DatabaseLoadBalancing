namespace DatabaseLoadBalancing.Server.Execution;

public sealed class MutationCoordinator : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<T> EnqueueAsync<T>(Func<Task<T>> mutation, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await mutation();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}

