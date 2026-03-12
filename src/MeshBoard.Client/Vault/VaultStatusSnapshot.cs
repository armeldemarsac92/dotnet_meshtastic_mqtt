namespace MeshBoard.Client.Vault;

public sealed class VaultStatusSnapshot
{
    public bool IsReady { get; set; }

    public bool HasVault { get; set; }

    public bool IsLocked { get; set; } = true;

    public bool IsUnlocked { get; set; }

    public bool NeedsPassphraseSetup { get; set; } = true;

    public bool PersistentStorageSupported { get; set; }

    public bool PersistentStorageGranted { get; set; }

    public int StoredKeyCount { get; set; }

    public string? KdfName { get; set; }
}
