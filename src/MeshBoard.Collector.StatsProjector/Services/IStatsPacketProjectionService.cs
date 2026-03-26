using MeshBoard.Contracts.CollectorEvents.Normalized;

namespace MeshBoard.Collector.StatsProjector.Services;

public interface IStatsPacketProjectionService
{
    Task ProjectAsync(PacketNormalized packet, CancellationToken ct = default);
}
