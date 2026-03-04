namespace MeshBoard.Contracts.Meshtastic;

public sealed class BrokerStatus
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public bool IsConnected { get; set; }

    public string? LastStatusMessage { get; set; }

    public List<string> TopicFilters { get; set; } = [];
}
