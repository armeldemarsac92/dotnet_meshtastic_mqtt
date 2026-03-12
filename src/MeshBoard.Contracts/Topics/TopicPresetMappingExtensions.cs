namespace MeshBoard.Contracts.Topics;

public static class TopicPresetMappingExtensions
{
    public static SavedTopicPreset ToSavedTopicPreset(this TopicPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return new SavedTopicPreset
        {
            Id = preset.Id,
            Name = preset.Name,
            TopicPattern = preset.TopicPattern,
            IsDefault = preset.IsDefault,
            CreatedAtUtc = preset.CreatedAtUtc
        };
    }
}
