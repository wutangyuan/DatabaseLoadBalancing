using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Data.Tables;

public sealed class TableMetadataRepository
{
    private readonly TableMaintenanceService _service;

    public TableMetadataRepository(TableMaintenanceService service) => _service = service;

    public Task<IReadOnlyList<TableMetadata>> ListAsync(CancellationToken cancellationToken = default) =>
        _service.ListTablesAsync(cancellationToken);
}

