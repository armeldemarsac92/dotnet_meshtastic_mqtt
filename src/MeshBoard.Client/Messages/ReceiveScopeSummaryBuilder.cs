using MeshBoard.Contracts.Configuration;

namespace MeshBoard.Client.Messages;

public sealed class ReceiveScopeSummaryBuilder
{
    public ReceiveScopeSummary Build(SavedBrokerServerProfile? activeServer)
    {
        if (activeServer is null)
        {
            return ReceiveScopeSummary.Empty;
        }

        var topics = new List<ReceiveScopeTopic>
        {
            new()
            {
                TopicPattern = "msh/#",
                SourceLabel = "Default",
                IsFallback = false
            }
        };

        return new ReceiveScopeSummary
        {
            HasActiveServer = true,
            ServerName = activeServer.Name,
            ServerAddress = activeServer.ServerAddress,
            UsesFallbackTopic = false,
            Topics = topics
        };
    }
}
