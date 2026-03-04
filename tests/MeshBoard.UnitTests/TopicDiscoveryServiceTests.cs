using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class TopicDiscoveryServiceTests
{
    [Fact]
    public async Task RecordObservedTopic_ShouldNormalizeAndPersistTopicPattern()
    {
        var repository = new FakeDiscoveredTopicRepository();
        var service = new TopicDiscoveryService(
            repository,
            new TopicExplorerService(),
            NullLogger<TopicDiscoveryService>.Instance);
        var observedAtUtc = new DateTimeOffset(2026, 3, 4, 23, 0, 0, TimeSpan.Zero);

        await service.RecordObservedTopic("msh/EU_868/2/json/MediumFast/!12345678", observedAtUtc);

        Assert.Equal("msh/EU_868/2/e/MediumFast/#", repository.LastTopicPattern);
        Assert.Equal("EU_868", repository.LastRegion);
        Assert.Equal("MediumFast", repository.LastChannel);
        Assert.Equal(observedAtUtc, repository.LastObservedAtUtc);
    }

    [Fact]
    public async Task RecordObservedTopic_ShouldIgnoreNonMeshtasticTopic()
    {
        var repository = new FakeDiscoveredTopicRepository();
        var service = new TopicDiscoveryService(
            repository,
            new TopicExplorerService(),
            NullLogger<TopicDiscoveryService>.Instance);

        await service.RecordObservedTopic("invalid/topic/value", DateTimeOffset.UtcNow);

        Assert.Equal(0, repository.UpsertCount);
    }

    private sealed class FakeDiscoveredTopicRepository : IDiscoveredTopicRepository
    {
        public string? LastChannel { get; private set; }

        public DateTimeOffset? LastObservedAtUtc { get; private set; }

        public string? LastRegion { get; private set; }

        public string? LastTopicPattern { get; private set; }

        public int UpsertCount { get; private set; }

        public Task<IReadOnlyCollection<TopicCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<TopicCatalogEntry> discoveredTopics = [];
            return Task.FromResult(discoveredTopics);
        }

        public Task UpsertAsync(
            string topicPattern,
            string region,
            string channel,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken = default)
        {
            LastTopicPattern = topicPattern;
            LastRegion = region;
            LastChannel = channel;
            LastObservedAtUtc = observedAtUtc;
            UpsertCount++;
            return Task.CompletedTask;
        }
    }
}
