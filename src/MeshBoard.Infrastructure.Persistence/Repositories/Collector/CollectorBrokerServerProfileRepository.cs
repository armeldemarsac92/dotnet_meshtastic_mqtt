using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Persistence.Repositories.Collector;

internal sealed class CollectorBrokerServerProfileRepository : IBrokerServerProfileRepository
{
    private readonly BrokerServerProfile _activeProfile;
    private readonly ILogger<CollectorBrokerServerProfileRepository> _logger;
    private readonly WorkspaceBrokerServerProfile _workspaceBrokerServerProfile;

    public CollectorBrokerServerProfileRepository(
        IOptions<BrokerOptions> brokerOptions,
        ILogger<CollectorBrokerServerProfileRepository> logger)
    {
        _logger = logger;
        _activeProfile = brokerOptions.Value.ToCollectorBrokerServerProfile();
        _workspaceBrokerServerProfile = _activeProfile.ToWorkspaceBrokerServerProfile(WorkspaceConstants.DefaultWorkspaceId);
    }

    public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch collector active broker server profiles");
        return Task.FromResult<IReadOnlyCollection<WorkspaceBrokerServerProfile>>([_workspaceBrokerServerProfile]);
    }

    public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveUserOwnedAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch collector active broker server profiles for runtime bootstrap");
        return Task.FromResult<IReadOnlyCollection<WorkspaceBrokerServerProfile>>([_workspaceBrokerServerProfile]);
    }

    public Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<BrokerServerProfile> profiles = IsCollectorWorkspace(workspaceId)
            ? [_activeProfile]
            : [];
        return Task.FromResult(profiles);
    }

    public Task<BrokerServerProfile?> GetActiveAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsCollectorWorkspace(workspaceId) ? _activeProfile : null);
    }

    public Task<BrokerServerProfile?> GetByIdAsync(
        string workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            IsCollectorWorkspace(workspaceId) && _activeProfile.Id == id
                ? _activeProfile
                : null);
    }

    public Task SetExclusiveActiveAsync(
        string workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!IsCollectorWorkspace(workspaceId) || id != _activeProfile.Id)
        {
            throw new InvalidOperationException("The collector broker profile is configuration-backed and cannot be changed at runtime.");
        }

        return Task.CompletedTask;
    }

    public Task<BrokerServerProfile> UpsertAsync(
        string workspaceId,
        SaveBrokerServerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("The collector broker profile is configuration-backed and cannot be modified through persistence.");
    }

    private static bool IsCollectorWorkspace(string workspaceId)
    {
        return string.Equals(
            workspaceId?.Trim(),
            WorkspaceConstants.DefaultWorkspaceId,
            StringComparison.Ordinal);
    }
}
