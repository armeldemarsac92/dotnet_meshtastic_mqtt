namespace MeshBoard.Contracts.Topics;

public sealed class SavedChannelFilter
{
    public Guid BrokerServerProfileId { get; set; }

    public string TopicFilter { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
