using System.Buffers.Binary;
using System.Text.Json;
using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Contracts.Transport;

public static class ConsoleServiceOperations
{
    public const string Health = "health";
    public const string ListTables = "tables.list";
    public const string GetTable = "tables.get";
    public const string SqlQuery = "sql.query";
    public const string SqlScalar = "sql.scalar";
    public const string SqlExecute = "sql.execute";
    public const string SqlTransaction = "sql.transaction";
    public const string CreateRecord = "record.create";
    public const string UpdateRecord = "record.update";
    public const string DeleteRecord = "record.delete";
    public const string ClearTable = "table.clear";
}

public sealed class ConsoleServiceRequest
{
    public string Operation { get; set; } = string.Empty;

    public JsonElement? Payload { get; set; }
}

public sealed class ConsoleServiceResponse
{
    public bool Success { get; set; }

    public JsonElement? Payload { get; set; }

    public string? Error { get; set; }
}

public sealed class TableDataRequest
{
    public string TableName { get; set; } = string.Empty;

    public int Limit { get; set; } = 200;

    public int Offset { get; set; }
}

public sealed class TableRecordCommand
{
    public string TableName { get; set; } = string.Empty;

    public long RowId { get; set; }

    public string? RequestId { get; set; }

    public TableRecordRequest? Record { get; set; }
}

public sealed class ClearTableCommand
{
    public string TableName { get; set; } = string.Empty;

    public string? RequestId { get; set; }
}

public static class LengthPrefixedJsonProtocol
{
    public const int MaximumMessageBytes = 1024 * 1024;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        if (payload.Length > MaximumMessageBytes)
        {
            throw new InvalidOperationException($"Message exceeds the {MaximumMessageBytes} byte limit.");
        }

        var length = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
        await stream.WriteAsync(length, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        var lengthBuffer = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, lengthBuffer, cancellationToken);
        var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
        if (length is <= 0 or > MaximumMessageBytes)
        {
            throw new InvalidOperationException("Message length is invalid.");
        }

        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("The connection closed before a complete message was received.");
            }

            offset += read;
        }
    }
}
