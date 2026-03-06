using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Messages;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class MessageServiceTests
{
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

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public string? LastBrokerServer { get; private set; }

        public string? LastSenderNodeId { get; private set; }

        public int LastTake { get; private set; }

        public Task<bool> AddAsync(SaveObservedMessageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
