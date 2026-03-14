using Microsoft.JSInterop;

namespace MeshBoard.Client.Realtime;

public sealed class RealtimePacketWorkerClient : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public RealtimePacketWorkerClient(IJSRuntime jsRuntime)
    {
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/realtimePacketWorkerClient.js").AsTask());
    }

    public async Task<RealtimePacketWorkerResult> ProcessAsync(
        RealtimePacketWorkerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var module = await GetModuleAsync();
        return await module.InvokeAsync<RealtimePacketWorkerResult>("processPacket", cancellationToken, request);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_moduleTask.IsValueCreated)
        {
            return;
        }

        var module = await _moduleTask.Value;

        try
        {
            await module.InvokeVoidAsync("dispose");
        }
        catch (JSDisconnectedException)
        {
        }

        await module.DisposeAsync();
    }

    private Task<IJSObjectReference> GetModuleAsync()
    {
        return _moduleTask.Value;
    }
}
