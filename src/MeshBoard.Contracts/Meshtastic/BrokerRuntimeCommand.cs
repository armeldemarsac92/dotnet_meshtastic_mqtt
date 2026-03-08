namespace MeshBoard.Contracts.Meshtastic;

public sealed class BrokerRuntimeCommand
{
    public Guid Id { get; set; }

    public string WorkspaceId { get; set; } = string.Empty;

    public BrokerRuntimeCommandType CommandType { get; set; }

    public BrokerRuntimeCommandStatus Status { get; set; } = BrokerRuntimeCommandStatus.Pending;

    public string? Topic { get; set; }

    public string? Payload { get; set; }

    public string? TopicFilter { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset AvailableAtUtc { get; set; }

    public DateTimeOffset? LeasedAtUtc { get; set; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? FailedAtUtc { get; set; }

    public string? LastError { get; set; }
}
