namespace MeshBoard.Contracts.Topics;

public sealed class TopicPreset
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string TopicPattern { get; set; } = string.Empty;

    public string? EncryptionKeyBase64 { get; set; }

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
