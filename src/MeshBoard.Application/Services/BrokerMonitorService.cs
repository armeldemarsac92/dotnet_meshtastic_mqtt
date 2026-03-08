using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Services;

public interface IBrokerMonitorService
{
    Task EnsureConnected(CancellationToken cancellationToken = default);

    BrokerStatus GetBrokerStatus();

    Task SubscribeToDefaultTopic(CancellationToken cancellationToken = default);

    Task SubscribeToTopic(string topicFilter, CancellationToken cancellationToken = default);

    Task SubscribeToEphemeralTopic(string topicFilter, CancellationToken cancellationToken = default);

    Task SwitchActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default);

    Task UnsubscribeFromTopic(string topicFilter, CancellationToken cancellationToken = default);

    Task UnsubscribeFromEphemeralTopic(string topicFilter, CancellationToken cancellationToken = default);
}

public sealed class BrokerMonitorService : IBrokerMonitorService
{
    private readonly BrokerOptions _fallbackBrokerOptions;
    private readonly IBrokerServerProfileRepository _brokerServerProfileRepository;
    private readonly IBrokerServerProfileService _brokerServerProfileService;
    private readonly IWorkspaceBrokerSessionManager _brokerSessionManager;
    private readonly ILogger<BrokerMonitorService> _logger;
    private readonly IBrokerRuntimeRegistry _brokerRuntimeRegistry;
    private readonly ISubscriptionIntentRepository _subscriptionIntentRepository;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public BrokerMonitorService(
        IWorkspaceBrokerSessionManager brokerSessionManager,
        IBrokerServerProfileService brokerServerProfileService,
        IBrokerServerProfileRepository brokerServerProfileRepository,
        ISubscriptionIntentRepository subscriptionIntentRepository,
        IWorkspaceContextAccessor workspaceContextAccessor,
        IOptions<BrokerOptions> brokerOptions,
        IBrokerRuntimeRegistry brokerRuntimeRegistry,
        ILogger<BrokerMonitorService> logger)
    {
        _brokerSessionManager = brokerSessionManager;
        _brokerServerProfileService = brokerServerProfileService;
        _brokerServerProfileRepository = brokerServerProfileRepository;
        _subscriptionIntentRepository = subscriptionIntentRepository;
        _workspaceContextAccessor = workspaceContextAccessor;
        _fallbackBrokerOptions = brokerOptions.Value;
        _brokerRuntimeRegistry = brokerRuntimeRegistry;
        _logger = logger;
    }

    public async Task EnsureConnected(CancellationToken cancellationToken = default)
    {
        var workspaceId = GetWorkspaceId();

        if (_brokerSessionManager.IsConnected(workspaceId))
        {
            return;
        }

        var activeServer = await RefreshActiveServerSnapshot(cancellationToken);

        _logger.LogInformation("Attempting to ensure the MQTT session is connected");

        await _brokerSessionManager.ConnectAsync(workspaceId, cancellationToken);
        await ReconcileSubscriptionIntents(activeServer, cancellationToken);
    }

    public BrokerStatus GetBrokerStatus()
    {
        var workspaceId = GetWorkspaceId();
        var runtimeSnapshot = _brokerRuntimeRegistry.GetSnapshot();
        var activeServerAddress = string.IsNullOrWhiteSpace(runtimeSnapshot.ActiveServerAddress)
            ? $"{_fallbackBrokerOptions.Host}:{_fallbackBrokerOptions.Port}"
            : runtimeSnapshot.ActiveServerAddress;

        return new BrokerStatus
        {
            ActiveServerProfileId = runtimeSnapshot.ActiveServerProfileId,
            ActiveServerName = runtimeSnapshot.ActiveServerName,
            ActiveServerAddress = activeServerAddress,
            Host = activeServerAddress.Split(':', 2)[0],
            Port = TryParsePort(activeServerAddress),
            IsConnected = _brokerSessionManager.IsConnected(workspaceId),
            LastStatusMessage = _brokerSessionManager.GetLastStatusMessage(workspaceId),
            TopicFilters = NormalizeTopicFiltersForDisplay(_brokerSessionManager.GetTopicFilters(workspaceId))
        };
    }

    public async Task SubscribeToDefaultTopic(CancellationToken cancellationToken = default)
    {
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        UpdateRuntimeSnapshot(activeServer);
        await EnsureDefaultSubscriptionIntent(activeServer, cancellationToken);
        await ReconcileSubscriptionIntents(activeServer, cancellationToken);
    }

    public async Task SubscribeToTopic(string topicFilter, CancellationToken cancellationToken = default)
    {
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);
        await EnsureConnected(cancellationToken);
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        var workspaceId = GetWorkspaceId();

        _logger.LogInformation("Attempting to persist subscription intent: {TopicFilter}", normalizedTopicFilter);

        await _subscriptionIntentRepository.AddAsync(
            workspaceId,
            activeServer.Id,
            normalizedTopicFilter,
            cancellationToken);

        await EnsureProfileSubscriptionIntentsInitialized(workspaceId, activeServer.Id, cancellationToken);
        await ReconcileSubscriptionIntents(activeServer, cancellationToken);
    }

    public async Task SubscribeToEphemeralTopic(string topicFilter, CancellationToken cancellationToken = default)
    {
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);

        _logger.LogInformation("Attempting to subscribe ephemerally to topic filter: {TopicFilter}", normalizedTopicFilter);

        await EnsureConnected(cancellationToken);
        await ApplyRuntimeSubscription(normalizedTopicFilter, cancellationToken);
    }

    public async Task SwitchActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default)
    {
        var activeProfile = await _brokerServerProfileService.SetActiveServerProfile(profileId, cancellationToken);
        UpdateRuntimeSnapshot(activeProfile);

        _logger.LogInformation(
            "Attempting to switch active MQTT server to {ServerName} ({ServerAddress})",
            activeProfile.Name,
            activeProfile.ServerAddress);

        var workspaceId = GetWorkspaceId();
        var previousTopicFilters = _brokerSessionManager.GetTopicFilters(workspaceId).ToList();

        foreach (var topicFilter in previousTopicFilters)
        {
            await _brokerSessionManager.UnsubscribeAsync(workspaceId, topicFilter, cancellationToken);
        }

        await _brokerSessionManager.DisconnectAsync(workspaceId, cancellationToken);
        await _brokerSessionManager.ConnectAsync(workspaceId, cancellationToken);

        await EnsureDefaultSubscriptionIntent(activeProfile, cancellationToken);
        await ReconcileSubscriptionIntents(activeProfile, cancellationToken);
    }

    public async Task UnsubscribeFromTopic(string topicFilter, CancellationToken cancellationToken = default)
    {
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        var workspaceId = GetWorkspaceId();

        _logger.LogInformation("Attempting to remove subscription intent: {TopicFilter}", normalizedTopicFilter);

        await _subscriptionIntentRepository.DeleteAsync(
            workspaceId,
            activeServer.Id,
            normalizedTopicFilter,
            cancellationToken);

        await ReconcileSubscriptionIntents(activeServer, cancellationToken);
    }

    public async Task UnsubscribeFromEphemeralTopic(string topicFilter, CancellationToken cancellationToken = default)
    {
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);

        _logger.LogInformation("Attempting to unsubscribe ephemerally from topic filter: {TopicFilter}", normalizedTopicFilter);
        var workspaceId = GetWorkspaceId();

        foreach (var filter in ExpandWithCompanionFilter(normalizedTopicFilter))
        {
            await _brokerSessionManager.UnsubscribeAsync(workspaceId, filter, cancellationToken);
        }
    }

    private static List<string> ExpandWithCompanionFilter(string topicFilter)
    {
        var expanded = new List<string> { topicFilter };

        if (TryMapCompanionFilter(topicFilter, out var companionFilter))
        {
            if (!string.Equals(companionFilter, topicFilter, StringComparison.Ordinal))
            {
                expanded.Add(companionFilter);
            }
        }

        return expanded;
    }

    private static List<string> NormalizeTopicFiltersForDisplay(IEnumerable<string> topicFilters)
    {
        return topicFilters
            .Select(NormalizeTopicFilterForDisplay)
            .Where(filter => !string.IsNullOrWhiteSpace(filter))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(filter => filter, StringComparer.Ordinal)
            .ToList();
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

    private static bool TryMapCompanionFilter(string topicFilter, out string companionFilter)
    {
        companionFilter = string.Empty;

        if (!TrySplitMeshtasticTopic(topicFilter, out var segments))
        {
            return false;
        }

        if (string.Equals(segments[3], "e", StringComparison.OrdinalIgnoreCase))
        {
            segments[3] = "json";
        }
        else if (string.Equals(segments[3], "json", StringComparison.OrdinalIgnoreCase))
        {
            segments[3] = "e";
        }
        else
        {
            return false;
        }

        companionFilter = string.Join('/', segments);
        return true;
    }

    private static bool TrySplitMeshtasticTopic(string topicFilter, out string[] segments)
    {
        segments = topicFilter
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 4)
        {
            return false;
        }

        return string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeToServerRootTopicFilter(string topicFilter)
    {
        if (!TrySplitMeshtasticTopic(topicFilter, out var segments))
        {
            return topicFilter;
        }

        if (!string.Equals(segments[3], "e", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(segments[3], "json", StringComparison.OrdinalIgnoreCase))
        {
            return topicFilter;
        }

        return string.Join('/', segments[..4]) + "/#";
    }

    private async Task<BrokerServerProfile> RefreshActiveServerSnapshot(CancellationToken cancellationToken)
    {
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        UpdateRuntimeSnapshot(activeServer);
        return activeServer;
    }

    private static int TryParsePort(string serverAddress)
    {
        var split = serverAddress.Split(':', 2, StringSplitOptions.TrimEntries);
        return split.Length == 2 && int.TryParse(split[1], out var port) ? port : 0;
    }

    private void UpdateRuntimeSnapshot(BrokerServerProfile activeServer)
    {
        _brokerRuntimeRegistry.UpdateSnapshot(
            new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = activeServer.Id,
                ActiveServerName = activeServer.Name,
                ActiveServerAddress = activeServer.ServerAddress
            });
    }

    private async Task EnsureDefaultSubscriptionIntent(
        BrokerServerProfile activeServer,
        CancellationToken cancellationToken)
    {
        var workspaceId = GetWorkspaceId();

        if (await _brokerServerProfileRepository.AreSubscriptionIntentsInitializedAsync(
                workspaceId,
                activeServer.Id,
                cancellationToken))
        {
            return;
        }

        var defaultTopicFilter = NormalizeToServerRootTopicFilter(activeServer.DefaultTopicPattern);

        if (!string.IsNullOrWhiteSpace(defaultTopicFilter))
        {
            await _subscriptionIntentRepository.AddAsync(
                workspaceId,
                activeServer.Id,
                NormalizeTopicFilterForIntent(defaultTopicFilter),
                cancellationToken);
        }

        await EnsureProfileSubscriptionIntentsInitialized(workspaceId, activeServer.Id, cancellationToken);
    }

    private async Task EnsureProfileSubscriptionIntentsInitialized(
        string workspaceId,
        Guid profileId,
        CancellationToken cancellationToken)
    {
        await _brokerServerProfileRepository.MarkSubscriptionIntentsInitializedAsync(
            workspaceId,
            profileId,
            cancellationToken);
    }

    private async Task ReconcileSubscriptionIntents(
        BrokerServerProfile activeServer,
        CancellationToken cancellationToken)
    {
        var workspaceId = GetWorkspaceId();
        var intents = await _subscriptionIntentRepository.GetAllAsync(workspaceId, activeServer.Id, cancellationToken);

        var desiredFilters = intents
            .SelectMany(intent => ExpandWithCompanionFilter(intent.TopicFilter))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var currentFilters = _brokerSessionManager.GetTopicFilters(workspaceId).ToHashSet(StringComparer.Ordinal);

        foreach (var topicFilter in currentFilters.Except(desiredFilters, StringComparer.Ordinal))
        {
            await _brokerSessionManager.UnsubscribeAsync(workspaceId, topicFilter, cancellationToken);
        }

        foreach (var topicFilter in desiredFilters.Except(currentFilters, StringComparer.Ordinal))
        {
            await _brokerSessionManager.SubscribeAsync(workspaceId, topicFilter, cancellationToken);
        }
    }

    private async Task ApplyRuntimeSubscription(string topicFilter, CancellationToken cancellationToken)
    {
        var workspaceId = GetWorkspaceId();

        foreach (var filter in ExpandWithCompanionFilter(topicFilter))
        {
            await _brokerSessionManager.SubscribeAsync(workspaceId, filter, cancellationToken);
        }
    }

    private static string NormalizeTopicFilterForIntent(string topicFilter)
    {
        if (string.IsNullOrWhiteSpace(topicFilter))
        {
            throw new BadRequestException("A topic filter is required.");
        }

        return NormalizeTopicFilterForDisplay(topicFilter.Trim());
    }

    private string GetWorkspaceId()
    {
        return _workspaceContextAccessor.GetWorkspaceId();
    }
}
