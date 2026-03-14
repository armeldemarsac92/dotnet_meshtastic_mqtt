using MeshBoard.Client.Realtime;

namespace MeshBoard.UnitTests;

public sealed class RealtimePacketWorkerContractTests
{
    [Fact]
    public void RequestFactory_ShouldTrimAndMapTransportMessage()
    {
        var factory = new RealtimePacketWorkerRequestFactory();
        var request = factory.Create(
            new BrowserRealtimeClient.RealtimeMessageEvent
            {
                Topic = " meshboard/workspaces/workspace-a/live/packets ",
                PayloadBase64 = " AQID ",
                ReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T15:05:00Z")
            });

        Assert.Equal("meshboard/workspaces/workspace-a/live/packets", request.DownstreamTopic);
        Assert.Equal("AQID", request.PayloadBase64);
        Assert.Equal(DateTimeOffset.Parse("2026-03-14T15:05:00Z"), request.ReceivedAtUtc);
    }

    [Fact]
    public void WorkerResult_WhenSuccessfulRawPacketExists_ShouldExposeMetadata()
    {
        var result = new RealtimePacketWorkerResult
        {
            IsSuccess = true,
            DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.NotAttempted,
            RawPacket = new RealtimeRawPacketEvent
            {
                WorkspaceId = "workspace-a",
                BrokerServer = "broker.meshboard.test",
                SourceTopic = "msh/US/2/e/LongFast/!abc",
                DownstreamTopic = "meshboard/workspaces/workspace-a/live/packets",
                PayloadBase64 = "AQID",
                PayloadSizeBytes = 3,
                ReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T15:00:00Z"),
                IsEncrypted = true,
                DecryptionAttempted = true,
                DecryptionSucceeded = false,
                DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.EncryptedButNotDecrypted,
                FailureClassification = RealtimePacketWorkerFailureKinds.NoMatchingKey,
                FromNodeNumber = 1337,
                PacketId = 42
            }
        };

        Assert.True(result.IsSuccess);
        Assert.Equal(RealtimePacketWorkerDecryptResultClassifications.NotAttempted, result.DecryptResultClassification);
        Assert.NotNull(result.RawPacket);
        Assert.Equal("workspace-a", result.RawPacket!.WorkspaceId);
        Assert.Equal(3, result.RawPacket.PayloadSizeBytes);
        Assert.True(result.RawPacket.IsEncrypted);
        Assert.Equal(RealtimePacketWorkerDecryptResultClassifications.EncryptedButNotDecrypted, result.RawPacket.DecryptResultClassification);
        Assert.Equal(RealtimePacketWorkerFailureKinds.NoMatchingKey, result.RawPacket.FailureClassification);
        Assert.Equal((uint)1337, result.RawPacket.FromNodeNumber);
        Assert.Equal((uint)42, result.RawPacket.PacketId);
    }

    [Fact]
    public void WorkerResult_WhenFailureOccurs_ShouldAllowBoundedFailureClassification()
    {
        var result = new RealtimePacketWorkerResult
        {
            IsSuccess = false,
            DecryptResultClassification = RealtimePacketWorkerDecryptResultClassifications.NotAttempted,
            FailureClassification = RealtimePacketWorkerFailureKinds.MalformedPayload,
            ErrorDetail = "The downstream packet envelope is invalid."
        };

        Assert.False(result.IsSuccess);
        Assert.Equal(RealtimePacketWorkerFailureKinds.MalformedPayload, result.FailureClassification);
        Assert.Equal("The downstream packet envelope is invalid.", result.ErrorDetail);
        Assert.Null(result.RawPacket);
    }
}
