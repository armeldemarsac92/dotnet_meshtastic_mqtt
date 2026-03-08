using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Nodes;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class NodeServiceTests
{
    [Fact]
    public async Task GetNodesPage_ShouldReturnItemsAndTotalCount()
    {
        var repository = new FakeNodeRepository(
        [
            new NodeSummary { NodeId = "!00000001" },
            new NodeSummary { NodeId = "!00000002" },
            new NodeSummary { NodeId = "!00000003" }
        ]);
        var service = new NodeService(repository, NullLogger<NodeService>.Instance);

        var page = await service.GetNodesPage(offset: 1, take: 1);

        Assert.Equal(3, page.TotalCount);
        var node = Assert.Single(page.Items);
        Assert.Equal("!00000002", node.NodeId);
    }

    [Fact]
    public async Task GetNodes_ShouldClampTake_ToMaximumWindow()
    {
        var repository = new FakeNodeRepository(
            Enumerable.Range(0, 2_000)
                .Select(index => new NodeSummary { NodeId = $"!{index:x8}" })
                .ToList());
        var service = new NodeService(repository, NullLogger<NodeService>.Instance);

        var nodes = await service.GetNodes(take: 5_000);

        Assert.Equal(1_000, nodes.Count);
        Assert.Equal(1_000, repository.LastTake);
    }

    [Fact]
    public async Task GetNodeById_ShouldForwardExactNodeId()
    {
        var expectedNode = new NodeSummary { NodeId = "!abc12345" };
        var repository = new FakeNodeRepository([expectedNode]);
        var service = new NodeService(repository, NullLogger<NodeService>.Instance);

        var node = await service.GetNodeById(" !abc12345 ");

        Assert.NotNull(node);
        Assert.Equal("!abc12345", node.NodeId);
        Assert.Equal("!abc12345", repository.LastNodeId);
    }

    [Fact]
    public async Task GetLocatedNodes_ShouldClampTake_ToLocationWindow()
    {
        var repository = new FakeNodeRepository(
            Enumerable.Range(0, 12_000)
                .Select(index => new NodeSummary
                {
                    NodeId = $"!{index:x8}",
                    LastKnownLatitude = 48.0 + index,
                    LastKnownLongitude = 2.0 + index
                })
                .ToList());
        var service = new NodeService(repository, NullLogger<NodeService>.Instance);

        var nodes = await service.GetLocatedNodes(take: 20_000);

        Assert.Equal(10_000, nodes.Count);
        Assert.Equal(10_000, repository.LastLocatedTake);
    }

    private sealed class FakeNodeRepository : INodeRepository
    {
        private readonly IReadOnlyCollection<NodeSummary> _nodes;

        public FakeNodeRepository(IReadOnlyCollection<NodeSummary> nodes)
        {
            _nodes = nodes;
        }

        public int LastTake { get; private set; }

        public int LastLocatedTake { get; private set; }

        public string? LastNodeId { get; private set; }

        public Task<int> CountAsync(NodeQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_nodes.Count);
        }

        public Task<NodeSummary?> GetByIdAsync(string nodeId, CancellationToken cancellationToken = default)
        {
            LastNodeId = nodeId;
            return Task.FromResult(_nodes.FirstOrDefault(node => node.NodeId == nodeId));
        }

        public Task<IReadOnlyCollection<NodeSummary>> GetLocatedAsync(
            string? searchText,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastLocatedTake = take;

            IReadOnlyCollection<NodeSummary> nodes = _nodes
                .Where(node => node.LastKnownLatitude.HasValue && node.LastKnownLongitude.HasValue)
                .Take(take)
                .ToList();

            return Task.FromResult(nodes);
        }

        public Task<IReadOnlyCollection<NodeSummary>> GetPageAsync(
            NodeQuery query,
            int offset,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastTake = take;
            IReadOnlyCollection<NodeSummary> page = _nodes.Skip(offset).Take(take).ToList();
            return Task.FromResult(page);
        }

        public Task UpsertAsync(UpsertObservedNodeRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
