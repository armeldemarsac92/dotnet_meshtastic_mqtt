using MeshBoard.Contracts.Topics;

namespace MeshBoard.UnitTests;

public sealed class TopicPresetMappingExtensionsTests
{
    [Fact]
    public void ToSavedTopicPreset_ShouldPreserveOnlyMetadata()
    {
        var serverProfileId = Guid.NewGuid();
        var preset = new TopicPreset
        {
            Id = Guid.NewGuid(),
            Name = "Public feed",
            TopicPattern = "msh/EU/2/e/#",
            EncryptionKeyBase64 = "AQ==",
            IsDefault = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var saved = preset.ToSavedTopicPreset(serverProfileId, "Default server", "mqtt.example.org:1883");

        Assert.Equal(preset.Id, saved.Id);
        Assert.Equal(serverProfileId, saved.ServerProfileId);
        Assert.Equal("Default server", saved.ServerProfileName);
        Assert.Equal("mqtt.example.org:1883", saved.ServerAddress);
        Assert.Equal(preset.Name, saved.Name);
        Assert.Equal(preset.TopicPattern, saved.TopicPattern);
        Assert.True(saved.IsDefault);
        Assert.Equal(preset.CreatedAtUtc, saved.CreatedAtUtc);
    }
}
