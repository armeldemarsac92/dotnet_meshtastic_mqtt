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
    private readonly IMqttSession _mqttSession;

    public SendCapabilityService(IOptions<BrokerOptions> brokerOptions, IMqttSession mqttSession)
    {
        _brokerOptions = brokerOptions.Value;
        _mqttSession = mqttSession;
    }

    public SendCapabilityStatus GetStatus()
    {
        var status = new SendCapabilityStatus
        {
            Host = _brokerOptions.Host,
            Port = _brokerOptions.Port,
            DownlinkTopic = _brokerOptions.DownlinkTopic,
            IsBrokerConnected = _mqttSession.IsConnected
        };

        if (!_brokerOptions.EnableSend)
        {
            status.BlockingReasons.Add("Sending is disabled by configuration. Set Broker:EnableSend to true to enable compose.");
        }

        if (!_mqttSession.IsConnected)
        {
            status.BlockingReasons.Add("The MQTT session is not connected.");
        }

        if (string.IsNullOrWhiteSpace(_brokerOptions.DownlinkTopic))
        {
            status.BlockingReasons.Add("No downlink topic is configured. Set Broker:DownlinkTopic.");
        }

        if (!string.IsNullOrWhiteSpace(_brokerOptions.DownlinkTopic) &&
            !_brokerOptions.DownlinkTopic.StartsWith("msh/", StringComparison.Ordinal))
        {
            status.BlockingReasons.Add("The configured downlink topic does not match expected Meshtastic topic format.");
        }

        if (string.IsNullOrWhiteSpace(_brokerOptions.Host))
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
}
