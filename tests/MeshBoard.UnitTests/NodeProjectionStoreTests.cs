using MeshBoard.Client.Nodes;
using MeshBoard.Client.Realtime;
using MeshBoard.Contracts.Nodes;

namespace MeshBoard.UnitTests;

public sealed class NodeProjectionStoreTests
{
    [Fact]
    public void Project_ShouldCreateNodeFromTextMessageProjection()
    {
        var store = new NodeProjectionStore(new NodeProjectionState());

        store.Project(
            CreatePacketResult(
                packetType: "Text Message",
                payloadPreview: "hello mesh",
                lastTextMessageAtUtc: DateTimeOffset.Parse("2026-03-14T17:00:00Z")));

        var node = Assert.Single(store.Current.Nodes);
        Assert.Equal("!0000162e", node.NodeId);
        Assert.Equal("US/LongFast", node.LastHeardChannel);
        Assert.Equal("Text Message", node.LastPacketType);
        Assert.Equal("hello mesh", node.LastPayloadPreview);
        Assert.Equal(DateTimeOffset.Parse("2026-03-14T17:00:00Z"), node.LastTextMessageAtUtc);
        Assert.Equal(1, node.ObservedPacketCount);
    }

    [Fact]
    public void Project_ShouldMergeNamesAndTelemetryAcrossPackets()
    {
        var store = new NodeProjectionStore(new NodeProjectionState());

        store.Project(
            CreatePacketResult(
                packetType: "Node Info",
                payloadPreview: "Node info: Atlas (ATLS)",
                shortName: "ATLS",
                longName: "Atlas"));
        store.Project(
            CreatePacketResult(
                packetType: "Telemetry",
                payloadPreview: "Device metrics: 98% battery",
                batteryLevelPercent: 98,
                voltage: 4.12,
                receivedAtUtc: DateTimeOffset.Parse("2026-03-14T17:02:00Z")));

        var node = Assert.Single(store.Current.Nodes);
        Assert.Equal("ATLS", node.ShortName);
        Assert.Equal("Atlas", node.LongName);
        Assert.Equal(98, node.BatteryLevelPercent);
        Assert.Equal(4.12, node.Voltage);
        Assert.True(node.HasTelemetry);
        Assert.Equal(2, node.ObservedPacketCount);
        Assert.Equal(DateTimeOffset.Parse("2026-03-14T17:02:00Z"), node.LastHeardAtUtc);
    }

    [Fact]
    public void Project_ShouldMergePositionAndSupportQueryFilters()
    {
        var store = new NodeProjectionStore(new NodeProjectionState());

        store.Project(
            CreatePacketResult(
                packetType: "Position Update",
                payloadPreview: "Position: 48.85660, 2.35220",
                latitude: 48.8566,
                longitude: 2.3522));
        store.Project(
            CreatePacketResult(
                nodeId: "!00001000",
                nodeNumber: 4096,
                packetType: "Telemetry",
                payloadPreview: "Environment metrics: 21.5C",
                temperatureCelsius: 21.5,
                sourceTopic: "msh/EU/2/e/FieldOps/!00001000"));

        var locationOnly = NodeProjectionStore.ApplyQuery(
            store.Current,
            new NodeQuery
            {
                OnlyWithLocation = true
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var nameSorted = NodeProjectionStore.ApplyQuery(
            store.Current,
            new NodeQuery
            {
                SortBy = NodeSortOption.NameAsc
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Single(locationOnly);
        Assert.Equal("!0000162e", locationOnly[0].NodeId);
        Assert.Equal(2, nameSorted.Count);
        Assert.Equal("!00001000", nameSorted[0].NodeId);
    }

    private static RealtimePacketWorkerResult CreatePacketResult(
        string nodeId = "!0000162e",
        uint nodeNumber = 5678,
        string sourceTopic = "msh/US/2/e/LongFast/!0000162e",
        string packetType = "Text Message",
        string payloadPreview = "hello mesh",
        DateTimeOffset? receivedAtUtc = null,
        DateTimeOffset? lastTextMessageAtUtc = null,
        string? shortName = null,
        string? longName = null,
        double? latitude = null,
        double? longitude = null,
        int? batteryLevelPercent = null,
        double? voltage = null,
        double? temperatureCelsius = null)
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
                SourceTopic = sourceTopic,
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
                PortNumValue = 1,
                PortNumName = "TEXT_MESSAGE_APP",
                PacketType = packetType,
                PayloadBase64 = "AQID",
                PayloadSizeBytes = 3,
                PayloadPreview = payloadPreview,
                SourceNodeNumber = nodeNumber,
                NodeProjection = new RealtimeNodeProjectionEvent
                {
                    NodeId = nodeId,
                    NodeNumber = nodeNumber,
                    LastHeardAtUtc = timestamp,
                    LastHeardChannel = sourceTopic.Contains("/FieldOps/", StringComparison.Ordinal)
                        ? "EU/FieldOps"
                        : "US/LongFast",
                    LastTextMessageAtUtc = lastTextMessageAtUtc,
                    ShortName = shortName,
                    LongName = longName,
                    PacketType = packetType,
                    PayloadPreview = payloadPreview,
                    LastKnownLatitude = latitude,
                    LastKnownLongitude = longitude,
                    BatteryLevelPercent = batteryLevelPercent,
                    Voltage = voltage,
                    TemperatureCelsius = temperatureCelsius
                }
            }
        };
    }
}
