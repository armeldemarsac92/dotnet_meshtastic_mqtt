using MeshBoard.Contracts.Collector;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface ICollectorPacketRollupRepository
{
    Task RecordObservedMessageAsync(
        CollectorObservedPacketRollupRequest request,
        CancellationToken cancellationToken = default);
}
