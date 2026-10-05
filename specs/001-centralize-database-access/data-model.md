# Data Model: 集中式数据库访问服务

## Overview

The feature does not redefine existing business tables. It adds a small
service-owned request outcome record so that a committed write can be safely
recognized when a client retries after losing a response.

## Entities

### SqlRequest

Transport object representing one client request.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `requestId` | string | Yes for mutations | Globally unique within the request-outcome retention window; stable across retries |
| `operation` | enum | Yes | `query`, `execute`, `scalar`, or `transaction` |
| `sql` | string | Yes for query/execute/scalar | One non-empty statement; must use named parameters for values |
| `parameters` | object map | No | Names must match SQL placeholders; values must be supported SQLite scalar types |
| `commands` | array | Yes for transaction | Non-empty ordered SQL commands; each command has SQL and parameters |
| `timeoutMs` | integer | No | Bounded by the service policy; client cannot disable the limit |

Validation: request size, SQL length, parameter count, command count, parameter
names, and supported value types are checked before queueing. Mutation SQL must
be classified as allowed DML; administrative statements use a separate policy.

### SqlParameter

Named input value bound by the service without string concatenation.

Allowed values are null, Boolean, integer, floating-point, text, and byte data.
The service rejects unsupported nested objects and unbounded payloads.

### TableMetadata

Service-approved metadata for one browsable data table. This is a transport
entity; it does not expose a SQLite file path or unrestricted schema objects.

| Field | Type | Rules |
|---|---|---|
| `tableId` | string | Stable server-approved identifier used by table-maintenance endpoints |
| `displayName` | string | Name shown in the WPF table list |
| `columns` | array of `TableColumnMetadata` | Contains only client-visible columns in display order |
| `primaryKeyColumns` | array of string | Identifies a record for edit and delete operations |
| `allowedOperations` | array | Subset of `read`, `create`, `update`, `delete`, and `clear` approved by the server |
| `recordCount` | integer | Current count for the selected table; may change before a later mutation |

### TableColumnMetadata

Describes how the WPF client displays and validates one record field.

| Field | Type | Rules |
|---|---|---|
| `name` | string | Database column name approved for the client contract |
| `displayName` | string | Human-readable label |
| `dataType` | string | Supported scalar type used for input conversion and validation |
| `isRequired` | Boolean | Indicates whether empty input is rejected |
| `isPrimaryKey` | Boolean | Primary-key input is immutable during record edit unless explicitly allowed |
| `isEditable` | Boolean | Controls whether the client permits editing the field |

### TableRecordMutationRequest

Structured request used by the WPF client to create, edit, or delete one record
in an authorized table.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `requestId` | string | Yes | Stable across retries for create, update, and delete |
| `tableId` | string | Yes | Must exist in the server-approved metadata list |
| `key` | object map | Update/delete | Must identify exactly one record through the approved primary key |
| `values` | object map | Create/update | Contains only visible, permitted columns and valid scalar values |

### ClearTableRequest

Structured request used after the user explicitly confirms clearing the selected
table.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `requestId` | string | Yes | Stable across retries and recorded with the clear outcome |
| `tableId` | string | Yes | Must be the currently selected server-authorized table with `clear` permission |

The server resolves the current rows at execution time, deletes only records in
the requested table inside one transaction, preserves schema and other tables,
and returns the actual affected-row count.

### RequestExecution

Service-owned SQLite table used for mutation idempotency and outcome lookup.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `request_id` | text | Yes | Primary key; one record per mutation request |
| `request_hash` | text | Yes | Hash of normalized operation, SQL, parameters, and command order |
| `operation` | text | Yes | Operation category |
| `status` | text | Yes | `Succeeded` or `Failed` for committed outcomes |
| `affected_rows` | integer | No | Present for mutation outcomes |
| `error_code` | text | No | Present for committed business or validation failure outcomes |
| `error_message` | text | No | Sanitized client-facing reason |
| `created_at` | timestamp | Yes | Server UTC time |
| `completed_at` | timestamp | Yes | Server UTC time |

The request outcome record and the business mutation commit in the same
transaction. A repeated request ID with a different request hash is rejected as
a conflict. Query requests do not need persistent idempotency records unless a
later requirement makes query replay auditing necessary.

### SqlExecutionResult

Transient response returned to the client.

| Field | Type | Rules |
|---|---|---|
| `requestId` | string | Echoes the submitted request ID when present |
| `success` | Boolean | True only when the requested operation completed |
| `errorCode` | string? | Stable category for validation, conflict, timeout, unavailable, or execution failure |
| `errorMessage` | string? | Sanitized, actionable explanation |
| `affectedRows` | integer? | Returned for mutation operations |
| `columns` | array? | Returned for query results |
| `rows` | array? | Returned for query results and bounded by response limits |
| `durationMs` | integer | Service-measured duration |
| `retryable` | Boolean | True only when retrying is safe under the request contract |

### ServiceHealth

Runtime status used by the health endpoint and client connection state.

| Field | Meaning |
|---|---|
| `status` | `Starting`, `Ready`, `Degraded`, or `Unavailable` |
| `databaseAccessible` | Whether the service can open and validate the configured SQLite file |
| `lastError` | Sanitized recent error, if any |
| `checkedAt` | UTC health-check time |

## State Transitions

### Mutation request lifecycle

```text
Received -> Validated -> Queued -> Executing -> Succeeded
                                   └─────────> Failed
Duplicate request ID -> Recorded outcome returned
Conflicting request hash -> Rejected
```

An executing mutation is not automatically retried after an unknown transport
failure. The client queries the request status using the request ID; the service
returns the committed outcome if the transaction completed.

### Clear-table lifecycle

```text
Table selected -> Name and count displayed -> User cancels -> No service request
                                       └----> User confirms -> Clear queued
                                                              -> Transactional delete
                                                              -> Actual affected count returned
```

## Relationships

- `SqlRequest` produces one `SqlExecutionResult`.
- A mutation `SqlRequest` creates or reuses one `RequestExecution` record.
- `TableMetadata` exposes a controlled subset of the database schema to the WPF
  client and governs `TableRecordMutationRequest` and `ClearTableRequest`.
- A `TableRecordMutationRequest` or `ClearTableRequest` produces one
  `SqlExecutionResult` and creates or reuses one `RequestExecution` record.
- A `ServiceHealth` value describes the service and its access to the SQLite
  database; it is not business data.
