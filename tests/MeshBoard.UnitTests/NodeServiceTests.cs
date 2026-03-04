using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Nodes;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class NodeServiceTests
{
    [Fact]
    public async Task GetNodes_ShouldFilterBySearchLocationAndTelemetry()
    {
        var repository = new FakeNodeRepository(
        [
            new NodeSummary
            {
                NodeId = "!aaaa1111",
                LongName = "Alpha",
                LastKnownLatitude = 48.8566,
                LastKnownLongitude = 2.3522,
                BatteryLevelPercent = 72
            },
            new NodeSummary
            {
                NodeId = "!bbbb2222",
                LongName = "Bravo",
                LastKnownLatitude = 40.7128,
                LastKnownLongitude = -74.0060
            },
            new NodeSummary
            {
                NodeId = "!cccc3333",
                LongName = "Charlie",
                BatteryLevelPercent = 55
            }
        ]);
        var service = new NodeService(repository, NullLogger<NodeService>.Instance);

        var nodes = await service.GetNodes(
            new NodeQuery
            {
                SearchText = "alp",
                OnlyWithLocation = true,
                OnlyWithTelemetry = true
            });

        var node = Assert.Single(nodes);
        Assert.Equal("Alpha", node.LongName);
    }

    [Fact]
    public async Task GetNodes_ShouldSortByBatteryDescending()
    {
        var repository = new FakeNodeRepository(
        [
            new NodeSummary { NodeId = "!cccc3333", LongName = "Charlie", BatteryLevelPercent = 40 },
            new NodeSummary { NodeId = "!aaaa1111", LongName = "Alpha", BatteryLevelPercent = 88 },
            new NodeSummary { NodeId = "!bbbb2222", LongName = "Bravo" }
        ]);
        var service = new NodeService(repository, NullLogger<NodeService>.Instance);

        var nodes = await service.GetNodes(new NodeQuery { SortBy = NodeSortOption.BatteryDesc });

        Assert.Collection(
            nodes,
            node => Assert.Equal("Alpha", node.LongName),
            node => Assert.Equal("Charlie", node.LongName),
            node => Assert.Equal("Bravo", node.LongName));
    }

    private sealed class FakeNodeRepository : INodeRepository
    {
        private readonly IReadOnlyCollection<NodeSummary> _nodes;

        public FakeNodeRepository(IReadOnlyCollection<NodeSummary> nodes)
        {
            _nodes = nodes;
        }

        public Task<IReadOnlyCollection<NodeSummary>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_nodes);
        }

        public Task UpsertAsync(UpsertObservedNodeRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
