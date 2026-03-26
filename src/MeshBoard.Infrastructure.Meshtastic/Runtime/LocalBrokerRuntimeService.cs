using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace MeshBoard.Infrastructure.Meshtastic.Runtime;

internal sealed class LocalBrokerRuntimeService : IBrokerRuntimeService
{
    private readonly ILogger<LocalBrokerRuntimeService> _logger;
    private readonly IBrokerRuntimeRegistry _brokerRuntimeRegistry;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IWorkspaceBrokerSessionManager _workspaceBrokerSessionManager;

    public LocalBrokerRuntimeService(
        IServiceScopeFactory serviceScopeFactory,
        IWorkspaceBrokerSessionManager workspaceBrokerSessionManager,
        IBrokerRuntimeRegistry brokerRuntimeRegistry,
        ILogger<LocalBrokerRuntimeService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _workspaceBrokerSessionManager = workspaceBrokerSessionManager;
        _brokerRuntimeRegistry = brokerRuntimeRegistry;
        _logger = logger;
    }

    public async Task EnsureConnectedAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var activeProfile = await GetRequiredActiveProfileAsync(workspaceId, cancellationToken);
        await EnsureConnectedCoreAsync(workspaceId, activeProfile, cancellationToken);
    }

    public async Task ReconcileActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var activeProfile = await GetRequiredActiveProfileAsync(workspaceId, cancellationToken);
        await EnsureConnectedCoreAsync(workspaceId, activeProfile, cancellationToken);
    }

    public async Task ResetAndReconnectActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var activeProfile = await GetRequiredActiveProfileAsync(workspaceId, cancellationToken);

        _logger.LogInformation(
            "Resetting MQTT runtime for workspace {WorkspaceId} and profile {ProfileId}",
            workspaceId,
            activeProfile.Id);

        await _workspaceBrokerSessionManager.ResetRuntimeAsync(workspaceId, cancellationToken);
        await _workspaceBrokerSessionManager.ConnectAsync(workspaceId, cancellationToken);
        await ReconcileDesiredTopicFiltersAsync(workspaceId, activeProfile, cancellationToken);
        UpdateRuntimeSnapshot(workspaceId, activeProfile);
    }

    public async Task PublishAsync(
        string workspaceId,
        string topic,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var activeProfile = await GetRequiredActiveProfileAsync(workspaceId, cancellationToken);

        await EnsureConnectedAsync(workspaceId, cancellationToken);
        await _workspaceBrokerSessionManager.PublishAsync(workspaceId, topic, payload, cancellationToken);
        UpdateRuntimeSnapshot(workspaceId, activeProfile);
    }

    public async Task SubscribeEphemeralAsync(
        string workspaceId,
        string topicFilter,
        CancellationToken cancellationToken = default)
    {
        var activeProfile = await GetRequiredActiveProfileAsync(workspaceId, cancellationToken);
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);

        await EnsureConnectedAsync(workspaceId, cancellationToken);

        foreach (var filter in ExpandWithCompanionFilter(normalizedTopicFilter))
        {
            await _workspaceBrokerSessionManager.SubscribeAsync(workspaceId, filter, cancellationToken);
        }

        UpdateRuntimeSnapshot(workspaceId, activeProfile);
    }

    public async Task UnsubscribeEphemeralAsync(
        string workspaceId,
        string topicFilter,
        CancellationToken cancellationToken = default)
    {
        var activeProfile = await GetRequiredActiveProfileAsync(workspaceId, cancellationToken);
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);

        foreach (var filter in ExpandWithCompanionFilter(normalizedTopicFilter))
        {
            await _workspaceBrokerSessionManager.UnsubscribeAsync(workspaceId, filter, cancellationToken);
        }

        UpdateRuntimeSnapshot(workspaceId, activeProfile);
    }

    private async Task<BrokerServerProfile> GetRequiredActiveProfileAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var brokerServerProfileRepository = scope.ServiceProvider.GetRequiredService<IBrokerServerProfileRepository>();

        return await brokerServerProfileRepository.GetActiveAsync(workspaceId, cancellationToken)
            ?? throw new InvalidOperationException($"No active broker server profile is configured for workspace '{workspaceId}'.");
    }

    private async Task RefreshRuntimeSnapshotAsync(string workspaceId, CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var brokerServerProfileRepository = scope.ServiceProvider.GetRequiredService<IBrokerServerProfileRepository>();
        var activeProfile = await brokerServerProfileRepository.GetActiveAsync(workspaceId, cancellationToken);

        if (activeProfile is not null)
        {
            UpdateRuntimeSnapshot(workspaceId, activeProfile);
        }
    }

    private void UpdateRuntimeSnapshot(string workspaceId, BrokerServerProfile activeProfile)
    {
        _brokerRuntimeRegistry.UpdateSnapshot(
            workspaceId,
            new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = activeProfile.Id,
                ActiveServerName = activeProfile.Name,
                ActiveServerAddress = activeProfile.ServerAddress,
                IsConnected = _workspaceBrokerSessionManager.IsConnected(workspaceId),
                LastStatusMessage = _workspaceBrokerSessionManager.GetLastStatusMessage(workspaceId),
                TopicFilters = NormalizeTopicFiltersForDisplay(_workspaceBrokerSessionManager.GetTopicFilters(workspaceId))
            });
    }

    private async Task EnsureConnectedCoreAsync(
        string workspaceId,
        BrokerServerProfile activeProfile,
        CancellationToken cancellationToken)
    {
        var isConnected = _workspaceBrokerSessionManager.IsConnected(workspaceId);
        var snapshot = _brokerRuntimeRegistry.GetSnapshot(workspaceId);

        if (isConnected && RequiresReconnect(snapshot, activeProfile))
        {
            _logger.LogInformation(
                "Detected active server change for workspace {WorkspaceId}. Reconnecting runtime from profile {ExistingProfileId} to {ProfileId}",
                workspaceId,
                snapshot.ActiveServerProfileId,
                activeProfile.Id);

            await _workspaceBrokerSessionManager.ResetRuntimeAsync(workspaceId, cancellationToken);
            isConnected = false;
        }

        if (!isConnected)
        {
            _logger.LogInformation(
                "Ensuring MQTT runtime connection for workspace {WorkspaceId} and profile {ProfileId}",
                workspaceId,
                activeProfile.Id);

            await _workspaceBrokerSessionManager.ConnectAsync(workspaceId, cancellationToken);
        }

        await ReconcileDesiredTopicFiltersAsync(workspaceId, activeProfile, cancellationToken);
        UpdateRuntimeSnapshot(workspaceId, activeProfile);
    }

    private async Task ReconcileDesiredTopicFiltersAsync(
        string workspaceId,
        BrokerServerProfile activeProfile,
        CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var desiredFilters = await ResolveDesiredTopicFiltersAsync(
            scope.ServiceProvider,
            workspaceId,
            activeProfile,
            cancellationToken);

        var currentFilters = _workspaceBrokerSessionManager.GetTopicFilters(workspaceId).ToHashSet(StringComparer.Ordinal);

        foreach (var topicFilter in currentFilters.Except(desiredFilters, StringComparer.Ordinal))
        {
            await _workspaceBrokerSessionManager.UnsubscribeAsync(workspaceId, topicFilter, cancellationToken);
        }

        foreach (var topicFilter in desiredFilters.Except(currentFilters, StringComparer.Ordinal))
        {
            await _workspaceBrokerSessionManager.SubscribeAsync(workspaceId, topicFilter, cancellationToken);
        }
    }

    private static bool RequiresReconnect(BrokerRuntimeSnapshot snapshot, BrokerServerProfile activeProfile)
    {
        if (!snapshot.IsConnected)
        {
            return false;
        }

        return snapshot.ActiveServerProfileId != activeProfile.Id ||
               !string.Equals(snapshot.ActiveServerAddress, activeProfile.ServerAddress, StringComparison.Ordinal) ||
               !string.Equals(snapshot.ActiveServerName, activeProfile.Name, StringComparison.Ordinal);
    }

    private static Task<HashSet<string>> ResolveDesiredTopicFiltersAsync(
        IServiceProvider serviceProvider,
        string workspaceId,
        BrokerServerProfile activeProfile,
        CancellationToken cancellationToken)
    {
        const string rootFilter = "msh/+/2/e/#";
        var filters = ExpandWithCompanionFilter(rootFilter)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        return Task.FromResult(filters);
    }

    private static List<string> ExpandWithCompanionFilter(string topicFilter)
    {
        var expanded = new List<string> { topicFilter };

        if (TryMapCompanionFilter(topicFilter, out var companionFilter) &&
            !string.Equals(companionFilter, topicFilter, StringComparison.Ordinal))
        {
            expanded.Add(companionFilter);
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

    private static string NormalizeTopicFilterForIntent(string topicFilter)
    {
        if (string.IsNullOrWhiteSpace(topicFilter))
        {
            throw new InvalidOperationException("A topic filter is required.");
        }

        return NormalizeTopicFilterForDisplay(topicFilter.Trim());
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

        return segments.Length >= 4 &&
            string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase);
    }
}
