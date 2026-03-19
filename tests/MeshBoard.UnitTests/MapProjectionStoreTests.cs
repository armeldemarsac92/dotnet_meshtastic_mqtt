using MeshBoard.Client.Maps;
using MeshBoard.Client.Realtime;
using System.Text;

namespace MeshBoard.UnitTests;

public sealed class MapProjectionStoreTests
{
    [Fact]
    public void Project_ShouldCreateLocatedNodeAndPulse()
    {
        var store = new MapProjectionStore(new MapProjectionState());

        store.Project(CreatePacketResult(latitude: 48.8566, longitude: 2.3522, channel: "US/LongFast"));

        var node = Assert.Single(store.Current.Nodes);
        Assert.Equal("!0000162e", node.NodeId);
        Assert.Equal(48.8566, node.Latitude, 4);
        Assert.Equal(2.3522, node.Longitude, 4);
        Assert.Equal("US/LongFast", node.Channel);
        Assert.Equal("Position Update", node.LastPacketType);
        Assert.Single(store.DrainActivityPulses());
    }

    [Fact]
    public void Project_ShouldIgnoreNodeWithoutCoordinatesUntilLocationExists()
    {
        var store = new MapProjectionStore(new MapProjectionState());

        store.Project(CreatePacketResult(latitude: null, longitude: null));

        Assert.Empty(store.Current.Nodes);
        Assert.Empty(store.DrainActivityPulses());
    }

    [Fact]
    public void Project_ShouldMergeTelemetryAfterLocationExists()
    {
        var store = new MapProjectionStore(new MapProjectionState());

        store.Project(CreatePacketResult(latitude: 48.8566, longitude: 2.3522, packetType: "Position Update"));
        store.Project(CreatePacketResult(latitude: null, longitude: null, packetType: "Telemetry", batteryLevelPercent: 92));

        var node = Assert.Single(store.Current.Nodes);
        Assert.Equal(92, node.BatteryLevelPercent);
        Assert.Equal(2, node.ObservedPacketCount);
        Assert.Equal("Telemetry", node.LastPacketType);
        Assert.Equal(2, store.DrainActivityPulses().Single().PulseCount);
    }

    [Fact]
    public void ApplyQuery_ShouldFilterBySearchAndFocusedChannel()
    {
        var store = new MapProjectionStore(new MapProjectionState());

        store.Project(CreatePacketResult(latitude: 48.8566, longitude: 2.3522, channel: "US/LongFast", shortName: "ATLS"));
        store.Project(CreatePacketResult(nodeId: "!00001000", nodeNumber: 4096, latitude: 43.2965, longitude: 5.3698, channel: "EU/FieldOps", shortName: "FIELD", sourceTopic: "msh/EU/2/e/FieldOps/!00001000"));

        var searchFiltered = MapProjectionStore.ApplyQuery(store.Current, "field", null);
        var channelFiltered = MapProjectionStore.ApplyQuery(store.Current, string.Empty, "US/LongFast");

        Assert.Single(searchFiltered);
        Assert.Equal("!00001000", searchFiltered[0].NodeId);
        Assert.Single(channelFiltered);
        Assert.Equal("!0000162e", channelFiltered[0].NodeId);
    }

    [Fact]
    public void Project_WhenPacketIsDuplicated_ShouldIgnoreRepeatObservationAndPulse()
    {
        var store = new MapProjectionStore(new MapProjectionState());
        var packet = CreatePacketResult(packetId: 55, latitude: 48.8566, longitude: 2.3522);

        store.Project(packet);
        store.Project(packet);

        var node = Assert.Single(store.Current.Nodes);
        Assert.Equal(1, node.ObservedPacketCount);
        Assert.Equal(1, store.Current.TotalProjected);

        var pulse = Assert.Single(store.DrainActivityPulses());
        Assert.Equal(1, pulse.PulseCount);
    }

    [Fact]
    public void Project_WhenOlderPacketArrives_ShouldKeepNewestLocationAndBackfillMissingTelemetry()
    {
        var store = new MapProjectionStore(new MapProjectionState());

        store.Project(
            CreatePacketResult(
                packetId: 71,
                receivedAtUtc: DateTimeOffset.Parse("2026-03-14T17:05:00Z"),
                latitude: 48.8566,
                longitude: 2.3522,
                packetType: "Position Update"));
        store.Project(
            CreatePacketResult(
                packetId: 72,
                receivedAtUtc: DateTimeOffset.Parse("2026-03-14T17:00:00Z"),
                latitude: 43.2965,
                longitude: 5.3698,
                packetType: "Telemetry",
                batteryLevelPercent: 92));

        var node = Assert.Single(store.Current.Nodes);
        Assert.Equal(48.8566, node.Latitude, 4);
        Assert.Equal(2.3522, node.Longitude, 4);
        Assert.Equal(DateTimeOffset.Parse("2026-03-14T17:05:00Z"), node.LastHeardAtUtc);
        Assert.Equal("Position Update", node.LastPacketType);
        Assert.Equal(92, node.BatteryLevelPercent);
        Assert.Equal(2, node.ObservedPacketCount);
    }

    private static RealtimePacketWorkerResult CreatePacketResult(
        string nodeId = "!0000162e",
        uint nodeNumber = 5678,
        string sourceTopic = "msh/US/2/e/LongFast/!0000162e",
        string channel = "US/LongFast",
        string packetType = "Position Update",
        DateTimeOffset? receivedAtUtc = null,
        uint? packetId = null,
        string? shortName = null,
        double? latitude = 48.8566,
        double? longitude = 2.3522,
        int? batteryLevelPercent = null)
    {
        var timestamp = receivedAtUtc ?? DateTimeOffset.Parse("2026-03-14T17:00:00Z");
        var payloadPreview = packetType == "Telemetry"
            ? "Telemetry update"
            : "Position update";
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{packetType}:{payloadPreview}"));

        return new RealtimePacketWorkerResult
        {
            IsSuccess = true,
            DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.Decrypted,
            RawPacket = new RealtimeRawPacketEvent
            {
                WorkspaceId = "workspace-a",
                BrokerServer = "broker.meshboard.test",
                SourceTopic = sourceTopic,
                DownstreamTopic = "meshboard/workspaces/workspace-a/live/packets",
                PayloadBase64 = payloadBase64,
                PayloadSizeBytes = payloadBase64.Length,
                ReceivedAtUtc = timestamp,
                IsEncrypted = true,
                PacketId = packetId,
                FromNodeNumber = nodeNumber
            },
            DecodedPacket = new RealtimeDecodedPacketEvent
            {
                PortNumValue = 3,
                PortNumName = "POSITION_APP",
                PacketType = packetType,
                PayloadBase64 = payloadBase64,
                PayloadSizeBytes = payloadBase64.Length,
                PayloadPreview = payloadPreview,
                SourceNodeNumber = nodeNumber,
                NodeProjection = new RealtimeNodeProjectionEvent
                {
                    NodeId = nodeId,
                    NodeNumber = nodeNumber,
                    LastHeardAtUtc = timestamp,
                    LastHeardChannel = channel,
                    ShortName = shortName,
                    LongName = "Atlas",
                    PacketType = packetType,
                    PayloadPreview = payloadPreview,
                    LastKnownLatitude = latitude,
                    LastKnownLongitude = longitude,
                    BatteryLevelPercent = batteryLevelPercent
                }
            }
        };
    }
}
