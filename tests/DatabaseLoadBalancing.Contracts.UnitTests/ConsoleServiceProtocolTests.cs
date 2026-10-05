using System.Text.Json;
using DatabaseLoadBalancing.Contracts.Transport;

namespace DatabaseLoadBalancing.Contracts.UnitTests;

public sealed class ConsoleServiceProtocolTests
{
    [Fact]
    public async Task LengthPrefixedProtocol_RoundTripsAConsoleRequest()
    {
        var request = new ConsoleServiceRequest
        {
            Operation = ConsoleServiceOperations.SqlExecute,
            Payload = JsonSerializer.SerializeToElement(new { Sql = "SELECT 1" }, LengthPrefixedJsonProtocol.JsonOptions)
        };
        await using var stream = new MemoryStream();

        await LengthPrefixedJsonProtocol.WriteAsync(stream, request);
        stream.Position = 0;
        var received = await LengthPrefixedJsonProtocol.ReadAsync<ConsoleServiceRequest>(stream);

        Assert.NotNull(received);
        Assert.Equal(ConsoleServiceOperations.SqlExecute, received!.Operation);
        Assert.Equal("SELECT 1", received.Payload!.Value.GetProperty("sql").GetString());
    }
}
