using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Services;

public interface IBrokerRuntimeCommandQueryService
{
    Task<IReadOnlyCollection<BrokerRuntimeCommand>> GetRecentCommands(
        int take = 20,
        CancellationToken cancellationToken = default);
}

public sealed class BrokerRuntimeCommandQueryService : IBrokerRuntimeCommandQueryService
{
    private readonly IBrokerRuntimeCommandRepository _brokerRuntimeCommandRepository;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public BrokerRuntimeCommandQueryService(
        IBrokerRuntimeCommandRepository brokerRuntimeCommandRepository,
        IWorkspaceContextAccessor workspaceContextAccessor)
    {
        _brokerRuntimeCommandRepository = brokerRuntimeCommandRepository;
        _workspaceContextAccessor = workspaceContextAccessor;
    }

    public Task<IReadOnlyCollection<BrokerRuntimeCommand>> GetRecentCommands(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var normalizedTake = Math.Clamp(take, 1, 100);

        return _brokerRuntimeCommandRepository.GetRecentAsync(
            workspaceId,
            normalizedTake,
            cancellationToken);
    }
}
