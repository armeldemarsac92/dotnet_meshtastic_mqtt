using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MeshBoard.UnitTests;

public sealed class MessageRetentionServiceTests
{
    [Fact]
    public async Task PruneExpiredMessages_ShouldUseConfiguredRetentionWindow()
    {
        var fixedUtcNow = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero);
        var repository = new FakeMessageRepository(rowsDeleted: 3);
        var service = new MessageRetentionService(
            repository,
            new FixedTimeProvider(fixedUtcNow),
            Options.Create(
                new PersistenceOptions
                {
                    MessageRetentionDays = 14
                }),
            NullLogger<MessageRetentionService>.Instance);

        var deletedCount = await service.PruneExpiredMessages();

        Assert.Equal(3, deletedCount);
        Assert.Equal(fixedUtcNow.AddDays(-14), repository.LastCutoffUtc);
    }

    [Fact]
    public async Task PruneExpiredMessages_ShouldForwardCancellationToken()
    {
        var repository = new FakeMessageRepository(rowsDeleted: 0);
        var service = new MessageRetentionService(
            repository,
            new FixedTimeProvider(new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero)),
            Options.Create(new PersistenceOptions()),
            NullLogger<MessageRetentionService>.Instance);

        using var cancellationTokenSource = new CancellationTokenSource();
        await service.PruneExpiredMessages(cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, repository.LastCancellationToken);
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        private readonly int _rowsDeleted;

        public FakeMessageRepository(int rowsDeleted)
        {
            _rowsDeleted = rowsDeleted;
        }

        public CancellationToken LastCancellationToken { get; private set; }

        public DateTimeOffset? LastCutoffUtc { get; private set; }

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
            LastCutoffUtc = cutoffUtc;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(_rowsDeleted);
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetPageAsync(
            MessageQuery query,
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

        public Task<ChannelSummary> GetChannelSummaryAsync(
            string region,
            string channel,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountByChannelAsync(
            string region,
            string channel,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetPageByChannelAsync(
            string region,
            string channel,
            int offset,
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetPageBySenderAsync(
            string senderNodeId,
            int offset,
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

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
