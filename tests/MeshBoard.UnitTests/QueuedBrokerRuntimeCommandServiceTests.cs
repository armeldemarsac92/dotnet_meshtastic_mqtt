using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Meshtastic.Runtime;

namespace MeshBoard.UnitTests;

public sealed class QueuedBrokerRuntimeCommandServiceTests
{
    [Fact]
    public async Task PublishAndSubscriptionCommands_ShouldBeEnqueuedWithExpectedPayload()
    {
        var repository = new FakeBrokerRuntimeCommandRepository();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.Zero));
        var service = new QueuedBrokerRuntimeCommandService(repository, timeProvider);

        await service.EnsureConnectedAsync("workspace-a");
        await service.PublishAsync("workspace-a", "msh/US/2/json/mqtt/", """{"type":"sendtext","payload":"hello"}""");
        await service.SubscribeEphemeralAsync("workspace-a", "msh/US/2/e/LongFast/#");

        Assert.Collection(
            repository.Commands,
            command =>
            {
                Assert.Equal("workspace-a", command.WorkspaceId);
                Assert.Equal(BrokerRuntimeCommandType.EnsureConnected, command.CommandType);
                Assert.Null(command.Topic);
                Assert.Null(command.TopicFilter);
            },
            command =>
            {
                Assert.Equal(BrokerRuntimeCommandType.Publish, command.CommandType);
                Assert.Equal("msh/US/2/json/mqtt/", command.Topic);
                Assert.Equal("""{"type":"sendtext","payload":"hello"}""", command.Payload);
            },
            command =>
            {
                Assert.Equal(BrokerRuntimeCommandType.SubscribeEphemeral, command.CommandType);
                Assert.Equal("msh/US/2/e/LongFast/#", command.TopicFilter);
            });

        Assert.All(repository.Commands, command => Assert.Equal(timeProvider.GetUtcNow(), command.CreatedAtUtc));
    }

    private sealed class FakeBrokerRuntimeCommandRepository : IBrokerRuntimeCommandRepository
    {
        public List<BrokerRuntimeCommand> Commands { get; } = [];

        public Task EnqueueAsync(BrokerRuntimeCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<BrokerRuntimeCommand>> LeasePendingAsync(
            string processorId,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task MarkCompletedAsync(Guid commandId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task MarkPendingAsync(
            Guid commandId,
            DateTimeOffset availableAtUtc,
            string? lastError,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task MarkFailedAsync(
            Guid commandId,
            string? lastError,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
