using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DatabaseLoadBalancing.Server.Execution;

public sealed class RequestIdempotencyService
{
    public string ComputeHash(string operation, object payload)
    {
        var canonical = JsonSerializer.Serialize(new { operation, payload });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public bool IsConflict(string existingHash, string incomingHash) =>
        !string.Equals(existingHash, incomingHash, StringComparison.Ordinal);
}

