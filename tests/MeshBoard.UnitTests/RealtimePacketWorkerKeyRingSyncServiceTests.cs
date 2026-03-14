using MeshBoard.Client.Realtime;
using MeshBoard.Client.Vault;

namespace MeshBoard.UnitTests;

public sealed class RealtimePacketWorkerKeyRingSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_WhenVaultIsLocked_ShouldClearWorkerKeyRing()
    {
        var provider = new FakeVaultRuntimeKeyRecordProvider();
        var workerKeyRingClient = new FakeRealtimePacketWorkerKeyRingClient();
        var service = new RealtimePacketWorkerKeyRingSyncService(provider, workerKeyRingClient);

        await service.SyncAsync(isUnlocked: false);

        Assert.True(workerKeyRingClient.ClearCalled);
        Assert.Empty(workerKeyRingClient.ReplacedKeyRecords);
    }

    [Fact]
    public async Task SyncAsync_WhenVaultIsUnlocked_ShouldReplaceWorkerKeyRing()
    {
        var provider = new FakeVaultRuntimeKeyRecordProvider
        {
            RuntimeKeyRecords =
            [
                new LocalVaultRuntimeKeyRecord
                {
                    Id = "key-1",
                    Name = "Primary",
                    TopicPattern = "msh/US/2/e/LongFast/#",
                    BrokerServerProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    NormalizedKeyBase64 = "AQIDBA==",
                    KeyLengthBytes = 4
                }
            ]
        };
        var workerKeyRingClient = new FakeRealtimePacketWorkerKeyRingClient();
        var service = new RealtimePacketWorkerKeyRingSyncService(provider, workerKeyRingClient);

        await service.SyncAsync(isUnlocked: true);

        Assert.False(workerKeyRingClient.ClearCalled);
        var keyRecord = Assert.Single(workerKeyRingClient.ReplacedKeyRecords);
        Assert.Equal("key-1", keyRecord.Id);
        Assert.Equal("Primary", keyRecord.Name);
        Assert.Equal("msh/US/2/e/LongFast/#", keyRecord.TopicPattern);
        Assert.Equal("11111111-1111-1111-1111-111111111111", keyRecord.BrokerServerProfileId);
        Assert.Equal("AQIDBA==", keyRecord.NormalizedKeyBase64);
        Assert.Equal(4, keyRecord.KeyLengthBytes);
    }

    private sealed class FakeVaultRuntimeKeyRecordProvider : IVaultRuntimeKeyRecordProvider
    {
        public IReadOnlyList<LocalVaultRuntimeKeyRecord> RuntimeKeyRecords { get; init; } = [];

        public Task<IReadOnlyList<LocalVaultRuntimeKeyRecord>> GetRuntimeKeyRecordsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RuntimeKeyRecords);
        }
    }

    private sealed class FakeRealtimePacketWorkerKeyRingClient : IRealtimePacketWorkerKeyRingClient
    {
        public bool ClearCalled { get; private set; }

        public List<RealtimePacketWorkerKeyRecord> ReplacedKeyRecords { get; } = [];

        public Task ClearKeyRecordsAsync(CancellationToken cancellationToken = default)
        {
            ClearCalled = true;
            ReplacedKeyRecords.Clear();
            return Task.CompletedTask;
        }

        public Task ReplaceKeyRecordsAsync(
            IReadOnlyList<RealtimePacketWorkerKeyRecord> keyRecords,
            CancellationToken cancellationToken = default)
        {
            ReplacedKeyRecords.Clear();
            ReplacedKeyRecords.AddRange(keyRecords);
            return Task.CompletedTask;
        }
    }
}
