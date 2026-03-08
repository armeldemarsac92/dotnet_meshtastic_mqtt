using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Messages;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface IMessageService
{
    Task<MessagePageResult> GetMessagesPage(
        MessageQuery? query = null,
        int offset = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<MessagePageResult> GetMessagesPageBySender(
        string senderNodeId,
        int offset = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetRecentMessages(int take = 250, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetRecentMessagesByBroker(
        string brokerServer,
        int take = 250,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetRecentMessagesByChannel(
        string region,
        string channel,
        int take = 250,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetRecentMessagesBySender(
        string senderNodeId,
        int take = 250,
        CancellationToken cancellationToken = default);
}

public sealed class MessageService : IMessageService
{
    private const int MaxTake = 250;

    private readonly ILogger<MessageService> _logger;
    private readonly IMessageRepository _messageRepository;

    public MessageService(IMessageRepository messageRepository, ILogger<MessageService> logger)
    {
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task<MessagePageResult> GetMessagesPage(
        MessageQuery? query = null,
        int offset = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = query ?? new MessageQuery();
        var sanitizedOffset = Math.Max(0, offset);
        var sanitizedTake = SanitizeTake(take);

        _logger.LogDebug(
            "Attempting to get messages page with offset: {Offset}, take: {Take}",
            sanitizedOffset,
            sanitizedTake);

        var totalCountTask = _messageRepository.CountAsync(sanitizedQuery, cancellationToken);
        var itemsTask = _messageRepository.GetPageAsync(
            sanitizedQuery,
            sanitizedOffset,
            sanitizedTake,
            cancellationToken);

        await Task.WhenAll(totalCountTask, itemsTask);

        return new MessagePageResult
        {
            TotalCount = await totalCountTask,
            Items = await itemsTask
        };
    }

    public async Task<MessagePageResult> GetMessagesPageBySender(
        string senderNodeId,
        int offset = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(senderNodeId))
        {
            return new MessagePageResult();
        }

        var normalizedSenderNodeId = senderNodeId.Trim();
        var sanitizedOffset = Math.Max(0, offset);
        var sanitizedTake = SanitizeTake(take);

        _logger.LogDebug(
            "Attempting to get sender messages page for sender {SenderNodeId} with offset: {Offset}, take: {Take}",
            normalizedSenderNodeId,
            sanitizedOffset,
            sanitizedTake);

        var totalCountTask = _messageRepository.CountBySenderAsync(normalizedSenderNodeId, cancellationToken);
        var itemsTask = _messageRepository.GetPageBySenderAsync(
            normalizedSenderNodeId,
            sanitizedOffset,
            sanitizedTake,
            cancellationToken);

        await Task.WhenAll(totalCountTask, itemsTask);

        return new MessagePageResult
        {
            TotalCount = await totalCountTask,
            Items = await itemsTask
        };
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentMessages(
        int take = 250,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to get recent messages with take: {Take}", take);

        var messages = await _messageRepository.GetRecentAsync(take, cancellationToken);

        _logger.LogDebug("Retrieved {MessageCount} recent messages", messages.Count);

        return messages;
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentMessagesByBroker(
        string brokerServer,
        int take = 250,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(brokerServer))
        {
            return await GetRecentMessages(take, cancellationToken);
        }

        _logger.LogDebug(
            "Attempting to get recent messages for broker {BrokerServer} with take: {Take}",
            brokerServer,
            take);

        var messages = await _messageRepository.GetRecentByBrokerAsync(
            brokerServer.Trim(),
            take,
            cancellationToken);

        _logger.LogDebug(
            "Retrieved {MessageCount} recent messages for broker {BrokerServer}",
            messages.Count,
            brokerServer);

        return messages;
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentMessagesByChannel(
        string region,
        string channel,
        int take = 250,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(channel))
        {
            return [];
        }

        _logger.LogDebug(
            "Attempting to get recent messages for region {Region}, channel {Channel} with take: {Take}",
            region,
            channel,
            take);

        var messages = await _messageRepository.GetRecentByChannelAsync(
            region.Trim(),
            channel.Trim(),
            take,
            cancellationToken);

        _logger.LogDebug(
            "Retrieved {MessageCount} recent messages for region {Region}, channel {Channel}",
            messages.Count,
            region,
            channel);

        return messages;
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentMessagesBySender(
        string senderNodeId,
        int take = 250,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(senderNodeId))
        {
            return [];
        }

        _logger.LogDebug(
            "Attempting to get recent messages by sender {SenderNodeId} with take: {Take}",
            senderNodeId,
            take);

        var messages = await _messageRepository.GetRecentBySenderAsync(
            senderNodeId.Trim(),
            take,
            cancellationToken);

        _logger.LogDebug(
            "Retrieved {MessageCount} recent messages by sender {SenderNodeId}",
            messages.Count,
            senderNodeId);

        return messages;
    }

    private static int SanitizeTake(int take)
    {
        if (take <= 0)
        {
            return 1;
        }

        return Math.Min(take, MaxTake);
    }
}
