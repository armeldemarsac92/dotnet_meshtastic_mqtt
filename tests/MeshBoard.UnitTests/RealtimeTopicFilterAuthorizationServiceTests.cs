using MeshBoard.Application.Realtime;
using MeshBoard.Contracts.Realtime;

namespace MeshBoard.UnitTests;

public sealed class RealtimeTopicFilterAuthorizationServiceTests
{
    private readonly RealtimeTopicFilterAuthorizationService _service = new();

    [Fact]
    public void IsSubscriptionAllowed_WhenTopicIsWithinWorkspaceScope_ShouldReturnTrue()
    {
        var isAllowed = _service.IsSubscriptionAllowed(
            RealtimeTopicNames.BuildWorkspacePacketTopic("workspace-a"),
            [RealtimeTopicNames.BuildWorkspaceLiveWildcard("workspace-a")]);

        Assert.True(isAllowed);
    }

    [Fact]
    public void IsSubscriptionAllowed_WhenSharedSubscriptionTargetsWorkspaceScope_ShouldReturnTrue()
    {
        var isAllowed = _service.IsSubscriptionAllowed(
            $"$share/ops/{RealtimeTopicNames.BuildWorkspacePacketTopic("workspace-a")}",
            [RealtimeTopicNames.BuildWorkspaceLiveWildcard("workspace-a")]);

        Assert.True(isAllowed);
    }

    [Fact]
    public void IsSubscriptionAllowed_WhenTopicEscapesWorkspaceScope_ShouldReturnFalse()
    {
        var isAllowed = _service.IsSubscriptionAllowed(
            RealtimeTopicNames.BuildWorkspacePacketTopic("workspace-b"),
            [RealtimeTopicNames.BuildWorkspaceLiveWildcard("workspace-a")]);

        Assert.False(isAllowed);
    }
}
