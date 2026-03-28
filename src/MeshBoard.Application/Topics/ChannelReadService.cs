using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Topics;

public interface IChannelReadService
{
    Task<MessagePageResult> GetMessagesPageByChannel(
        string region,
        string channel,
        int offset = 0,
        int take = 25,
        CancellationToken cancellationToken = default);

    Task<ChannelSummary> GetChannelSummary(
        string region,
        string channel,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ChannelTopNode>> GetTopNodesByChannel(
        string region,
        string channel,
        int take = 12,
        CancellationToken cancellationToken = default);
}

public sealed class ChannelReadService : IChannelReadService
{
    private const int MaxMessagesTake = 100;
    private const int MaxTopNodesTake = 100;

    private readonly ILogger<ChannelReadService> _logger;
    private readonly IMessageRepository _messageRepository;

    public ChannelReadService(
        IMessageRepository messageRepository,
        ILogger<ChannelReadService> logger)
    {
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task<MessagePageResult> GetMessagesPageByChannel(
        string region,
        string channel,
        int offset = 0,
        int take = 25,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(channel))
        {
            return Array.Empty<MessageSummary>().ToMessagePageResult(0);
        }

        var normalizedRegion = region.Trim();
        var normalizedChannel = channel.Trim();
        var normalizedOffset = Math.Max(0, offset);
        var normalizedTake = Math.Clamp(take, 1, MaxMessagesTake);

        _logger.LogDebug(
            "Attempting to get channel messages page for region {Region}, channel {Channel} with offset {Offset} and take {Take}",
            normalizedRegion,
            normalizedChannel,
            normalizedOffset,
            normalizedTake);

        var totalCountTask = _messageRepository.CountByChannelAsync(
            normalizedRegion,
            normalizedChannel,
            cancellationToken);
        var itemsTask = _messageRepository.GetPageByChannelAsync(
            normalizedRegion,
            normalizedChannel,
            normalizedOffset,
            normalizedTake,
            cancellationToken);

        await Task.WhenAll(totalCountTask, itemsTask);

        return (await itemsTask).ToMessagePageResult(await totalCountTask);
    }

    public async Task<ChannelSummary> GetChannelSummary(
        string region,
        string channel,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(channel))
        {
            return (region, channel).ToEmptyChannelSummary();
        }

        var normalizedRegion = region.Trim();
        var normalizedChannel = channel.Trim();

        _logger.LogDebug(
            "Attempting to get channel summary for region {Region}, channel {Channel}",
            normalizedRegion,
            normalizedChannel);

        var summary = await _messageRepository.GetChannelSummaryAsync(
            normalizedRegion,
            normalizedChannel,
            cancellationToken);

        _logger.LogDebug(
            "Retrieved channel summary for region {Region}, channel {Channel} with {PacketCount} packets",
            normalizedRegion,
            normalizedChannel,
            summary.PacketCount);

        return summary;
    }

    public async Task<IReadOnlyCollection<ChannelTopNode>> GetTopNodesByChannel(
        string region,
        string channel,
        int take = 12,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(channel))
        {
            return [];
        }

        var normalizedRegion = region.Trim();
        var normalizedChannel = channel.Trim();
        var normalizedTake = Math.Clamp(take, 1, MaxTopNodesTake);

        _logger.LogDebug(
            "Attempting to get top nodes for region {Region}, channel {Channel} with take {Take}",
            normalizedRegion,
            normalizedChannel,
            normalizedTake);

        var topNodes = await _messageRepository.GetTopNodesByChannelAsync(
            normalizedRegion,
            normalizedChannel,
            normalizedTake,
            cancellationToken);

        _logger.LogDebug(
            "Retrieved {TopNodeCount} top nodes for region {Region}, channel {Channel}",
            topNodes.Count,
            normalizedRegion,
            normalizedChannel);

        return topNodes;
    }
}
