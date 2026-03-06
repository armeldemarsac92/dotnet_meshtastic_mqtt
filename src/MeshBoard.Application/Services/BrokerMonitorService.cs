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
    private Guid? _activeServerProfileId;
    private string _activeServerAddress;
    private string _activeServerName;

    public BrokerMonitorService(
        IMqttSession mqttSession,
        IBrokerServerProfileService brokerServerProfileService,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<BrokerMonitorService> logger)
    {
        _mqttSession = mqttSession;
        _brokerServerProfileService = brokerServerProfileService;
        _fallbackBrokerOptions = brokerOptions.Value;
        _logger = logger;
        _activeServerName = "Default server";
        _activeServerAddress = $"{_fallbackBrokerOptions.Host}:{_fallbackBrokerOptions.Port}";
    }

    public async Task EnsureConnected(CancellationToken cancellationToken = default)
    {
        await RefreshActiveServerSnapshot(cancellationToken);

        if (_mqttSession.IsConnected)
        {
            return;
        }

        _logger.LogInformation("Attempting to ensure the MQTT session is connected");

        await _mqttSession.ConnectAsync(cancellationToken);
    }

    public BrokerStatus GetBrokerStatus()
    {
        return new BrokerStatus
        {
            ActiveServerProfileId = _activeServerProfileId,
            ActiveServerName = _activeServerName,
            ActiveServerAddress = _activeServerAddress,
            Host = _activeServerAddress.Split(':', 2)[0],
            Port = TryParsePort(_activeServerAddress),
            IsConnected = _mqttSession.IsConnected,
            LastStatusMessage = _mqttSession.LastStatusMessage,
            TopicFilters = NormalizeTopicFiltersForDisplay(_mqttSession.TopicFilters)
        };
    }

    public async Task SubscribeToDefaultTopic(CancellationToken cancellationToken = default)
    {
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        await SubscribeToTopic(activeServer.DefaultTopicPattern, cancellationToken);
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
        _activeServerProfileId = activeProfile.Id;
        _activeServerName = activeProfile.Name;
        _activeServerAddress = activeProfile.ServerAddress;

        _logger.LogInformation(
            "Attempting to switch active MQTT server to {ServerName} ({ServerAddress})",
            _activeServerName,
            _activeServerAddress);

        await _mqttSession.DisconnectAsync(cancellationToken);
        await _mqttSession.ConnectAsync(cancellationToken);

        if (_mqttSession.TopicFilters.Count == 0)
        {
            await SubscribeToTopic(activeProfile.DefaultTopicPattern, cancellationToken);
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

    private async Task RefreshActiveServerSnapshot(CancellationToken cancellationToken)
    {
        var activeServer = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        _activeServerProfileId = activeServer.Id;
        _activeServerName = activeServer.Name;
        _activeServerAddress = activeServer.ServerAddress;
    }

    private static int TryParsePort(string serverAddress)
    {
        var split = serverAddress.Split(':', 2, StringSplitOptions.TrimEntries);
        return split.Length == 2 && int.TryParse(split[1], out var port) ? port : 0;
    }
}
