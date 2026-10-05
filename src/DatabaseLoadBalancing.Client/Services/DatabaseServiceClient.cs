using System.Net.Sockets;
using System.Text.Json;
using DatabaseLoadBalancing.Contracts.Health;
using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Contracts.Tables;
using DatabaseLoadBalancing.Contracts.Transport;

namespace DatabaseLoadBalancing.Client.Services;

public sealed class DatabaseServiceClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;

    public DatabaseServiceClient(string serviceAddress)
    {
        (_host, _port) = ParseAddress(serviceAddress);
    }

    public Task<ServiceHealth?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        SendAsync<ServiceHealth>(ConsoleServiceOperations.Health, cancellationToken);

    public async Task<IReadOnlyList<TableMetadata>> GetTablesAsync(CancellationToken cancellationToken = default) =>
        await SendAsync<List<TableMetadata>>(ConsoleServiceOperations.ListTables, cancellationToken) ?? [];

    public Task<TableDataResponse?> GetTableAsync(string tableName, CancellationToken cancellationToken = default) =>
        SendAsync<TableDataRequest, TableDataResponse>(ConsoleServiceOperations.GetTable, new TableDataRequest { TableName = tableName }, cancellationToken);

    public Task<SqlExecutionResult?> ExecuteQueryAsync(SqlRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SqlRequest, SqlExecutionResult>(ConsoleServiceOperations.SqlQuery, request, cancellationToken);

    public Task<SqlExecutionResult?> ExecuteScalarAsync(SqlRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SqlRequest, SqlExecutionResult>(ConsoleServiceOperations.SqlScalar, request, cancellationToken);

    public Task<SqlExecutionResult?> ExecuteAsync(SqlRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SqlRequest, SqlExecutionResult>(ConsoleServiceOperations.SqlExecute, request, cancellationToken);

    public Task<SqlExecutionResult?> CreateRecordAsync(string tableName, TableRecordRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<TableRecordCommand, SqlExecutionResult>(ConsoleServiceOperations.CreateRecord, new TableRecordCommand { TableName = tableName, Record = request }, cancellationToken);

    public Task<SqlExecutionResult?> UpdateRecordAsync(string tableName, long rowId, TableRecordRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<TableRecordCommand, SqlExecutionResult>(ConsoleServiceOperations.UpdateRecord, new TableRecordCommand { TableName = tableName, RowId = rowId, Record = request }, cancellationToken);

    public Task<SqlExecutionResult?> DeleteRecordAsync(string tableName, long rowId, CancellationToken cancellationToken = default) =>
        SendAsync<TableRecordCommand, SqlExecutionResult>(ConsoleServiceOperations.DeleteRecord, new TableRecordCommand { TableName = tableName, RowId = rowId }, cancellationToken);

    public Task<ClearTableResponse?> ClearTableAsync(string tableName, CancellationToken cancellationToken = default) =>
        SendAsync<ClearTableCommand, ClearTableResponse>(ConsoleServiceOperations.ClearTable, new ClearTableCommand { TableName = tableName }, cancellationToken);

    private async Task<TResponse?> SendAsync<TResponse>(string operation, CancellationToken cancellationToken) =>
        await SendAsync<object, TResponse>(operation, new object(), cancellationToken);

    private async Task<TResponse?> SendAsync<TRequest, TResponse>(string operation, TRequest payload, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port, cancellationToken);
        await using var stream = client.GetStream();
        var request = new ConsoleServiceRequest
        {
            Operation = operation,
            Payload = JsonSerializer.SerializeToElement(payload, LengthPrefixedJsonProtocol.JsonOptions)
        };
        await LengthPrefixedJsonProtocol.WriteAsync(stream, request, cancellationToken);
        var response = await LengthPrefixedJsonProtocol.ReadAsync<ConsoleServiceResponse>(stream, cancellationToken)
            ?? throw new InvalidOperationException("The service did not return a response.");
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error ?? "The service rejected the request.");
        }

        return response.Payload is { } responsePayload
            ? responsePayload.Deserialize<TResponse>(LengthPrefixedJsonProtocol.JsonOptions)
            : default;
    }

    private static (string Host, int Port) ParseAddress(string serviceAddress)
    {
        if (string.IsNullOrWhiteSpace(serviceAddress))
        {
            throw new ArgumentException("TCP service address is required.", nameof(serviceAddress));
        }

        var candidate = serviceAddress.Contains("://", StringComparison.Ordinal)
            ? serviceAddress
            : $"tcp://{serviceAddress}";
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "tcp", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host) || uri.Port is <= 0 or > 65535)
        {
            throw new ArgumentException("Service address must use the TCP format host:port.", nameof(serviceAddress));
        }

        return (uri.Host, uri.Port);
    }

    public void Dispose()
    {
    }
}
