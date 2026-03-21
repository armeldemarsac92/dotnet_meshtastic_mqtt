using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class MeshtasticIngestionServiceTests
{
    [Fact]
    public async Task IngestEnvelope_ShouldPersistNeighborLinks_WhenNeighborInfoIsPresent()
    {
        var messageRepository = new FakeMessageRepository { AddResult = true };
        var nodeRepository = new FakeNodeRepository();
        var neighborLinkRepository = new FakeNeighborLinkRepository();
        var topicDiscoveryService = new FakeTopicDiscoveryService();
        var unitOfWork = new FakeUnitOfWork();
        var service = new MeshtasticIngestionService(
            messageRepository,
            nodeRepository,
            topicDiscoveryService,
            unitOfWork,
            NullLogger<MeshtasticIngestionService>.Instance,
            neighborLinkRepository);
        var receivedAtUtc = DateTimeOffset.Parse("2026-03-21T10:00:00Z");

        await service.IngestEnvelope(
            new MeshtasticEnvelope
            {
                WorkspaceId = "workspace-tests",
                BrokerServer = "broker.meshboard.test",
                Topic = "msh/US/2/e/LongFast/!00005678",
                PacketType = "Neighbor Info",
                PayloadPreview = "Neighbor info: 2 neighbors reported",
                FromNodeId = "!00005678",
                LastHeardChannel = "US/LongFast",
                ReceivedAtUtc = receivedAtUtc,
                Neighbors =
                [
                    new MeshtasticNeighborEntry
                    {
                        NodeId = "!00001234",
                        SnrDb = 6.0f,
                        LastRxAtUtc = DateTimeOffset.Parse("2026-03-21T09:59:00Z")
                    },
                    new MeshtasticNeighborEntry
                    {
                        NodeId = "!00005678",
                        SnrDb = 3.0f
                    }
                ]
            });

        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
        Assert.Equal(1, messageRepository.AddCalls);
        Assert.Equal(1, nodeRepository.UpsertCalls);
        Assert.Equal(1, topicDiscoveryService.RecordCalls);
        Assert.Equal(1, neighborLinkRepository.UpsertCalls);

        var persistedLink = Assert.Single(neighborLinkRepository.LastLinks);
        Assert.Equal("!00001234", persistedLink.SourceNodeId);
        Assert.Equal("!00005678", persistedLink.TargetNodeId);
        Assert.Equal(6.0f, persistedLink.SnrDb);
        Assert.Equal(DateTimeOffset.Parse("2026-03-21T09:59:00Z"), persistedLink.LastSeenAtUtc);
    }

    [Fact]
    public async Task IngestEnvelope_ShouldSkipNeighborLinkPersistence_WhenMessageIsDuplicate()
    {
        var messageRepository = new FakeMessageRepository { AddResult = false };
        var nodeRepository = new FakeNodeRepository();
        var neighborLinkRepository = new FakeNeighborLinkRepository();
        var topicDiscoveryService = new FakeTopicDiscoveryService();
        var unitOfWork = new FakeUnitOfWork();
        var service = new MeshtasticIngestionService(
            messageRepository,
            nodeRepository,
            topicDiscoveryService,
            unitOfWork,
            NullLogger<MeshtasticIngestionService>.Instance,
            neighborLinkRepository);

        await service.IngestEnvelope(
            new MeshtasticEnvelope
            {
                WorkspaceId = "workspace-tests",
                BrokerServer = "broker.meshboard.test",
                Topic = "msh/US/2/e/LongFast/!00005678",
                PacketType = "Neighbor Info",
                PayloadPreview = "Neighbor info: 1 neighbor reported",
                FromNodeId = "!00005678",
                ReceivedAtUtc = DateTimeOffset.Parse("2026-03-21T10:00:00Z"),
                Neighbors =
                [
                    new MeshtasticNeighborEntry
                    {
                        NodeId = "!00001234",
                        SnrDb = 6.0f
                    }
                ]
            });

        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
        Assert.Equal(1, messageRepository.AddCalls);
        Assert.Equal(0, nodeRepository.UpsertCalls);
        Assert.Equal(0, topicDiscoveryService.RecordCalls);
        Assert.Equal(0, neighborLinkRepository.UpsertCalls);
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public bool AddResult { get; set; } = true;

        public int AddCalls { get; private set; }

        public Task<bool> AddAsync(SaveObservedMessageRequest request, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            return Task.FromResult(AddResult);
        }

        public Task<int> CountAsync(MessageQuery query, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlyCollection<MessageSummary>> GetPageAsync(MessageQuery query, int offset, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);

        public Task<int> CountBySenderAsync(string senderNodeId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentByBrokerAsync(string brokerServer, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);

        public Task<ChannelSummary> GetChannelSummaryAsync(string region, string channel, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChannelSummary());

        public Task<int> CountByChannelAsync(string region, string channel, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlyCollection<ChannelTopNode>> GetTopNodesByChannelAsync(string region, string channel, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ChannelTopNode>>([]);

        public Task<IReadOnlyCollection<MessageSummary>> GetPageByChannelAsync(string region, string channel, int offset, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentByChannelAsync(string region, string channel, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentBySenderAsync(string senderNodeId, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);

        public Task<IReadOnlyCollection<MessageSummary>> GetPageBySenderAsync(string senderNodeId, int offset, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);
    }

    private sealed class FakeNodeRepository : INodeRepository
    {
        public int UpsertCalls { get; private set; }

        public Task<int> CountAsync(NodeQuery query, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<NodeSummary?> GetByIdAsync(string nodeId, CancellationToken cancellationToken = default) => Task.FromResult<NodeSummary?>(null);

        public Task<IReadOnlyCollection<NodeSummary>> GetLocatedAsync(string? searchText, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<NodeSummary>>([]);

        public Task<IReadOnlyCollection<NodeSummary>> GetPageAsync(NodeQuery query, int offset, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<NodeSummary>>([]);

        public Task UpsertAsync(UpsertObservedNodeRequest request, CancellationToken cancellationToken = default)
        {
            UpsertCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNeighborLinkRepository : INeighborLinkRepository
    {
        public int UpsertCalls { get; private set; }

        public IReadOnlyList<NeighborLinkRecord> LastLinks { get; private set; } = [];

        public Task UpsertAsync(string workspaceId, IReadOnlyList<NeighborLinkRecord> links, CancellationToken cancellationToken = default)
        {
            UpsertCalls++;
            LastLinks = links.ToArray();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<NeighborLinkRecord>> GetActiveLinksAsync(string workspaceId, DateTimeOffset notBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NeighborLinkRecord>>([]);
    }

    private sealed class FakeTopicDiscoveryService : ITopicDiscoveryService
    {
        public int RecordCalls { get; private set; }

        public Task<IReadOnlyCollection<TopicCatalogEntry>> GetDiscoveredTopics(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TopicCatalogEntry>>([]);

        public Task RecordObservedTopic(string topicValue, DateTimeOffset observedAtUtc, string? brokerServer = null, string? workspaceId = null, CancellationToken cancellationToken = default)
        {
            RecordCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }
    }
}
