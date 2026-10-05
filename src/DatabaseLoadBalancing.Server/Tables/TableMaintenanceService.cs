using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Contracts.Tables;

namespace DatabaseLoadBalancing.Server.Tables;

public sealed class TableMaintenanceService
{
    private readonly DatabaseLoadBalancing.Data.Tables.TableMaintenanceService _dataService;

    public TableMaintenanceService(DatabaseLoadBalancing.Data.Tables.TableMaintenanceService dataService) => _dataService = dataService;

    public Task<IReadOnlyList<TableMetadata>> ListTablesAsync(CancellationToken cancellationToken = default) =>
        _dataService.ListTablesAsync(cancellationToken);

    public Task<TableDataResponse> GetTableAsync(string tableName, int limit = 200, int offset = 0, CancellationToken cancellationToken = default) =>
        _dataService.GetTableAsync(tableName, limit, offset, cancellationToken);

    public Task<SqlExecutionResult> CreateRecordAsync(string tableName, TableRecordRequest request, CancellationToken cancellationToken = default) =>
        _dataService.CreateRecordAsync(tableName, request, cancellationToken);

    public Task<SqlExecutionResult> UpdateRecordAsync(string tableName, long rowId, TableRecordRequest request, CancellationToken cancellationToken = default) =>
        _dataService.UpdateRecordAsync(tableName, rowId, request, cancellationToken);

    public Task<SqlExecutionResult> DeleteRecordAsync(string tableName, long rowId, string? requestId = null, CancellationToken cancellationToken = default) =>
        _dataService.DeleteRecordAsync(tableName, rowId, requestId, cancellationToken);

    public Task<ClearTableResponse> ClearTableAsync(string tableName, string? requestId = null, CancellationToken cancellationToken = default) =>
        _dataService.ClearTableAsync(tableName, requestId, cancellationToken);
}

