using System.Net.Http.Json;
using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Server.IntegrationTests.Support;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Requests;

public sealed class RequestIdempotencyTests : IClassFixture<TestServiceFixture>
{
    private readonly TestServiceFixture _fixture;

    public RequestIdempotencyTests(TestServiceFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SameRequestId_IsCommittedOnlyOnceAndHashConflictIsRejected()
    {
        var request = new SqlRequest
        {
            RequestId = "idempotent-request",
            Sql = "INSERT INTO audit(message) VALUES (@message)",
            Parameters = new Dictionary<string, object?> { ["message"] = "once" }
        };
        var first = await (await _fixture.Client.PostAsJsonAsync("api/v1/sql/execute", request)).Content.ReadFromJsonAsync<SqlExecutionResult>();
        var second = await (await _fixture.Client.PostAsJsonAsync("api/v1/sql/execute", request)).Content.ReadFromJsonAsync<SqlExecutionResult>();
        Assert.True(first!.Success);
        Assert.True(second!.Success);

        var conflict = await _fixture.Client.PostAsJsonAsync("api/v1/sql/execute", new SqlRequest
        {
            RequestId = request.RequestId,
            Sql = "INSERT INTO audit(message) VALUES (@message)",
            Parameters = new Dictionary<string, object?> { ["message"] = "different" }
        });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, conflict.StatusCode);
    }
}

