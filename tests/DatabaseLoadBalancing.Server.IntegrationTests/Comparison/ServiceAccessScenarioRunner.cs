using System.Net.Http.Json;
using System.Text.Json;
using DatabaseLoadBalancing.Contracts.Sql;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Comparison;

public sealed class ServiceAccessScenarioRunner
{
    public async Task<ComparisonResult> RunAsync(HttpClient client, int clientCount = 10, int writeCount = 100, CancellationToken cancellationToken = default)
    {
        var success = 0;
        var failure = 0;
        var timeout = 0;
        var contention = 0;
        using var gate = new SemaphoreSlim(clientCount, clientCount);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var writes = Enumerable.Range(0, writeCount).Select(async index =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var response = await client.PostAsJsonAsync("api/v1/sql/execute", new SqlRequest
                {
                    RequestId = $"comparison-{index}",
                    Sql = "INSERT INTO items(value) VALUES (@value)",
                    Parameters = new Dictionary<string, object?> { ["value"] = $"service-{index}" }
                }, cancellationToken);
                var result = await response.Content.ReadFromJsonAsync<SqlExecutionResult>(cancellationToken: cancellationToken);
                if (result?.Success == true) Interlocked.Increment(ref success);
                else if (result?.Retryable == true) Interlocked.Increment(ref contention);
                else Interlocked.Increment(ref failure);
            }
            catch (OperationCanceledException) { Interlocked.Increment(ref timeout); }
            catch { Interlocked.Increment(ref failure); }
            finally { gate.Release(); }
        });
        await Task.WhenAll(writes);
        stopwatch.Stop();
        var countResult = await client.PostAsJsonAsync("api/v1/sql/scalar", new SqlRequest { Sql = "SELECT COUNT(*) FROM items" }, cancellationToken);
        var countResponse = await countResult.Content.ReadFromJsonAsync<SqlExecutionResult>(cancellationToken: cancellationToken);
        var count = ((JsonElement)countResponse!.Rows[0]["value"]!).GetInt64();
        return new ComparisonResult("service", writeCount, success, failure, timeout, contention, stopwatch.Elapsed, count);
    }
}

