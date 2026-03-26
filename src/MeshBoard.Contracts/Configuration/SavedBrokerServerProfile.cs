namespace MeshBoard.Contracts.Configuration;

public sealed class SavedBrokerServerProfile
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public bool UseTls { get; set; }

    public string Username { get; set; } = string.Empty;

    public bool HasPasswordConfigured { get; set; }

    public string DownlinkTopic { get; set; } = string.Empty;

    public bool EnableSend { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string ServerAddress { get; set; } = string.Empty;
}
