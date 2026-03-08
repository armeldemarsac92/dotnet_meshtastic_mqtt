using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.UnitTests;

public sealed class BrokerRuntimeCommandQueryServiceTests
{
    [Fact]
    public async Task GetRecentCommands_ShouldReadCurrentWorkspace_AndClampTake()
    {
        var repository = new FakeBrokerRuntimeCommandRepository();
        var workspaceContextAccessor = new FakeWorkspaceContextAccessor();
        var service = new BrokerRuntimeCommandQueryService(repository, workspaceContextAccessor);

        var commands = await service.GetRecentCommands(250);

        Assert.Same(repository.Commands, commands);
        Assert.Equal("workspace-tests", repository.WorkspaceId);
        Assert.Equal(100, repository.Take);
    }

    private sealed class FakeBrokerRuntimeCommandRepository : IBrokerRuntimeCommandRepository
    {
        public IReadOnlyCollection<BrokerRuntimeCommand> Commands { get; } =
        [
            new()
            {
                Id = Guid.NewGuid(),
                WorkspaceId = "workspace-tests",
                CommandType = BrokerRuntimeCommandType.EnsureConnected,
                Status = BrokerRuntimeCommandStatus.Pending,
                CreatedAtUtc = new DateTimeOffset(2026, 3, 8, 18, 0, 0, TimeSpan.Zero),
                AvailableAtUtc = new DateTimeOffset(2026, 3, 8, 18, 0, 0, TimeSpan.Zero)
            }
        ];

        public string? WorkspaceId { get; private set; }

        public int Take { get; private set; }

        public Task EnqueueAsync(BrokerRuntimeCommand command, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<BrokerRuntimeCommand>> GetRecentAsync(
            string workspaceId,
            int take,
            CancellationToken cancellationToken = default)
        {
            WorkspaceId = workspaceId;
            Take = take;
            return Task.FromResult(Commands);
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

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }
}
