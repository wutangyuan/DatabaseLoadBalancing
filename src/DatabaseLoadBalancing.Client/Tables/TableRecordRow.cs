using System.Text.Json;
using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Client.Tables;

public sealed class TableRecordRow
{
    public TableRecordRow(TableRecord record)
    {
        Record = record;
        Json = JsonSerializer.Serialize(record.Values, new JsonSerializerOptions { WriteIndented = true });
    }

    public TableRecord Record { get; }

    public long RowId => Record.RowId;

    public string Json { get; }
}

