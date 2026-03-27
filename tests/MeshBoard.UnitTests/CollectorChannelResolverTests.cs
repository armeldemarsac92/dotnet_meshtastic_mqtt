using MeshBoard.Application.Collector;

namespace MeshBoard.UnitTests;

public sealed class CollectorChannelResolverTests
{
    private readonly CollectorChannelResolver _resolver = new();

    [Fact]
    public void ResolveChannelKey_WhenTopicHasEnoughSegments_ReturnsChannelSegment()
    {
        var channelKey = _resolver.ResolveChannelKey("msh/US/2/e/LongFast/!00001234");

        Assert.Equal("LongFast", channelKey);
    }

    [Fact]
    public void ResolveChannelKey_WhenTopicTooShort_ReturnsNull()
    {
        var channelKey = _resolver.ResolveChannelKey("msh/US/2/e");

        Assert.Null(channelKey);
    }

    [Fact]
    public void ResolveTopicPattern_WhenTopicEndsWithNodeId_StripsLastSegment()
    {
        var topicPattern = _resolver.ResolveTopicPattern("msh/US/2/e/LongFast/!00001234");

        Assert.Equal("msh/US/2/e/LongFast", topicPattern);
    }

    [Fact]
    public void ResolveTopicPattern_WhenTopicEndsWithWildcard_ReturnsAsIs()
    {
        var topicPattern = _resolver.ResolveTopicPattern("msh/US/2/e/LongFast/#");

        Assert.Equal("msh/US/2/e/LongFast/#", topicPattern);
    }

    [Fact]
    public void ResolveTopicPattern_WhenTopicIsNullOrWhitespace_ReturnsNull()
    {
        Assert.Null(_resolver.ResolveTopicPattern(null!));
        Assert.Null(_resolver.ResolveTopicPattern("   "));
    }
}
