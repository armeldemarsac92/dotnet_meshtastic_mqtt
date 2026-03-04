using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ITopicProbeService
{
    Task<TopicProbeResult> ProbeTopics(
        string searchText,
        int durationSeconds = 20,
        CancellationToken cancellationToken = default);
}

public sealed class TopicProbeService : ITopicProbeService
{
    private readonly IBrokerMonitorService _brokerMonitorService;
    private readonly ILogger<TopicProbeService> _logger;
    private readonly ITopicExplorerService _topicExplorerService;

    public TopicProbeService(
        ITopicExplorerService topicExplorerService,
        IBrokerMonitorService brokerMonitorService,
        ILogger<TopicProbeService> logger)
    {
        _topicExplorerService = topicExplorerService;
        _brokerMonitorService = brokerMonitorService;
        _logger = logger;
    }

    public async Task<TopicProbeResult> ProbeTopics(
        string searchText,
        int durationSeconds = 20,
        CancellationToken cancellationToken = default)
    {
        var search = searchText.Trim();
        var duration = Math.Clamp(durationSeconds, 5, 60);
        var status = _brokerMonitorService.GetBrokerStatus();
        var existingFilters = status.TopicFilters.ToHashSet(StringComparer.Ordinal);

        var candidateFilters = _topicExplorerService.GetRecommendedTopics()
            .Where(entry => MatchesSearch(entry, search))
            .Select(entry => entry.TopicPattern)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (candidateFilters.Count == 0)
        {
            return new TopicProbeResult
            {
                DurationSeconds = duration,
                SearchText = search
            };
        }

        var temporaryFilters = candidateFilters
            .Where(filter => !existingFilters.Contains(filter))
            .ToList();

        _logger.LogInformation(
            "Attempting topic discovery probe with {FilterCount} temporary filters for {DurationSeconds} seconds",
            temporaryFilters.Count,
            duration);

        foreach (var filter in temporaryFilters)
        {
            await _brokerMonitorService.SubscribeToTopic(filter, cancellationToken);
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(duration), cancellationToken);
        }
        finally
        {
            foreach (var filter in temporaryFilters)
            {
                try
                {
                    await _brokerMonitorService.UnsubscribeFromTopic(filter, cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Failed to unsubscribe temporary probe filter: {TopicFilter}",
                        filter);
                }
            }
        }

        return new TopicProbeResult
        {
            DurationSeconds = duration,
            SearchText = search,
            ProbedTopicFilters = candidateFilters,
            TemporaryTopicFilters = temporaryFilters
        };
    }

    private static bool MatchesSearch(TopicCatalogEntry entry, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return entry.Region.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            entry.Channel.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            entry.TopicPattern.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            entry.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }
}
