using MeshBoard.Contracts.Realtime;

namespace MeshBoard.Client.Messages;

public sealed class LiveMessageFeedService
{
    private const int MaxRetainedMessages = 100;
    private readonly LiveMessageFeedState _state;

    public LiveMessageFeedService(LiveMessageFeedState state)
    {
        _state = state;
    }

    public event Action? Changed
    {
        add => _state.Changed += value;
        remove => _state.Changed -= value;
    }

    public LiveMessageFeedSnapshot Current => _state.Snapshot;

    public void Clear()
    {
        _state.SetSnapshot(new());
    }

    public void RecordMessage(RealtimePacketEnvelope envelope, string downstreamTopic)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (string.IsNullOrWhiteSpace(envelope.Topic))
        {
            throw new InvalidOperationException("A source topic is required.");
        }

        var current = _state.Snapshot;
        var receivedAtUtc = envelope.ReceivedAtUtc == default
            ? DateTimeOffset.UtcNow
            : envelope.ReceivedAtUtc;
        var nextMessage = new LiveMessageEnvelope
        {
            BrokerServer = envelope.BrokerServer?.Trim() ?? string.Empty,
            DownstreamTopic = downstreamTopic?.Trim() ?? string.Empty,
            PayloadBase64 = Convert.ToBase64String(envelope.Payload),
            PayloadSizeBytes = envelope.Payload.Length,
            ReceivedAtUtc = receivedAtUtc,
            SourceTopic = envelope.Topic.Trim()
        };

        var messages = current.Messages
            .Prepend(nextMessage)
            .Take(MaxRetainedMessages)
            .ToArray();

        _state.SetSnapshot(current with
        {
            Messages = messages,
            LastReceivedAtUtc = receivedAtUtc,
            TotalReceived = current.TotalReceived + 1
        });
    }
}
