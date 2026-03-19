namespace MeshBoard.Contracts.Topics;

public static class TopicPresetMappingExtensions
{
    public static SavedTopicPreset ToSavedTopicPreset(
        this TopicPreset preset,
        Guid serverProfileId = default,
        string serverProfileName = "",
        string serverAddress = "")
    {
        ArgumentNullException.ThrowIfNull(preset);

        return new SavedTopicPreset
        {
            Id = preset.Id,
            ServerProfileId = serverProfileId,
            ServerProfileName = serverProfileName,
            ServerAddress = serverAddress,
            Name = preset.Name,
            TopicPattern = preset.TopicPattern,
            IsDefault = preset.IsDefault,
            CreatedAtUtc = preset.CreatedAtUtc
        };
    }
}
