namespace MeshBoard.Infrastructure.Meshtastic.Mqtt;

internal sealed class MqttSessionConnectionSettings
{
    public required string WorkspaceId { get; init; }

    public required Guid BrokerServerProfileId { get; init; }

    public required string Host { get; init; }

    public int Port { get; init; }

    public bool UseTls { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ServerAddress => $"{Host}:{Port}";
}
