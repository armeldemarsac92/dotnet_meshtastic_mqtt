using MeshBoard.Contracts.Realtime;

namespace MeshBoard.RealtimeBridge;

internal interface IRealtimePacketPublisher
{
    Task PublishAsync(RealtimePacketEnvelope envelope);
}
