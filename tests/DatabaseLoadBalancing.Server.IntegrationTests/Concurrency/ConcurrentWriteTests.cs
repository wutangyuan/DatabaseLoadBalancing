using System.Net.Http.Json;
using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Server.IntegrationTests.Support;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Concurrency;

public sealed class ConcurrentWriteTests : IClassFixture<TestServiceFixture>
{
    private readonly TestServiceFixture _fixture;

    public ConcurrentWriteTests(TestServiceFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task TenClientsAndOneHundredWrites_ReturnExplicitSuccesses()
    {
        using var gate = new SemaphoreSlim(10, 10);
        var tasks = Enumerable.Range(0, 100).Select(async index =>
        {
            await gate.WaitAsync();
            try
            {
                using var client = new HttpClient { BaseAddress = _fixture.Client.BaseAddress };
                var request = new SqlRequest
                {
                    RequestId = $"concurrent-{index}",
                    Sql = "INSERT INTO items(value) VALUES (@value)",
                    Parameters = new Dictionary<string, object?> { ["value"] = $"value-{index}" }
                };
                var response = await client.PostAsJsonAsync("api/v1/sql/execute", request);
                return await response.Content.ReadFromJsonAsync<SqlExecutionResult>();
            }
            finally
            {
                gate.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        Assert.All(results, result => Assert.True(result!.Success, result.ErrorMessage));
    }
}

