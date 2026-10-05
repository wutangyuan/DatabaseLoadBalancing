namespace DatabaseLoadBalancing.Contracts.Tables;

public sealed class TableMetadata
{
    public string Name { get; set; } = string.Empty;

    public string TableId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public long RecordCount { get; set; }

    public List<TableColumnMetadata> Columns { get; set; } = [];

    public List<string> PrimaryKeyColumns { get; set; } = [];

    public List<string> AllowedOperations { get; set; } = ["read", "create", "update", "delete", "clear"];
}

public sealed class TableColumnMetadata
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public bool IsNullable { get; set; }

    public bool IsPrimaryKey { get; set; }

    public bool IsRequired { get; set; }

    public bool IsEditable { get; set; } = true;
}

public sealed class TableDataResponse
{
    public TableMetadata Table { get; set; } = new();

    public List<TableRecord> Records { get; set; } = [];
}

public sealed class TableRecord
{
    public long RowId { get; set; }

    public Dictionary<string, object?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class TableRecordRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

    public string TableId { get; set; } = string.Empty;

    public Dictionary<string, object?> Key { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, object?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TableRecordMutationRequest : TableRecordRequest
{
}

public sealed class ClearTableRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

    public string TableId { get; set; } = string.Empty;
}

public sealed class ClearTableResponse
{
    public string TableName { get; set; } = string.Empty;

    public long PreviousRecordCount { get; set; }

    public long DeletedRecordCount { get; set; }
}

