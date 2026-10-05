using System.Net.Http.Json;
using DatabaseLoadBalancing.Contracts.Tables;
using DatabaseLoadBalancing.Server.IntegrationTests.Support;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Tables;

public sealed class TableMaintenanceContractTests : IClassFixture<TestServiceFixture>
{
    private readonly TestServiceFixture _fixture;

    public TableMaintenanceContractTests(TestServiceFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task TablesEndpoint_ReturnsOnlyVisibleBusinessTables()
    {
        var tables = await _fixture.Client.GetFromJsonAsync<List<TableMetadata>>("api/v1/tables");

        Assert.NotNull(tables);
        Assert.Contains(tables!, table => table.TableId == "items");
        Assert.DoesNotContain(tables!, table => table.Name.StartsWith("__", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecordEndpoint_ReturnsBoundedTableData()
    {
        var response = await _fixture.Client.GetFromJsonAsync<TableDataResponse>("api/v1/tables/items/records?limit=10&offset=0");

        Assert.NotNull(response);
        Assert.Equal("items", response!.Table.TableId);
    }
}

