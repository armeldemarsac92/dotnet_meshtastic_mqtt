using MeshBoard.Application.Collector;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.UnitTests;

public sealed class LinkDerivationServiceTests
{
    private readonly LinkDerivationService _service = new();

    [Fact]
    public void DeriveLinks_WhenEnvelopeHasNeighbors_ReturnsCanonicalLinks()
    {
        var receivedAtUtc = DateTimeOffset.Parse("2026-03-26T10:00:00Z");
        var envelope = new MeshtasticEnvelope
        {
            FromNodeId = "!00005678",
            ReceivedAtUtc = receivedAtUtc,
            Neighbors =
            [
                new MeshtasticNeighborEntry
                {
                    NodeId = "!00001234",
                    SnrDb = 6.0f,
                    LastRxAtUtc = receivedAtUtc.AddMinutes(-1)
                },
                new MeshtasticNeighborEntry
                {
                    NodeId = "!00009999",
                    SnrDb = 2.5f,
                    LastRxAtUtc = receivedAtUtc.AddMinutes(-2)
                }
            ]
        };

        var links = _service.DeriveLinks(envelope);

        Assert.Equal(2, links.Count);

        var firstLink = Assert.Single(links, link => link.TargetNodeId == "!00005678");
        Assert.Equal("!00001234", firstLink.SourceNodeId);
        Assert.Equal(6.0f, firstLink.SnrDb);

        var secondLink = Assert.Single(links, link => link.TargetNodeId == "!00009999");
        Assert.Equal("!00005678", secondLink.SourceNodeId);
        Assert.Equal(2.5f, secondLink.SnrDb);
    }

    [Fact]
    public void DeriveLinks_WhenNeighborIsSameAsSender_SkipsSelfLink()
    {
        var envelope = new MeshtasticEnvelope
        {
            FromNodeId = "!00005678",
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-26T10:00:00Z"),
            Neighbors =
            [
                new MeshtasticNeighborEntry
                {
                    NodeId = "!00005678",
                    SnrDb = 3.0f
                }
            ]
        };

        var links = _service.DeriveLinks(envelope);

        Assert.Empty(links);
    }

    [Fact]
    public void DeriveLinks_WhenNeighborsIsNull_ReturnsEmpty()
    {
        var envelope = new MeshtasticEnvelope
        {
            FromNodeId = "!00005678",
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-26T10:00:00Z"),
            Neighbors = null
        };

        var links = _service.DeriveLinks(envelope);

        Assert.Empty(links);
    }

    [Fact]
    public void DeriveLinks_WhenDirectPacketIsHopZeroWithGateway_ReturnsLink()
    {
        var receivedAtUtc = DateTimeOffset.Parse("2026-03-26T10:00:00Z");
        var envelope = new MeshtasticEnvelope
        {
            FromNodeId = "!11223344",
            GatewayNodeId = "!aabbccdd",
            HopStart = 3,
            HopLimit = 3,
            RxSnr = -2.5f,
            ReceivedAtUtc = receivedAtUtc
        };

        var links = _service.DeriveLinks(envelope);

        var link = Assert.Single(links);
        Assert.Equal("!11223344", link.SourceNodeId);
        Assert.Equal("!aabbccdd", link.TargetNodeId);
        Assert.Equal(-2.5f, link.SnrDb);
        Assert.Equal(receivedAtUtc, link.LastSeenAtUtc);
    }

    [Fact]
    public void DeriveLinks_WhenHopStartIsZero_ReturnsNoMeshPacketLink()
    {
        var envelope = new MeshtasticEnvelope
        {
            FromNodeId = "!11223344",
            GatewayNodeId = "!aabbccdd",
            HopStart = 0,
            HopLimit = 0,
            RxSnr = -2.5f,
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-26T10:00:00Z")
        };

        var links = _service.DeriveLinks(envelope);

        Assert.Empty(links);
    }

    [Fact]
    public void DeriveLinks_WhenFromNodeIdEqualsGatewayNodeId_ReturnsNoMeshPacketLink()
    {
        var envelope = new MeshtasticEnvelope
        {
            FromNodeId = "!11223344",
            GatewayNodeId = "!11223344",
            HopStart = 3,
            HopLimit = 3,
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-26T10:00:00Z")
        };

        var links = _service.DeriveLinks(envelope);

        Assert.Empty(links);
    }

    [Fact]
    public void DeriveLinks_WhenTracerouteHasTwoHops_ReturnsOneLink()
    {
        var receivedAtUtc = DateTimeOffset.Parse("2026-03-26T10:00:00Z");
        var envelope = new MeshtasticEnvelope
        {
            ReceivedAtUtc = receivedAtUtc,
            TracerouteHops =
            [
                new MeshtasticTracerouteHop { NodeId = "!00005678" },
                new MeshtasticTracerouteHop { NodeId = "!00001234", SnrDb = 4.5f }
            ]
        };

        var links = _service.DeriveLinks(envelope);

        var link = Assert.Single(links);
        Assert.Equal("!00001234", link.SourceNodeId);
        Assert.Equal("!00005678", link.TargetNodeId);
        Assert.Equal(4.5f, link.SnrDb);
        Assert.Equal(receivedAtUtc, link.LastSeenAtUtc);
    }

    [Fact]
    public void DeriveLinks_WhenTracerouteHasFewerThanTwoHops_ReturnsEmpty()
    {
        var envelope = new MeshtasticEnvelope
        {
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-26T10:00:00Z"),
            TracerouteHops =
            [
                new MeshtasticTracerouteHop { NodeId = "!00005678" }
            ]
        };

        var links = _service.DeriveLinks(envelope);

        Assert.Empty(links);
    }

    [Fact]
    public void DeriveLinks_WhenTracerouteHasDuplicateHop_DeduplicatesLink()
    {
        var envelope = new MeshtasticEnvelope
        {
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-26T10:00:00Z"),
            TracerouteHops =
            [
                new MeshtasticTracerouteHop { NodeId = "!00005678" },
                new MeshtasticTracerouteHop { NodeId = "!00001234", SnrDb = 4.5f },
                new MeshtasticTracerouteHop { NodeId = "!00005678", SnrDb = 1.5f }
            ]
        };

        var links = _service.DeriveLinks(envelope);

        var link = Assert.Single(links);
        Assert.Equal("!00001234", link.SourceNodeId);
        Assert.Equal("!00005678", link.TargetNodeId);
        Assert.Equal(4.5f, link.SnrDb);
    }

    [Fact]
    public void DeriveLinks_CanonicalOrdering_AlwaysLexicographicallySorted()
    {
        var envelope = new MeshtasticEnvelope
        {
            FromNodeId = "!0000ffff",
            GatewayNodeId = "!00000002",
            HopStart = 1,
            HopLimit = 1,
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-26T10:00:00Z"),
            Neighbors =
            [
                new MeshtasticNeighborEntry { NodeId = "!00000001", SnrDb = 1.0f }
            ],
            TracerouteHops =
            [
                new MeshtasticTracerouteHop { NodeId = "!00000004" },
                new MeshtasticTracerouteHop { NodeId = "!00000003", SnrDb = 2.0f }
            ]
        };

        var links = _service.DeriveLinks(envelope);

        Assert.Equal(3, links.Count);
        Assert.All(
            links,
            link => Assert.True(
                StringComparer.OrdinalIgnoreCase.Compare(link.SourceNodeId, link.TargetNodeId) <= 0,
                $"Expected canonical ordering for {link.SourceNodeId} and {link.TargetNodeId}."));
    }
}
