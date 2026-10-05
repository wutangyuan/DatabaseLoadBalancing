using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using DatabaseLoadBalancing.Client.Services;
using DatabaseLoadBalancing.Client.Tables;
using DatabaseLoadBalancing.Client.Views;
using DatabaseLoadBalancing.Contracts.Tables;
using log4net;

namespace DatabaseLoadBalancing.Client;

public partial class MainWindow : Window
{
    private readonly ILog _logger = LogManager.GetLogger(typeof(MainWindow));
    private readonly ObservableCollection<TableRecordRow> _records = [];
    private DatabaseServiceClient? _client;

    public MainWindow()
    {
        InitializeComponent();
        RecordsGrid.ItemsSource = _records;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        await ConnectAndRefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadCurrentTableAsync();
    }

    private async void TablesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        await LoadCurrentTableAsync();
    }

    private async Task ConnectAndRefreshAsync()
    {
        try
        {
            _client?.Dispose();
            _client = new DatabaseServiceClient(ServiceUrlTextBox.Text.Trim());
            var tables = await _client.GetTablesAsync();
            TablesListBox.ItemsSource = tables;
            if (tables.Count > 0)
            {
                TablesListBox.SelectedIndex = 0;
            }

            StatusTextBlock.Text = tables.Count == 0
                ? "已连接，但当前数据库没有数据表。请先通过服务端 SQL 创建表。"
                : $"已连接，发现 {tables.Count} 张表";
        }
        catch (Exception exception)
        {
            _logger.Error("连接服务端失败。", exception);
            StatusTextBlock.Text = $"连接失败：{exception.Message}";
        }
    }

    private async Task LoadCurrentTableAsync()
    {
        if (_client is null || TablesListBox.SelectedItem is not TableMetadata table)
        {
            return;
        }

        try
        {
            var data = await _client.GetTableAsync(table.Name);
            _records.Clear();
            foreach (var record in data?.Records ?? [])
            {
                _records.Add(new TableRecordRow(record));
            }

            StatusTextBlock.Text = $"{table.Name}：{data?.Table.RecordCount ?? 0} 条记录";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = $"读取失败：{exception.Message}";
        }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedTable(out var client, out var table))
        {
            return;
        }

        var editor = new RecordEditorWindow(null) { Owner = this };
        if (editor.ShowDialog() != true)
        {
            return;
        }

        await ExecuteRecordOperationAsync(() => client.CreateRecordAsync(table.Name, ToRequest(editor.JsonText)));
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedTable(out var client, out var table))
        {
            return;
        }

        if (RecordsGrid.SelectedItem is not TableRecordRow row)
        {
            ShowOperationHint("请先选择一条要编辑的记录。");
            return;
        }

        var editor = new RecordEditorWindow(row.Json) { Owner = this };
        if (editor.ShowDialog() != true)
        {
            return;
        }

        await ExecuteRecordOperationAsync(() => client.UpdateRecordAsync(table.Name, row.RowId, ToRequest(editor.JsonText)));
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedTable(out var client, out var table))
        {
            return;
        }

        if (RecordsGrid.SelectedItem is not TableRecordRow row)
        {
            ShowOperationHint("请先选择一条要删除的记录。");
            return;
        }

        if (MessageBox.Show(this, $"确定删除 {table.Name} 的记录 {row.RowId} 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await ExecuteRecordOperationAsync(() => client.DeleteRecordAsync(table.Name, row.RowId));
    }

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedTable(out var client, out var table))
        {
            return;
        }

        var answer = MessageBox.Show(this, $"将清空表“{table.Name}”当前全部 {table.RecordCount} 条记录，表结构和其他表不会改变。\n是否继续？", "确认清空", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await client.ClearTableAsync(table.Name);
            await ConnectAndRefreshAsync();
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = $"清空失败：{exception.Message}";
        }
    }

    private async Task ExecuteRecordOperationAsync(Func<Task<DatabaseLoadBalancing.Contracts.Sql.SqlExecutionResult?>> operation)
    {
        try
        {
            var result = await operation();
            if (result is { Success: false })
            {
                throw new InvalidOperationException(result.Error?.Message ?? "操作失败。");
            }

            await ConnectAndRefreshAsync();
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = $"操作失败：{exception.Message}";
        }
    }

    private static TableRecordRequest ToRequest(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? throw new JsonException("JSON 对象不能为空。");
        return new TableRecordRequest { Values = values };
    }

    private bool TryGetSelectedTable(out DatabaseServiceClient client, out TableMetadata table)
    {
        client = null!;
        table = null!;
        if (_client is null)
        {
            ShowOperationHint("请先连接 TCP 服务端。");
            return false;
        }

        if (TablesListBox.SelectedItem is TableMetadata selected)
        {
            client = _client;
            table = selected;
            return true;
        }

        var message = TablesListBox.Items.Count == 0
            ? "当前数据库没有可操作的数据表。请先通过服务端 SQL 创建表。"
            : "请先在左侧列表中选择一个数据表。";
        ShowOperationHint(message);
        return false;
    }

    private void ShowOperationHint(string message)
    {
        StatusTextBlock.Text = message;
        MessageBox.Show(this, message, "无法执行操作", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

