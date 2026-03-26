using System.Globalization;
using MeshBoard.Application.Abstractions.Collector;
using MeshBoard.Contracts.Collector;
using MeshBoard.Contracts.Nodes;
using Neo4j.Driver;

namespace MeshBoard.Infrastructure.Neo4j.Repositories;

public sealed class Neo4jTopologyReadRepository : ITopologyReadAdapter
{
    private const string GetNodesAllCypher =
        """
        MATCH (n:MeshNode)
        WHERE n.lastHeardAtUtc IS NOT NULL
          AND n.lastHeardAtUtc >= datetime($notBefore)
        RETURN
            n.nodeId AS nodeId,
            n.brokerServer AS brokerServer,
            n.shortName AS shortName,
            n.longName AS longName,
            toString(n.lastHeardAtUtc) AS lastHeardAtUtc,
            toString(n.lastTextMessageAtUtc) AS lastTextMessageAtUtc,
            n.lastKnownLatitude AS lastKnownLatitude,
            n.lastKnownLongitude AS lastKnownLongitude
        ORDER BY lastHeardAtUtc DESC,
                 coalesce(longName, shortName, nodeId) ASC,
                 nodeId ASC
        LIMIT $maxNodes
        """;

    private const string GetNodesWithBrokerCypher =
        """
        MATCH (n:MeshNode)
        WHERE n.lastHeardAtUtc IS NOT NULL
          AND n.lastHeardAtUtc >= datetime($notBefore)
          AND n.brokerServer = $brokerServer
        RETURN
            n.nodeId AS nodeId,
            n.brokerServer AS brokerServer,
            n.shortName AS shortName,
            n.longName AS longName,
            toString(n.lastHeardAtUtc) AS lastHeardAtUtc,
            toString(n.lastTextMessageAtUtc) AS lastTextMessageAtUtc,
            n.lastKnownLatitude AS lastKnownLatitude,
            n.lastKnownLongitude AS lastKnownLongitude
        ORDER BY lastHeardAtUtc DESC,
                 coalesce(longName, shortName, nodeId) ASC,
                 nodeId ASC
        LIMIT $maxNodes
        """;

    private const string GetLinksAllCypher =
        """
        MATCH (src:MeshNode)-[r:RADIO_LINK]->(tgt:MeshNode)
        WHERE r.lastSeenAtUtc IS NOT NULL
          AND r.lastSeenAtUtc >= datetime($notBefore)
        RETURN
            src.nodeId AS sourceNodeId,
            tgt.nodeId AS targetNodeId,
            r.lastSnrDb AS snrDb,
            toString(r.lastSeenAtUtc) AS lastSeenAtUtc,
            r.brokerServer AS brokerServer,
            r.channelKey AS channelKey
        ORDER BY lastSeenAtUtc DESC,
                 sourceNodeId ASC,
                 targetNodeId ASC
        LIMIT $maxLinks
        """;

    private const string GetLinksWithBrokerCypher =
        """
        MATCH (src:MeshNode)-[r:RADIO_LINK]->(tgt:MeshNode)
        WHERE r.lastSeenAtUtc IS NOT NULL
          AND r.lastSeenAtUtc >= datetime($notBefore)
          AND r.brokerServer = $brokerServer
        RETURN
            src.nodeId AS sourceNodeId,
            tgt.nodeId AS targetNodeId,
            r.lastSnrDb AS snrDb,
            toString(r.lastSeenAtUtc) AS lastSeenAtUtc,
            r.brokerServer AS brokerServer,
            r.channelKey AS channelKey
        ORDER BY lastSeenAtUtc DESC,
                 sourceNodeId ASC,
                 targetNodeId ASC
        LIMIT $maxLinks
        """;

    private readonly IDriver _driver;

    public Neo4jTopologyReadRepository(IDriver driver)
    {
        _driver = driver;
    }

    public Task<IReadOnlyCollection<NodeSummary>> GetTopologyNodesAsync(
        CollectorTopologyQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var hasBrokerFilter = !string.IsNullOrWhiteSpace(query.ServerAddress);
        var cypher = hasBrokerFilter ? GetNodesWithBrokerCypher : GetNodesAllCypher;
        var parameters = CreateParameters(notBeforeUtc, query.MaxNodes, hasBrokerFilter ? query.ServerAddress : null);

        return RunQueryAsync(cypher, parameters, MapNode, cancellationToken);
    }

    public Task<IReadOnlyCollection<CollectorMapLinkSummary>> GetTopologyLinksAsync(
        CollectorTopologyQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var hasBrokerFilter = !string.IsNullOrWhiteSpace(query.ServerAddress);
        var cypher = hasBrokerFilter ? GetLinksWithBrokerCypher : GetLinksAllCypher;
        var parameters = CreateParameters(notBeforeUtc, query.MaxLinks, hasBrokerFilter ? query.ServerAddress : null);

        return RunQueryAsync(cypher, parameters, MapLink, cancellationToken);
    }

    private async Task<IReadOnlyCollection<T>> RunQueryAsync<T>(
        string cypher,
        IReadOnlyDictionary<string, object> parameters,
        Func<IRecord, T> map,
        CancellationToken cancellationToken)
    {
        await using var session = _driver.AsyncSession();
        var cursor = await session.RunAsync(cypher, parameters);
        var results = new List<T>();

        while (await cursor.FetchAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(map(cursor.Current));
        }

        return results;
    }

    private static IReadOnlyDictionary<string, object> CreateParameters(
        DateTimeOffset notBeforeUtc,
        int limit,
        string? brokerServer)
    {
        var parameters = new Dictionary<string, object>
        {
            ["notBefore"] = notBeforeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        };

        if (brokerServer is not null)
        {
            parameters["brokerServer"] = brokerServer;
        }

        if (limit > 0)
        {
            parameters["maxNodes"] = limit;
            parameters["maxLinks"] = limit;
        }

        return parameters;
    }

    private static NodeSummary MapNode(IRecord record)
    {
        return new NodeSummary
        {
            NodeId = GetRequiredString(record, "nodeId"),
            BrokerServer = GetRequiredString(record, "brokerServer"),
            ShortName = GetOptionalString(record, "shortName"),
            LongName = GetOptionalString(record, "longName"),
            LastHeardAtUtc = ParseNullableDateTimeOffset(GetOptionalString(record, "lastHeardAtUtc")),
            LastTextMessageAtUtc = ParseNullableDateTimeOffset(GetOptionalString(record, "lastTextMessageAtUtc")),
            LastKnownLatitude = GetOptionalDouble(record, "lastKnownLatitude"),
            LastKnownLongitude = GetOptionalDouble(record, "lastKnownLongitude")
        };
    }

    private static CollectorMapLinkSummary MapLink(IRecord record)
    {
        return new CollectorMapLinkSummary
        {
            SourceNodeId = GetRequiredString(record, "sourceNodeId"),
            TargetNodeId = GetRequiredString(record, "targetNodeId"),
            SnrDb = GetOptionalFloat(record, "snrDb"),
            LastSeenAtUtc = ParseDateTimeOffset(GetRequiredString(record, "lastSeenAtUtc")),
            ServerAddress = GetRequiredString(record, "brokerServer"),
            Region = string.Empty,
            MeshVersion = string.Empty,
            ChannelName = string.Empty
        };
    }

    private static string GetRequiredString(IRecord record, string key)
    {
        return record[key] switch
        {
            string value => value,
            null => string.Empty,
            var value => value.ToString() ?? string.Empty
        };
    }

    private static string? GetOptionalString(IRecord record, string key)
    {
        return record[key] switch
        {
            null => null,
            string value => value,
            var value => value.ToString()
        };
    }

    private static double? GetOptionalDouble(IRecord record, string key)
    {
        return record[key] switch
        {
            null => null,
            double value => value,
            float value => value,
            decimal value => (double)value,
            long value => value,
            int value => value,
            string value when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue) => parsedValue,
            _ => null
        };
    }

    private static float? GetOptionalFloat(IRecord record, string key)
    {
        return record[key] switch
        {
            null => null,
            float value => value,
            double value => (float)value,
            decimal value => (float)value,
            long value => value,
            int value => value,
            string value when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue) => parsedValue,
            _ => null
        };
    }

    private static DateTimeOffset ParseDateTimeOffset(string value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedValue)
            ? parsedValue
            : throw new FormatException($"Could not parse Neo4j datetime value '{value}'.");
    }

    private static DateTimeOffset? ParseNullableDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedValue)
            ? parsedValue
            : null;
    }
}
