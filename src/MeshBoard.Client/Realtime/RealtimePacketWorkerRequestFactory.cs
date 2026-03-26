namespace MeshBoard.Client.Realtime;

public sealed class RealtimePacketWorkerRequestFactory
{
    public RealtimePacketWorkerRequest Create(BrowserRealtimeClient.RealtimeMessageEvent messageEvent)
    {
        ArgumentNullException.ThrowIfNull(messageEvent);

        return new RealtimePacketWorkerRequest
        {
            DownstreamTopic = messageEvent.Topic?.Trim() ?? string.Empty,
            PayloadBase64 = messageEvent.PayloadBase64?.Trim() ?? string.Empty,
            ReceivedAtUtc = messageEvent.ReceivedAtUtc
        };
    }
}
