using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ISavedChannelPreferenceService
{
    Task<IReadOnlyCollection<SavedChannelFilter>> GetSavedChannels(CancellationToken cancellationToken = default);

    Task SaveChannel(SaveChannelFilterRequest request, CancellationToken cancellationToken = default);

    Task RemoveChannel(string topicFilter, CancellationToken cancellationToken = default);
}

public sealed class SavedChannelPreferenceService : ISavedChannelPreferenceService
{
    private readonly IBrokerServerProfileService _brokerServerProfileService;
    private readonly ILogger<SavedChannelPreferenceService> _logger;
    private readonly ISavedChannelFilterRepository _savedChannelFilterRepository;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public SavedChannelPreferenceService(
        IBrokerServerProfileService brokerServerProfileService,
        ISavedChannelFilterRepository savedChannelFilterRepository,
        IWorkspaceContextAccessor workspaceContextAccessor,
        ILogger<SavedChannelPreferenceService> logger)
    {
        _brokerServerProfileService = brokerServerProfileService;
        _savedChannelFilterRepository = savedChannelFilterRepository;
        _workspaceContextAccessor = workspaceContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<SavedChannelFilter>> GetSavedChannels(CancellationToken cancellationToken = default)
    {
        var workspaceId = GetWorkspaceId();
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);

        _logger.LogInformation(
            "Attempting to fetch saved channel filters for workspace {WorkspaceId} and broker {BrokerServerProfileId}",
            workspaceId,
            activeServer.Id);

        return await _savedChannelFilterRepository.GetAllAsync(workspaceId, activeServer.Id, cancellationToken);
    }

    public async Task SaveChannel(SaveChannelFilterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedTopicFilter = NormalizeTopicFilterForIntent(request.TopicFilter);
        var label = NormalizeLabel(request.Label);
        var workspaceId = GetWorkspaceId();
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);

        _logger.LogInformation(
            "Attempting to persist saved channel {TopicFilter} for workspace {WorkspaceId} and broker {BrokerServerProfileId}",
            normalizedTopicFilter,
            workspaceId,
            activeServer.Id);

        await _savedChannelFilterRepository.UpsertAsync(
            workspaceId,
            activeServer.Id,
            normalizedTopicFilter,
            label,
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

        await _savedChannelFilterRepository.DeleteAsync(
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

    private static string? NormalizeLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return label.Trim();
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
