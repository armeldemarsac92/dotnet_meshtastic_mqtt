using MeshBoard.Client.Channels;
using MeshBoard.Client.Messages;
using MeshBoard.Client.Nodes;
using MeshBoard.Client.Realtime;

namespace MeshBoard.Client.Dashboard;

public sealed class ClientDashboardSummaryBuilder
{
    private const int MaxActiveChannels = 5;
    private const int MaxActiveNodes = 5;
    private const int MaxRecentMessages = 5;

    public ClientDashboardSummary Build(
        RealtimeClientSnapshot realtime,
        LiveMessageFeedSnapshot liveMessages,
        DecryptedMessageSnapshot decryptedMessages,
        NodeProjectionSnapshot nodeProjections,
        ChannelProjectionSnapshot channelProjections)
    {
        ArgumentNullException.ThrowIfNull(realtime);
        ArgumentNullException.ThrowIfNull(liveMessages);
        ArgumentNullException.ThrowIfNull(decryptedMessages);
        ArgumentNullException.ThrowIfNull(nodeProjections);
        ArgumentNullException.ThrowIfNull(channelProjections);

        var rawPacketCount = Math.Max(realtime.MessageCount, liveMessages.TotalReceived);
        var successfulDecryptCount = liveMessages.Messages.Count(message => message.DecryptionSucceeded);
        var noMatchingKeyCount = liveMessages.Messages.Count(
            message => string.Equals(
                message.FailureClassification,
                RealtimePacketWorkerFailureKinds.NoMatchingKey,
                StringComparison.Ordinal));
        var decryptFailureCount = liveMessages.Messages.Count(
            message => !message.DecryptionSucceeded &&
                !string.Equals(message.DecryptResultClassification, RealtimePacketWorkerDecryptResultClassifications.NotAttempted, StringComparison.Ordinal) &&
                !string.Equals(message.FailureClassification, RealtimePacketWorkerFailureKinds.NoMatchingKey, StringComparison.Ordinal));

        return new ClientDashboardSummary
        {
            RawPacketCount = rawPacketCount,
            DecryptedMessageCount = decryptedMessages.TotalProjected,
            ObservedNodeCount = nodeProjections.Nodes.Count,
            LocatedNodeCount = nodeProjections.Nodes.Count(node => node.HasLocation),
            ObservedChannelCount = channelProjections.Channels.Count,
            LastActivityAtUtc = Max(
                liveMessages.LastReceivedAtUtc,
                decryptedMessages.LastProjectedAtUtc,
                nodeProjections.LastProjectedAtUtc,
                channelProjections.LastProjectedAtUtc),
            SuccessfulDecryptCount = successfulDecryptCount,
            NoMatchingKeyCount = noMatchingKeyCount,
            DecryptFailureCount = decryptFailureCount,
            ActiveChannels = channelProjections.Channels
                .OrderByDescending(channel => channel.ObservedPacketCount)
                .ThenByDescending(channel => channel.LastObservedAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(channel => channel.ChannelKey, StringComparer.OrdinalIgnoreCase)
                .Take(MaxActiveChannels)
                .Select(channel => new ClientDashboardChannelSummary
                {
                    ChannelKey = channel.ChannelKey,
                    LastPacketType = channel.LastPacketType,
                    ObservedPacketCount = channel.ObservedPacketCount,
                    DistinctNodeCount = channel.DistinctNodeCount,
                    LastObservedAtUtc = channel.LastObservedAtUtc
                })
                .ToArray(),
            ActiveNodes = nodeProjections.Nodes
                .OrderByDescending(node => node.ObservedPacketCount)
                .ThenByDescending(node => node.LastHeardAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(MaxActiveNodes)
                .Select(node => new ClientDashboardNodeSummary
                {
                    NodeId = node.NodeId,
                    DisplayName = node.DisplayName,
                    Channel = node.LastHeardChannel,
                    ObservedPacketCount = node.ObservedPacketCount,
                    HasLocation = node.HasLocation,
                    HasTelemetry = node.HasTelemetry,
                    LastHeardAtUtc = node.LastHeardAtUtc
                })
                .ToArray(),
            RecentMessages = decryptedMessages.Messages
                .OrderByDescending(message => message.ReceivedAtUtc)
                .Take(MaxRecentMessages)
                .Select(message => new ClientDashboardMessageSummary
                {
                    PacketType = message.PacketType,
                    PayloadPreview = message.PayloadPreview,
                    SourceTopic = message.SourceTopic,
                    ReceivedAtUtc = message.ReceivedAtUtc
                })
                .ToArray()
        };
    }

    private static DateTimeOffset? Max(params DateTimeOffset?[] values)
    {
        return values
            .Where(value => value.HasValue)
            .OrderByDescending(value => value!.Value)
            .Select(value => value!.Value)
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();
    }
}
