using DatabaseLoadBalancing.Contracts.Sql;
using Microsoft.AspNetCore.Http;

namespace DatabaseLoadBalancing.Server.Api;

public static class ApiErrorMapper
{
    public static int StatusCode(SqlError? error)
    {
        if (error is null) return StatusCodes.Status500InternalServerError;
        if (error.Code is "sql.policy" or "sql.empty" or "sql.multiple" or "transaction.empty" or "request.reused")
            return StatusCodes.Status400BadRequest;
        if (error.Retryable) return StatusCodes.Status503ServiceUnavailable;
        if (error.Code.Contains("timeout", StringComparison.OrdinalIgnoreCase)) return StatusCodes.Status408RequestTimeout;
        return StatusCodes.Status500InternalServerError;
    }
}

