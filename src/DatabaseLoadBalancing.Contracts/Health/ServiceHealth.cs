namespace DatabaseLoadBalancing.Contracts.Health;

public sealed class ServiceHealth
{
    public string Status { get; set; } = "ok";

    public string Database { get; set; } = "ok";

    public bool DatabaseAccessible { get; set; } = true;

    public string? LastError { get; set; }

    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}

