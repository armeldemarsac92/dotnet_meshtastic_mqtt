namespace MeshBoard.Client.Vault;

public sealed class LocalVaultRuntimeKeyRecord
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string TopicPattern { get; set; } = string.Empty;

    public Guid? BrokerServerProfileId { get; set; }

    public string NormalizedKeyBase64 { get; set; } = string.Empty;

    public int KeyLengthBytes { get; set; }
}
