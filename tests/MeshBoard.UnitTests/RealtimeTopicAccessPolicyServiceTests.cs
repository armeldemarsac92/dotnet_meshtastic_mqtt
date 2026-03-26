using MeshBoard.Application.Services;

namespace MeshBoard.UnitTests;

public sealed class RealtimeTopicAccessPolicyServiceTests
{
    [Fact]
    public void CreateForWorkspace_ShouldReturnWorkspaceScopedSubscribeAcl()
    {
        var service = new RealtimeTopicAccessPolicyService();

        var policy = service.CreateForWorkspace("workspace-a");

        Assert.Equal(["meshboard/workspaces/workspace-a/live/#"], policy.SubscribeTopicPatterns);
        Assert.Empty(policy.PublishTopicPatterns);
    }
}
