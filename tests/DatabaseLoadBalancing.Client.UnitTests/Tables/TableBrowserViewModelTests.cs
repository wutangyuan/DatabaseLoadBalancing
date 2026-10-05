using DatabaseLoadBalancing.Client.Services;
using DatabaseLoadBalancing.Client.ViewModels;

namespace DatabaseLoadBalancing.Client.UnitTests.Tables;

public sealed class TableBrowserViewModelTests
{
    [Fact]
    public void BrowserStartsWithExplicitEmptyState()
    {
        using var client = new DatabaseServiceClient("127.0.0.1:5080");
        var viewModel = new TableBrowserViewModel(client);

        Assert.Equal("请选择数据表。", viewModel.EmptyStateText);
        Assert.Empty(viewModel.Tables);
    }
}

