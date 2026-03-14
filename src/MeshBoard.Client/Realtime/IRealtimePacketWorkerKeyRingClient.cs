namespace MeshBoard.Client.Realtime;

public interface IRealtimePacketWorkerKeyRingClient
{
    Task ClearKeyRecordsAsync(CancellationToken cancellationToken = default);

    Task ReplaceKeyRecordsAsync(
        IReadOnlyList<RealtimePacketWorkerKeyRecord> keyRecords,
        CancellationToken cancellationToken = default);
}
