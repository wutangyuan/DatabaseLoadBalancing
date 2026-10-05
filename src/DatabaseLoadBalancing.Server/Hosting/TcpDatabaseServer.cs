using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using DatabaseLoadBalancing.Contracts.Sql;
using DatabaseLoadBalancing.Contracts.Tables;
using DatabaseLoadBalancing.Contracts.Transport;
using DatabaseLoadBalancing.Data.Requests;
using DatabaseLoadBalancing.Server.Tables;
using log4net;

namespace DatabaseLoadBalancing.Server.Hosting;

public sealed class TcpDatabaseServer
{
    private readonly int _port;
    private readonly string _providerName;
    private readonly SqlExecutionService _sqlExecutionService;
    private readonly TableMaintenanceService _tableService;
    private readonly ServiceReadinessMonitor _readinessMonitor;
    private readonly ILog _logger = LogManager.GetLogger(typeof(TcpDatabaseServer));

    public TcpDatabaseServer(
        int port,
        string providerName,
        SqlExecutionService sqlExecutionService,
        TableMaintenanceService tableService,
        ServiceReadinessMonitor readinessMonitor)
    {
        _port = port is > 0 and <= 65535 ? port : throw new ArgumentOutOfRangeException(nameof(port));
        _providerName = providerName;
        _sqlExecutionService = sqlExecutionService;
        _tableService = tableService;
        _readinessMonitor = readinessMonitor;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Loopback, _port);
        listener.Start();
        _logger.Info($"DatabaseLoadBalancing.Server 已启动：控制台 TCP 服务，Provider={_providerName}，端口={_port}。");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.Warn("DatabaseLoadBalancing.Server 正在停止。");
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverCancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                var request = await LengthPrefixedJsonProtocol.ReadAsync<ConsoleServiceRequest>(stream, timeout.Token)
                    ?? throw new InvalidOperationException("Request payload is required.");
                var response = await DispatchAsync(request, timeout.Token);
                await LengthPrefixedJsonProtocol.WriteAsync(stream, response, timeout.Token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !serverCancellationToken.IsCancellationRequested)
            {
                _logger.Error("TCP 客户端请求处理失败。", exception);
                try
                {
                    await LengthPrefixedJsonProtocol.WriteAsync(stream, new ConsoleServiceResponse { Error = exception.Message }, CancellationToken.None);
                }
                catch
                {
                    // The connection is no longer writable.
                }
            }
        }
    }

    private async Task<ConsoleServiceResponse> DispatchAsync(ConsoleServiceRequest request, CancellationToken cancellationToken)
    {
        _logger.Info($"TCP 请求：{request.Operation}。");
        return request.Operation switch
        {
            ConsoleServiceOperations.Health => Ok(_readinessMonitor.Check()),
            ConsoleServiceOperations.ListTables => Ok(await _tableService.ListTablesAsync(cancellationToken)),
            ConsoleServiceOperations.GetTable => await GetTableAsync(request, cancellationToken),
            ConsoleServiceOperations.SqlQuery => Ok(await _sqlExecutionService.ExecuteQueryAsync(Payload<SqlRequest>(request), cancellationToken)),
            ConsoleServiceOperations.SqlScalar => Ok(await _sqlExecutionService.ExecuteScalarAsync(Payload<SqlRequest>(request), cancellationToken)),
            ConsoleServiceOperations.SqlExecute => Ok(await _sqlExecutionService.ExecuteMutationAsync(Payload<SqlRequest>(request), cancellationToken)),
            ConsoleServiceOperations.SqlTransaction => Ok(await _sqlExecutionService.ExecuteTransactionAsync(Payload<SqlTransactionRequest>(request), cancellationToken)),
            ConsoleServiceOperations.CreateRecord => await CreateRecordAsync(request, cancellationToken),
            ConsoleServiceOperations.UpdateRecord => await UpdateRecordAsync(request, cancellationToken),
            ConsoleServiceOperations.DeleteRecord => await DeleteRecordAsync(request, cancellationToken),
            ConsoleServiceOperations.ClearTable => await ClearTableAsync(request, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported operation: {request.Operation}.")
        };
    }

    private async Task<ConsoleServiceResponse> GetTableAsync(ConsoleServiceRequest request, CancellationToken cancellationToken)
    {
        var command = Payload<TableDataRequest>(request);
        return Ok(await _tableService.GetTableAsync(command.TableName, command.Limit, command.Offset, cancellationToken));
    }

    private async Task<ConsoleServiceResponse> CreateRecordAsync(ConsoleServiceRequest request, CancellationToken cancellationToken)
    {
        var command = Payload<TableRecordCommand>(request);
        return Ok(await _tableService.CreateRecordAsync(command.TableName, command.Record ?? throw new InvalidOperationException("Record is required."), cancellationToken));
    }

    private async Task<ConsoleServiceResponse> UpdateRecordAsync(ConsoleServiceRequest request, CancellationToken cancellationToken)
    {
        var command = Payload<TableRecordCommand>(request);
        return Ok(await _tableService.UpdateRecordAsync(command.TableName, command.RowId, command.Record ?? throw new InvalidOperationException("Record is required."), cancellationToken));
    }

    private async Task<ConsoleServiceResponse> DeleteRecordAsync(ConsoleServiceRequest request, CancellationToken cancellationToken)
    {
        var command = Payload<TableRecordCommand>(request);
        return Ok(await _tableService.DeleteRecordAsync(command.TableName, command.RowId, command.RequestId, cancellationToken));
    }

    private async Task<ConsoleServiceResponse> ClearTableAsync(ConsoleServiceRequest request, CancellationToken cancellationToken)
    {
        var command = Payload<ClearTableCommand>(request);
        return Ok(await _tableService.ClearTableAsync(command.TableName, command.RequestId, cancellationToken));
    }

    private static T Payload<T>(ConsoleServiceRequest request) => request.Payload is { } payload
        ? payload.Deserialize<T>(LengthPrefixedJsonProtocol.JsonOptions) ?? throw new InvalidOperationException("Request payload is invalid.")
        : throw new InvalidOperationException("Request payload is required.");

    private static ConsoleServiceResponse Ok<T>(T value) => new()
    {
        Success = true,
        Payload = JsonSerializer.SerializeToElement(value, LengthPrefixedJsonProtocol.JsonOptions)
    };
}
