using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Client.Services;

public sealed class TableMaintenanceClient
{
    private readonly DatabaseServiceClient _client;

    public TableMaintenanceClient(DatabaseServiceClient client) => _client = client;

    public Task<IReadOnlyList<TableMetadata>> ListAsync(CancellationToken cancellationToken = default) => _client.GetTablesAsync(cancellationToken);

    public Task<TableDataResponse?> ReadAsync(string tableName, CancellationToken cancellationToken = default) => _client.GetTableAsync(tableName, cancellationToken);

    public Task<ClearTableResponse?> ClearAsync(string tableName, CancellationToken cancellationToken = default) => _client.ClearTableAsync(tableName, cancellationToken);
}

