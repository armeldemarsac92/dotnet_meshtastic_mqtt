namespace MeshBoard.Contracts.Topics;

public static class TopicPresetPreferenceRequestMappingExtensions
{
    public static SaveTopicPresetRequest ToSaveTopicPresetRequest(
        this SaveTopicPresetPreferenceRequest request,
        TopicPreset? existingPreset = null)
    {
        return new SaveTopicPresetRequest
        {
            Name = request.Name,
            TopicPattern = request.TopicPattern,
            EncryptionKeyBase64 = null,
            IsDefault = request.IsDefault
        };
    }
}
