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

    public void RecordMessage(string topic, string payloadBase64, int payloadSizeBytes, DateTimeOffset receivedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new InvalidOperationException("A message topic is required.");
        }

        var current = _state.Snapshot;
        var nextMessage = new LiveMessageEnvelope
        {
            PayloadBase64 = payloadBase64?.Trim() ?? string.Empty,
            PayloadSizeBytes = payloadSizeBytes,
            ReceivedAtUtc = receivedAtUtc,
            Topic = topic.Trim()
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
