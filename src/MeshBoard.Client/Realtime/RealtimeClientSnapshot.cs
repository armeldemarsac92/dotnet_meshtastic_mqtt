namespace MeshBoard.Client.Realtime;

public sealed record RealtimeClientSnapshot
{
    public static readonly IReadOnlyList<string> EmptyTopics = Array.Empty<string>();

    public bool IsReady { get; init; }

    public bool IsConnecting { get; init; }

    public bool IsConnected { get; init; }

    public bool IsDisconnecting { get; init; }

    public string Status { get; init; } = "Idle";

    public string? BrokerUrl { get; init; }

    public string? ClientId { get; init; }

    public IReadOnlyList<string> AllowedTopicPatterns { get; init; } = EmptyTopics;

    public int ActiveSubscriptionCount { get; init; }

    public long MessageCount { get; init; }

    public string? LastMessageTopic { get; init; }

    public int? LastPayloadSizeBytes { get; init; }

    public DateTimeOffset? SessionExpiresAtUtc { get; init; }

    public DateTimeOffset? ConnectedAtUtc { get; init; }

    public DateTimeOffset? LastMessageReceivedAtUtc { get; init; }

    public DateTimeOffset? LastDisconnectedAtUtc { get; init; }

    public string? LastDisconnectReason { get; init; }

    public string? LastError { get; init; }
}
