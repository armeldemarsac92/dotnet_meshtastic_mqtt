namespace MeshBoard.Client.Vault;

public sealed class SaveLocalVaultKeyRecordRequest
{
    public string? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? BrokerServerProfileId { get; set; }

    public string TopicPattern { get; set; } = string.Empty;

    public string KeyValue { get; set; } = string.Empty;
}
