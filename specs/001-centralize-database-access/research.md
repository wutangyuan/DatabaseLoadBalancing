# Research: 集中式数据库访问服务

## Decision 1: .NET 10 multi-project solution

- **Decision**: Use C# with .NET 10, targeting `net10.0-windows` for the WPF
  client and .NET 10 for the console service and shared libraries.
- **Rationale**: The host provides .NET SDK 10.0.201 and the Windows Desktop
  runtime. This directly supports the constitution's WPF and console requirements
  without introducing a second runtime.
- **Alternatives considered**: .NET 8 LTS was rejected for the initial plan
  because the available environment already provides .NET 10 and the repository
  has no existing compatibility constraint. The exact package versions remain
  pinned during bootstrap.

## Decision 2: Console-hosted HTTP/JSON service

- **Decision**: Host an HTTP/JSON API inside the console server process.
- **Rationale**: Multiple clients may run on different Windows machines. HTTP
  is easy to diagnose, supports explicit request and response contracts, and
  keeps the WPF client independent from the database provider.
- **Alternatives considered**: Named pipes are appropriate for same-machine
  communication but do not fit a likely multi-client LAN deployment. Raw TCP
  would require a custom framing and compatibility protocol. gRPC is viable but
  adds a binary contract and code-generation dependency that is not required for
  this first release.

## Decision 3: Separate read execution from coordinated writes

- **Decision**: Permit safe read requests to execute concurrently and route all
  mutations through one service-owned write coordinator. Each mutation or batch
  is executed inside an explicit transaction with bounded timeout and retry
  behavior for transient locks.
- **Rationale**: SQLite permits concurrent reads but has a single-writer limit.
  Centralizing writes removes cross-process contention while preserving useful
  read concurrency. A single queue also makes ordering and failure handling
  observable.
- **Alternatives considered**: A process-wide lock without a queue does not
  provide fair ordering or request status. Multiple server workers writing the
  same SQLite file would reintroduce lock contention and is outside the first
  release scope.

## Decision 4: Parameterized SQL with server-side command policy

- **Decision**: Accept SQL text plus named parameters, but expose separate query,
  mutation, scalar, and transaction contracts. Ordinary client requests allow
  `SELECT`, `INSERT`, `UPDATE`, and `DELETE`; schema and database-administration
  statements require a separately protected administrative path.
- **Rationale**: This preserves the user's requirement that the service execute
  SQL while preventing clients from directly controlling the database file or
  executing unrestricted destructive operations. Parameter binding prevents
  value concatenation errors and injection through values.
- **Alternatives considered**: Fixed business endpoints provide stronger domain
  safety but do not meet the requirement for SQL execution. Unrestricted raw
  SQL would make destructive operations, resource exhaustion, and accidental
  schema changes too easy.

## Decision 5: Request IDs and committed outcomes for write idempotency

- **Decision**: Every mutation and transaction request carries a unique request
  ID. The service records the request hash and final outcome in the same SQLite
  transaction as the business SQL. A repeated ID with the same hash returns the
  recorded outcome; a repeated ID with a different hash is rejected.
- **Rationale**: A client can lose the response after the database commits. The
  committed outcome lets the client query status or retry without duplicating a
  write. Recording the outcome atomically with the business SQL prevents a
  success record from being stored when the business transaction rolled back.
- **Alternatives considered**: Blind client retries can duplicate INSERTs. An
  in-memory request cache is lost on restart and cannot prove whether a commit
  already happened.

## Decision 6: Test strategy

- **Decision**: Use xUnit for isolated unit tests and integration tests that
  start the service against temporary SQLite files and submit concurrent HTTP
  requests.
- **Rationale**: The main risk is a boundary interaction among transport,
  queueing, transactions, and SQLite locking. Unit tests cover deterministic
  validation and coordination rules; integration tests prove the actual
  multi-client behavior and recovery scenarios.
- **Alternatives considered**: Unit tests alone cannot prove real SQLite lock,
  transaction, restart, or concurrent-request behavior. Manual testing alone is
  not repeatable enough for the constitution's data-accuracy requirement.

## Decision 7: Server-authorized metadata for multi-table maintenance

- **Decision**: The service exposes a list of authorized data tables and their
  column metadata. The WPF client uses this metadata to render the table browser,
  record list, and record editor; it never enumerates database files or reads the
  SQLite schema directly.
- **Rationale**: The client must browse all supported tables and their records,
  while the service remains the single policy and database boundary. An explicit
  table policy prevents hidden infrastructure tables, request-outcome metadata,
  and unsupported tables from appearing in the client.
- **Alternatives considered**: Letting the client inspect SQLite schema would
  violate the no-direct-access rule. Exposing every physical SQLite table would
  reveal service metadata and make it too easy to offer unsupported destructive
  operations.

## Decision 8: Confirmed, selected-table clear operation

- **Decision**: Clearing is a dedicated service mutation for the currently
  selected authorized table. The WPF client displays the table name and current
  record count, then requires an explicit second confirmation. Cancellation sends
  no request; a confirmed clear returns the actual affected-row count.
- **Rationale**: The operation is destructive and must not remove another table's
  data or schema. Server-side execution keeps it transactional and makes its
  result auditable even if table data changes between the UI preview and commit.
- **Alternatives considered**: A generic arbitrary `DELETE` request provides no
  reliable confirmation semantics. A bulk clear across all tables or table-drop
  operation has an unnecessarily broad blast radius and is out of scope.

## Resolved Unknowns

- Runtime and target platform: resolved to .NET 10 on Windows.
- Service transport: resolved to HTTP/JSON hosted by the console process.
- SQLite concurrency model: resolved to concurrent reads plus a single write
  coordinator and transactional mutations.
- SQL scope: resolved to parameterized query and DML SQL for normal clients,
  with administrative SQL separated from ordinary requests.
- Table browsing: resolved to server-authorized table metadata and record lists,
  not database-file browsing or unrestricted schema discovery.
- Destructive maintenance: resolved to selected-table record clear with displayed
  name and record count, explicit confirmation, no undo, and no schema removal.
- Initial scale: resolved to 10 concurrent clients and 20 operations per second,
  with one active service instance per SQLite file.
