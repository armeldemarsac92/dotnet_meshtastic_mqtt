using MeshBoard.Contracts.Messages;

namespace MeshBoard.Contracts.Collector;

public static class CollectorObservedPacketRollupMappingExtensions
{
    public static CollectorObservedPacketRollupRequest ToCollectorObservedPacketRollupRequest(
        this SaveObservedMessageRequest request,
        long channelId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CollectorObservedPacketRollupRequest
        {
            ChannelId = channelId,
            NodeId = request.FromNodeId,
            PacketType = request.PacketType,
            ObservedAtUtc = request.ReceivedAtUtc
        };
    }
}
