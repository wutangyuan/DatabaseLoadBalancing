using DatabaseLoadBalancing.Data.Sqlite;

namespace DatabaseLoadBalancing.Server.IntegrationTests.Comparison;

public sealed class DirectSqliteBaselineRunner
{
    public async Task<ComparisonResult> RunAsync(string databasePath, int clientCount = 10, int writeCount = 100, CancellationToken cancellationToken = default)
    {
        var factory = new SqliteConnectionFactory(databasePath);
        var success = 0;
        var failure = 0;
        var timeout = 0;
        var contention = 0;
        var gate = new SemaphoreSlim(clientCount, clientCount);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var writes = Enumerable.Range(0, writeCount).Select(async index =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                await using var connection = factory.OpenConnection();
                await using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO items(value) VALUES ($value)";
                command.Parameters.AddWithValue("$value", $"direct-{index}");
                await command.ExecuteNonQueryAsync(cancellationToken);
                Interlocked.Increment(ref success);
            }
            catch (OperationCanceledException) { Interlocked.Increment(ref timeout); }
            catch (Microsoft.Data.Sqlite.SqliteException exception)
            {
                if (exception.Message.Contains("busy", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("locked", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref contention);
                else Interlocked.Increment(ref failure);
            }
            catch { Interlocked.Increment(ref failure); }
            finally { gate.Release(); }
        });
        await Task.WhenAll(writes);
        stopwatch.Stop();
        await using var countConnection = factory.OpenConnection();
        await using var countCommand = countConnection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM items";
        var count = Convert.ToInt64(await countCommand.ExecuteScalarAsync(cancellationToken));
        return new ComparisonResult("direct-sqlite", writeCount, success, failure, timeout, contention, stopwatch.Elapsed, count);
    }
}

