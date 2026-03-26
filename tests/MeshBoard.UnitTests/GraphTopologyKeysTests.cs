using MeshBoard.Contracts.CollectorEvents;

namespace MeshBoard.UnitTests;

public sealed class GraphTopologyKeysTests
{
    [Theory]
    [InlineData("!aa", "!bb", "!aa", "!bb")]
    [InlineData("!bb", "!aa", "!aa", "!bb")]
    [InlineData("!aaa", "!aa", "!aa", "!aaa")]
    [InlineData("!same", "!same", "!same", "!same")]
    public void CanonicalNodePair_ReturnsExpectedPair(
        string sourceNodeId,
        string targetNodeId,
        string expectedSourceNodeId,
        string expectedTargetNodeId)
    {
        var pair = GraphTopologyKeys.CanonicalNodePair(sourceNodeId, targetNodeId);

        Assert.Equal(expectedSourceNodeId, pair.SourceNodeId);
        Assert.Equal(expectedTargetNodeId, pair.TargetNodeId);
    }
}
