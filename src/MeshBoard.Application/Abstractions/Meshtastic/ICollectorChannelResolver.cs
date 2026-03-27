namespace MeshBoard.Application.Abstractions.Meshtastic;

public interface ICollectorChannelResolver
{
    string? ResolveChannelKey(string topic);

    string? ResolveTopicPattern(string topic);
}
