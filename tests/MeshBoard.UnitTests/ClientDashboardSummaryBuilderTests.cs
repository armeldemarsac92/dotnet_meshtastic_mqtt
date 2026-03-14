using MeshBoard.Client.Channels;
using MeshBoard.Client.Dashboard;
using MeshBoard.Client.Messages;
using MeshBoard.Client.Nodes;
using MeshBoard.Client.Realtime;

namespace MeshBoard.UnitTests;

public sealed class ClientDashboardSummaryBuilderTests
{
    private readonly ClientDashboardSummaryBuilder _builder = new();

    [Fact]
    public void Build_ShouldAggregateCountsAndLatestActivity()
    {
        var summary = _builder.Build(
            realtime: new RealtimeClientSnapshot
            {
                MessageCount = 12
            },
            liveMessages: new LiveMessageFeedSnapshot
            {
                TotalReceived = 10,
                LastReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T17:02:00Z"),
                Messages =
                [
                    new LiveMessageEnvelope
                    {
                        DecryptionSucceeded = true,
                        DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.Decrypted
                    },
                    new LiveMessageEnvelope
                    {
                        DecryptionAttempted = true,
                        FailureClassification = RealtimePacketWorkerFailureKinds.NoMatchingKey,
                        DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.EncryptedButNotDecrypted
                    },
                    new LiveMessageEnvelope
                    {
                        DecryptionAttempted = true,
                        FailureClassification = RealtimePacketWorkerFailureKinds.DecryptFailure,
                        DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.EncryptedButNotDecrypted
                    }
                ]
            },
            decryptedMessages: new DecryptedMessageSnapshot
            {
                TotalProjected = 4,
                LastProjectedAtUtc = DateTimeOffset.Parse("2026-03-14T17:01:00Z")
            },
            nodeProjections: new NodeProjectionSnapshot
            {
                LastProjectedAtUtc = DateTimeOffset.Parse("2026-03-14T17:03:00Z"),
                Nodes =
                [
                    new NodeProjectionEnvelope
                    {
                        NodeId = "!0000162e",
                        LongName = "Atlas",
                        LastKnownLatitude = 48.8566,
                        LastKnownLongitude = 2.3522,
                        BatteryLevelPercent = 90,
                        ObservedPacketCount = 5
                    },
                    new NodeProjectionEnvelope
                    {
                        NodeId = "!00001000",
                        LongName = "Field",
                        ObservedPacketCount = 2
                    }
                ]
            },
            channelProjections: new ChannelProjectionSnapshot
            {
                LastProjectedAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:30Z"),
                Channels =
                [
                    new ChannelProjectionEnvelope
                    {
                        ChannelKey = "US/LongFast",
                        ObservedPacketCount = 7,
                        LastObservedAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z"),
                        ObservedNodeIds = ["!0000162e", "!00001000"]
                    }
                ]
            });

        Assert.Equal(12, summary.RawPacketCount);
        Assert.Equal(4, summary.DecryptedMessageCount);
        Assert.Equal(2, summary.ObservedNodeCount);
        Assert.Equal(1, summary.LocatedNodeCount);
        Assert.Equal(1, summary.ObservedChannelCount);
        Assert.Equal(DateTimeOffset.Parse("2026-03-14T17:03:00Z"), summary.LastActivityAtUtc);
        Assert.Equal(1, summary.SuccessfulDecryptCount);
        Assert.Equal(1, summary.NoMatchingKeyCount);
        Assert.Equal(1, summary.DecryptFailureCount);
    }

    [Fact]
    public void Build_ShouldOrderTopLists()
    {
        var summary = _builder.Build(
            realtime: new RealtimeClientSnapshot(),
            liveMessages: new LiveMessageFeedSnapshot(),
            decryptedMessages: new DecryptedMessageSnapshot
            {
                Messages =
                [
                    new DecryptedMessageEnvelope
                    {
                        PacketType = "Telemetry",
                        PayloadPreview = "battery 88%",
                        SourceTopic = "msh/US/2/e/LongFast/!00001000",
                        ReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T17:05:00Z")
                    },
                    new DecryptedMessageEnvelope
                    {
                        PacketType = "Text Message",
                        PayloadPreview = "hello",
                        SourceTopic = "msh/US/2/e/LongFast/!0000162e",
                        ReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T17:04:00Z")
                    }
                ]
            },
            nodeProjections: new NodeProjectionSnapshot
            {
                Nodes =
                [
                    new NodeProjectionEnvelope
                    {
                        NodeId = "!00001000",
                        LongName = "Field",
                        ObservedPacketCount = 3,
                        LastHeardAtUtc = DateTimeOffset.Parse("2026-03-14T17:02:00Z")
                    },
                    new NodeProjectionEnvelope
                    {
                        NodeId = "!0000162e",
                        LongName = "Atlas",
                        ObservedPacketCount = 9,
                        LastHeardAtUtc = DateTimeOffset.Parse("2026-03-14T17:01:00Z")
                    }
                ]
            },
            channelProjections: new ChannelProjectionSnapshot
            {
                Channels =
                [
                    new ChannelProjectionEnvelope
                    {
                        ChannelKey = "EU/FieldOps",
                        ObservedPacketCount = 2
                    },
                    new ChannelProjectionEnvelope
                    {
                        ChannelKey = "US/LongFast",
                        ObservedPacketCount = 8
                    }
                ]
            });

        Assert.Equal("US/LongFast", summary.ActiveChannels[0].ChannelKey);
        Assert.Equal("Atlas", summary.ActiveNodes[0].DisplayName);
        Assert.Equal("Telemetry", summary.RecentMessages[0].PacketType);
    }
}
