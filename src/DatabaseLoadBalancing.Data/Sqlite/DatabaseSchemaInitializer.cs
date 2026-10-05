using DatabaseLoadBalancing.Data.Abstractions;

namespace DatabaseLoadBalancing.Data.Sqlite;

public sealed class DatabaseSchemaInitializer
{
    private readonly IDatabaseProvider _provider;

    public DatabaseSchemaInitializer(IDatabaseProvider provider)
    {
        _provider = provider;
    }

    public void Initialize()
    {
        using var connection = _provider.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = _provider.Dialect.RequestExecutionTableSql;
        command.ExecuteNonQuery();
    }
}

