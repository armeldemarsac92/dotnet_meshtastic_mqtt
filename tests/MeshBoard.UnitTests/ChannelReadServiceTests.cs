using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Topics;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class ChannelReadServiceTests
{
    [Fact]
    public async Task GetChannelSummary_ShouldForwardRegionAndChannel()
    {
        var repository = new FakeMessageRepository();
        var service = new ChannelReadService(repository, NullLogger<ChannelReadService>.Instance);

        var summary = await service.GetChannelSummary("US", "LongFast");

        Assert.Equal("US", repository.LastRegion);
        Assert.Equal("LongFast", repository.LastChannel);
        Assert.Equal(3, summary.PacketCount);
        Assert.Equal(2, summary.UniqueSenderCount);
    }

    [Fact]
    public async Task GetTopNodesByChannel_ShouldClampTake()
    {
        var repository = new FakeMessageRepository();
        var service = new ChannelReadService(repository, NullLogger<ChannelReadService>.Instance);

        var topNodes = await service.GetTopNodesByChannel("US", "LongFast", 500);

        Assert.Equal("US", repository.LastRegion);
        Assert.Equal("LongFast", repository.LastChannel);
        Assert.Equal(100, repository.LastTake);
        Assert.Single(topNodes);
    }

    [Fact]
    public async Task GetMessagesPageByChannel_ShouldForwardPagingAndReturnPagedResult()
    {
        var repository = new FakeMessageRepository();
        var service = new ChannelReadService(repository, NullLogger<ChannelReadService>.Instance);

        var page = await service.GetMessagesPageByChannel("US", "LongFast", offset: 25, take: 40);

        Assert.Equal("US", repository.LastRegion);
        Assert.Equal("LongFast", repository.LastChannel);
        Assert.Equal(25, repository.LastOffset);
        Assert.Equal(40, repository.LastTake);
        Assert.Equal(3, page.TotalCount);
        Assert.Single(page.Items);
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public string? LastChannel { get; private set; }

        public string? LastRegion { get; private set; }

        public int LastTake { get; private set; }

        public int LastOffset { get; private set; }

        public Task<bool> AddAsync(SaveObservedMessageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountAsync(MessageQuery query, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public Task<ChannelSummary> GetChannelSummaryAsync(
            string region,
            string channel,
            CancellationToken cancellationToken = default)
        {
            LastRegion = region;
            LastChannel = channel;

            return Task.FromResult(
                new ChannelSummary
                {
                    PacketCount = 3,
                    UniqueSenderCount = 2,
                    DecodedPacketCount = 2,
                    ObservedBrokerServers = ["mqtt.example.org:1883"]
                });
        }

        public Task<int> CountByChannelAsync(
            string region,
            string channel,
            CancellationToken cancellationToken = default)
        {
            LastRegion = region;
            LastChannel = channel;

            return Task.FromResult(3);
        }

        public Task<int> CountBySenderAsync(
            string senderNodeId,
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
            LastRegion = region;
            LastChannel = channel;
            LastTake = take;

            IReadOnlyCollection<ChannelTopNode> nodes =
            [
                new()
                {
                    NodeId = "!abc12345",
                    DisplayName = "Alpha",
                    PacketCount = 12
                }
            ];

            return Task.FromResult(nodes);
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetPageByChannelAsync(
            string region,
            string channel,
            int offset,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastRegion = region;
            LastChannel = channel;
            LastOffset = offset;
            LastTake = take;

            IReadOnlyCollection<MessageSummary> messages =
            [
                new()
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

        public Task<IReadOnlyCollection<MessageSummary>> GetPageBySenderAsync(
            string senderNodeId,
            int offset,
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentAsync(
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentByBrokerAsync(
            string brokerServer,
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
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentBySenderAsync(
            string senderNodeId,
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
