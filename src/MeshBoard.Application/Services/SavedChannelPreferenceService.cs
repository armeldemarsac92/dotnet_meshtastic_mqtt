using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ISavedChannelPreferenceService
{
    Task<IReadOnlyCollection<SubscriptionIntent>> GetSavedChannels(CancellationToken cancellationToken = default);

    Task SaveChannel(string topicFilter, CancellationToken cancellationToken = default);

    Task RemoveChannel(string topicFilter, CancellationToken cancellationToken = default);
}

public sealed class SavedChannelPreferenceService : ISavedChannelPreferenceService
{
    private readonly IBrokerServerProfileService _brokerServerProfileService;
    private readonly ILogger<SavedChannelPreferenceService> _logger;
    private readonly ISubscriptionIntentRepository _subscriptionIntentRepository;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public SavedChannelPreferenceService(
        IBrokerServerProfileService brokerServerProfileService,
        ISubscriptionIntentRepository subscriptionIntentRepository,
        IWorkspaceContextAccessor workspaceContextAccessor,
        ILogger<SavedChannelPreferenceService> logger)
    {
        _brokerServerProfileService = brokerServerProfileService;
        _subscriptionIntentRepository = subscriptionIntentRepository;
        _workspaceContextAccessor = workspaceContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<SubscriptionIntent>> GetSavedChannels(CancellationToken cancellationToken = default)
    {
        var workspaceId = GetWorkspaceId();
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);

        _logger.LogInformation(
            "Attempting to fetch saved channel filters for workspace {WorkspaceId} and broker {BrokerServerProfileId}",
            workspaceId,
            activeServer.Id);

        return await _subscriptionIntentRepository.GetAllAsync(workspaceId, activeServer.Id, cancellationToken);
    }

    public async Task SaveChannel(string topicFilter, CancellationToken cancellationToken = default)
    {
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);
        var workspaceId = GetWorkspaceId();
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);

        _logger.LogInformation(
            "Attempting to persist saved channel {TopicFilter} for workspace {WorkspaceId} and broker {BrokerServerProfileId}",
            normalizedTopicFilter,
            workspaceId,
            activeServer.Id);

        await _subscriptionIntentRepository.AddAsync(
            workspaceId,
            activeServer.Id,
            normalizedTopicFilter,
            cancellationToken);
    }

    public async Task RemoveChannel(string topicFilter, CancellationToken cancellationToken = default)
    {
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);
        var workspaceId = GetWorkspaceId();
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);

        _logger.LogInformation(
            "Attempting to delete saved channel {TopicFilter} for workspace {WorkspaceId} and broker {BrokerServerProfileId}",
            normalizedTopicFilter,
            workspaceId,
            activeServer.Id);

        await _subscriptionIntentRepository.DeleteAsync(
            workspaceId,
            activeServer.Id,
            normalizedTopicFilter,
            cancellationToken);
    }

    private static string NormalizeTopicFilterForIntent(string topicFilter)
    {
        if (string.IsNullOrWhiteSpace(topicFilter))
        {
            throw new BadRequestException("A topic filter is required.");
        }

        return NormalizeTopicFilterForDisplay(topicFilter.Trim());
    }

    private static string NormalizeTopicFilterForDisplay(string topicFilter)
    {
        if (!TrySplitMeshtasticTopic(topicFilter, out var segments))
        {
            return topicFilter;
        }

        if (string.Equals(segments[3], "json", StringComparison.OrdinalIgnoreCase))
        {
            segments[3] = "e";
        }

        return string.Join('/', segments);
    }

    private static bool TrySplitMeshtasticTopic(string topicFilter, out string[] segments)
    {
        segments = topicFilter
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Length >= 4 &&
               string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase);
    }

    private string GetWorkspaceId()
    {
        return _workspaceContextAccessor.GetWorkspaceId();
    }
}
