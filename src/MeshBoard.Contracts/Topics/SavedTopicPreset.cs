namespace MeshBoard.Contracts.Topics;

public sealed class SavedTopicPreset
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string TopicPattern { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
