using MeshBoard.Client.Messages;
using MeshBoard.Client.Realtime;

namespace MeshBoard.UnitTests;

public sealed class DecryptedMessageStoreTests
{
    [Fact]
    public void Project_ShouldStoreDecodedMessageMetadata()
    {
        var store = new DecryptedMessageStore(new DecryptedMessageState());

        store.Project(CreateDecodedResult(packetId: 42, fromNodeNumber: 99));

        var message = Assert.Single(store.Current.Messages);
        Assert.Equal("workspace-a", message.WorkspaceId);
        Assert.Equal("broker.meshboard.test", message.BrokerServer);
        Assert.Equal("msh/US/2/e/LongFast/!0063", message.SourceTopic);
        Assert.Equal("Text Message", message.PacketType);
        Assert.Equal("hello mesh", message.PayloadPreview);
        Assert.Equal(1, message.PortNumValue);
        Assert.Equal("TEXT_MESSAGE_APP", message.PortNumName);
        Assert.Equal((uint)42, message.PacketId);
        Assert.Equal((uint)99, message.FromNodeNumber);
        Assert.Equal((uint)5678, message.SourceNodeNumber);
        Assert.Equal((uint)1234, message.DestinationNodeNumber);
        Assert.Equal(1, store.Current.TotalProjected);
    }

    [Fact]
    public void Project_ShouldDeduplicateByBrokerTopicPacketIdAndFromNode()
    {
        var store = new DecryptedMessageStore(new DecryptedMessageState());
        var result = CreateDecodedResult(packetId: 42, fromNodeNumber: 99);

        store.Project(result);
        store.Project(result);

        Assert.Single(store.Current.Messages);
        Assert.Equal(1, store.Current.TotalProjected);
    }

    [Fact]
    public void Project_WhenIdentityMetadataIsMissing_ShouldFallbackToStableContentHash()
    {
        var store = new DecryptedMessageStore(new DecryptedMessageState());
        var result = CreateDecodedResult(packetId: null, fromNodeNumber: null);

        store.Project(result);
        store.Project(result);

        var message = Assert.Single(store.Current.Messages);
        Assert.False(string.IsNullOrWhiteSpace(message.Id));
        Assert.Equal(1, store.Current.TotalProjected);
    }

    [Fact]
    public void Project_WhenDecodedPacketIsMissing_ShouldIgnorePacket()
    {
        var store = new DecryptedMessageStore(new DecryptedMessageState());

        store.Project(
            new RealtimePacketWorkerResult
            {
                IsSuccess = true,
                DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.EncryptedButNotDecrypted,
                RawPacket = new RealtimeRawPacketEvent
                {
                    WorkspaceId = "workspace-a",
                    BrokerServer = "broker.meshboard.test",
                    SourceTopic = "msh/US/2/e/LongFast/!0063",
                    PayloadBase64 = "AQID",
                    PayloadSizeBytes = 3,
                    ReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z"),
                    IsEncrypted = true
                }
            });

        Assert.Empty(store.Current.Messages);
        Assert.Equal(0, store.Current.TotalProjected);
    }

    private static RealtimePacketWorkerResult CreateDecodedResult(uint? packetId, uint? fromNodeNumber)
    {
        return new RealtimePacketWorkerResult
        {
            IsSuccess = true,
            DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.Decrypted,
            RawPacket = new RealtimeRawPacketEvent
            {
                WorkspaceId = "workspace-a",
                BrokerServer = "broker.meshboard.test",
                SourceTopic = "msh/US/2/e/LongFast/!0063",
                DownstreamTopic = "meshboard/workspaces/workspace-a/live/packets",
                PayloadBase64 = "AQID",
                PayloadSizeBytes = 3,
                ReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T17:00:00Z"),
                IsEncrypted = true,
                DecryptionAttempted = true,
                DecryptionSucceeded = true,
                PacketId = packetId,
                FromNodeNumber = fromNodeNumber
            },
            DecodedPacket = new RealtimeDecodedPacketEvent
            {
                PortNumValue = 1,
                PortNumName = "TEXT_MESSAGE_APP",
                PacketType = "Text Message",
                PayloadBase64 = Convert.ToBase64String("hello mesh"u8.ToArray()),
                PayloadSizeBytes = 10,
                PayloadPreview = "hello mesh",
                SourceNodeNumber = 5678,
                DestinationNodeNumber = 1234
            }
        };
    }
}
