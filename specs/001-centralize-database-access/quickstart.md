# Quickstart Validation: 集中式数据库访问服务

This guide validates the feature after implementation. It does not prescribe
the internal service or controller implementation.

## Prerequisites

- Windows host with the .NET 10 SDK.
- A writable local directory for a temporary SQLite database.
- The service and client can communicate over localhost or the trusted LAN.

## Build and unit-test validation

From the repository root:

```powershell
dotnet restore DatabaseLoadBalancing.sln
dotnet build DatabaseLoadBalancing.sln --configuration Release
dotnet test DatabaseLoadBalancing.sln --configuration Release --no-build
```

Expected result: all projects build successfully and all unit tests pass.

## Start the service

Use a temporary database path and bind the service to a test address:

```powershell
dotnet run --project src/DatabaseLoadBalancing.Server -- --database .test-data\quickstart.db --urls http://127.0.0.1:5080
```

Expected result:

- The console reports startup and database readiness as Info.
- The service exposes `GET /health` with `status: Ready`.
- The SQLite file exists only under the service-owned test-data directory.

## Contract checks

1. Call `GET /health` and confirm a ready response.
2. Call `POST /api/v1/sql/query` with a parameterized `SELECT` and verify the
   returned columns and rows.
3. Call `POST /api/v1/sql/execute` with an `INSERT` or `UPDATE` and verify the
   affected-row count.
4. Submit the same mutation request twice with the same request ID and verify
   that the second call returns the recorded outcome without a second write.
5. Submit a transaction containing multiple mutations and force one command to
   fail; verify that no command's data remains committed.
6. Submit a mutation with invalid or unbound parameters and verify a
   `ValidationFailed` response without a database change.

The full endpoint shapes and status behavior are defined in
[contracts/sql-service.md](contracts/sql-service.md), and the request outcome
rules are defined in [data-model.md](data-model.md).

## WPF data-maintenance validation

Start the WPF client after the service reports ready:

```powershell
dotnet run --project src/DatabaseLoadBalancing.Client
```

Use a test database containing at least two authorized data tables. Verify:

1. The client displays only the service-provided table list and does not show a
   database file path.
2. Selecting each table shows its columns, record list, and a clear empty-state
   message when it contains no records.
3. Create, edit, and delete one record in each table; verify the list refreshes
   and the server returns explicit outcomes.
4. Start a clear operation and verify that the confirmation view displays the
   selected table name and its current record count.
5. Cancel the confirmation and verify no request is sent and no record changes.
6. Confirm the clear operation and verify the selected table has zero records,
   its schema remains available, the result reports the actual affected count,
   and the second table remains unchanged.

The endpoint behavior for this workflow is defined in
[contracts/sql-service.md](contracts/sql-service.md#table-maintenance-endpoints).

## Concurrency validation

Run the integration test suite with at least 10 concurrent clients and 100
total write requests. Verify:

- Every request receives an explicit result.
- No request fails solely because another client directly holds the database;
  clients must never open the SQLite file.
- Duplicate request IDs do not duplicate writes.
- A conflict on the same logical data record is reported according to the
  implemented business or optimistic-concurrency policy.

## Recovery validation

1. Stop the service while no write is active and confirm clients receive a
   `ServiceUnavailable` result.
2. Restart the service and confirm `/health` returns `Ready`.
3. Submit a new query and mutation and confirm existing committed data remains
   accurate.
4. Interrupt a request during a write, query its request ID after restart, and
   confirm the committed outcome is returned if the transaction completed; do
   not blindly repeat an unknown mutation.

## Constitution checks

- Confirm log4net records request lifecycle and failure context without secrets.
- Confirm Info, Warn, and Error console output has distinct colors and text
  labels.
- Confirm WPF client projects contain no SQLite package reference or database
  file access code.
- Confirm the WPF connection/status and data-maintenance UI use shared style
  resources, including the destructive-action confirmation dialog.

## Observed validation notes

- `dotnet build DatabaseLoadBalancing.sln --configuration Release` passed with zero warnings and zero errors.
- `dotnet test DatabaseLoadBalancing.sln --configuration Release --no-build` passed for the Contracts, Data, Server, Server.IntegrationTests, and Client test projects.
- The integration suite includes the 10-client/100-write service scenario, duplicate request checks, rollback, multi-table CRUD/clear isolation, restart recovery, and the disposable direct SQLite comparison runner.
