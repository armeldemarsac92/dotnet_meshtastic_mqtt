namespace MeshBoard.Contracts.Configuration;

public sealed class BrokerOptions
{
    public const string SectionName = "Broker";

    public string Host { get; set; } = "mqtt.meshtastic.org";

    public int Port { get; set; } = 1883;

    public bool UseTls { get; set; }

    public string ClientId { get; set; } = "meshboard-local";

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DefaultTopicPattern { get; set; } = "msh/US/2/e/#";

    public string DownlinkTopic { get; set; } = "msh/US/2/json/mqtt/";

    public bool EnableSend { get; set; }
}
