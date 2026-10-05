using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DatabaseLoadBalancing.Contracts.Health;
using DatabaseLoadBalancing.Data.Requests;
using DatabaseLoadBalancing.Data.Sqlite;
using DatabaseLoadBalancing.Data.Tables;
using DatabaseLoadBalancing.Server.Hosting;
using log4net;

var server = ConsoleServerApplication.Create(args);
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
await server.RunAsync(shutdown.Token);

public static class ServerApplication
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        Log4NetConsoleConfiguration.Configure(LogManager.GetRepository(typeof(Program).Assembly));

        var databaseProvider = DatabaseProviderFactory.Create(builder.Configuration);
        var sqlExecutionService = new SqlExecutionService(databaseProvider);
        builder.Services.AddSingleton<DatabaseLoadBalancing.Data.Abstractions.IDatabaseProvider>(databaseProvider);
        builder.Services.AddSingleton(sqlExecutionService);
        builder.Services.AddSingleton<DatabaseLoadBalancing.Data.Tables.TableMaintenanceService>();
        builder.Services.AddSingleton<DatabaseLoadBalancing.Server.Tables.TableMaintenanceService>();
        builder.Services.AddSingleton<ServiceReadinessMonitor>();

        var app = builder.Build();
        var logger = LogManager.GetLogger(typeof(ServerApplication));
        logger.Info($"DatabaseLoadBalancing.Server 正在启动，Provider={databaseProvider.ProviderName}。控制台服务已创建 HTTP 通信通道。");

        app.Use(async (context, next) =>
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                await next();
            }
            finally
            {
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                logger.Info($"{context.Request.Method} {context.Request.Path} => {context.Response.StatusCode} ({elapsed:0.0} ms)");
            }
        });

        app.Lifetime.ApplicationStarted.Register(() => logger.Info("DatabaseLoadBalancing.Server 已就绪，等待客户端请求。"));
        app.Lifetime.ApplicationStopping.Register(() => logger.Warn("DatabaseLoadBalancing.Server 正在停止。"));

        app.MapGet("/health", (ServiceReadinessMonitor readinessMonitor) =>
        {
            var health = readinessMonitor.Check();
            return health.DatabaseAccessible
                ? Results.Ok(health)
                : Results.Json(health, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        app.MapPost("/api/v1/sql/query", async (DatabaseLoadBalancing.Contracts.Sql.SqlRequest request, SqlExecutionService service, CancellationToken cancellationToken) =>
            ToHttpResult(await service.ExecuteQueryAsync(request, cancellationToken)));
        app.MapPost("/api/v1/sql/scalar", async (DatabaseLoadBalancing.Contracts.Sql.SqlRequest request, SqlExecutionService service, CancellationToken cancellationToken) =>
            ToHttpResult(await service.ExecuteScalarAsync(request, cancellationToken)));
        app.MapPost("/api/v1/sql/execute", async (DatabaseLoadBalancing.Contracts.Sql.SqlRequest request, SqlExecutionService service, CancellationToken cancellationToken) =>
            ToHttpResult(await service.ExecuteMutationAsync(request, cancellationToken)));
        app.MapPost("/api/v1/sql/transaction", async (DatabaseLoadBalancing.Contracts.Sql.SqlTransactionRequest request, SqlExecutionService service, CancellationToken cancellationToken) =>
            ToHttpResult(await service.ExecuteTransactionAsync(request, cancellationToken)));
        app.MapGet("/api/v1/requests/{requestId}", async (string requestId, SqlExecutionService service, CancellationToken cancellationToken) =>
            ToHttpResult(await service.GetRequestOutcomeAsync(requestId, cancellationToken)));

        app.MapGet("/api/v1/tables", async (DatabaseLoadBalancing.Server.Tables.TableMaintenanceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListTablesAsync(cancellationToken)));
        app.MapGet("/api/v1/tables/{tableName}", async (string tableName, DatabaseLoadBalancing.Server.Tables.TableMaintenanceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTableAsync(tableName, cancellationToken: cancellationToken)));
        app.MapGet("/api/v1/tables/{tableName}/records", async (string tableName, int? limit, int? offset, DatabaseLoadBalancing.Server.Tables.TableMaintenanceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTableAsync(tableName, limit ?? 200, offset ?? 0, cancellationToken)));
        app.MapPost("/api/v1/tables/{tableName}/records", async (string tableName, DatabaseLoadBalancing.Contracts.Tables.TableRecordRequest request, DatabaseLoadBalancing.Server.Tables.TableMaintenanceService service, CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateRecordAsync(tableName, request, cancellationToken)));
        app.MapPut("/api/v1/tables/{tableName}/records/{rowId:long}", async (string tableName, long rowId, DatabaseLoadBalancing.Contracts.Tables.TableRecordRequest request, DatabaseLoadBalancing.Server.Tables.TableMaintenanceService service, CancellationToken cancellationToken) =>
            ToHttpResult(await service.UpdateRecordAsync(tableName, rowId, request, cancellationToken)));
        app.MapDelete("/api/v1/tables/{tableName}/records/{rowId:long}", async (string tableName, long rowId, DatabaseLoadBalancing.Server.Tables.TableMaintenanceService service, CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeleteRecordAsync(tableName, rowId, cancellationToken: cancellationToken)));
        app.MapPost("/api/v1/tables/{tableName}/clear", async (string tableName, DatabaseLoadBalancing.Contracts.Tables.ClearTableRequest request, DatabaseLoadBalancing.Server.Tables.TableMaintenanceService service, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await service.ClearTableAsync(tableName, request.RequestId, cancellationToken));
            }
            catch (Exception exception)
            {
                logger.Error($"清空表 {tableName} 失败。", exception);
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        return app;
    }

    private static IResult ToHttpResult(DatabaseLoadBalancing.Contracts.Sql.SqlExecutionResult result)
    {
        if (result.Success)
        {
            return Results.Ok(result);
        }

        var status = result.Error?.Code is "sql.policy" or "sql.empty" or "sql.multiple" or "sql.parameters" or "transaction.empty" or "request.reused"
            ? StatusCodes.Status400BadRequest
            : result.Error?.Code == "request.not_found" ? StatusCodes.Status404NotFound
            : result.Error?.Retryable == true ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
        return Results.Json(result, statusCode: status);
    }
}

public partial class Program
{
}

