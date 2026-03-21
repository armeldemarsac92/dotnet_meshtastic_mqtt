using MeshBoard.Client.Maps;
using MeshBoard.Client.Realtime;
using System.Text;

namespace MeshBoard.UnitTests;

public sealed class RadioLinkProjectionStoreTests
{
    [Fact]
    public void Project_ShouldCreateCanonicalRadioLink()
    {
        var store = new RadioLinkProjectionStore();

        store.Project(
            CreatePacketResult(
                reportingNodeId: "!00005678",
                reportingNodeNumber: 0x00005678,
                packetId: 71,
                neighbors:
                [
                    new RealtimeNeighborEntry
                    {
                        NodeId = "!00001234",
                        SnrDb = 4.5f,
                        LastRxAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z")
                    }
                ]));

        var link = Assert.Single(store.Current);
        Assert.Equal("!00001234", link.SourceNodeId);
        Assert.Equal("!00005678", link.TargetNodeId);
        Assert.Equal(4.5f, link.SnrDb);
        Assert.Equal(DateTimeOffset.Parse("2026-03-14T17:00:00Z"), link.LastSeenAtUtc);
    }

    [Fact]
    public void Project_WhenReverseObservationArrives_ShouldMergeNewestMetadata()
    {
        var store = new RadioLinkProjectionStore();

        store.Project(
            CreatePacketResult(
                reportingNodeId: "!00001234",
                reportingNodeNumber: 0x00001234,
                packetId: 81,
                neighbors:
                [
                    new RealtimeNeighborEntry
                    {
                        NodeId = "!00005678",
                        LastRxAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z")
                    }
                ]));
        store.Project(
            CreatePacketResult(
                reportingNodeId: "!00005678",
                reportingNodeNumber: 0x00005678,
                packetId: 82,
                receivedAtUtc: DateTimeOffset.Parse("2026-03-14T17:05:00Z"),
                neighbors:
                [
                    new RealtimeNeighborEntry
                    {
                        NodeId = "!00001234",
                        SnrDb = -2.5f,
                        LastRxAtUtc = DateTimeOffset.Parse("2026-03-14T17:05:00Z")
                    }
                ]));

        var link = Assert.Single(store.Current);
        Assert.Equal("!00001234", link.SourceNodeId);
        Assert.Equal("!00005678", link.TargetNodeId);
        Assert.Equal(-2.5f, link.SnrDb);
        Assert.Equal(DateTimeOffset.Parse("2026-03-14T17:05:00Z"), link.LastSeenAtUtc);
    }

    [Fact]
    public void Project_WhenPacketIsDuplicated_ShouldIgnoreRepeatObservation()
    {
        var store = new RadioLinkProjectionStore();
        var packet = CreatePacketResult(
            reportingNodeId: "!00001234",
            reportingNodeNumber: 0x00001234,
            packetId: 91,
            neighbors:
            [
                new RealtimeNeighborEntry
                {
                    NodeId = "!00005678",
                    SnrDb = 1.25f,
                    LastRxAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z")
                }
            ]);

        store.Project(packet);
        store.Project(packet);

        var link = Assert.Single(store.Current);
        Assert.Equal(1.25f, link.SnrDb);
    }

    [Fact]
    public void Project_WhenNewPacketExceedsStalenessWindow_ShouldEvictOldLinks()
    {
        var store = new RadioLinkProjectionStore();

        store.Project(
            CreatePacketResult(
                reportingNodeId: "!00001234",
                reportingNodeNumber: 0x00001234,
                packetId: 101,
                receivedAtUtc: DateTimeOffset.Parse("2026-03-14T17:00:00Z"),
                neighbors:
                [
                    new RealtimeNeighborEntry
                    {
                        NodeId = "!00005678",
                        SnrDb = 3.5f,
                        LastRxAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z")
                    }
                ]));
        store.Project(
            CreatePacketResult(
                reportingNodeId: "!00009abc",
                reportingNodeNumber: 0x00009abc,
                packetId: 102,
                receivedAtUtc: DateTimeOffset.Parse("2026-03-14T19:30:01Z"),
                neighbors:
                [
                    new RealtimeNeighborEntry
                    {
                        NodeId = "!0000def0",
                        SnrDb = 6.25f,
                        LastRxAtUtc = DateTimeOffset.Parse("2026-03-14T19:30:01Z")
                    }
                ]));

        var link = Assert.Single(store.Current);
        Assert.Equal("!00009abc", link.SourceNodeId);
        Assert.Equal("!0000def0", link.TargetNodeId);
    }

    [Fact]
    public void Project_ShouldTrackObservedReportAndNeighborCounts()
    {
        var store = new RadioLinkProjectionStore();
        var observedAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z");

        store.Project(
            CreatePacketResult(
                reportingNodeId: "!00001234",
                reportingNodeNumber: 0x00001234,
                packetId: 111,
                receivedAtUtc: observedAtUtc,
                neighbors:
                [
                    new RealtimeNeighborEntry
                    {
                        NodeId = "!00005678",
                        SnrDb = 3.25f
                    },
                    new RealtimeNeighborEntry
                    {
                        NodeId = "!00009abc",
                        SnrDb = -4.75f
                    }
                ]));

        Assert.Equal(1, store.ObservedReportCount);
        Assert.Equal(2, store.ObservedNeighborCount);
        Assert.Equal(observedAtUtc, store.LastObservedAtUtc);

        store.Clear();

        Assert.Equal(0, store.ObservedReportCount);
        Assert.Equal(0, store.ObservedNeighborCount);
        Assert.Null(store.LastObservedAtUtc);
    }

    private static RealtimePacketWorkerResult CreatePacketResult(
        string reportingNodeId,
        uint reportingNodeNumber,
        IReadOnlyList<RealtimeNeighborEntry> neighbors,
        uint packetId,
        DateTimeOffset? receivedAtUtc = null)
    {
        var timestamp = receivedAtUtc ?? DateTimeOffset.Parse("2026-03-14T17:00:00Z");
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"neighbor-info:{reportingNodeId}:{packetId}"));

        return new RealtimePacketWorkerResult
        {
            IsSuccess = true,
            DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.Decrypted,
            RawPacket = new RealtimeRawPacketEvent
            {
                WorkspaceId = "workspace-a",
                BrokerServer = "broker.meshboard.test",
                SourceTopic = $"msh/US/2/e/LongFast/{reportingNodeId}",
                DownstreamTopic = "meshboard/workspaces/workspace-a/live/packets",
                PayloadBase64 = payloadBase64,
                PayloadSizeBytes = payloadBase64.Length,
                ReceivedAtUtc = timestamp,
                IsEncrypted = true,
                PacketId = packetId,
                FromNodeNumber = reportingNodeNumber
            },
            DecodedPacket = new RealtimeDecodedPacketEvent
            {
                PortNumValue = 71,
                PortNumName = "NEIGHBORINFO_APP",
                PacketType = "Neighbor Info",
                PayloadBase64 = payloadBase64,
                PayloadSizeBytes = payloadBase64.Length,
                PayloadPreview = $"Neighbor info: {neighbors.Count} neighbors reported",
                SourceNodeNumber = reportingNodeNumber,
                NeighborInfo = new RealtimeNeighborInfoEvent
                {
                    ReportingNodeId = reportingNodeId,
                    Neighbors = neighbors
                }
            }
        };
    }
}
