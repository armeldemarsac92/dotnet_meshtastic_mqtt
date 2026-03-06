using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Services;

public interface ISendCapabilityService
{
    SendCapabilityStatus GetStatus();
}

public sealed class SendCapabilityService : ISendCapabilityService
{
    private readonly BrokerOptions _brokerOptions;
    private readonly IBrokerServerProfileService _brokerServerProfileService;
    private readonly IMqttSession _mqttSession;

    public SendCapabilityService(
        IOptions<BrokerOptions> brokerOptions,
        IMqttSession mqttSession,
        IBrokerServerProfileService brokerServerProfileService)
    {
        _brokerOptions = brokerOptions.Value;
        _mqttSession = mqttSession;
        _brokerServerProfileService = brokerServerProfileService;
    }

    public SendCapabilityStatus GetStatus()
    {
        var activeServer = TryGetActiveServerProfile();
        var host = activeServer?.Host ?? _brokerOptions.Host;
        var port = activeServer?.Port ?? _brokerOptions.Port;
        var downlinkTopic = activeServer?.DownlinkTopic ?? _brokerOptions.DownlinkTopic;
        var enableSend = activeServer?.EnableSend ?? _brokerOptions.EnableSend;

        var status = new SendCapabilityStatus
        {
            Host = host,
            Port = port,
            DownlinkTopic = downlinkTopic,
            IsBrokerConnected = _mqttSession.IsConnected
        };

        if (!enableSend)
        {
            status.BlockingReasons.Add("Sending is disabled by configuration. Set Broker:EnableSend to true to enable compose.");
        }

        if (!_mqttSession.IsConnected)
        {
            status.BlockingReasons.Add("The MQTT session is not connected.");
        }

        if (string.IsNullOrWhiteSpace(downlinkTopic))
        {
            status.BlockingReasons.Add("No downlink topic is configured. Set Broker:DownlinkTopic.");
        }

        if (!string.IsNullOrWhiteSpace(downlinkTopic) &&
            !downlinkTopic.StartsWith("msh/", StringComparison.Ordinal))
        {
            status.BlockingReasons.Add("The configured downlink topic does not match expected Meshtastic topic format.");
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            status.BlockingReasons.Add("No MQTT broker host is configured.");
        }

        status.Advisories.Add(
            "Sending still requires a Meshtastic gateway node configured for downlink on the selected broker topic.");
        status.Advisories.Add(
            "Do not enable compose on public infrastructure unless you control the gateway and channel configuration.");

        status.IsEnabled = status.BlockingReasons.Count == 0;
        return status;
    }

    private Contracts.Configuration.BrokerServerProfile? TryGetActiveServerProfile()
    {
        try
        {
            return _brokerServerProfileService.GetActiveServerProfile().GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }
}
