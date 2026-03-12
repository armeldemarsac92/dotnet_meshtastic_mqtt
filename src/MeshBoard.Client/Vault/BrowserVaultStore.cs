using Microsoft.JSInterop;

namespace MeshBoard.Client.Vault;

public sealed class BrowserVaultStore : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public BrowserVaultStore(IJSRuntime jsRuntime)
    {
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/localVault.js").AsTask());
    }

    public async Task<VaultStatusSnapshot> CreateVaultAsync(string passphrase, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<VaultStatusSnapshot>("createVault", cancellationToken, passphrase);
    }

    public async Task<VaultStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<VaultStatusSnapshot>("getStatus", cancellationToken);
    }

    public async Task<VaultStatusSnapshot> LockAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<VaultStatusSnapshot>("lockVault", cancellationToken);
    }

    public async Task<VaultStatusSnapshot> RequestPersistentStorageAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<VaultStatusSnapshot>("requestPersistentStorage", cancellationToken);
    }

    public async Task<VaultStatusSnapshot> UnlockAsync(string passphrase, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<VaultStatusSnapshot>("unlockVault", cancellationToken, passphrase);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_moduleTask.IsValueCreated)
        {
            return;
        }

        var module = await _moduleTask.Value;
        await module.DisposeAsync();
    }

    private Task<IJSObjectReference> GetModuleAsync()
    {
        return _moduleTask.Value;
    }
}
