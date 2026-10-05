using System.Text.Json;
using System.Windows;

namespace DatabaseLoadBalancing.Client.Views;

public partial class RecordEditorWindow : Window
{
    public RecordEditorWindow(string? initialJson)
    {
        InitializeComponent();
        JsonTextBox.Text = string.IsNullOrWhiteSpace(initialJson) ? "{\n  \n}" : initialJson;
    }

    public string JsonText => JsonTextBox.Text;

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(JsonTextBox.Text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("根节点必须是 JSON 对象。");
            }

            DialogResult = true;
        }
        catch (JsonException exception)
        {
            MessageBox.Show(this, $"JSON 校验失败：{exception.Message}", "数据校验", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

