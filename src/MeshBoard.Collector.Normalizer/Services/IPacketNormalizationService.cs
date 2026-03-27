using MeshBoard.Contracts.CollectorEvents.RawPackets;

namespace MeshBoard.Collector.Normalizer.Services;

public interface IPacketNormalizationService
{
    Task NormalizeAsync(RawPacketReceived rawPacket, CancellationToken cancellationToken = default);
}
