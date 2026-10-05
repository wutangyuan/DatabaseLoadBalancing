namespace DatabaseLoadBalancing.Server.Api;

public static class TableMaintenanceEndpoints
{
    public const string Tables = "/api/v1/tables";
    public const string Records = "/api/v1/tables/{tableId}/records";
    public const string Clear = "/api/v1/tables/{tableId}/clear";
}

