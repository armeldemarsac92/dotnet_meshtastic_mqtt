using MeshBoard.Contracts.Topics;

namespace MeshBoard.Client.Vault;

public sealed class LocalVaultService
{
    private readonly BrowserVaultStore _browserVaultStore;
    private readonly VaultSessionState _vaultSessionState;

    public LocalVaultService(BrowserVaultStore browserVaultStore, VaultSessionState vaultSessionState)
    {
        _browserVaultStore = browserVaultStore;
        _vaultSessionState = vaultSessionState;
    }

    public VaultStatusSnapshot Current => _vaultSessionState.Snapshot;

    public Task<IReadOnlyList<LocalVaultKeyRecordSummary>> GetKeyRecordsAsync(CancellationToken cancellationToken = default)
    {
        return _browserVaultStore.GetKeyRecordsAsync(cancellationToken);
    }

    public async Task CreateVaultAsync(string passphrase, CancellationToken cancellationToken = default)
    {
        ValidatePassphrase(passphrase);
        await RefreshAsync(() => _browserVaultStore.CreateVaultAsync(passphrase, cancellationToken));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(() => _browserVaultStore.GetStatusAsync(cancellationToken));
    }

    public async Task LockAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(() => _browserVaultStore.LockAsync(cancellationToken));
    }

    public async Task RequestPersistentStorageAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(() => _browserVaultStore.RequestPersistentStorageAsync(cancellationToken));
    }

    public async Task RemoveKeyRecordAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("The vault key identifier is missing.");
        }

        await RefreshAsync(() => _browserVaultStore.RemoveKeyRecordAsync(id, cancellationToken));
    }

    public async Task SaveKeyRecordAsync(
        SaveLocalVaultKeyRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hasReplacementKey = !string.IsNullOrWhiteSpace(request.KeyValue);
        var normalizedKeyBase64 = hasReplacementKey
            ? TopicEncryptionKey.NormalizeToBase64OrNull(request.KeyValue)
            : null;

        if (hasReplacementKey && normalizedKeyBase64 is null)
        {
            throw new InvalidOperationException("Enter a valid Meshtastic key in base64 or hex format.");
        }

        var mutation = new BrowserVaultStore.BrowserVaultKeyRecordMutation
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? null : request.Id.Trim(),
            Name = request.Name.Trim(),
            BrokerServerProfileId = request.BrokerServerProfileId?.ToString(),
            TopicPattern = request.TopicPattern.Trim(),
            NormalizedKeyBase64 = normalizedKeyBase64,
            KeyLengthBytes = normalizedKeyBase64 is null
                ? null
                : Convert.FromBase64String(normalizedKeyBase64).Length
        };

        ValidateKeyRecord(mutation);
        await RefreshAsync(() => _browserVaultStore.SaveKeyRecordAsync(mutation, cancellationToken));
    }

    public async Task UnlockAsync(string passphrase, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new InvalidOperationException("Enter the vault passphrase.");
        }

        await RefreshAsync(() => _browserVaultStore.UnlockAsync(passphrase, cancellationToken));
    }

    private async Task RefreshAsync(Func<Task<VaultStatusSnapshot>> action)
    {
        var snapshot = await action();
        snapshot.IsReady = true;
        _vaultSessionState.SetSnapshot(snapshot);
    }

    private static void ValidatePassphrase(string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new InvalidOperationException("Enter a dedicated vault passphrase.");
        }

        if (passphrase.Trim().Length < 12)
        {
            throw new InvalidOperationException("Vault passphrases must be at least 12 characters.");
        }
    }

    private static void ValidateKeyRecord(BrowserVaultStore.BrowserVaultKeyRecordMutation request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Enter a local label for the key record.");
        }

        if (string.IsNullOrWhiteSpace(request.TopicPattern))
        {
            throw new InvalidOperationException("Enter the topic pattern this key applies to.");
        }

        if (string.IsNullOrWhiteSpace(request.Id) && string.IsNullOrWhiteSpace(request.NormalizedKeyBase64))
        {
            throw new InvalidOperationException("Enter a Meshtastic key to create a new local record.");
        }
    }
}
