namespace MeshBoard.Contracts.Configuration;

public sealed class RealtimeDownstreamBrokerOptions
{
    public const string SectionName = "RealtimeDownstreamBroker";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1883;

    public bool UseTls { get; set; }

    public string ClientId { get; set; } = "meshboard-realtime-bridge";

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
