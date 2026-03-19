using MeshBoard.Contracts.Topics;

namespace MeshBoard.UnitTests;

public sealed class TopicPresetPreferenceRequestMappingExtensionsTests
{
    [Fact]
    public void ToSaveTopicPresetRequest_ShouldUseNullEncryptionKey_ForNewPresets()
    {
        var request = new SaveTopicPresetPreferenceRequest
        {
            Name = "Public feed",
            TopicPattern = "msh/US/2/e/#",
            IsDefault = true
        };

        var mapped = request.ToSaveTopicPresetRequest();

        Assert.Null(mapped.EncryptionKeyBase64);
        Assert.Equal(request.TopicPattern, mapped.TopicPattern);
        Assert.True(mapped.IsDefault);
    }

    [Fact]
    public void ToSaveTopicPresetRequest_ShouldClearServerOwnedEncryptionKey_ForExistingPresets()
    {
        var existing = new TopicPreset
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            TopicPattern = "msh/US/2/e/#",
            EncryptionKeyBase64 = "AQ==",
            IsDefault = false
        };

        var request = new SaveTopicPresetPreferenceRequest
        {
            Name = "Existing updated",
            TopicPattern = existing.TopicPattern,
            IsDefault = true
        };

        var mapped = request.ToSaveTopicPresetRequest(existing);

        Assert.Null(mapped.EncryptionKeyBase64);
        Assert.Equal(request.Name, mapped.Name);
        Assert.True(mapped.IsDefault);
    }
}
