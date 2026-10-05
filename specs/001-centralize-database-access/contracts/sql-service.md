# SQL Service Contract

## Transport

- Protocol: HTTP/JSON over the trusted local network.
- Base path: `/api/v1`.
- Content type: `application/json`.
- Every mutation request MUST include a unique `requestId`.
- The client MUST NOT receive or use the SQLite file path.

## Endpoints

### `GET /health`

Returns service and database readiness.

Successful response:

```json
{
  "status": "Ready",
  "databaseAccessible": true,
  "lastError": null,
  "checkedAt": "2026-10-05T12:00:00Z"
}
```

Status codes:

- `200`: service is ready or degraded with a valid health payload.
- `503`: service is unavailable or the database failed startup validation.

### `POST /api/v1/sql/query`

Executes one read-only query and returns bounded rows.

Request:

```json
{
  "sql": "SELECT Id, Name FROM Users WHERE Id = @Id",
  "parameters": { "Id": 7 },
  "timeoutMs": 2000
}
```

Response:

```json
{
  "success": true,
  "columns": ["Id", "Name"],
  "rows": [[7, "Example"]],
  "affectedRows": null,
  "errorCode": null,
  "errorMessage": null,
  "durationMs": 18,
  "retryable": false
}
```

The server rejects mutation or administrative statements on this endpoint.

### `POST /api/v1/sql/execute`

Executes one parameterized `INSERT`, `UPDATE`, or `DELETE` statement.

Request:

```json
{
  "requestId": "client-a-20261005-000001",
  "sql": "UPDATE Users SET Name = @Name WHERE Id = @Id",
  "parameters": { "Name": "Updated", "Id": 7 },
  "timeoutMs": 2000
}
```

Response:

```json
{
  "requestId": "client-a-20261005-000001",
  "success": true,
  "affectedRows": 1,
  "errorCode": null,
  "errorMessage": null,
  "durationMs": 24,
  "retryable": false
}
```

Repeated requests with the same `requestId` and identical request hash return
the recorded outcome without executing the SQL again. A different request with
the same ID returns a conflict.

### `POST /api/v1/sql/transaction`

Executes an ordered list of parameterized DML commands as one transaction.

Request:

```json
{
  "requestId": "client-a-20261005-000002",
  "commands": [
    {
      "sql": "UPDATE Accounts SET Balance = Balance - @Amount WHERE Id = @From",
      "parameters": { "Amount": 10, "From": 1 }
    },
    {
      "sql": "UPDATE Accounts SET Balance = Balance + @Amount WHERE Id = @To",
      "parameters": { "Amount": 10, "To": 2 }
    }
  ],
  "timeoutMs": 3000
}
```

The service commits all commands or rolls them all back. The request is one
write-queue item and has one recorded outcome.

### `POST /api/v1/sql/scalar`

Executes a read-only statement expected to return one value. It follows the
same parameter and timeout rules as `/sql/query`.

### `GET /api/v1/requests/{requestId}`

Returns the committed outcome of a mutation request so a client can recover
after a response timeout or connection loss.

Responses:

- `200`: recorded success or failure outcome.
- `404`: no committed outcome exists for the request ID.
- `409`: the request ID is associated with a different request hash when the
  hash is supplied for verification.

## Table Maintenance Endpoints

These endpoints give the WPF client a controlled record-maintenance interface.
They expose only service-authorized tables and fields; they do not expose a
SQLite file path or unrestricted schema discovery.

### `GET /api/v1/tables`

Returns the data tables that the current client is permitted to browse.

Response:

```json
{
  "tables": [
    {
      "tableId": "users",
      "displayName": "Users",
      "primaryKeyColumns": ["Id"],
      "allowedOperations": ["read", "create", "update", "delete", "clear"],
      "recordCount": 42,
      "columns": [
        { "name": "Id", "displayName": "Id", "dataType": "integer", "isRequired": true, "isPrimaryKey": true, "isEditable": false },
        { "name": "Name", "displayName": "Name", "dataType": "text", "isRequired": true, "isPrimaryKey": false, "isEditable": true }
      ]
    }
  ]
}
```

### `GET /api/v1/tables/{tableId}/records`

Returns a bounded, ordered list of records for an authorized table. The request
accepts pagination parameters such as `offset` and `limit`; the service applies
a maximum limit to prevent unbounded responses.

Response:

```json
{
  "tableId": "users",
  "columns": ["Id", "Name"],
  "rows": [{ "Id": 7, "Name": "Example" }],
  "totalCount": 42
}
```

### `POST /api/v1/tables/{tableId}/records`

Creates one record in an authorized table.

```json
{
  "requestId": "client-a-20261005-000010",
  "values": { "Name": "New user" }
}
```

The service validates visible and required fields, executes the create through
the write coordinator, and returns the created key and affected-row count.

### `PUT /api/v1/tables/{tableId}/records`

Edits one record in an authorized table.

```json
{
  "requestId": "client-a-20261005-000011",
  "key": { "Id": 7 },
  "values": { "Name": "Updated user" }
}
```

The key must identify exactly one current record. The service rejects unknown,
non-editable, or invalid fields and returns a conflict if the configured
concurrency policy prevents the update.

### `DELETE /api/v1/tables/{tableId}/records`

Deletes one record in an authorized table.

```json
{
  "requestId": "client-a-20261005-000012",
  "key": { "Id": 7 }
}
```

The response uses the common mutation result and includes `affectedRows`.

### `POST /api/v1/tables/{tableId}/clear`

Clears all records in the selected authorized table after the WPF client has
displayed its name and current record count and the user has explicitly
confirmed the action.

```json
{
  "requestId": "client-a-20261005-000013"
}
```

The service clears only the requested table in one transaction, preserves its
schema and all other tables, and returns the actual `affectedRows` value. A
client cancellation must not call this endpoint.

## Error Contract

All non-success responses use:

```json
{
  "success": false,
  "errorCode": "ValidationFailed",
  "errorMessage": "Parameter 'Id' is required.",
  "retryable": false,
  "requestId": "client-a-20261005-000003"
}
```

Initial error categories are `ValidationFailed`, `SqlNotAllowed`,
`RequestConflict`, `DatabaseBusy`, `Timeout`, `ServiceUnavailable`, and
`ExecutionFailed`. Error messages must not include secrets or raw file paths.
