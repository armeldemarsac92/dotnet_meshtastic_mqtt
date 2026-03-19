namespace MeshBoard.Contracts.Topics;

public sealed class SavedChannelFilter
{
    public Guid Id { get; set; }

    public Guid BrokerServerProfileId { get; set; }

    public string TopicFilter { get; set; } = string.Empty;

    public string? Label { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
