using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class BrokerServerProfileServiceTests
{
    [Fact]
    public async Task GetActiveServerProfile_ShouldReturnConfiguredActiveProfile()
    {
        var activeProfile = CreateProfile(isActive: true);
        var repository = new FakeBrokerServerProfileRepository
        {
            ActiveProfile = activeProfile
        };
        var service = CreateService(repository);

        var result = await service.GetActiveServerProfile();

        Assert.Same(activeProfile, result);
        Assert.Equal(1, repository.GetActiveAsyncCallCount);
        Assert.Equal(0, repository.UpsertAsyncCallCount);
    }

    [Fact]
    public async Task GetActiveServerProfile_ShouldThrow_WhenNoActiveProfileExists()
    {
        var repository = new FakeBrokerServerProfileRepository();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => service.GetActiveServerProfile());

        Assert.Equal("No active broker server profile is configured.", exception.Message);
        Assert.Equal(1, repository.GetActiveAsyncCallCount);
        Assert.Equal(0, repository.UpsertAsyncCallCount);
        Assert.Equal(0, repository.SetExclusiveActiveAsyncCallCount);
    }

    [Fact]
    public async Task SaveServerProfile_ShouldPromoteSavedProfile_WhenNoActiveProfileExists()
    {
        var savedProfile = CreateProfile(isActive: false);
        var repository = new FakeBrokerServerProfileRepository
        {
            UpsertResult = savedProfile
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var result = await service.SaveServerProfile(CreateSaveRequest(isActive: false));

        Assert.True(result.IsActive);
        Assert.Equal(savedProfile.Id, repository.LastExclusiveActiveProfileId);
        Assert.Equal(1, unitOfWork.BeginCallCount);
        Assert.Equal(1, unitOfWork.CommitCallCount);
        Assert.Equal(0, unitOfWork.RollbackCallCount);
    }

    [Fact]
    public async Task SetActiveServerProfile_ShouldRollback_WhenProfileDoesNotExist()
    {
        var repository = new FakeBrokerServerProfileRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);
        var missingProfileId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => service.SetActiveServerProfile(missingProfileId));

        Assert.Equal($"Broker server profile '{missingProfileId}' was not found.", exception.Message);
        Assert.Equal(1, unitOfWork.BeginCallCount);
        Assert.Equal(0, unitOfWork.CommitCallCount);
        Assert.Equal(1, unitOfWork.RollbackCallCount);
        Assert.Equal(0, repository.SetExclusiveActiveAsyncCallCount);
    }

    private static BrokerServerProfileService CreateService(
        FakeBrokerServerProfileRepository repository,
        FakeUnitOfWork? unitOfWork = null)
    {
        return new BrokerServerProfileService(
            repository,
            unitOfWork ?? new FakeUnitOfWork(),
            new FakeWorkspaceContextAccessor(),
            NullLogger<BrokerServerProfileService>.Instance);
    }

    private static BrokerServerProfile CreateProfile(bool isActive)
    {
        return new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "Default server",
            Host = "mqtt.meshtastic.org",
            Port = 1883,
            DownlinkTopic = "msh/US/2/json/mqtt/",
            EnableSend = true,
            IsActive = isActive,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static SaveBrokerServerProfileRequest CreateSaveRequest(bool isActive)
    {
        return new SaveBrokerServerProfileRequest
        {
            Name = "Default server",
            Host = "mqtt.meshtastic.org",
            Port = 1883,
            DownlinkTopic = "msh/US/2/json/mqtt/",
            EnableSend = true,
            IsActive = isActive
        };
    }

    private sealed class FakeBrokerServerProfileRepository : IBrokerServerProfileRepository
    {
        public BrokerServerProfile? ActiveProfile { get; set; }

        public BrokerServerProfile? GetByIdResult { get; set; }

        public int GetActiveAsyncCallCount { get; private set; }

        public int SetExclusiveActiveAsyncCallCount { get; private set; }

        public int UpsertAsyncCallCount { get; private set; }

        public Guid? LastExclusiveActiveProfileId { get; private set; }

        public SaveBrokerServerProfileRequest? LastUpsertRequest { get; private set; }

        public BrokerServerProfile UpsertResult { get; set; } = CreateProfile(isActive: false);

        public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<WorkspaceBrokerServerProfile>>([]);
        }

        public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveUserOwnedAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<WorkspaceBrokerServerProfile>>([]);
        }

        public Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(
            string workspaceId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<BrokerServerProfile> profiles = ActiveProfile is null ? [] : [ActiveProfile];
            return Task.FromResult(profiles);
        }

        public Task<BrokerServerProfile?> GetActiveAsync(
            string workspaceId,
            CancellationToken cancellationToken = default)
        {
            GetActiveAsyncCallCount++;
            return Task.FromResult(ActiveProfile);
        }

        public Task<BrokerServerProfile?> GetByIdAsync(
            string workspaceId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetByIdResult);
        }

        public Task SetExclusiveActiveAsync(
            string workspaceId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            SetExclusiveActiveAsyncCallCount++;
            LastExclusiveActiveProfileId = id;

            if (UpsertResult.Id == id)
            {
                UpsertResult.IsActive = true;
                ActiveProfile = UpsertResult;
            }
            else if (GetByIdResult?.Id == id)
            {
                GetByIdResult.IsActive = true;
                ActiveProfile = GetByIdResult;
            }

            return Task.CompletedTask;
        }

        public Task<bool> AreSubscriptionIntentsInitializedAsync(
            string workspaceId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task MarkSubscriptionIntentsInitializedAsync(
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
            UpsertAsyncCallCount++;
            LastUpsertRequest = request;
            UpsertResult.IsActive = request.IsActive;
            return Task.FromResult(UpsertResult);
        }
    }

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int BeginCallCount { get; private set; }

        public int CommitCallCount { get; private set; }

        public int RollbackCallCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BeginCallCount++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCallCount++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCallCount++;
            return Task.CompletedTask;
        }
    }
}
