namespace MeshBoard.Contracts.Meshtastic;

public sealed class SubscriptionIntent
{
    public Guid BrokerServerProfileId { get; set; }

    public string TopicFilter { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
