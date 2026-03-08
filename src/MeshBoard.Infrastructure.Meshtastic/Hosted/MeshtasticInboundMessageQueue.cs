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
    private readonly ConcurrentDictionary<string, WorkspaceInboundQueueMetrics> _metricsByWorkspace = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

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
        var workspaceId = NormalizeWorkspaceId(inboundMessage.WorkspaceId);
        var metrics = _metricsByWorkspace.GetOrAdd(workspaceId, static key => new WorkspaceInboundQueueMetrics(key));

        if (!_channel.Writer.TryWrite(
                new MeshtasticInboundQueueItem(
                    inboundMessage,
                    _timeProvider.GetUtcNow())))
        {
            metrics.OnDropped();
            return false;
        }

        metrics.OnEnqueued(_timeProvider.GetUtcNow());
        return true;
    }

    public async IAsyncEnumerable<MeshtasticInboundQueueItem> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            var workspaceId = NormalizeWorkspaceId(item.InboundMessage.WorkspaceId);
            var metrics = _metricsByWorkspace.GetOrAdd(workspaceId, static key => new WorkspaceInboundQueueMetrics(key));
            metrics.OnDequeued();
            yield return item;
        }
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }

    public MeshtasticInboundQueueSnapshot GetSnapshot(string workspaceId)
    {
        var normalizedWorkspaceId = NormalizeWorkspaceId(workspaceId);
        if (!_metricsByWorkspace.TryGetValue(normalizedWorkspaceId, out var metrics))
        {
            return new MeshtasticInboundQueueSnapshot
            {
                WorkspaceId = normalizedWorkspaceId,
                Capacity = _capacity
            };
        }

        return metrics.CreateSnapshot(_capacity, _timeProvider);
    }

    public IReadOnlyCollection<MeshtasticInboundQueueSnapshot> GetSnapshots()
    {
        return _metricsByWorkspace.Values
            .Select(metrics => metrics.CreateSnapshot(_capacity, _timeProvider))
            .ToList();
    }

    private static string NormalizeWorkspaceId(string workspaceId)
    {
        return string.IsNullOrWhiteSpace(workspaceId) ? string.Empty : workspaceId.Trim();
    }
}

internal sealed record MeshtasticInboundQueueItem(
    MqttInboundMessage InboundMessage,
    DateTimeOffset EnqueuedAtUtc);

internal sealed class MeshtasticInboundQueueSnapshot
{
    public string WorkspaceId { get; init; } = string.Empty;

    public int Capacity { get; init; }

    public long CurrentDepth { get; init; }

    public long EnqueuedCount { get; init; }

    public long DequeuedCount { get; init; }

    public long DroppedCount { get; init; }

    public long OldestMessageAgeMilliseconds { get; init; }
}

internal sealed class WorkspaceInboundQueueMetrics
{
    private readonly ConcurrentQueue<DateTimeOffset> _enqueuedAtUtc = new();
    private long _currentDepth;
    private long _dequeuedCount;
    private long _droppedCount;
    private long _enqueuedCount;

    public WorkspaceInboundQueueMetrics(string workspaceId)
    {
        WorkspaceId = workspaceId;
    }

    public string WorkspaceId { get; }

    public void OnEnqueued(DateTimeOffset enqueuedAtUtc)
    {
        _enqueuedAtUtc.Enqueue(enqueuedAtUtc);
        Interlocked.Increment(ref _enqueuedCount);
        Interlocked.Increment(ref _currentDepth);
    }

    public void OnDequeued()
    {
        _enqueuedAtUtc.TryDequeue(out _);
        Interlocked.Decrement(ref _currentDepth);
        Interlocked.Increment(ref _dequeuedCount);
    }

    public void OnDropped()
    {
        Interlocked.Increment(ref _droppedCount);
    }

    public MeshtasticInboundQueueSnapshot CreateSnapshot(int capacity, TimeProvider timeProvider)
    {
        var hasOldest = _enqueuedAtUtc.TryPeek(out var oldestEnqueuedAtUtc);

        return new MeshtasticInboundQueueSnapshot
        {
            WorkspaceId = WorkspaceId,
            Capacity = capacity,
            CurrentDepth = Interlocked.Read(ref _currentDepth),
            EnqueuedCount = Interlocked.Read(ref _enqueuedCount),
            DequeuedCount = Interlocked.Read(ref _dequeuedCount),
            DroppedCount = Interlocked.Read(ref _droppedCount),
            OldestMessageAgeMilliseconds = hasOldest
                ? Math.Max(0, (long)(timeProvider.GetUtcNow() - oldestEnqueuedAtUtc).TotalMilliseconds)
                : 0
        };
    }
}
