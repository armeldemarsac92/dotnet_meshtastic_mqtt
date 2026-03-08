using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Meshtastic.Hosted;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MeshBoard.UnitTests;

public sealed class BrokerRuntimeCommandProcessorHostedServiceTests
{
    [Fact]
    public async Task StartAsync_ShouldExecuteAndCompletePendingCommands()
    {
        var command = new BrokerRuntimeCommand
        {
            Id = Guid.NewGuid(),
            WorkspaceId = "workspace-a",
            CommandType = BrokerRuntimeCommandType.Publish,
            Topic = "msh/US/2/json/mqtt/",
            Payload = """{"type":"sendtext","payload":"hello"}""",
            AttemptCount = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AvailableAtUtc = DateTimeOffset.UtcNow
        };
        var repository = new FakeBrokerRuntimeCommandRepository(command);
        var executor = new FakeBrokerRuntimeCommandExecutor();
        var service = new BrokerRuntimeCommandProcessorHostedService(
            repository,
            executor,
            Options.Create(new MeshtasticRuntimeOptions
            {
                CommandProcessorPollIntervalMilliseconds = 10,
                CommandProcessorBatchSize = 8,
                CommandLeaseDurationSeconds = 30,
                CommandMaxAttempts = 3,
                CommandRetryDelayMilliseconds = 10
            }),
            TimeProvider.System,
            NullLogger<BrokerRuntimeCommandProcessorHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await repository.WaitForCompletionAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(
            [
                "publish:workspace-a:msh/US/2/json/mqtt/:{\"type\":\"sendtext\",\"payload\":\"hello\"}"
            ],
            executor.ExecutedCommands);
        Assert.Equal([command.Id], repository.CompletedCommandIds);
        Assert.Empty(repository.PendingCommandIds);
        Assert.Empty(repository.FailedCommandIds);
    }

    [Fact]
    public async Task StartAsync_ShouldMarkCommandFailed_WhenMaxAttemptsReached()
    {
        var command = new BrokerRuntimeCommand
        {
            Id = Guid.NewGuid(),
            WorkspaceId = "workspace-a",
            CommandType = BrokerRuntimeCommandType.EnsureConnected,
            AttemptCount = 3,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AvailableAtUtc = DateTimeOffset.UtcNow
        };
        var repository = new FakeBrokerRuntimeCommandRepository(command);
        var executor = new FakeBrokerRuntimeCommandExecutor
        {
            ExceptionToThrow = new InvalidOperationException("runtime unavailable")
        };
        var service = new BrokerRuntimeCommandProcessorHostedService(
            repository,
            executor,
            Options.Create(new MeshtasticRuntimeOptions
            {
                CommandProcessorPollIntervalMilliseconds = 10,
                CommandProcessorBatchSize = 8,
                CommandLeaseDurationSeconds = 30,
                CommandMaxAttempts = 3,
                CommandRetryDelayMilliseconds = 10
            }),
            TimeProvider.System,
            NullLogger<BrokerRuntimeCommandProcessorHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await repository.WaitForFailureAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Empty(repository.CompletedCommandIds);
        Assert.Equal([command.Id], repository.FailedCommandIds);
    }

    private sealed class FakeBrokerRuntimeCommandRepository : IBrokerRuntimeCommandRepository
    {
        private readonly Queue<IReadOnlyCollection<BrokerRuntimeCommand>> _leaseResults = new();
        private readonly TaskCompletionSource _completedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _failedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FakeBrokerRuntimeCommandRepository(params BrokerRuntimeCommand[] commands)
        {
            _leaseResults.Enqueue(commands);
        }

        public List<Guid> CompletedCommandIds { get; } = [];

        public List<Guid> PendingCommandIds { get; } = [];

        public List<Guid> FailedCommandIds { get; } = [];

        public Task EnqueueAsync(BrokerRuntimeCommand command, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<BrokerRuntimeCommand>> GetRecentAsync(
            string workspaceId,
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<BrokerRuntimeCommand>> LeasePendingAsync(
            string processorId,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            var commands = _leaseResults.Count > 0 ? _leaseResults.Dequeue() : [];
            return Task.FromResult(commands);
        }

        public Task MarkCompletedAsync(Guid commandId, CancellationToken cancellationToken = default)
        {
            CompletedCommandIds.Add(commandId);
            _completedTcs.TrySetResult();
            return Task.CompletedTask;
        }

        public Task MarkPendingAsync(
            Guid commandId,
            DateTimeOffset availableAtUtc,
            string? lastError,
            CancellationToken cancellationToken = default)
        {
            PendingCommandIds.Add(commandId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid commandId,
            string? lastError,
            CancellationToken cancellationToken = default)
        {
            FailedCommandIds.Add(commandId);
            _failedTcs.TrySetResult();
            return Task.CompletedTask;
        }

        public Task WaitForCompletionAsync()
        {
            return _completedTcs.Task;
        }

        public Task WaitForFailureAsync()
        {
            return _failedTcs.Task;
        }
    }

    private sealed class FakeBrokerRuntimeCommandExecutor : IBrokerRuntimeCommandExecutor
    {
        public Exception? ExceptionToThrow { get; set; }

        public List<string> ExecutedCommands { get; } = [];

        public Task EnsureConnectedAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            ExecutedCommands.Add($"ensure:{workspaceId}");
            return Task.CompletedTask;
        }

        public Task ReconcileActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            ExecutedCommands.Add($"reconcile:{workspaceId}");
            return Task.CompletedTask;
        }

        public Task ResetAndReconnectActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            ExecutedCommands.Add($"reset:{workspaceId}");
            return Task.CompletedTask;
        }

        public Task PublishAsync(string workspaceId, string topic, string payload, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            ExecutedCommands.Add($"publish:{workspaceId}:{topic}:{payload}");
            return Task.CompletedTask;
        }

        public Task SubscribeEphemeralAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            ExecutedCommands.Add($"subscribe:{workspaceId}:{topicFilter}");
            return Task.CompletedTask;
        }

        public Task UnsubscribeEphemeralAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            ExecutedCommands.Add($"unsubscribe:{workspaceId}:{topicFilter}");
            return Task.CompletedTask;
        }

        private void ThrowIfConfigured()
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }
        }
    }
}
