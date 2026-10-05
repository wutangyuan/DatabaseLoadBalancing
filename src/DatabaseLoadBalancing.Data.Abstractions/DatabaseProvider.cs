using System.Data.Common;

namespace DatabaseLoadBalancing.Data.Abstractions;

/// <summary>
/// Provider boundary owned by the server. Implementations own connection creation
/// and provider-specific database behavior; application services use only Db* APIs.
/// </summary>
public interface IDatabaseProvider
{
    string ProviderName { get; }

    IDatabaseDialect Dialect { get; }

    DbConnection OpenConnection();
}

public interface IDatabaseDialect
{
    string ProviderName { get; }

    string RequestExecutionTableSql { get; }

    string TablesQuery { get; }

    string BuildCountQuery(string quotedTableName);

    string BuildColumnsQuery(string quotedTableName);

    string BuildRowsQuery(string quotedTableName);

    string BuildUpdateRecordQuery(string quotedTableName, string assignments);

    string BuildDeleteRecordQuery(string quotedTableName);

    string QuoteIdentifier(string identifier);

    bool IsInfrastructureTable(string tableName);

    bool IsBusy(Exception exception);
}

