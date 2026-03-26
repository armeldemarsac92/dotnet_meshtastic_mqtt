using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Favorites;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class FavoriteNodeServiceTests
{
    [Fact]
    public async Task SaveFavoriteNode_ShouldCommitTransaction()
    {
        var repository = new FakeFavoriteNodeRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new FavoriteNodeService(
            repository,
            unitOfWork,
            new FakeWorkspaceContextAccessor(),
            NullLogger<FavoriteNodeService>.Instance);

        var favoriteNode = await service.SaveFavoriteNode(
            new SaveFavoriteNodeRequest
            {
                NodeId = "!abcd1234",
                ShortName = "ABCD",
                LongName = "Alpha Bravo"
            });

        Assert.Equal("!abcd1234", favoriteNode.NodeId);
        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task SaveFavoriteNode_ShouldRollback_WhenRepositoryFails()
    {
        var repository = new FakeFavoriteNodeRepository { ThrowOnUpsert = true };
        var unitOfWork = new FakeUnitOfWork();
        var service = new FavoriteNodeService(
            repository,
            unitOfWork,
            new FakeWorkspaceContextAccessor(),
            NullLogger<FavoriteNodeService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveFavoriteNode(
                new SaveFavoriteNodeRequest
                {
                    NodeId = "!abcd1234"
                }));

        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(0, unitOfWork.CommitCount);
        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task RemoveFavoriteNode_ShouldCommit_WhenNodeExists()
    {
        var repository = new FakeFavoriteNodeRepository { DeleteResult = true };
        var unitOfWork = new FakeUnitOfWork();
        var service = new FavoriteNodeService(
            repository,
            unitOfWork,
            new FakeWorkspaceContextAccessor(),
            NullLogger<FavoriteNodeService>.Instance);

        await service.RemoveFavoriteNode("!abcd1234");

        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task RemoveFavoriteNode_ShouldRollbackAndThrowNotFound_WhenNodeDoesNotExist()
    {
        var repository = new FakeFavoriteNodeRepository { DeleteResult = false };
        var unitOfWork = new FakeUnitOfWork();
        var service = new FavoriteNodeService(
            repository,
            unitOfWork,
            new FakeWorkspaceContextAccessor(),
            NullLogger<FavoriteNodeService>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => service.RemoveFavoriteNode("!missing"));

        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(0, unitOfWork.CommitCount);
        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    private sealed class FakeFavoriteNodeRepository : IFavoriteNodeRepository
    {
        public bool DeleteResult { get; set; } = true;

        public bool ThrowOnUpsert { get; set; }

        public Task<bool> DeleteAsync(
            string workspaceId,
            string nodeId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeleteResult);
        }

        public Task<IReadOnlyCollection<FavoriteNode>> GetAllAsync(
            string workspaceId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<FavoriteNode> result = [];
            return Task.FromResult(result);
        }

        public Task<FavoriteNode> UpsertAsync(
            string workspaceId,
            SaveFavoriteNodeRequest request,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnUpsert)
            {
                throw new InvalidOperationException("Simulated repository failure.");
            }

            return Task.FromResult(
                new FavoriteNode
                {
                    Id = Guid.NewGuid(),
                    NodeId = request.NodeId,
                    ShortName = request.ShortName,
                    LongName = request.LongName,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
        }
    }

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }
    }
}
