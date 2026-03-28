using MeshBoard.Contracts.Realtime;

namespace MeshBoard.Application.Services;

public interface IRealtimeTopicAccessPolicyService
{
    RealtimeTopicAccessPolicy CreateForWorkspace(string workspaceId);
}

public sealed class RealtimeTopicAccessPolicyService : IRealtimeTopicAccessPolicyService
{
    public RealtimeTopicAccessPolicy CreateForWorkspace(string workspaceId)
    {
        return new RealtimeTopicAccessPolicy
        {
            SubscribeTopicPatterns = [RealtimeTopicNames.BuildWorkspaceLiveWildcard(workspaceId)],
            PublishTopicPatterns = []
        };
    }
}
