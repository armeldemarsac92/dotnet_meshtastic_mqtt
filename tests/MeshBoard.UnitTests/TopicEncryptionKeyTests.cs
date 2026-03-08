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
    public void TryParse_ShouldAcceptShortDefaultPskIndex()
    {
        var parsed = TopicEncryptionKey.TryParse("AQ==", out var keyBytes);

        Assert.True(parsed);
        Assert.Equal(TopicEncryptionKey.DefaultKeyBytes, keyBytes);
    }

    [Fact]
    public void TryParse_ShouldExpandShortPskIndex()
    {
        var parsed = TopicEncryptionKey.TryParse("Ag==", out var keyBytes);

        Assert.True(parsed);
        Assert.Equal(TopicEncryptionKey.DefaultKeyBytes.Length, keyBytes.Length);
        Assert.Equal((byte)(TopicEncryptionKey.DefaultKeyBytes[^1] + 1), keyBytes[^1]);
    }

    [Fact]
    public void TryParse_ShouldAcceptUrlSafeBase64WithoutPadding()
    {
        var canonical = Convert.ToBase64String(TopicEncryptionKey.DefaultKeyBytes);
        var urlSafeWithoutPadding = canonical
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var parsed = TopicEncryptionKey.TryParse(urlSafeWithoutPadding, out var keyBytes);

        Assert.True(parsed);
        Assert.Equal(TopicEncryptionKey.DefaultKeyBytes, keyBytes);
    }

    [Fact]
    public void TryParse_ShouldRejectUnsupportedByteLength()
    {
        var parsed = TopicEncryptionKey.TryParse("AQI=", out var keyBytes);

        Assert.False(parsed);
        Assert.Empty(keyBytes);
    }
}
