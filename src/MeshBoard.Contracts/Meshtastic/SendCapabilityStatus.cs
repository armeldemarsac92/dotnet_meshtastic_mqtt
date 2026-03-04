namespace MeshBoard.Contracts.Meshtastic;

public sealed class SendCapabilityStatus
{
    public bool IsEnabled { get; set; }

    public bool IsBrokerConnected { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string DownlinkTopic { get; set; } = string.Empty;

    public List<string> BlockingReasons { get; set; } = [];

    public List<string> Advisories { get; set; } = [];
}
