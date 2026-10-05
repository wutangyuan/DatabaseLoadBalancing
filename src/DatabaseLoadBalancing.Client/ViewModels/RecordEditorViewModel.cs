using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Client.ViewModels;

public sealed class RecordEditorViewModel
{
    public bool Validate(TableMetadata table, IReadOnlyDictionary<string, object?> values, out string message)
    {
        foreach (var column in table.Columns.Where(column => column.IsRequired && column.IsEditable))
        {
            if (!values.TryGetValue(column.Name, out var value) || value is null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                message = $"字段 {column.DisplayName} 为必填项。";
                return false;
            }
        }

        if (values.Keys.Any(key => table.Columns.All(column => !string.Equals(column.Name, key, StringComparison.OrdinalIgnoreCase))))
        {
            message = "包含服务端不允许的字段。";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

