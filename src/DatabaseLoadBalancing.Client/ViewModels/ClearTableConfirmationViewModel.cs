using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Client.ViewModels;

public sealed class ClearTableConfirmationViewModel
{
    public ClearTableConfirmationViewModel(TableMetadata table)
    {
        TableName = table.DisplayName.Length == 0 ? table.Name : table.DisplayName;
        RecordCount = table.RecordCount;
    }

    public string TableName { get; }

    public long RecordCount { get; }

    public string ConfirmationText => $"将清空表“{TableName}”当前全部 {RecordCount} 条记录。";

    public bool Confirmed { get; private set; }

    public void Confirm() => Confirmed = true;
}

