using System.Collections.Concurrent;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Meshtastic.Mqtt;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Runtime;

internal sealed class WorkspaceBrokerSessionManager : IWorkspaceBrokerSessionManager
{
    private readonly IBrokerServerProfileRepository _brokerServerProfileRepository;
    private readonly ILogger<WorkspaceBrokerSessionManager> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _workspaceLocks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, WorkspaceRuntime> _runtimes = new(StringComparer.Ordinal);
    private readonly IMqttSessionFactory _mqttSessionFactory;
    private event Func<MqttInboundMessage, Task>? _messageReceived;

    public WorkspaceBrokerSessionManager(
        IBrokerServerProfileRepository brokerServerProfileRepository,
        IMqttSessionFactory mqttSessionFactory,
        ILogger<WorkspaceBrokerSessionManager> logger)
    {
        _brokerServerProfileRepository = brokerServerProfileRepository;
        _mqttSessionFactory = mqttSessionFactory;
        _logger = logger;
    }

    public bool IsConnected(string workspaceId)
    {
        ValidateWorkspaceId(workspaceId);
        return _runtimes.TryGetValue(workspaceId, out var runtime) && runtime.Session.IsConnected;
    }

    public string? GetLastStatusMessage(string workspaceId)
    {
        ValidateWorkspaceId(workspaceId);
        return _runtimes.TryGetValue(workspaceId, out var runtime) ? runtime.Session.LastStatusMessage : null;
    }

    public IReadOnlyCollection<string> GetTopicFilters(string workspaceId)
    {
        ValidateWorkspaceId(workspaceId);
        return _runtimes.TryGetValue(workspaceId, out var runtime) ? runtime.Session.TopicFilters : [];
    }

    public event Func<MqttInboundMessage, Task>? MessageReceived
    {
        add => _messageReceived += value;
        remove => _messageReceived -= value;
    }

    public async Task ResetRuntimeAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        ValidateWorkspaceId(workspaceId);
        var workspaceLock = GetWorkspaceLock(workspaceId);
        await workspaceLock.WaitAsync(cancellationToken);

        try
        {
            if (!_runtimes.TryRemove(workspaceId, out var runtime))
            {
                return;
            }

            runtime.Session.MessageReceived -= runtime.MessageHandler;
            await runtime.Session.DisconnectAsync(cancellationToken);
        }
        finally
        {
            workspaceLock.Release();
        }
    }

    public async Task ConnectAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var runtime = await GetOrCreateRuntimeAsync(workspaceId, cancellationToken);
        await runtime.Session.ConnectAsync(cancellationToken);
    }

    public async Task DisconnectAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        ValidateWorkspaceId(workspaceId);

        if (_runtimes.TryGetValue(workspaceId, out var runtime))
        {
            await runtime.Session.DisconnectAsync(cancellationToken);
        }
    }

    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var workspaceId in _runtimes.Keys.ToArray())
        {
            await ResetRuntimeAsync(workspaceId, cancellationToken);
        }
    }

    public async Task PublishAsync(string workspaceId, string topic, string payload, CancellationToken cancellationToken = default)
    {
        var runtime = await GetOrCreateRuntimeAsync(workspaceId, cancellationToken);
        await runtime.Session.PublishAsync(topic, payload, cancellationToken);
    }

    public async Task SubscribeAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
    {
        var runtime = await GetOrCreateRuntimeAsync(workspaceId, cancellationToken);
        await runtime.Session.SubscribeAsync(topicFilter, cancellationToken);
    }

    public async Task UnsubscribeAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
    {
        ValidateWorkspaceId(workspaceId);

        if (!_runtimes.TryGetValue(workspaceId, out var runtime))
        {
            return;
        }

        await runtime.Session.UnsubscribeAsync(topicFilter, cancellationToken);
    }

    private async Task<WorkspaceRuntime> GetOrCreateRuntimeAsync(string workspaceId, CancellationToken cancellationToken)
    {
        ValidateWorkspaceId(workspaceId);
        var activeProfile = await _brokerServerProfileRepository.GetActiveAsync(workspaceId, cancellationToken)
            ?? throw new InvalidOperationException($"No active broker server profile is configured for workspace '{workspaceId}'.");

        var workspaceLock = GetWorkspaceLock(workspaceId);
        await workspaceLock.WaitAsync(cancellationToken);

        try
        {
            if (_runtimes.TryGetValue(workspaceId, out var existingRuntime) && existingRuntime.Matches(activeProfile))
            {
                return existingRuntime;
            }

            if (existingRuntime is not null)
            {
                _logger.LogInformation(
                    "Replacing MQTT runtime for workspace {WorkspaceId} with profile {ProfileId} ({ServerAddress})",
                    workspaceId,
                    activeProfile.Id,
                    activeProfile.ServerAddress);

                existingRuntime.Session.MessageReceived -= existingRuntime.MessageHandler;
                await existingRuntime.Session.DisconnectAsync(cancellationToken);
            }

            var runtime = CreateRuntime(workspaceId, activeProfile);
            _runtimes[workspaceId] = runtime;
            return runtime;
        }
        finally
        {
            workspaceLock.Release();
        }
    }

    private WorkspaceRuntime CreateRuntime(string workspaceId, BrokerServerProfile activeProfile)
    {
        var session = _mqttSessionFactory.Create(
            new MqttSessionConnectionSettings
            {
                WorkspaceId = workspaceId,
                BrokerServerProfileId = activeProfile.Id,
                Host = activeProfile.Host,
                Port = activeProfile.Port,
                UseTls = activeProfile.UseTls,
                Username = activeProfile.Username,
                Password = activeProfile.Password
            });

        Func<MqttInboundMessage, Task> messageHandler = OnMessageReceivedAsync;
        session.MessageReceived += messageHandler;

        _logger.LogInformation(
            "Created MQTT runtime for workspace {WorkspaceId} with profile {ProfileId} ({ServerAddress})",
            workspaceId,
            activeProfile.Id,
            activeProfile.ServerAddress);

        return new WorkspaceRuntime(
            activeProfile.Id,
            activeProfile.ServerAddress,
            activeProfile.UseTls,
            activeProfile.Username,
            activeProfile.Password,
            session,
            messageHandler);
    }

    private Task OnMessageReceivedAsync(MqttInboundMessage inboundMessage)
    {
        var handler = _messageReceived;
        return handler?.Invoke(inboundMessage) ?? Task.CompletedTask;
    }

    private SemaphoreSlim GetWorkspaceLock(string workspaceId)
    {
        return _workspaceLocks.GetOrAdd(workspaceId, _ => new SemaphoreSlim(1, 1));
    }

    private static void ValidateWorkspaceId(string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new InvalidOperationException("A workspace ID is required to use the broker session manager.");
        }
    }

    private sealed class WorkspaceRuntime
    {
        public WorkspaceRuntime(
            Guid profileId,
            string serverAddress,
            bool useTls,
            string username,
            string password,
            IMqttSession session,
            Func<MqttInboundMessage, Task> messageHandler)
        {
            ProfileId = profileId;
            ServerAddress = serverAddress;
            UseTls = useTls;
            Username = username;
            Password = password;
            Session = session;
            MessageHandler = messageHandler;
        }

        public Guid ProfileId { get; }

        public string ServerAddress { get; }

        public bool UseTls { get; }

        public string Username { get; }

        public string Password { get; }

        public IMqttSession Session { get; }

        public Func<MqttInboundMessage, Task> MessageHandler { get; }

        public bool Matches(BrokerServerProfile profile)
        {
            return ProfileId == profile.Id &&
                string.Equals(ServerAddress, profile.ServerAddress, StringComparison.Ordinal) &&
                UseTls == profile.UseTls &&
                string.Equals(Username, profile.Username, StringComparison.Ordinal) &&
                string.Equals(Password, profile.Password, StringComparison.Ordinal);
        }
    }
}
