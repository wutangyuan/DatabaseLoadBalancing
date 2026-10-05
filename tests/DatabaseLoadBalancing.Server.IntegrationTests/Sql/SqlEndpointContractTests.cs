using System.Net.Http.Json;
using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Server.IntegrationTests.Support;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Sql;

public sealed class SqlEndpointContractTests : IClassFixture<TestServiceFixture>
{
    private readonly TestServiceFixture _fixture;

    public SqlEndpointContractTests(TestServiceFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task QueryScalarExecuteAndTransaction_UseTheSharedContract()
    {
        var queryResponse = await _fixture.Client.PostAsJsonAsync("api/v1/sql/query", new SqlRequest
        {
            Sql = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @name",
            Parameters = new Dictionary<string, object?> { ["name"] = "items" }
        });
        var query = await queryResponse.Content.ReadFromJsonAsync<SqlExecutionResult>();
        Assert.True(queryResponse.IsSuccessStatusCode);
        Assert.True(query!.Success);

        var scalarResponse = await _fixture.Client.PostAsJsonAsync("api/v1/sql/scalar", new SqlRequest
        {
            Sql = "SELECT COUNT(*) FROM items"
        });
        var scalar = await scalarResponse.Content.ReadFromJsonAsync<SqlExecutionResult>();
        Assert.True(scalar!.Success);

        var request = new SqlRequest
        {
            RequestId = "contract-insert-1",
            Sql = "INSERT INTO items(value) VALUES (@value)",
            Parameters = new Dictionary<string, object?> { ["value"] = "contract" }
        };
        var execute = await (await _fixture.Client.PostAsJsonAsync("api/v1/sql/execute", request)).Content.ReadFromJsonAsync<SqlExecutionResult>();
        Assert.True(execute!.Success);

        var transaction = new SqlTransactionRequest
        {
            RequestId = "contract-transaction-1",
            Commands = [new SqlCommandRequest { Sql = "INSERT INTO audit(message) VALUES (@message)", Parameters = new Dictionary<string, object?> { ["message"] = "ok" } }]
        };
        var transactionResult = await (await _fixture.Client.PostAsJsonAsync("api/v1/sql/transaction", transaction)).Content.ReadFromJsonAsync<SqlExecutionResult>();
        Assert.True(transactionResult!.Success);
    }

    [Fact]
    public async Task InvalidMutation_IsExplicitlyRejected()
    {
        var response = await _fixture.Client.PostAsJsonAsync("api/v1/sql/query", new SqlRequest { Sql = "DELETE FROM items" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}

