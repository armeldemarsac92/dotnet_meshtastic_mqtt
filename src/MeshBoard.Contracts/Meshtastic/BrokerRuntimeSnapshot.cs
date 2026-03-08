namespace MeshBoard.Contracts.Meshtastic;

public sealed class BrokerRuntimeSnapshot
{
    public Guid? ActiveServerProfileId { get; set; }

    public string? ActiveServerName { get; set; }

    public string? ActiveServerAddress { get; set; }

    public bool IsConnected { get; set; }

    public string? LastStatusMessage { get; set; }

    public List<string> TopicFilters { get; set; } = [];
}
