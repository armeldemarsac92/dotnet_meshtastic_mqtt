using MeshBoard.Client.Maps;
using MeshBoard.Client.Nodes;
using MeshBoard.Client.Realtime;

namespace MeshBoard.UnitTests;

public sealed class MapProjectionStoreTests
{
    [Fact]
    public void Project_ShouldCreateLocatedNodeFromRealtimePacket()
    {
        var store = new MapProjectionStore(new MapProjectionState());

        store.Project(CreatePacketResult(latitude: 48.8566, longitude: 2.3522));

        var node = Assert.Single(store.Current.Nodes);
        var pulse = Assert.Single(store.Current.ActivityPulses);
        Assert.Equal("!0000162e", node.NodeId);
        Assert.Equal("Atlas", node.DisplayName);
        Assert.Equal("US/LongFast", node.Channel);
        Assert.Equal(48.8566, node.Latitude, 4);
        Assert.Equal(2.3522, node.Longitude, 4);
        Assert.Equal(1, pulse.PulseCount);
    }

    [Fact]
    public void Project_WhenFollowUpPacketHasNoLocation_ShouldRetainExistingCoordinates()
    {
        var store = new MapProjectionStore(new MapProjectionState());

        store.Project(CreatePacketResult(latitude: 48.8566, longitude: 2.3522));
        store.Project(CreatePacketResult(
            packetType: "Telemetry",
            latitude: null,
            longitude: null,
            batteryLevelPercent: 93,
            receivedAtUtc: DateTimeOffset.Parse("2026-03-14T17:02:00Z")));

        var node = Assert.Single(store.Current.Nodes);
        Assert.Equal(48.8566, node.Latitude, 4);
        Assert.Equal(2.3522, node.Longitude, 4);
        Assert.Equal(93, node.BatteryLevelPercent);
        Assert.Equal(2, node.ObservedPacketCount);
        Assert.Equal("Telemetry", node.LastPacketType);
    }

    [Fact]
    public void ReconcileFromNodeProjections_ShouldGeneratePulseForObservedPacketDelta()
    {
        var store = new MapProjectionStore(new MapProjectionState());

        store.SeedFromNodeProjections(
            [
                CreateNodeProjectionEnvelope(observedPacketCount: 2)
            ]);
        store.ReconcileFromNodeProjections(
            [
                CreateNodeProjectionEnvelope(observedPacketCount: 5, batteryLevelPercent: 88)
            ]);

        var node = Assert.Single(store.Current.Nodes);
        var pulse = Assert.Single(store.Current.ActivityPulses);
        Assert.Equal(88, node.BatteryLevelPercent);
        Assert.Equal(5, node.ObservedPacketCount);
        Assert.Equal(3, pulse.PulseCount);
    }

    [Fact]
    public void ReconcileFromNodeProjections_ShouldBoundRetainedNodes()
    {
        var store = new MapProjectionStore(new MapProjectionState());

        store.SeedFromNodeProjections(
            Enumerable.Range(0, 1_050)
                .Select(index => CreateNodeProjectionEnvelope(
                    nodeId: $"!{index:x8}",
                    observedPacketCount: index + 1,
                    latitude: 40 + (index * 0.001),
                    longitude: -70 - (index * 0.001))));

        Assert.Equal(1_000, store.Current.Nodes.Count);
        Assert.DoesNotContain(store.Current.Nodes, node => node.NodeId == "!00000000");
    }

    private static RealtimePacketWorkerResult CreatePacketResult(
        string nodeId = "!0000162e",
        uint nodeNumber = 5678,
        string packetType = "Position Update",
        DateTimeOffset? receivedAtUtc = null,
        double? latitude = 48.8566,
        double? longitude = 2.3522,
        int? batteryLevelPercent = null)
    {
        var timestamp = receivedAtUtc ?? DateTimeOffset.Parse("2026-03-14T17:00:00Z");

        return new RealtimePacketWorkerResult
        {
            IsSuccess = true,
            DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.Decrypted,
            RawPacket = new RealtimeRawPacketEvent
            {
                WorkspaceId = "workspace-a",
                BrokerServer = "broker.meshboard.test",
                SourceTopic = "msh/US/2/e/LongFast/!0000162e",
                DownstreamTopic = "meshboard/workspaces/workspace-a/live/packets",
                PayloadBase64 = "AQID",
                PayloadSizeBytes = 3,
                ReceivedAtUtc = timestamp,
                IsEncrypted = true,
                PacketId = 42,
                FromNodeNumber = nodeNumber
            },
            DecodedPacket = new RealtimeDecodedPacketEvent
            {
                PortNumValue = 3,
                PortNumName = "POSITION_APP",
                PacketType = packetType,
                PayloadBase64 = "AQID",
                PayloadSizeBytes = 3,
                PayloadPreview = "Position update",
                SourceNodeNumber = nodeNumber,
                NodeProjection = new RealtimeNodeProjectionEvent
                {
                    NodeId = nodeId,
                    NodeNumber = nodeNumber,
                    LastHeardAtUtc = timestamp,
                    LastHeardChannel = "US/LongFast",
                    ShortName = "ATLS",
                    LongName = "Atlas",
                    PacketType = packetType,
                    PayloadPreview = "Position update",
                    LastKnownLatitude = latitude,
                    LastKnownLongitude = longitude,
                    BatteryLevelPercent = batteryLevelPercent
                }
            }
        };
    }

    private static NodeProjectionEnvelope CreateNodeProjectionEnvelope(
        string nodeId = "!0000162e",
        int observedPacketCount = 1,
        double latitude = 48.8566,
        double longitude = 2.3522,
        int? batteryLevelPercent = null)
    {
        return new NodeProjectionEnvelope
        {
            NodeId = nodeId,
            BrokerServer = "broker.meshboard.test",
            ShortName = "ATLS",
            LongName = "Atlas",
            LastHeardAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z").AddMinutes(observedPacketCount),
            LastHeardChannel = "US/LongFast",
            LastPacketType = "Position Update",
            LastKnownLatitude = latitude,
            LastKnownLongitude = longitude,
            BatteryLevelPercent = batteryLevelPercent,
            ObservedPacketCount = observedPacketCount
        };
    }
}
