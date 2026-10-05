---

description: "集中式数据库访问服务的实现任务清单"
---

# Tasks: 集中式数据库访问服务

**Input**: `specs/001-centralize-database-access/` 下的 plan、spec、research、data-model、contracts 与 quickstart。

**Prerequisites**: `plan.md`、`spec.md`、`research.md`、`data-model.md`、`contracts/sql-service.md`。

**Tests**: 必需。项目宪章要求单元测试；规格还要求并发、恢复、CRUD、清空及直连基线的集成验证。

**Organization**: 任务按用户故事划分，使每个故事都可单独实现和验收。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可在其前置依赖完成后，与其他不同文件任务并行执行。
- **[USn]**: 用户故事标签；仅用于用户故事阶段的任务。
- 所有任务包含精确文件路径。

## Path Conventions

- 解决方案：`DatabaseLoadBalancing.sln`
- 源码：`src/`
- 测试：`tests/`
- 本功能文档：`specs/001-centralize-database-access/`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 初始化 .NET 10 多项目解决方案、依赖与测试工程。

- [X] T001 Create `DatabaseLoadBalancing.sln` and the planned `src/` and `tests/` project directories at repository root
- [X] T002 Create `src/DatabaseLoadBalancing.Contracts/DatabaseLoadBalancing.Contracts.csproj`, `src/DatabaseLoadBalancing.Data/DatabaseLoadBalancing.Data.csproj`, `src/DatabaseLoadBalancing.Server/DatabaseLoadBalancing.Server.csproj`, and `src/DatabaseLoadBalancing.Client/DatabaseLoadBalancing.Client.csproj` targeting .NET 10 with WPF enabled only for the client project
- [X] T003 [P] Create `Directory.Build.props` and `Directory.Packages.props` with shared .NET 10 settings and pinned Microsoft.Data.Sqlite, log4net, and xUnit package versions
- [X] T004 [P] Create `.editorconfig` with C# formatting, nullable-reference, and warning-as-error conventions for the solution
- [X] T005 [P] Create the five test project files under `tests/` and add their project references in `DatabaseLoadBalancing.sln`
- [X] T006 [P] Create `src/DatabaseLoadBalancing.Server/log4net.config` with non-sensitive structured layouts and separate Info, Warn, and Error appenders

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 建立所有用户故事共用的合同、SQLite 所有权、并发协调、日志和测试基础。

**⚠️ CRITICAL**: 本阶段完成前不得开始用户故事任务。

- [X] T007 Create generic SQL request, command-batch, parameter, result, and error DTOs in `src/DatabaseLoadBalancing.Contracts/Sql/`
- [X] T008 [P] Create `TableMetadata`, `TableColumnMetadata`, `TableRecordMutationRequest`, and `ClearTableRequest` DTOs in `src/DatabaseLoadBalancing.Contracts/Tables/`, preserving the constraints that `tableId` is server-approved, `key` identifies exactly one record for update/delete, and `values` contains only visible permitted columns and valid scalar values
- [X] T009 [P] Create `ServiceHealth` and health-response DTOs in `src/DatabaseLoadBalancing.Contracts/Health/`
- [X] T010 Create server database and HTTP configuration options in `src/DatabaseLoadBalancing.Server/Hosting/ServerOptions.cs` for local SQLite path, listen address, request limits, timeout limits, and allowed table policy
- [X] T011 Create SQLite connection factory and startup pragmas in `src/DatabaseLoadBalancing.Data/Sqlite/SqliteConnectionFactory.cs` with service-owned local-file access, bounded busy timeout, and read-friendly journal configuration
- [X] T012 Create schema initializer and validator in `src/DatabaseLoadBalancing.Data/Sqlite/DatabaseSchemaInitializer.cs` that creates the request-outcome table and rejects an unavailable or incompatible database before service readiness
- [X] T013 Create `RequestExecution` persistence model and repository in `src/DatabaseLoadBalancing.Data/Requests/RequestExecutionRepository.cs` with `request_id` as primary key and the exact fields defined in `data-model.md`
- [X] T014 Create SQL statement classifier and bound-parameter validator in `src/DatabaseLoadBalancing.Data/Sqlite/SqlCommandPolicy.cs` that permits normal `SELECT`, `INSERT`, `UPDATE`, and `DELETE` operations while rejecting administrative or unbounded client SQL
- [X] T015 Create single-writer mutation coordinator in `src/DatabaseLoadBalancing.Server/Execution/MutationCoordinator.cs` that queues mutations and treats each transaction batch as one atomic queue item
- [X] T016 Create SQL execution dispatcher in `src/DatabaseLoadBalancing.Server/Execution/SqlExecutionDispatcher.cs` for concurrent safe reads, transactional mutations, bounded lock retries, and rollback on failure
- [X] T017 [P] Create common error-code mapping in `src/DatabaseLoadBalancing.Server/Api/ApiErrorMapper.cs` for `ValidationFailed`, `SqlNotAllowed`, `RequestConflict`, `DatabaseBusy`, `Timeout`, `ServiceUnavailable`, and `ExecutionFailed`
- [X] T018 Create log4net bootstrap and safe request-context logging in `src/DatabaseLoadBalancing.Server/Hosting/LoggingBootstrap.cs` without SQL parameter values, secrets, or raw SQLite paths
- [X] T019 Create colored Info, Warn, and Error console appender in `src/DatabaseLoadBalancing.Server/Hosting/ColoredConsoleAppender.cs` with textual level labels when color is unavailable
- [X] T020 Create console-service composition root in `src/DatabaseLoadBalancing.Server/Program.cs` that registers configuration, SQLite, dispatcher, coordinator, logging, and HTTP routing dependencies
- [X] T021 Create shared temporary-database, HTTP-host, and two-authorized-table fixtures in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Support/`

**Checkpoint**: Foundation ready — all user stories can start, and no WPF project has a SQLite package reference or file-access dependency.

---

## Phase 3: User Story 1 - 客户端通过服务端完成数据操作 (Priority: P1) 🎯 MVP

**Goal**: 客户端只能通过服务端执行参数化查询、标量查询、写入和事务，不再直接访问 SQLite 文件。

**Independent Test**: 启动服务端后，使用服务客户端完成一次参数化查询和写入；在客户端无法访问数据库文件时仍可完成操作并得到明确结果。

### Tests for User Story 1

- [X] T022 [P] [US1] Create contract tests for `/api/v1/sql/query`, `/api/v1/sql/scalar`, `/api/v1/sql/execute`, and `/api/v1/sql/transaction` in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Sql/SqlEndpointContractTests.cs`
- [X] T023 [P] [US1] Create dispatcher unit tests for allowed statements, parameter binding, validation failure, and transaction rollback in `tests/DatabaseLoadBalancing.Server.UnitTests/Execution/SqlExecutionDispatcherTests.cs`
- [X] T024 [P] [US1] Create WPF service-client tests for explicit success, validation, timeout, and unavailable responses in `tests/DatabaseLoadBalancing.Client.UnitTests/Services/SqlServiceClientTests.cs`

### Implementation for User Story 1

- [X] T025 [US1] Implement query, scalar, execute, transaction, and request-outcome endpoint mappings in `src/DatabaseLoadBalancing.Server/Api/SqlEndpoints.cs`
- [X] T026 [US1] Complete generic query, scalar, execute, and transaction handlers in `src/DatabaseLoadBalancing.Server/Execution/SqlExecutionDispatcher.cs` using the shared request and result contracts
- [X] T027 [US1] Implement HTTP SQL service client in `src/DatabaseLoadBalancing.Client/Services/SqlServiceClient.cs` without SQLite references or database file-path handling
- [X] T028 [US1] Implement connection-state view model and user-facing unavailable/error messages in `src/DatabaseLoadBalancing.Client/ViewModels/ConnectionStatusViewModel.cs`
- [X] T029 [US1] Run and fix the US1 contract, dispatcher, and WPF service-client tests in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Sql/SqlEndpointContractTests.cs`

**Checkpoint**: A client can complete supported data operations only through the running service and receives explicit outcomes.

---

## Phase 4: User Story 2 - 多客户端并发写入得到可靠处理 (Priority: P1)

**Goal**: 多个客户端并发写入时，服务端统一排队、事务化处理并防止重复提交和部分写入。

**Independent Test**: 使用 10 个并发客户端提交至少 100 个写入请求；每个请求都有明确结果，成功写入与最终数据一致，重复请求不重复生效。

### Tests for User Story 2

- [X] T030 [P] [US2] Create a 10-client and 100-write concurrent integration test in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Concurrency/ConcurrentWriteTests.cs`
- [X] T031 [P] [US2] Create duplicate-request-ID and different-hash conflict tests in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Requests/RequestIdempotencyTests.cs`
- [X] T032 [P] [US2] Create mutation-coordinator ordering, bounded-retry, and rollback unit tests in `tests/DatabaseLoadBalancing.Server.UnitTests/Execution/MutationCoordinatorTests.cs`

### Implementation for User Story 2

- [X] T033 [US2] Implement request-hash comparison, committed-outcome reuse, and different-hash rejection in `src/DatabaseLoadBalancing.Server/Execution/RequestIdempotencyService.cs`
- [X] T034 [US2] Wire `RequestExecutionRepository` into mutation commits in `src/DatabaseLoadBalancing.Server/Execution/SqlExecutionDispatcher.cs` so the request outcome and business mutation commit in one transaction
- [X] T035 [US2] Finalize FIFO mutation queue, bounded SQLite busy handling, and actual affected-row reporting in `src/DatabaseLoadBalancing.Server/Execution/MutationCoordinator.cs`
- [X] T036 [US2] Add Warn-level contention and retry logs plus Error-level rollback logs in `src/DatabaseLoadBalancing.Server/Execution/SqlExecutionDispatcher.cs`
- [X] T037 [US2] Run the concurrent-write and idempotency tests and verify the SC-001 through SC-004 outcomes in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Concurrency/ConcurrentWriteTests.cs`

**Checkpoint**: Concurrent writes are coordinated by the service; retries with the same request ID do not duplicate data.

---

## Phase 5: User Story 4 - 浏览和维护多个数据表 (Priority: P1)

**Goal**: WPF 客户端显示服务端允许的数据表和记录，并完成新增、编辑、删除、清空及二次确认。

**Independent Test**: 在包含至少两张允许表的测试数据库中，切换表并完成 CRUD 与清空；清空仅影响选中表，取消不产生写入。

### Tests for User Story 4

- [X] T038 [P] [US4] Create contract tests for `/api/v1/tables`, records CRUD, and `/clear` endpoints in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Tables/TableMaintenanceContractTests.cs`
- [X] T039 [P] [US4] Create unit tests for authorized-table filtering and column rules in `tests/DatabaseLoadBalancing.Server.UnitTests/Tables/TableMetadataServiceTests.cs`, including that `allowedOperations` is a subset of `read`, `create`, `update`, `delete`, and `clear`
- [X] T040 [P] [US4] Create integration tests for record create/edit/delete, clear isolation, schema preservation, and actual affected count in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Tables/TableRecordMutationTests.cs`
- [X] T041 [P] [US4] Create WPF view-model tests for table list, empty state, list refresh, and no-direct-file-path display in `tests/DatabaseLoadBalancing.Client.UnitTests/Tables/TableBrowserViewModelTests.cs`
- [X] T042 [P] [US4] Create WPF confirmation tests for showing table name and current count, cancellation without HTTP call, and confirmed clear result in `tests/DatabaseLoadBalancing.Client.UnitTests/Tables/ClearTableConfirmationViewModelTests.cs`

### Implementation for User Story 4

- [X] T043 [US4] Implement service-authorized table and column metadata discovery in `src/DatabaseLoadBalancing.Data/Tables/TableMetadataRepository.cs` without exposing SQLite paths or infrastructure tables
- [X] T044 [US4] Implement paged record retrieval and input validation in `src/DatabaseLoadBalancing.Data/Tables/TableRecordRepository.cs`, enforcing that client values are visible permitted columns and update/delete keys identify exactly one record
- [X] T045 [US4] Implement create, update, delete, and clear mutations in `src/DatabaseLoadBalancing.Server/Tables/TableMaintenanceService.cs`, preserving other tables and target table schema during clear
- [X] T046 [US4] Implement table list, record list, CRUD, and clear endpoint mappings in `src/DatabaseLoadBalancing.Server/Api/TableMaintenanceEndpoints.cs`
- [X] T047 [US4] Implement table-maintenance HTTP client in `src/DatabaseLoadBalancing.Client/Services/TableMaintenanceClient.cs` using only the shared table contracts
- [X] T048 [US4] Implement table browser and record-list view models in `src/DatabaseLoadBalancing.Client/Tables/TableBrowserViewModel.cs` with table switching, empty state, refresh, and explicit service errors
- [X] T049 [US4] Implement record editor view and view model in `src/DatabaseLoadBalancing.Client/Views/RecordEditorView.xaml` and `src/DatabaseLoadBalancing.Client/ViewModels/RecordEditorViewModel.cs` using `TableColumnMetadata` required, editable, and primary-key rules
- [X] T050 [US4] Implement shared-style clear confirmation view and view model in `src/DatabaseLoadBalancing.Client/Views/ClearTableConfirmationView.xaml` and `src/DatabaseLoadBalancing.Client/ViewModels/ClearTableConfirmationViewModel.cs` that displays the selected table name and current record count before issuing a request
- [X] T051 [US4] Wire data-maintenance views, dialogs, and shared styles in `src/DatabaseLoadBalancing.Client/Views/DataMaintenanceView.xaml` and `src/DatabaseLoadBalancing.Client/Resources/Styles.xaml`
- [X] T052 [US4] Run table-maintenance contract, integration, and WPF tests in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Tables/TableMaintenanceContractTests.cs`

**Checkpoint**: The WPF client safely maintains records in all service-authorized tables and can clear only the selected table after explicit confirmation.

---

## Phase 6: User Story 3 - 服务端故障和重启可控 (Priority: P2)

**Goal**: 服务端启动验证数据库、显示健康状态、记录彩色日志，并让客户端在不可用和恢复后得到明确状态。

**Independent Test**: 在数据库不可用、服务端停止和重启场景下，客户端获得明确结果；恢复后新的数据请求成功且已提交数据保持完整。

### Tests for User Story 3

- [X] T053 [P] [US3] Create startup-validation, unavailable-service, and restart integration tests in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Recovery/ServiceRecoveryTests.cs`
- [X] T054 [P] [US3] Create health-contract and WPF reconnection-state tests in `tests/DatabaseLoadBalancing.Client.UnitTests/Services/ServiceHealthClientTests.cs`
- [X] T055 [P] [US3] Create log4net level-label and colored-console fallback tests in `tests/DatabaseLoadBalancing.Server.UnitTests/Hosting/ColoredConsoleAppenderTests.cs`

### Implementation for User Story 3

- [X] T056 [US3] Implement database startup validation and readiness-state transitions in `src/DatabaseLoadBalancing.Server/Hosting/ServiceReadinessMonitor.cs`
- [X] T057 [US3] Implement `GET /health` endpoint and startup/recovery lifecycle responses in `src/DatabaseLoadBalancing.Server/Api/HealthEndpoints.cs`
- [X] T058 [US3] Implement health polling, unavailable prompt, and recovery refresh in `src/DatabaseLoadBalancing.Client/Services/ServiceHealthClient.cs` and `src/DatabaseLoadBalancing.Client/ViewModels/ConnectionStatusViewModel.cs`
- [X] T059 [US3] Add Info startup/request, Warn recoverable, and Error failure log events in `src/DatabaseLoadBalancing.Server/Hosting/ServiceReadinessMonitor.cs`
- [X] T060 [US3] Run recovery, health, and console-output tests in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Recovery/ServiceRecoveryTests.cs`

**Checkpoint**: Startup failures, unavailable service, and recovery are observable and do not corrupt committed data.

---

## Phase 7: User Story 5 - 对比直连与服务端访问的并发结果 (Priority: P2)

**Goal**: 生成可重复的直连 SQLite 基线与经服务端访问场景的并发写入对比结果。

**Independent Test**: 在两个相同初始数据的临时数据库副本上运行 10 客户端、至少 100 写入；产出每个场景的成功、失败、超时、锁定或竞争错误、耗时和最终数据校验结果。

### Tests for User Story 5

- [X] T061 [P] [US5] Create direct-SQLite baseline runner tests in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Comparison/DirectSqliteBaselineTests.cs` using only disposable database copies
- [X] T062 [P] [US5] Create service-path comparison runner tests in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Comparison/ServiceAccessComparisonTests.cs` for 10 clients and at least 100 writes
- [X] T063 [P] [US5] Create comparison-report metric and final-data-validation tests in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Comparison/ComparisonReportTests.cs`

### Implementation for User Story 5

- [X] T064 [US5] Implement isolated direct SQLite baseline runner in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Comparison/DirectSqliteBaselineRunner.cs` and prevent any production WPF project reference to it
- [X] T065 [US5] Implement service-access scenario runner in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Comparison/ServiceAccessScenarioRunner.cs` using the public HTTP service contract
- [X] T066 [US5] Implement comparison report writer in `tests/DatabaseLoadBalancing.Server.IntegrationTests/Comparison/ComparisonReportWriter.cs` for success, failure, timeout, lock or contention, duration, and final-data metrics
- [X] T067 [US5] Add an architecture test in `tests/DatabaseLoadBalancing.Client.UnitTests/Architecture/NoDirectSqliteAccessTests.cs` that rejects SQLite package or database-file access from production client assemblies
- [X] T068 [US5] Run the complete comparison scenario and save the acceptance output under `artifacts/comparison/001-centralize-database-access.md`

**Checkpoint**: The comparison report proves that the production service path avoids direct-client SQLite contention while preserving correct final data.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: 完成跨故事质量门禁、可维护性和端到端验收。

- [X] T069 [P] Review all log statements for sensitive parameter, secret, and raw SQLite-path exposure in `src/DatabaseLoadBalancing.Server/`
- [X] T070 [P] Review WPF data-maintenance controls and destructive-action dialog for shared typography, colors, spacing, control states, and interaction conventions in `src/DatabaseLoadBalancing.Client/Resources/Styles.xaml`
- [X] T071 Add solution-level build, test, and coverage commands to `README.md`
- [X] T072 Run the full Release build and all tests with `DatabaseLoadBalancing.sln` and record results in `artifacts/validation/001-centralize-database-access.md`
- [X] T073 Execute every quickstart scenario and update only the observed validation notes in `specs/001-centralize-database-access/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 — Setup**: Starts immediately.
- **Phase 2 — Foundational**: Depends on Setup and blocks every user story.
- **Phase 3 — US1**: Depends on Foundational; establishes the service-only client path and is the MVP.
- **Phase 4 — US2**: Depends on Foundational and the generic service execution surface from US1.
- **Phase 5 — US4**: Depends on Foundational and reuses the service-client conventions from US1; it may proceed alongside US2 once US1 contracts are stable.
- **Phase 6 — US3**: Depends on Foundational; can proceed alongside US2 or US4 after shared hosting contracts are stable.
- **Phase 7 — US5**: Depends on US1 and US2 because it compares the working service path with the direct baseline.
- **Phase 8 — Polish**: Depends on all selected user-story phases.

### User Story Completion Order

```text
Setup -> Foundational -> US1 (MVP) -> US2 and US4 -> US3 -> US5 -> Polish
```

### Parallel Opportunities

- After T001/T002, T003 through T006 affect independent configuration or project files and can run in parallel.
- After T007, T008, T009, and T017 affect separate contract or error-mapping files and can run in parallel.
- Within US1, T022 through T024 can be written in parallel before implementation.
- Within US2, T030 through T032 can be written in parallel before implementation.
- Within US4, T038 through T042 can be written in parallel before implementation.
- Within US3, T053 through T055 can be written in parallel before implementation.
- Within US5, T061 through T063 can be written in parallel before implementation.
- T069 and T070 can run in parallel after the feature work is complete.

## Parallel Example: User Story 4

```text
Task: "Create contract tests in tests/DatabaseLoadBalancing.Server.IntegrationTests/Tables/TableMaintenanceContractTests.cs"
Task: "Create metadata-policy tests in tests/DatabaseLoadBalancing.Server.UnitTests/Tables/TableMetadataServiceTests.cs"
Task: "Create WPF browser tests in tests/DatabaseLoadBalancing.Client.UnitTests/Tables/TableBrowserViewModelTests.cs"
Task: "Create clear-confirmation tests in tests/DatabaseLoadBalancing.Client.UnitTests/Tables/ClearTableConfirmationViewModelTests.cs"
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup and Foundational phases.
2. Complete US1 through T029.
3. Run the US1 contract, dispatcher, and WPF service-client tests.
4. Demonstrate a client query and write with the SQLite file inaccessible to the client.

### Incremental Delivery

1. Add US2 to validate the central write-coordination benefit.
2. Add US4 to deliver the WPF multi-table maintenance experience.
3. Add US3 to harden service startup, observability, and recovery.
4. Add US5 to produce the direct-versus-service evidence report.
5. Complete the cross-cutting build, test, style, logging, and quickstart gates.

## Notes

- All listed tests are required by the project constitution and feature specification.
- `[P]` means the task can run in parallel only after its prerequisites are complete.
- Production WPF code must never reference SQLite packages, database paths, or direct baseline test code.
- The direct SQLite runner exists only under integration tests and only uses disposable database copies.
