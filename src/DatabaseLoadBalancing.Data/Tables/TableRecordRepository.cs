using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Data.Tables;

public sealed class TableRecordRepository
{
    private readonly TableMaintenanceService _service;

    public TableRecordRepository(TableMaintenanceService service) => _service = service;

    public Task<TableDataResponse> GetAsync(string tableName, int limit = 200, int offset = 0, CancellationToken cancellationToken = default) =>
        _service.GetTableAsync(tableName, limit, offset, cancellationToken);
}

