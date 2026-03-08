using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IMessageRepository
{
    Task<bool> AddAsync(SaveObservedMessageRequest request, CancellationToken cancellationToken = default);

    Task<int> CountAsync(MessageQuery query, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetPageAsync(
        MessageQuery query,
        int offset,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetRecentAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetRecentByBrokerAsync(
        string brokerServer,
        int take,
        CancellationToken cancellationToken = default);

    Task<ChannelSummary> GetChannelSummaryAsync(
        string region,
        string channel,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ChannelTopNode>> GetTopNodesByChannelAsync(
        string region,
        string channel,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetRecentByChannelAsync(
        string region,
        string channel,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetRecentBySenderAsync(
        string senderNodeId,
        int take,
        CancellationToken cancellationToken = default);
}
