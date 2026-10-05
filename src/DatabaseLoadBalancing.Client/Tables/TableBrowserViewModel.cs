using System.Collections.ObjectModel;
using DatabaseLoadBalancing.Client.Services;
using DatabaseLoadBalancing.Client.Tables;
using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Client.ViewModels;

public sealed class TableBrowserViewModel
{
    private readonly DatabaseServiceClient _client;

    public TableBrowserViewModel(DatabaseServiceClient client)
    {
        _client = client;
    }

    public ObservableCollection<TableMetadata> Tables { get; } = [];

    public ObservableCollection<TableRecordRow> Records { get; } = [];

    public string EmptyStateText { get; private set; } = "请选择数据表。";

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Tables.Clear();
        foreach (var table in await _client.GetTablesAsync(cancellationToken)) Tables.Add(table);
        EmptyStateText = Tables.Count == 0 ? "服务端没有返回可访问的数据表。" : string.Empty;
    }

    public async Task SelectAsync(TableMetadata table, CancellationToken cancellationToken = default)
    {
        Records.Clear();
        var response = await _client.GetTableAsync(table.Name, cancellationToken);
        foreach (var record in response?.Records ?? []) Records.Add(new TableRecordRow(record));
        EmptyStateText = Records.Count == 0 ? "当前表没有记录。" : string.Empty;
    }
}

