namespace MeshBoard.Contracts.Topics;

public sealed class SaveChannelFilterRequest
{
    public required string TopicFilter { get; set; }

    public string? Label { get; set; }
}
