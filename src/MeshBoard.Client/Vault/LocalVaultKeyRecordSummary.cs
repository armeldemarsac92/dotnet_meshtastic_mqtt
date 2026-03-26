namespace MeshBoard.Client.Vault;

public sealed class LocalVaultKeyRecordSummary
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string TopicPattern { get; set; } = string.Empty;

    public Guid? BrokerServerProfileId { get; set; }

    public int KeyLengthBytes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
