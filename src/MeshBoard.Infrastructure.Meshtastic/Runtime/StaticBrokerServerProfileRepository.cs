using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Meshtastic.Runtime;

internal sealed class StaticBrokerServerProfileRepository : IBrokerServerProfileRepository
{
    public const string StaticWorkspaceId = "static";

    private static readonly Guid StaticProfileId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly BrokerServerProfile _profile;
    private readonly WorkspaceBrokerServerProfile _workspaceProfile;

    public StaticBrokerServerProfileRepository(IOptions<BrokerOptions> brokerOptions)
    {
        _profile = CreateProfile(brokerOptions.Value);
        _workspaceProfile = new WorkspaceBrokerServerProfile
        {
            WorkspaceId = StaticWorkspaceId,
            Profile = _profile
        };
    }

    public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<WorkspaceBrokerServerProfile>>([_workspaceProfile]);
    }

    public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveUserOwnedAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<WorkspaceBrokerServerProfile>>([_workspaceProfile]);
    }

    public Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<BrokerServerProfile> profiles = IsStaticWorkspace(workspaceId)
            ? [_profile]
            : [];
        return Task.FromResult(profiles);
    }

    public Task<BrokerServerProfile?> GetActiveAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsStaticWorkspace(workspaceId) ? _profile : null);
    }

    public Task<BrokerServerProfile?> GetByIdAsync(
        string workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            IsStaticWorkspace(workspaceId) && id == StaticProfileId
                ? _profile
                : null);
    }

    public Task SetExclusiveActiveAsync(
        string workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<BrokerServerProfile> UpsertAsync(
        string workspaceId,
        SaveBrokerServerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Static broker configuration does not support mutation.");
    }

    private static BrokerServerProfile CreateProfile(BrokerOptions options)
    {
        return new BrokerServerProfile
        {
            Id = StaticProfileId,
            Name = "static",
            Host = options.Host,
            Port = options.Port,
            UseTls = options.UseTls,
            Username = options.Username,
            Password = options.Password,
            DownlinkTopic = options.DownlinkTopic,
            EnableSend = options.EnableSend,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static bool IsStaticWorkspace(string workspaceId)
    {
        return string.Equals(workspaceId, StaticWorkspaceId, StringComparison.Ordinal);
    }
}
