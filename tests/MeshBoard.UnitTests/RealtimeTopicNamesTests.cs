using MeshBoard.Contracts.Realtime;

namespace MeshBoard.UnitTests;

public sealed class RealtimeTopicNamesTests
{
    [Fact]
    public void BuildWorkspaceLiveWildcard_ShouldReturnWorkspaceScopedWildcard()
    {
        var topic = RealtimeTopicNames.BuildWorkspaceLiveWildcard("workspace-a");

        Assert.Equal("meshboard/workspaces/workspace-a/live/#", topic);
    }

    [Fact]
    public void BuildWorkspacePacketTopic_ShouldReturnWorkspacePacketTopic()
    {
        var topic = RealtimeTopicNames.BuildWorkspacePacketTopic("workspace-a");

        Assert.Equal("meshboard/workspaces/workspace-a/live/packets", topic);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("workspace/a")]
    [InlineData("workspace+a")]
    [InlineData("workspace#a")]
    public void TopicBuilders_WhenWorkspaceIdIsInvalid_ShouldThrow(string workspaceId)
    {
        Assert.Throws<ArgumentException>(() => RealtimeTopicNames.BuildWorkspaceLiveWildcard(workspaceId));
        Assert.Throws<ArgumentException>(() => RealtimeTopicNames.BuildWorkspacePacketTopic(workspaceId));
    }
}
