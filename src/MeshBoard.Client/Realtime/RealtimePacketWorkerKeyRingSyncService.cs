using MeshBoard.Client.Vault;

namespace MeshBoard.Client.Realtime;

public sealed class RealtimePacketWorkerKeyRingSyncService
{
    private readonly IVaultRuntimeKeyRecordProvider _runtimeKeyRecordProvider;
    private readonly IRealtimePacketWorkerKeyRingClient _workerKeyRingClient;

    public RealtimePacketWorkerKeyRingSyncService(
        IVaultRuntimeKeyRecordProvider runtimeKeyRecordProvider,
        IRealtimePacketWorkerKeyRingClient workerKeyRingClient)
    {
        _runtimeKeyRecordProvider = runtimeKeyRecordProvider;
        _workerKeyRingClient = workerKeyRingClient;
    }

    public async Task SyncAsync(bool isUnlocked, CancellationToken cancellationToken = default)
    {
        if (!isUnlocked)
        {
            await _workerKeyRingClient.ClearKeyRecordsAsync(cancellationToken);
            return;
        }

        var runtimeKeyRecords = await _runtimeKeyRecordProvider.GetRuntimeKeyRecordsAsync(cancellationToken);
        var workerKeyRecords = runtimeKeyRecords
            .Select(
                record => new RealtimePacketWorkerKeyRecord
                {
                    Id = record.Id,
                    Name = record.Name,
                    TopicPattern = record.TopicPattern,
                    BrokerServerProfileId = record.BrokerServerProfileId?.ToString(),
                    NormalizedKeyBase64 = record.NormalizedKeyBase64,
                    KeyLengthBytes = record.KeyLengthBytes
                })
            .ToArray();

        await _workerKeyRingClient.ReplaceKeyRecordsAsync(workerKeyRecords, cancellationToken);
    }
}
