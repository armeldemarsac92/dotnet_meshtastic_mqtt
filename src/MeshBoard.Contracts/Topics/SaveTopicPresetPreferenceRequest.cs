namespace MeshBoard.Contracts.Topics;

public sealed class SaveTopicPresetPreferenceRequest
{
    public Guid ServerProfileId { get; set; }

    public required string Name { get; set; }

    public required string TopicPattern { get; set; }

    public bool IsDefault { get; set; }
}
