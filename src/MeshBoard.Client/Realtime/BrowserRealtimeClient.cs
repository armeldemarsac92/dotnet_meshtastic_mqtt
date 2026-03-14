using MeshBoard.Client.Authentication;
using MeshBoard.Client.Channels;
using MeshBoard.Client.Maps;
using MeshBoard.Client.Messages;
using MeshBoard.Client.Nodes;
using MeshBoard.Client.Services;
using MeshBoard.Contracts.Realtime;
using Microsoft.JSInterop;

namespace MeshBoard.Client.Realtime;

public sealed class BrowserRealtimeClient : IAsyncDisposable
{
    private readonly AuthSessionState _authSessionState;
    private readonly ChannelProjectionStore _channelProjectionStore;
    private readonly DecryptedMessageStore _decryptedMessageStore;
    private readonly LiveMessageFeedService _liveMessageFeedService;
    private readonly MapProjectionStore _mapProjectionStore;
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;
    private readonly NodeProjectionStore _nodeProjectionStore;
    private readonly RealtimeClientState _realtimeClientState;
    private readonly RealtimeSessionApiClient _realtimeSessionApiClient;
    private readonly RealtimePacketWorkerClient _realtimePacketWorkerClient;
    private readonly RealtimePacketWorkerRequestFactory _realtimePacketWorkerRequestFactory;
    private DotNetObjectReference<BrowserRealtimeClient>? _objectReference;

    public BrowserRealtimeClient(
        AuthSessionState authSessionState,
        ChannelProjectionStore channelProjectionStore,
        DecryptedMessageStore decryptedMessageStore,
        IJSRuntime jsRuntime,
        LiveMessageFeedService liveMessageFeedService,
        MapProjectionStore mapProjectionStore,
        NodeProjectionStore nodeProjectionStore,
        RealtimeClientState realtimeClientState,
        RealtimeSessionApiClient realtimeSessionApiClient,
        RealtimePacketWorkerClient realtimePacketWorkerClient,
        RealtimePacketWorkerRequestFactory realtimePacketWorkerRequestFactory)
    {
        _authSessionState = authSessionState;
        _channelProjectionStore = channelProjectionStore;
        _decryptedMessageStore = decryptedMessageStore;
        _liveMessageFeedService = liveMessageFeedService;
        _mapProjectionStore = mapProjectionStore;
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/realtimeClient.js").AsTask());
        _nodeProjectionStore = nodeProjectionStore;
        _realtimeClientState = realtimeClientState;
        _realtimeSessionApiClient = realtimeSessionApiClient;
        _realtimePacketWorkerClient = realtimePacketWorkerClient;
        _realtimePacketWorkerRequestFactory = realtimePacketWorkerRequestFactory;
    }

    public RealtimeClientSnapshot Current => _realtimeClientState.Snapshot;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var current = Current;
        if (current.IsConnecting || current.IsConnected)
        {
            return;
        }

        if (!_authSessionState.IsAuthenticated)
        {
            throw new InvalidOperationException("Sign in before opening the realtime connection.");
        }

        var session = await _realtimeSessionApiClient.CreateSessionAsync(cancellationToken);
        var allowedTopics = NormalizeAllowedTopics(session.AllowedTopicPatterns);

        SetSnapshot(snapshot => snapshot with
        {
            IsReady = true,
            IsConnecting = true,
            IsConnected = false,
            IsDisconnecting = false,
            Status = "Opening realtime connection...",
            BrokerUrl = session.BrokerUrl,
            ClientId = session.ClientId,
            AllowedTopicPatterns = allowedTopics,
            ActiveSubscriptionCount = 0,
            SessionExpiresAtUtc = session.ExpiresAtUtc,
            LastError = null,
            LastDisconnectReason = null
        });

        try
        {
            var module = await GetModuleAsync();
            _objectReference ??= DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("connect", cancellationToken, session, _objectReference);
        }
        catch (Exception exception)
        {
            SetSnapshot(snapshot => snapshot with
            {
                IsReady = true,
                IsConnecting = false,
                IsConnected = false,
                IsDisconnecting = false,
                Status = "Disconnected",
                LastDisconnectedAtUtc = DateTimeOffset.UtcNow,
                LastError = BuildConnectErrorMessage(exception)
            });

            throw new InvalidOperationException("Opening the realtime broker connection failed.", exception);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var current = Current;
        if (!current.IsConnected && !current.IsConnecting)
        {
            SetSnapshot(snapshot => snapshot with
            {
                IsReady = true,
                IsDisconnecting = false,
                Status = "Disconnected"
            });

            return;
        }

        SetSnapshot(snapshot => snapshot with
        {
            IsReady = true,
            IsConnecting = false,
            IsDisconnecting = true,
            Status = "Disconnecting..."
        });

        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("disconnect", cancellationToken);
    }

    [JSInvokable]
    public Task HandleConnectedAsync(RealtimeConnectionEvent connectionEvent)
    {
        SetSnapshot(snapshot => snapshot with
        {
            IsReady = true,
            IsConnecting = false,
            IsConnected = true,
            IsDisconnecting = false,
            Status = "Connected",
            ActiveSubscriptionCount = connectionEvent.SubscriptionCount,
            ConnectedAtUtc = connectionEvent.ConnectedAtUtc == default
                ? DateTimeOffset.UtcNow
                : connectionEvent.ConnectedAtUtc,
            LastError = null,
            LastDisconnectReason = null
        });

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task HandleDisconnectedAsync(RealtimeDisconnectionEvent disconnectionEvent)
    {
        SetSnapshot(snapshot => snapshot with
        {
            IsReady = true,
            IsConnecting = false,
            IsConnected = false,
            IsDisconnecting = false,
            Status = "Disconnected",
            ActiveSubscriptionCount = 0,
            LastDisconnectedAtUtc = disconnectionEvent.DisconnectedAtUtc == default
                ? DateTimeOffset.UtcNow
                : disconnectionEvent.DisconnectedAtUtc,
            LastDisconnectReason = NormalizeText(disconnectionEvent.Reason),
            LastError = snapshot.LastError
        });

        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task HandleMessageAsync(RealtimeMessageEvent messageEvent)
    {
        RealtimePacketWorkerResult processedPacket;

        try
        {
            processedPacket = await _realtimePacketWorkerClient.ProcessAsync(
                _realtimePacketWorkerRequestFactory.Create(messageEvent));
        }
        catch (Exception exception)
        {
            SetSnapshot(snapshot => snapshot with
            {
                IsReady = true,
                LastError = $"Ignoring realtime packet from {NormalizeText(messageEvent.Topic) ?? "(unknown topic)"}: {BuildConnectErrorMessage(exception)}"
            });

            return;
        }

        if (processedPacket.RawPacket is null)
        {
            SetSnapshot(snapshot => snapshot with
            {
                IsReady = true,
                LastError = BuildPacketWorkerErrorMessage(messageEvent.Topic, processedPacket)
            });

            return;
        }

        var rawPacket = processedPacket.RawPacket;
        var receivedAtUtc = rawPacket.ReceivedAtUtc == default
            ? DateTimeOffset.UtcNow
            : rawPacket.ReceivedAtUtc;

        _liveMessageFeedService.RecordMessage(rawPacket, processedPacket.DecodedPacket);
        _channelProjectionStore.Project(processedPacket);
        _decryptedMessageStore.Project(processedPacket);
        _mapProjectionStore.Project(processedPacket);
        _nodeProjectionStore.Project(processedPacket);

        SetSnapshot(snapshot => snapshot with
        {
            IsReady = true,
            MessageCount = snapshot.MessageCount + 1,
            LastError = null,
            LastMessageTopic = NormalizeText(rawPacket.SourceTopic),
            LastPayloadSizeBytes = rawPacket.PayloadSizeBytes,
            LastMessageReceivedAtUtc = receivedAtUtc
        });
    }

    [JSInvokable]
    public Task HandleReconnectingAsync()
    {
        SetSnapshot(snapshot => snapshot with
        {
            IsReady = true,
            IsConnecting = true,
            IsConnected = false,
            IsDisconnecting = false,
            Status = "Reconnecting..."
        });

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task HandleSessionRefreshedAsync(RealtimeSessionResponse session)
    {
        var allowedTopics = NormalizeAllowedTopics(session.AllowedTopicPatterns);

        SetSnapshot(snapshot => snapshot with
        {
            IsReady = true,
            BrokerUrl = session.BrokerUrl,
            ClientId = session.ClientId,
            AllowedTopicPatterns = allowedTopics,
            SessionExpiresAtUtc = session.ExpiresAtUtc,
            Status = snapshot.IsConnected ? "Connected" : snapshot.Status
        });

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task HandleTransportErrorAsync(string? error)
    {
        SetSnapshot(snapshot => snapshot with
        {
            IsReady = true,
            LastError = NormalizeText(error) ?? "The realtime broker connection reported an error."
        });

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task<RealtimeSessionResponse> RefreshSessionAsync()
    {
        return _realtimeSessionApiClient.CreateSessionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _objectReference?.Dispose();
        await _realtimePacketWorkerClient.DisposeAsync();

        if (!_moduleTask.IsValueCreated)
        {
            return;
        }

        var module = await _moduleTask.Value;
        try
        {
            await module.InvokeVoidAsync("disconnect");
        }
        catch (JSDisconnectedException)
        {
        }

        await module.DisposeAsync();
    }

    private static string BuildConnectErrorMessage(Exception exception)
    {
        if (exception is JSException jsException && !string.IsNullOrWhiteSpace(jsException.Message))
        {
            return jsException.Message.Trim();
        }

        return exception.Message.Trim();
    }

    private static string BuildPacketWorkerErrorMessage(string? downstreamTopic, RealtimePacketWorkerResult result)
    {
        var topic = NormalizeText(downstreamTopic) ?? "(unknown topic)";

        if (!string.IsNullOrWhiteSpace(result.ErrorDetail))
        {
            return $"Ignoring realtime packet from {topic}: {result.ErrorDetail.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(result.FailureClassification))
        {
            return $"Ignoring realtime packet from {topic}: {result.FailureClassification.Trim()}.";
        }

        return $"Ignoring realtime packet from {topic}.";
    }

    private Task<IJSObjectReference> GetModuleAsync()
    {
        return _moduleTask.Value;
    }

    private static IReadOnlyList<string> NormalizeAllowedTopics(IEnumerable<string>? topics)
    {
        if (topics is null)
        {
            return RealtimeClientSnapshot.EmptyTopics;
        }

        return topics
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Select(topic => topic.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(topic => topic, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private void SetSnapshot(Func<RealtimeClientSnapshot, RealtimeClientSnapshot> update)
    {
        _realtimeClientState.SetSnapshot(update(_realtimeClientState.Snapshot));
    }

    public sealed class RealtimeConnectionEvent
    {
        public DateTimeOffset ConnectedAtUtc { get; set; }

        public int SubscriptionCount { get; set; }
    }

    public sealed class RealtimeDisconnectionEvent
    {
        public DateTimeOffset DisconnectedAtUtc { get; set; }

        public string? Reason { get; set; }
    }

    public sealed class RealtimeMessageEvent
    {
        public string PayloadBase64 { get; set; } = string.Empty;

        public int PayloadSizeBytes { get; set; }

        public DateTimeOffset ReceivedAtUtc { get; set; }

        public string Topic { get; set; } = string.Empty;
    }
}
