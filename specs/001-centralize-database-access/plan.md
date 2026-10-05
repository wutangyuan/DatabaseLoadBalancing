# Implementation Plan: 集中式数据库访问服务

**Branch**: `001-centralize-database-access` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-centralize-database-access/spec.md`

## Summary

建立一个由控制台程序承载的集中式 SQL 服务。WPF 客户端只通过服务接口提交
参数化 SQL 或受控的数据维护请求，不再直接打开 SQLite 文件；服务端负责请求
校验、允许表元数据、读写分类、写入协调、事务提交或回滚、重复请求识别、日志
和结果返回。客户端显示服务端允许访问的数据表及记录，支持新增、编辑、删除和
当前选中表的二次确认清空。首版一个 SQLite 文件只对应一个活动服务端实例，查询
允许并发，写入通过统一协调器顺序处理。

## Technical Context

**Language/Version**: C# with .NET 10; WPF target `net10.0-windows`; service and
shared libraries target .NET 10.

**Primary Dependencies**: WPF, ASP.NET Core HTTP hosting in the console service,
Microsoft.Data.Sqlite, log4net, and xUnit for tests. Package versions will be
pinned during project bootstrap.

**Storage**: SQLite database stored on the service host's local disk. The
service-owned request execution table records completed write request outcomes
for duplicate detection.

**Testing**: xUnit unit tests plus integration tests using temporary SQLite
files and concurrent service requests. `dotnet test` is the validation command.

**Target Platform**: Windows desktop and Windows service host; WPF runs on
Windows, and the console service is reachable by clients on the trusted local
network.

**Project Type**: Multi-project .NET desktop client, console-hosted HTTP
service, shared contract library, and SQLite data-access library.

**Performance Goals**: Under the specified normal load of 10 concurrent clients
and 20 operations per second, at least 95% of requests return within 2 seconds;
100 concurrent-write acceptance requests receive explicit results.

**Constraints**: Clients MUST NOT access the SQLite file directly. One active
service instance owns one SQLite file. Writes are coordinated and transactional;
SQL uses bound parameters, request IDs, finite timeouts, and bounded retries.
Only service-authorized tables are visible to the WPF client; clearing requires
the table name and current record count to be displayed and explicitly confirmed.

**Scale/Scope**: One SQLite database, one active service instance, up to 10
concurrent clients and 20 operations per second for the first release. The
contract supports query, non-query, scalar, and transaction SQL operations.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **SQLite standard**: All persisted data and request execution metadata use
  SQLite; the database file is owned by the service.
- [x] **Client/server boundary**: The client remains WPF and the server is a
  console-hosted service; clients have no direct database access.
- [x] **Unified logging**: Service request lifecycle, warnings, and failures use
  log4net and expose distinguishable Info, Warn, and Error console output.
- [x] **Unit testing**: Unit and integration tests cover SQL validation,
  concurrency, transactions, duplicate requests, and recovery.
- [x] **Data validation and accuracy**: SQL and parameters are validated,
  writes are transactional, and request outcomes are explicit.
- [x] **Consistent WPF style**: The client adds service connection/status UI only
  through shared WPF resources and existing style conventions; the data-table
  browser, CRUD forms, and clear-confirmation dialog use those same resources.
- [x] **Gate result**: PASS. No constitution violations or exceptions require
  complexity tracking.

## Project Structure

### Documentation (this feature)

```text
specs/001-centralize-database-access/
├── plan.md
├── research.md
├── data-model.md
├── contracts/
├── quickstart.md
└── tasks.md
```

### Source Code (repository root)

```text
DatabaseLoadBalancing.sln
src/
├── DatabaseLoadBalancing.Contracts/
│   ├── Sql/
│   └── Health/
├── DatabaseLoadBalancing.Data/
│   ├── Sqlite/
│   ├── Requests/
│   ├── Tables/
│   └── Logging/
├── DatabaseLoadBalancing.Server/
│   ├── Api/
│   ├── Execution/
│   ├── Tables/
│   ├── Hosting/
│   └── log4net.config
└── DatabaseLoadBalancing.Client/
    ├── Views/
    ├── ViewModels/
    ├── Services/
    ├── Tables/
    └── Resources/

tests/
├── DatabaseLoadBalancing.Contracts.UnitTests/
├── DatabaseLoadBalancing.Data.UnitTests/
├── DatabaseLoadBalancing.Server.UnitTests/
├── DatabaseLoadBalancing.Server.IntegrationTests/
└── DatabaseLoadBalancing.Client.UnitTests/
```

**Structure Decision**: Use a single solution with separate contracts, data,
server, and WPF client projects. The server and data projects own all SQLite
access; the client references only contracts and a service client. Unit tests
remain project-specific, while integration tests exercise the real HTTP and
SQLite boundary with temporary database files.

## Complexity Tracking

No constitution violations were identified, so no exception is required.

## Implementation Design

1. **Shared contracts**: Define request, parameter, command batch, result,
   error, and health DTOs without referencing SQLite or WPF types.
2. **SQLite data layer**: Centralize connection creation, schema checks,
   parameter binding, transaction boundaries, request-outcome persistence,
   bounded busy retries, and database health checks.
3. **Execution coordinator**: Classify read and mutation commands, allow safe
   reads to run concurrently, and serialize mutations through one write
   coordinator. A transaction request is one atomic queue item.
4. **Console service**: Host the HTTP endpoints, validate payloads and allowed
   SQL categories, map execution outcomes to stable response codes, and wire
   log4net plus colored console output.
5. **Table maintenance service**: Discover only server-authorized table metadata,
   expose records and CRUD operations for those tables, and execute clear as one
   transactional mutation that reports its actual affected-row count.
6. **WPF client**: Replace direct SQLite calls with a service client; add a
   shared-style table browser, record editor, delete flow, and confirmation dialog
   that displays the selected table name and current record count before clear.
7. **Verification**: Test parameter binding, SQL category validation, table
   metadata filtering, CRUD validation, clear cancellation and isolation,
   transaction rollback, concurrent writes, duplicate request IDs, service
   restart, direct-file-access absence from the client project, and the
   direct-versus-service comparison scenario.

### Post-Design Constitution Gate

- SQLite remains the only persistence engine, including the request-outcome
  metadata.
- The WPF client has only service contracts and transport code; SQLite access is
  confined to the console service and data project.
- The service design includes log4net, colored Info/Warn/Error output, unit
  tests, integration tests, validation, transactional accuracy, and shared WPF
  styling for the connection/status and data-maintenance surfaces.
- **Result: PASS.** The design introduces no exception to the constitution.
