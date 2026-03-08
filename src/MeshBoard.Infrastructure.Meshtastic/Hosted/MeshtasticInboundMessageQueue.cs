using System.Threading.Channels;
using System.Collections.Concurrent;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class MeshtasticInboundMessageQueue
{
    private readonly Channel<MeshtasticInboundQueueItem> _channel;
    private readonly int _capacity;
    private readonly ConcurrentQueue<DateTimeOffset> _enqueuedAtUtc = new();
    private readonly TimeProvider _timeProvider;
    private long _currentDepth;
    private long _dequeuedCount;
    private long _droppedCount;
    private long _enqueuedCount;

    public MeshtasticInboundMessageQueue(
        IOptions<MeshtasticRuntimeOptions> runtimeOptions,
        TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;

        var options = runtimeOptions.Value;
        var capacity = Math.Max(1, options.InboundQueueCapacity);
        _capacity = capacity;

        _channel = Channel.CreateBounded<MeshtasticInboundQueueItem>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
    }

    public bool TryEnqueue(MqttInboundMessage inboundMessage)
    {
        ArgumentNullException.ThrowIfNull(inboundMessage);

        if (!_channel.Writer.TryWrite(
                new MeshtasticInboundQueueItem(
                    inboundMessage,
                    _timeProvider.GetUtcNow())))
        {
            Interlocked.Increment(ref _droppedCount);
            return false;
        }

        _enqueuedAtUtc.Enqueue(_timeProvider.GetUtcNow());
        Interlocked.Increment(ref _enqueuedCount);
        Interlocked.Increment(ref _currentDepth);
        return true;
    }

    public async IAsyncEnumerable<MeshtasticInboundQueueItem> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            _enqueuedAtUtc.TryDequeue(out _);
            Interlocked.Decrement(ref _currentDepth);
            Interlocked.Increment(ref _dequeuedCount);
            yield return item;
        }
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }

    public MeshtasticInboundQueueSnapshot GetSnapshot()
    {
        var hasOldest = _enqueuedAtUtc.TryPeek(out var oldestEnqueuedAtUtc);

        return new MeshtasticInboundQueueSnapshot
        {
            Capacity = _capacity,
            CurrentDepth = Interlocked.Read(ref _currentDepth),
            EnqueuedCount = Interlocked.Read(ref _enqueuedCount),
            DequeuedCount = Interlocked.Read(ref _dequeuedCount),
            DroppedCount = Interlocked.Read(ref _droppedCount),
            OldestMessageAgeMilliseconds = hasOldest
                ? Math.Max(0, (long)(_timeProvider.GetUtcNow() - oldestEnqueuedAtUtc).TotalMilliseconds)
                : 0
        };
    }
}

internal sealed record MeshtasticInboundQueueItem(
    MqttInboundMessage InboundMessage,
    DateTimeOffset EnqueuedAtUtc);

internal sealed class MeshtasticInboundQueueSnapshot
{
    public int Capacity { get; init; }

    public long CurrentDepth { get; init; }

    public long EnqueuedCount { get; init; }

    public long DequeuedCount { get; init; }

    public long DroppedCount { get; init; }

    public long OldestMessageAgeMilliseconds { get; init; }
}
