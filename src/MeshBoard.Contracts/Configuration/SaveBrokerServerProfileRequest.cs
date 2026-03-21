namespace MeshBoard.Contracts.Configuration;

public sealed class SaveBrokerServerProfileRequest
{
    public Guid? Id { get; set; }

    public required string Name { get; set; }

    public required string Host { get; set; }

    public int Port { get; set; } = 1883;

    public bool UseTls { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DownlinkTopic { get; set; } = "msh/US/2/json/mqtt/";

    public bool EnableSend { get; set; }

    public bool IsActive { get; set; }
}
