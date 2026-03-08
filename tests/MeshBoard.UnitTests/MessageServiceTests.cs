using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class MessageServiceTests
{
    [Fact]
    public async Task GetMessagesPage_ShouldForwardQueryAndPaginationToRepository()
    {
        var repository = new FakeMessageRepository();
        var service = new MessageService(repository, NullLogger<MessageService>.Instance);
        var query = new MessageQuery
        {
            BrokerServer = "mqtt.meshtastic.org:1883",
            SearchText = "alpha",
            PacketType = "Text Message",
            Visibility = MessageVisibilityFilter.DecodedOnly
        };

        var page = await service.GetMessagesPage(query, offset: 25, take: 50);

        Assert.Equal(query, repository.LastQuery);
        Assert.Equal(25, repository.LastOffset);
        Assert.Equal(50, repository.LastTake);
        Assert.Equal(3, page.TotalCount);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task GetRecentMessagesBySender_ShouldForwardSenderToRepository()
    {
        var repository = new FakeMessageRepository();
        var service = new MessageService(repository, NullLogger<MessageService>.Instance);

        var messages = await service.GetRecentMessagesBySender("!abc12345", take: 20);

        Assert.Equal("!abc12345", repository.LastSenderNodeId);
        Assert.Equal(20, repository.LastTake);
        Assert.Single(messages);
    }

    [Fact]
    public async Task GetRecentMessagesByBroker_ShouldForwardBrokerToRepository()
    {
        var repository = new FakeMessageRepository();
        var service = new MessageService(repository, NullLogger<MessageService>.Instance);

        var messages = await service.GetRecentMessagesByBroker("mqtt.meshtastic.org:1883", take: 30);

        Assert.Equal("mqtt.meshtastic.org:1883", repository.LastBrokerServer);
        Assert.Equal(30, repository.LastTake);
        Assert.Single(messages);
    }

    [Fact]
    public async Task GetRecentMessagesBySender_ShouldReturnEmptyWhenSenderIsBlank()
    {
        var repository = new FakeMessageRepository();
        var service = new MessageService(repository, NullLogger<MessageService>.Instance);

        var messages = await service.GetRecentMessagesBySender("   ");

        Assert.Empty(messages);
        Assert.Null(repository.LastSenderNodeId);
    }

    [Fact]
    public async Task GetRecentMessagesByChannel_ShouldForwardChannelToRepository()
    {
        var repository = new FakeMessageRepository();
        var service = new MessageService(repository, NullLogger<MessageService>.Instance);

        var messages = await service.GetRecentMessagesByChannel("US", "LongFast", take: 40);

        Assert.Equal("US", repository.LastRegion);
        Assert.Equal("LongFast", repository.LastChannel);
        Assert.Equal(40, repository.LastTake);
        Assert.Single(messages);
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public string? LastBrokerServer { get; private set; }

        public string? LastChannel { get; private set; }

        public string? LastRegion { get; private set; }

        public string? LastSenderNodeId { get; private set; }

        public MessageQuery? LastQuery { get; private set; }

        public int LastOffset { get; private set; }

        public int LastTake { get; private set; }

        public Task<bool> AddAsync(SaveObservedMessageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountAsync(MessageQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(3);
        }

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetPageAsync(
            MessageQuery query,
            int offset,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            LastOffset = offset;
            LastTake = take;

            IReadOnlyCollection<MessageSummary> messages =
            [
                new MessageSummary
                {
                    Id = Guid.NewGuid(),
                    BrokerServer = query.BrokerServer,
                    Topic = "msh/US/2/e/LongFast/!abc12345",
                    PacketType = query.PacketType,
                    FromNodeId = "!abc12345",
                    PayloadPreview = "hello",
                    IsPrivate = false,
                    ReceivedAtUtc = DateTimeOffset.UtcNow
                }
            ];

            return Task.FromResult(messages);
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentAsync(
            int take,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<MessageSummary> messages = [];
            return Task.FromResult(messages);
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentByBrokerAsync(
            string brokerServer,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastBrokerServer = brokerServer;
            LastTake = take;

            IReadOnlyCollection<MessageSummary> messages =
            [
                new MessageSummary
                {
                    Id = Guid.NewGuid(),
                    BrokerServer = brokerServer,
                    Topic = "msh/US/2/e/LongFast/!abc12345",
                    PacketType = "Text Message",
                    FromNodeId = "!abc12345",
                    PayloadPreview = "hello",
                    IsPrivate = false,
                    ReceivedAtUtc = DateTimeOffset.UtcNow
                }
            ];

            return Task.FromResult(messages);
        }

        public Task<ChannelSummary> GetChannelSummaryAsync(
            string region,
            string channel,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<ChannelTopNode>> GetTopNodesByChannelAsync(
            string region,
            string channel,
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentByChannelAsync(
            string region,
            string channel,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastRegion = region;
            LastChannel = channel;
            LastTake = take;

            IReadOnlyCollection<MessageSummary> messages =
            [
                new MessageSummary
                {
                    Id = Guid.NewGuid(),
                    Topic = $"msh/{region}/2/e/{channel}/!abc12345",
                    PacketType = "Text Message",
                    FromNodeId = "!abc12345",
                    PayloadPreview = "hello",
                    IsPrivate = false,
                    ReceivedAtUtc = DateTimeOffset.UtcNow
                }
            ];

            return Task.FromResult(messages);
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentBySenderAsync(
            string senderNodeId,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastSenderNodeId = senderNodeId;
            LastTake = take;

            IReadOnlyCollection<MessageSummary> messages =
            [
                new MessageSummary
                {
                    Id = Guid.NewGuid(),
                    Topic = "msh/US/2/e/LongFast/!abc12345",
                    PacketType = "Text Message",
                    FromNodeId = senderNodeId,
                    PayloadPreview = "hello",
                    IsPrivate = false,
                    ReceivedAtUtc = DateTimeOffset.UtcNow
                }
            ];

            return Task.FromResult(messages);
        }
    }
}
