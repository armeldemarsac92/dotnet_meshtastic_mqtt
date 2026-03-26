namespace MeshBoard.Contracts.Realtime;

public sealed class RealtimeTopicAccessPolicy
{
    public List<string> SubscribeTopicPatterns { get; set; } = [];

    public List<string> PublishTopicPatterns { get; set; } = [];
}
