using MeshBoard.Contracts.CollectorEvents;
using Neo4j.Driver;

namespace MeshBoard.Infrastructure.Persistence.Repositories.Neo4j;

public sealed class GraphTopologyRepository : IGraphTopologyRepository
{
    private const string UpsertNodeCypher =
        """
        MERGE (broker:BrokerServer { key: $brokerServerKey })
          ON CREATE SET
            broker.address = $brokerServer
        MERGE (meshNode:MeshNode { key: $nodeKey })
          ON CREATE SET
            meshNode.brokerServer = $brokerServer,
            meshNode.nodeId = $nodeId,
            meshNode.shortName = $shortName,
            meshNode.longName = $longName,
            meshNode.lastHeardAtUtc = datetime($lastHeardAtUtc),
            meshNode.lastTextMessageAtUtc = datetime($lastTextMessageAtUtc),
            meshNode.lastKnownLatitude = $latitude,
            meshNode.lastKnownLongitude = $longitude
          ON MATCH SET
            meshNode.brokerServer = coalesce(meshNode.brokerServer, $brokerServer),
            meshNode.nodeId = coalesce(meshNode.nodeId, $nodeId),
            meshNode.shortName = coalesce($shortName, meshNode.shortName),
            meshNode.longName = coalesce($longName, meshNode.longName),
            meshNode.lastHeardAtUtc = coalesce(datetime($lastHeardAtUtc), meshNode.lastHeardAtUtc),
            meshNode.lastTextMessageAtUtc = coalesce(datetime($lastTextMessageAtUtc), meshNode.lastTextMessageAtUtc),
            meshNode.lastKnownLatitude = coalesce($latitude, meshNode.lastKnownLatitude),
            meshNode.lastKnownLongitude = coalesce($longitude, meshNode.lastKnownLongitude)
        """;

    private const string UpsertObservedChannelCypher =
        """
        MATCH (broker:BrokerServer { key: $brokerServerKey })
        MATCH (meshNode:MeshNode { key: $nodeKey })
        MERGE (channel:CollectorChannel { key: $channelKey })
          ON CREATE SET
            channel.brokerServer = $brokerServer,
            channel.topicPattern = $channelTopicPattern
        MERGE (broker)-[:HAS_CHANNEL]->(channel)
        MERGE (meshNode)-[observedOn:OBSERVED_ON]->(channel)
          ON CREATE SET
            observedOn.lastSeenAtUtc = datetime($observedAtUtc)
          ON MATCH SET
            observedOn.lastSeenAtUtc = datetime($observedAtUtc)
        """;

    private const string UpsertLinkCypher =
        """
        MERGE (src:MeshNode { key: $sourceNodeKey })
          ON CREATE SET
            src.brokerServer = $brokerServer,
            src.nodeId = $sourceNodeId
        MERGE (tgt:MeshNode { key: $targetNodeKey })
          ON CREATE SET
            tgt.brokerServer = $brokerServer,
            tgt.nodeId = $targetNodeId
        MERGE (src)-[link:RADIO_LINK { key: $linkKey }]->(tgt)
          ON CREATE SET
            link.brokerServer = $brokerServer,
            link.channelKey = $channelKey,
            link.topicPattern = $topicPattern,
            link.observationCount = 1,
            link.lastSeenAtUtc = datetime($observedAtUtc),
            link.lastSnrDb = $snrDb,
            link.maxSnrDb = $snrDb,
            link.linkOrigins = [$linkOrigin]
          ON MATCH SET
            link.observationCount = link.observationCount + 1,
            link.lastSeenAtUtc = datetime($observedAtUtc),
            link.lastSnrDb = $snrDb,
            link.maxSnrDb = CASE
              WHEN $snrDb IS NULL THEN link.maxSnrDb
              WHEN link.maxSnrDb IS NULL OR $snrDb > link.maxSnrDb THEN $snrDb
              ELSE link.maxSnrDb
            END
        """;

    private readonly IDriver _driver;

    public GraphTopologyRepository(IDriver driver)
    {
        _driver = driver;
    }

    public async Task UpsertNodeAsync(GraphNodeWriteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nodeKey = $"{request.BrokerServer}|{request.NodeId}";
        var observedAtUtc = ToNeo4jDateTimeString(request.ObservedAtUtc);

        var nodeParameters = new Dictionary<string, object?>
        {
            ["brokerServerKey"] = request.BrokerServer,
            ["brokerServer"] = request.BrokerServer,
            ["nodeKey"] = nodeKey,
            ["nodeId"] = request.NodeId,
            ["shortName"] = request.ShortName,
            ["longName"] = request.LongName,
            ["lastHeardAtUtc"] = ToNeo4jDateTimeString(request.LastHeardAtUtc),
            ["lastTextMessageAtUtc"] = ToNeo4jDateTimeString(request.LastTextMessageAtUtc),
            ["latitude"] = request.Latitude,
            ["longitude"] = request.Longitude
        };

        await using var session = _driver.AsyncSession();

        await ExecuteAsync(session, UpsertNodeCypher, nodeParameters, ct);

        if (string.IsNullOrWhiteSpace(request.ChannelTopicPattern))
        {
            return;
        }

        var channelParameters = new Dictionary<string, object?>
        {
            ["brokerServerKey"] = request.BrokerServer,
            ["brokerServer"] = request.BrokerServer,
            ["nodeKey"] = nodeKey,
            ["channelKey"] = $"{request.BrokerServer}|{request.ChannelTopicPattern}",
            ["channelTopicPattern"] = request.ChannelTopicPattern,
            ["observedAtUtc"] = observedAtUtc
        };

        await ExecuteAsync(session, UpsertObservedChannelCypher, channelParameters, ct);
    }

    public async Task UpsertLinkAsync(GraphLinkWriteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (canonicalSourceNodeId, canonicalTargetNodeId) = GetCanonicalNodePair(
            request.SourceNodeId,
            request.TargetNodeId);

        var linkParameters = new Dictionary<string, object?>
        {
            ["brokerServer"] = request.BrokerServer,
            ["channelKey"] = request.ChannelKey,
            ["topicPattern"] = request.TopicPattern,
            ["sourceNodeKey"] = $"{request.BrokerServer}|{canonicalSourceNodeId}",
            ["sourceNodeId"] = canonicalSourceNodeId,
            ["targetNodeKey"] = $"{request.BrokerServer}|{canonicalTargetNodeId}",
            ["targetNodeId"] = canonicalTargetNodeId,
            ["linkKey"] = $"{request.BrokerServer}|{request.ChannelKey}|{canonicalSourceNodeId}|{canonicalTargetNodeId}",
            ["observedAtUtc"] = ToNeo4jDateTimeString(request.ObservedAtUtc),
            ["snrDb"] = request.SnrDb,
            ["linkOrigin"] = request.LinkOrigin
        };

        await using var session = _driver.AsyncSession();
        await ExecuteAsync(session, UpsertLinkCypher, linkParameters, ct);
    }

    private static async Task ExecuteAsync(
        IAsyncSession session,
        string cypher,
        IDictionary<string, object?> parameters,
        CancellationToken ct)
    {
        var cursor = await session.RunAsync(cypher, parameters);
        ct.ThrowIfCancellationRequested();
        await cursor.ConsumeAsync();
    }

    private static (string SourceNodeId, string TargetNodeId) GetCanonicalNodePair(string sourceNodeId, string targetNodeId)
    {
        return GraphTopologyKeys.CanonicalNodePair(sourceNodeId, targetNodeId);
    }

    private static string ToNeo4jDateTimeString(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("O");
    }

    private static string? ToNeo4jDateTimeString(DateTimeOffset? value)
    {
        return value?.UtcDateTime.ToString("O");
    }
}
