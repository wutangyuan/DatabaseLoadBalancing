using System.Net.Http.Json;
using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Contracts.Tables;
using DatabaseLoadBalancing.Server.IntegrationTests.Support;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Tables;

public sealed class TableRecordMutationTests : IClassFixture<TestServiceFixture>
{
    private readonly TestServiceFixture _fixture;

    public TableRecordMutationTests(TestServiceFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateEditDeleteAndClear_PreserveSchemaAndOtherTables()
    {
        var create = await (await _fixture.Client.PostAsJsonAsync("api/v1/tables/items/records", new TableRecordRequest
        {
            RequestId = "table-create-1",
            Values = new Dictionary<string, object?> { ["value"] = "maintenance" }
        })).Content.ReadFromJsonAsync<SqlExecutionResult>();
        Assert.True(create!.Success, create.ErrorMessage);

        var table = await _fixture.Client.GetFromJsonAsync<TableDataResponse>("api/v1/tables/items");
        var row = Assert.Single(table!.Records);
        var update = await (await _fixture.Client.PutAsJsonAsync($"api/v1/tables/items/records/{row.RowId}", new TableRecordRequest
        {
            RequestId = "table-update-1",
            Values = new Dictionary<string, object?> { ["value"] = "updated" }
        })).Content.ReadFromJsonAsync<SqlExecutionResult>();
        Assert.True(update!.Success, update.ErrorMessage);

        var delete = await (await _fixture.Client.DeleteAsync($"api/v1/tables/items/records/{row.RowId}")).Content.ReadFromJsonAsync<SqlExecutionResult>();
        Assert.True(delete!.Success, delete.ErrorMessage);

        await _fixture.Client.PostAsJsonAsync("api/v1/tables/items/records", new TableRecordRequest
        {
            RequestId = "table-clear-1",
            Values = new Dictionary<string, object?> { ["value"] = "clear-me" }
        });
        await _fixture.Client.PostAsJsonAsync("api/v1/tables/audit/records", new TableRecordRequest
        {
            RequestId = "audit-preserve-1",
            Values = new Dictionary<string, object?> { ["message"] = "keep" }
        });
        var clearResponse = await _fixture.Client.PostAsJsonAsync("api/v1/tables/items/clear", new ClearTableRequest { RequestId = "clear-request-1", TableId = "items" });
        var clear = await clearResponse.Content.ReadFromJsonAsync<ClearTableResponse>();
        Assert.True(clearResponse.IsSuccessStatusCode);
        Assert.Equal("items", clear!.TableName);
        Assert.Equal(1, clear.DeletedRecordCount);

        var after = await _fixture.Client.GetFromJsonAsync<TableDataResponse>("api/v1/tables/items");
        var other = await _fixture.Client.GetFromJsonAsync<TableDataResponse>("api/v1/tables/audit");
        Assert.Empty(after!.Records);
        Assert.DoesNotContain(after.Table.Columns, column => column.Name == "missing");
        Assert.Single(other!.Records);
    }
}

