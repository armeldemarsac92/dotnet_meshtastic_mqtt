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
    private readonly IBrokerServerProfileService _brokerServerProfileService;
    private readonly IBrokerRuntimeCommandService _brokerRuntimeCommandService;
    private readonly ILogger<BrokerMonitorService> _logger;
    private readonly IBrokerRuntimeRegistry _brokerRuntimeRegistry;
    private readonly ISubscriptionIntentRepository _subscriptionIntentRepository;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public BrokerMonitorService(
        IBrokerRuntimeCommandService brokerRuntimeCommandService,
        IBrokerServerProfileService brokerServerProfileService,
        ISubscriptionIntentRepository subscriptionIntentRepository,
        IWorkspaceContextAccessor workspaceContextAccessor,
        IOptions<BrokerOptions> brokerOptions,
        IBrokerRuntimeRegistry brokerRuntimeRegistry,
        ILogger<BrokerMonitorService> logger)
    {
        _brokerRuntimeCommandService = brokerRuntimeCommandService;
        _brokerServerProfileService = brokerServerProfileService;
        _subscriptionIntentRepository = subscriptionIntentRepository;
        _workspaceContextAccessor = workspaceContextAccessor;
        _fallbackBrokerOptions = brokerOptions.Value;
        _brokerRuntimeRegistry = brokerRuntimeRegistry;
        _logger = logger;
    }

    public async Task EnsureConnected(CancellationToken cancellationToken = default)
    {
        var workspaceId = GetWorkspaceId();

        _logger.LogInformation("Attempting to ensure the MQTT session is connected");

        await _brokerRuntimeCommandService.EnsureConnectedAsync(workspaceId, cancellationToken);
    }

    public BrokerStatus GetBrokerStatus()
    {
        var workspaceId = GetWorkspaceId();
        var runtimeSnapshot = _brokerRuntimeRegistry.GetSnapshot(workspaceId);
        var pipelineSnapshot = _brokerRuntimeRegistry.GetPipelineSnapshot(workspaceId);
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
            IsConnected = runtimeSnapshot.IsConnected,
            LastStatusMessage = runtimeSnapshot.LastStatusMessage,
            TopicFilters = [..runtimeSnapshot.TopicFilters],
            InboundQueueCapacity = pipelineSnapshot.InboundQueueCapacity,
            InboundWorkerCount = pipelineSnapshot.InboundWorkerCount,
            InboundQueueDepth = pipelineSnapshot.InboundQueueDepth,
            InboundOldestMessageAgeMilliseconds = pipelineSnapshot.InboundOldestMessageAgeMilliseconds,
            InboundEnqueuedCount = pipelineSnapshot.InboundEnqueuedCount,
            InboundDequeuedCount = pipelineSnapshot.InboundDequeuedCount,
            InboundDroppedCount = pipelineSnapshot.InboundDroppedCount,
            RuntimeMetricsUpdatedAtUtc = pipelineSnapshot.UpdatedAtUtc
        };
    }

    public async Task SubscribeToDefaultTopic(CancellationToken cancellationToken = default)
    {
        await _brokerRuntimeCommandService.ReconcileActiveProfileAsync(GetWorkspaceId(), cancellationToken);
    }

    public async Task SubscribeToTopic(string topicFilter, CancellationToken cancellationToken = default)
    {
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        var workspaceId = GetWorkspaceId();

        _logger.LogInformation("Attempting to persist subscription intent: {TopicFilter}", normalizedTopicFilter);

        await _subscriptionIntentRepository.AddAsync(
            workspaceId,
            activeServer.Id,
            normalizedTopicFilter,
            cancellationToken);

        await _brokerRuntimeCommandService.ReconcileActiveProfileAsync(workspaceId, cancellationToken);
    }

    public async Task SubscribeToEphemeralTopic(string topicFilter, CancellationToken cancellationToken = default)
    {
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);

        _logger.LogInformation("Attempting to subscribe ephemerally to topic filter: {TopicFilter}", normalizedTopicFilter);

        await _brokerRuntimeCommandService.SubscribeEphemeralAsync(GetWorkspaceId(), normalizedTopicFilter, cancellationToken);
    }

    public async Task SwitchActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default)
    {
        var activeProfile = await _brokerServerProfileService.SetActiveServerProfile(profileId, cancellationToken);

        _logger.LogInformation(
            "Attempting to switch active MQTT server to {ServerName} ({ServerAddress})",
            activeProfile.Name,
            activeProfile.ServerAddress);

        await _brokerRuntimeCommandService.ResetAndReconnectActiveProfileAsync(GetWorkspaceId(), cancellationToken);
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

        await _brokerRuntimeCommandService.ReconcileActiveProfileAsync(workspaceId, cancellationToken);
    }

    public async Task UnsubscribeFromEphemeralTopic(string topicFilter, CancellationToken cancellationToken = default)
    {
        var normalizedTopicFilter = NormalizeTopicFilterForIntent(topicFilter);

        _logger.LogInformation("Attempting to unsubscribe ephemerally from topic filter: {TopicFilter}", normalizedTopicFilter);
        await _brokerRuntimeCommandService.UnsubscribeEphemeralAsync(GetWorkspaceId(), normalizedTopicFilter, cancellationToken);
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

        if (segments.Length < 4)
        {
            return false;
        }

        return string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase);
    }

    private static int TryParsePort(string serverAddress)
    {
        var split = serverAddress.Split(':', 2, StringSplitOptions.TrimEntries);
        return split.Length == 2 && int.TryParse(split[1], out var port) ? port : 0;
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
