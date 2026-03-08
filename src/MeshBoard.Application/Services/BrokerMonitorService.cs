using MeshBoard.Application.Abstractions.Meshtastic;
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

    Task SwitchActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default);

    Task UnsubscribeFromTopic(string topicFilter, CancellationToken cancellationToken = default);
}

public sealed class BrokerMonitorService : IBrokerMonitorService
{
    private readonly BrokerOptions _fallbackBrokerOptions;
    private readonly IBrokerServerProfileService _brokerServerProfileService;
    private readonly ILogger<BrokerMonitorService> _logger;
    private readonly IMqttSession _mqttSession;
    private readonly IBrokerRuntimeRegistry _brokerRuntimeRegistry;

    public BrokerMonitorService(
        IMqttSession mqttSession,
        IBrokerServerProfileService brokerServerProfileService,
        IOptions<BrokerOptions> brokerOptions,
        IBrokerRuntimeRegistry brokerRuntimeRegistry,
        ILogger<BrokerMonitorService> logger)
    {
        _mqttSession = mqttSession;
        _brokerServerProfileService = brokerServerProfileService;
        _fallbackBrokerOptions = brokerOptions.Value;
        _brokerRuntimeRegistry = brokerRuntimeRegistry;
        _logger = logger;
    }

    public async Task EnsureConnected(CancellationToken cancellationToken = default)
    {
        if (_mqttSession.IsConnected)
        {
            return;
        }

        await RefreshActiveServerSnapshot(cancellationToken);

        _logger.LogInformation("Attempting to ensure the MQTT session is connected");

        await _mqttSession.ConnectAsync(cancellationToken);
    }

    public BrokerStatus GetBrokerStatus()
    {
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
            IsConnected = _mqttSession.IsConnected,
            LastStatusMessage = _mqttSession.LastStatusMessage,
            TopicFilters = NormalizeTopicFiltersForDisplay(_mqttSession.TopicFilters)
        };
    }

    public async Task SubscribeToDefaultTopic(CancellationToken cancellationToken = default)
    {
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        UpdateRuntimeSnapshot(activeServer);
        var topicFilter = NormalizeToServerRootTopicFilter(activeServer.DefaultTopicPattern);
        await SubscribeToTopic(topicFilter, cancellationToken);
    }

    public async Task SubscribeToTopic(string topicFilter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicFilter))
        {
            throw new BadRequestException("A topic filter is required.");
        }

        _logger.LogInformation("Attempting to subscribe to topic filter: {TopicFilter}", topicFilter);

        await EnsureConnected(cancellationToken);

        foreach (var filter in ExpandWithCompanionFilter(topicFilter.Trim()))
        {
            await _mqttSession.SubscribeAsync(filter, cancellationToken);
        }
    }

    public async Task SwitchActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default)
    {
        var activeProfile = await _brokerServerProfileService.SetActiveServerProfile(profileId, cancellationToken);
        UpdateRuntimeSnapshot(activeProfile);

        _logger.LogInformation(
            "Attempting to switch active MQTT server to {ServerName} ({ServerAddress})",
            activeProfile.Name,
            activeProfile.ServerAddress);

        var previousTopicFilters = _mqttSession.TopicFilters.ToList();

        foreach (var topicFilter in previousTopicFilters)
        {
            await _mqttSession.UnsubscribeAsync(topicFilter, cancellationToken);
        }

        await _mqttSession.DisconnectAsync(cancellationToken);
        await _mqttSession.ConnectAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(activeProfile.DefaultTopicPattern))
        {
            await SubscribeToTopic(
                NormalizeToServerRootTopicFilter(activeProfile.DefaultTopicPattern),
                cancellationToken);
        }
    }

    public async Task UnsubscribeFromTopic(string topicFilter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicFilter))
        {
            throw new BadRequestException("A topic filter is required.");
        }

        _logger.LogInformation("Attempting to unsubscribe from topic filter: {TopicFilter}", topicFilter);

        foreach (var filter in ExpandWithCompanionFilter(topicFilter.Trim()))
        {
            await _mqttSession.UnsubscribeAsync(filter, cancellationToken);
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

    private async Task RefreshActiveServerSnapshot(CancellationToken cancellationToken)
    {
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        UpdateRuntimeSnapshot(activeServer);
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
}
