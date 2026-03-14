using Microsoft.JSInterop;

namespace MeshBoard.Client.Vault;

public sealed class BrowserVaultStore : IVaultRuntimeKeyRecordProvider, IAsyncDisposable
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

    public async Task<IReadOnlyList<LocalVaultKeyRecordSummary>> GetKeyRecordsAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<List<LocalVaultKeyRecordSummary>>("getKeyRecords", cancellationToken);
    }

    public async Task<IReadOnlyList<LocalVaultRuntimeKeyRecord>> GetRuntimeKeyRecordsAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<List<LocalVaultRuntimeKeyRecord>>("getRuntimeKeyRecords", cancellationToken);
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

    public async Task<VaultStatusSnapshot> RemoveKeyRecordAsync(string id, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<VaultStatusSnapshot>("removeKeyRecord", cancellationToken, id);
    }

    public async Task<VaultStatusSnapshot> SaveKeyRecordAsync(
        BrowserVaultKeyRecordMutation request,
        CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<VaultStatusSnapshot>("saveKeyRecord", cancellationToken, request);
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

    public sealed class BrowserVaultKeyRecordMutation
    {
        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? BrokerServerProfileId { get; set; }

        public string TopicPattern { get; set; } = string.Empty;

        public string? NormalizedKeyBase64 { get; set; }

        public int? KeyLengthBytes { get; set; }
    }
}
