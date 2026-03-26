using MeshBoard.Contracts.Realtime;

namespace MeshBoard.Application.Abstractions.Realtime;

public interface IRealtimePacketPublicationFactory
{
    RealtimePacketPublication Create(RealtimePacketEnvelope envelope);
}
