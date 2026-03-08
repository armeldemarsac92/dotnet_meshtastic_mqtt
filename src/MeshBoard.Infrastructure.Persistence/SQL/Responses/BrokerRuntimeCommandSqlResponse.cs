namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class BrokerRuntimeCommandSqlResponse
{
    public string Id { get; set; } = string.Empty;

    public string WorkspaceId { get; set; } = string.Empty;

    public string CommandType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Topic { get; set; }

    public string? Payload { get; set; }

    public string? TopicFilter { get; set; }

    public int AttemptCount { get; set; }

    public string CreatedAtUtc { get; set; } = string.Empty;

    public string AvailableAtUtc { get; set; } = string.Empty;

    public string? LeasedAtUtc { get; set; }

    public string? LeaseExpiresAtUtc { get; set; }

    public string? CompletedAtUtc { get; set; }

    public string? FailedAtUtc { get; set; }

    public string? LastError { get; set; }
}
