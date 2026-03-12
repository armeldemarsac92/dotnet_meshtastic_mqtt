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
}
