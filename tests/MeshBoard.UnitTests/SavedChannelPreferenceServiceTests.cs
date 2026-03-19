using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class SavedChannelPreferenceServiceTests
{
    [Fact]
    public async Task SaveChannel_ShouldNormalizeJsonTopicPrefix_AndPersistAgainstActiveProfile()
    {
        var repository = new FakeSubscriptionIntentRepository();
        var service = new SavedChannelPreferenceService(
            new FakeBrokerServerProfileService(),
            repository,
            new FakeWorkspaceContextAccessor(),
            NullLogger<SavedChannelPreferenceService>.Instance);

        await service.SaveChannel("  msh/US/2/json/LongFast/#  ");

        var addedIntent = Assert.Single(repository.AddedIntents);
        Assert.Equal("workspace-tests", addedIntent.WorkspaceId);
        Assert.Equal(FakeBrokerServerProfileService.ActiveProfileId, addedIntent.ProfileId);
        Assert.Equal("msh/US/2/e/LongFast/#", addedIntent.TopicFilter);
    }

    [Fact]
    public async Task RemoveChannel_ShouldNormalizeTopicFilter_AndDeleteAgainstActiveProfile()
    {
        var repository = new FakeSubscriptionIntentRepository();
        var service = new SavedChannelPreferenceService(
            new FakeBrokerServerProfileService(),
            repository,
            new FakeWorkspaceContextAccessor(),
            NullLogger<SavedChannelPreferenceService>.Instance);

        await service.RemoveChannel("  msh/US/2/e/LongFast/#  ");

        var removedIntent = Assert.Single(repository.DeletedIntents);
        Assert.Equal("workspace-tests", removedIntent.WorkspaceId);
        Assert.Equal(FakeBrokerServerProfileService.ActiveProfileId, removedIntent.ProfileId);
        Assert.Equal("msh/US/2/e/LongFast/#", removedIntent.TopicFilter);
    }

    [Fact]
    public async Task SaveChannel_ShouldThrowBadRequest_WhenFilterIsMissing()
    {
        var service = new SavedChannelPreferenceService(
            new FakeBrokerServerProfileService(),
            new FakeSubscriptionIntentRepository(),
            new FakeWorkspaceContextAccessor(),
            NullLogger<SavedChannelPreferenceService>.Instance);

        await Assert.ThrowsAsync<BadRequestException>(() => service.SaveChannel(" "));
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
                    DefaultEncryptionKeyBase64 = "AQ==",
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

    private sealed class FakeSubscriptionIntentRepository : ISubscriptionIntentRepository
    {
        public List<(string WorkspaceId, Guid ProfileId, string TopicFilter)> AddedIntents { get; } = [];

        public List<(string WorkspaceId, Guid ProfileId, string TopicFilter)> DeletedIntents { get; } = [];

        public Task<bool> AddAsync(
            string workspaceId,
            Guid brokerServerProfileId,
            string topicFilter,
            CancellationToken cancellationToken = default)
        {
            AddedIntents.Add((workspaceId, brokerServerProfileId, topicFilter));
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(
            string workspaceId,
            Guid brokerServerProfileId,
            string topicFilter,
            CancellationToken cancellationToken = default)
        {
            DeletedIntents.Add((workspaceId, brokerServerProfileId, topicFilter));
            return Task.FromResult(true);
        }

        public Task<IReadOnlyCollection<SubscriptionIntent>> GetAllAsync(
            string workspaceId,
            Guid brokerServerProfileId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<SubscriptionIntent> result = [];
            return Task.FromResult(result);
        }
    }
}
