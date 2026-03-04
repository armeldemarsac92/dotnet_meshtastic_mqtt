namespace MeshBoard.Contracts.Topics;

public sealed class SaveTopicPresetRequest
{
    public required string Name { get; set; }

    public required string TopicPattern { get; set; }

    public bool IsDefault { get; set; }
}
