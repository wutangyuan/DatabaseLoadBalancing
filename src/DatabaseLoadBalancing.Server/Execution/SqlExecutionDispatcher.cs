using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Data.Requests;

namespace DatabaseLoadBalancing.Server.Execution;

public sealed class SqlExecutionDispatcher
{
    private readonly SqlExecutionService _service;

    public SqlExecutionDispatcher(SqlExecutionService service)
    {
        _service = service;
    }

    public Task<SqlExecutionResult> QueryAsync(SqlRequest request, CancellationToken cancellationToken = default) =>
        _service.ExecuteQueryAsync(request, cancellationToken);

    public Task<SqlExecutionResult> ScalarAsync(SqlRequest request, CancellationToken cancellationToken = default) =>
        _service.ExecuteScalarAsync(request, cancellationToken);

    public Task<SqlExecutionResult> ExecuteAsync(SqlRequest request, CancellationToken cancellationToken = default) =>
        _service.ExecuteMutationAsync(request, cancellationToken);

    public Task<SqlExecutionResult> TransactionAsync(SqlTransactionRequest request, CancellationToken cancellationToken = default) =>
        _service.ExecuteTransactionAsync(request, cancellationToken);
}

