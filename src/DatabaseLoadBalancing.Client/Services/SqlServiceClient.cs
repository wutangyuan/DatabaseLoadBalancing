using DatabaseLoadBalancing.Contracts.Sql;

namespace DatabaseLoadBalancing.Client.Services;

public sealed class SqlServiceClient
{
    private readonly DatabaseServiceClient _client;

    public SqlServiceClient(DatabaseServiceClient client) => _client = client;

    public Task<SqlExecutionResult?> QueryAsync(SqlRequest request, CancellationToken cancellationToken = default) =>
        _client.ExecuteQueryAsync(request, cancellationToken);

    public Task<SqlExecutionResult?> ScalarAsync(SqlRequest request, CancellationToken cancellationToken = default) =>
        _client.ExecuteScalarAsync(request, cancellationToken);

    public Task<SqlExecutionResult?> ExecuteAsync(SqlRequest request, CancellationToken cancellationToken = default) =>
        _client.ExecuteAsync(request, cancellationToken);
}

