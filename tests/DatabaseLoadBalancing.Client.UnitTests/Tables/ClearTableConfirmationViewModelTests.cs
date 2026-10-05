using DatabaseLoadBalancing.Client.ViewModels;
using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Client.UnitTests.Tables;

public sealed class ClearTableConfirmationViewModelTests
{
    [Fact]
    public void ConfirmationText_ContainsTableNameAndCurrentCount()
    {
        var viewModel = new ClearTableConfirmationViewModel(new TableMetadata { Name = "items", RecordCount = 12 });

        Assert.Contains("items", viewModel.ConfirmationText);
        Assert.Contains("12", viewModel.ConfirmationText);
        Assert.False(viewModel.Confirmed);
    }
}

