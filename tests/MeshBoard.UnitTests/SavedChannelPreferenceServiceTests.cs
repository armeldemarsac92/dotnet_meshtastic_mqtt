using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class SavedChannelPreferenceServiceTests
{
    [Fact]
    public async Task SaveChannel_ShouldNormalizeJsonTopicPrefix_AndPersistAgainstActiveProfile()
    {
        var repository = new FakeSavedChannelFilterRepository();
        var service = new SavedChannelPreferenceService(
            new FakeBrokerServerProfileService(),
            repository,
            new FakeWorkspaceContextAccessor(),
            NullLogger<SavedChannelPreferenceService>.Instance);

        await service.SaveChannel(
            new SaveChannelFilterRequest
            {
                TopicFilter = "  msh/US/2/json/LongFast/#  ",
                Label = "  LongFast  "
            });

        var savedFilter = Assert.Single(repository.UpsertedFilters);
        Assert.Equal("workspace-tests", savedFilter.WorkspaceId);
        Assert.Equal(FakeBrokerServerProfileService.ActiveProfileId, savedFilter.ProfileId);
        Assert.Equal("msh/US/2/e/LongFast/#", savedFilter.TopicFilter);
        Assert.Equal("LongFast", savedFilter.Label);
    }

    [Fact]
    public async Task RemoveChannel_ShouldNormalizeTopicFilter_AndDeleteAgainstActiveProfile()
    {
        var repository = new FakeSavedChannelFilterRepository();
        var service = new SavedChannelPreferenceService(
            new FakeBrokerServerProfileService(),
            repository,
            new FakeWorkspaceContextAccessor(),
            NullLogger<SavedChannelPreferenceService>.Instance);

        await service.RemoveChannel("  msh/US/2/e/LongFast/#  ");

        var removedFilter = Assert.Single(repository.DeletedFilters);
        Assert.Equal("workspace-tests", removedFilter.WorkspaceId);
        Assert.Equal(FakeBrokerServerProfileService.ActiveProfileId, removedFilter.ProfileId);
        Assert.Equal("msh/US/2/e/LongFast/#", removedFilter.TopicFilter);
    }

    [Fact]
    public async Task SaveChannel_ShouldThrowBadRequest_WhenFilterIsMissing()
    {
        var service = new SavedChannelPreferenceService(
            new FakeBrokerServerProfileService(),
            new FakeSavedChannelFilterRepository(),
            new FakeWorkspaceContextAccessor(),
            NullLogger<SavedChannelPreferenceService>.Instance);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.SaveChannel(new SaveChannelFilterRequest { TopicFilter = " " }));
    }

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }

    private sealed class FakeBrokerServerProfileService : IBrokerServerProfileService
    {
        public static readonly Guid ActiveProfileId = Guid.Parse("3ef19c82-c3cb-4256-bef0-9444f76df69b");

        public Task<BrokerServerProfile> GetActiveServerProfile(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new BrokerServerProfile
                {
                    Id = ActiveProfileId,
                    Name = "Primary",
                    Host = "mqtt.example.org",
                    Port = 1883,
                    DefaultTopicPattern = "msh/US/2/e/#",
                    DownlinkTopic = "msh/US/2/json/mqtt/",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    IsActive = true
                });
        }

        public Task<BrokerServerProfile?> GetServerProfileById(Guid profileId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<BrokerServerProfile>> GetServerProfiles(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<BrokerServerProfile> SaveServerProfile(
            SaveBrokerServerProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<BrokerServerProfile> SetActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeSavedChannelFilterRepository : ISavedChannelFilterRepository
    {
        public List<(string WorkspaceId, Guid ProfileId, string TopicFilter, string? Label)> UpsertedFilters { get; } = [];

        public List<(string WorkspaceId, Guid ProfileId, string TopicFilter)> DeletedFilters { get; } = [];

        public Task<IReadOnlyCollection<SavedChannelFilter>> GetAllAsync(
            string workspaceId,
            Guid brokerServerProfileId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<SavedChannelFilter> result = [];
            return Task.FromResult(result);
        }

        public Task<bool> UpsertAsync(
            string workspaceId,
            Guid brokerServerProfileId,
            string topicFilter,
            string? label,
            CancellationToken cancellationToken = default)
        {
            UpsertedFilters.Add((workspaceId, brokerServerProfileId, topicFilter, label));
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(
            string workspaceId,
            Guid brokerServerProfileId,
            string topicFilter,
            CancellationToken cancellationToken = default)
        {
            DeletedFilters.Add((workspaceId, brokerServerProfileId, topicFilter));
            return Task.FromResult(true);
        }
    }
}
