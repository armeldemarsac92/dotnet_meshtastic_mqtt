using MeshBoard.Contracts.Topics;

namespace MeshBoard.UnitTests;

public sealed class TopicEncryptionKeyTests
{
    [Fact]
    public void TryParse_ShouldAcceptDefaultBase64Key()
    {
        var parsed = TopicEncryptionKey.TryParse(TopicEncryptionKey.DefaultKeyBase64, out var keyBytes);

        Assert.True(parsed);
        Assert.Equal(16, keyBytes.Length);
        Assert.Equal(TopicEncryptionKey.DefaultKeyBytes, keyBytes);
    }

    [Fact]
    public void TryParse_ShouldAcceptHexKey()
    {
        const string hexValue = "d4f1bb3a20290759f0bcffabcf4e6901";

        var parsed = TopicEncryptionKey.TryParse(hexValue, out var keyBytes);

        Assert.True(parsed);
        Assert.Equal(16, keyBytes.Length);
        Assert.Equal(TopicEncryptionKey.DefaultKeyBytes, keyBytes);
    }

    [Fact]
    public void TryParse_ShouldRejectInvalidKeyLength()
    {
        var parsed = TopicEncryptionKey.TryParse("AQ==", out var keyBytes);

        Assert.False(parsed);
        Assert.Empty(keyBytes);
    }
}
