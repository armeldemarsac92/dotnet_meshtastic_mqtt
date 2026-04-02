using System.Globalization;
using Neo4j.Driver;

namespace MeshBoard.Infrastructure.Persistence.Repositories.Neo4j;

public sealed class Neo4jTopologyAnalyticsReadRepository : ITopologyAnalyticsReadRepository
{
    private const string GetNodeAnalyticsAllCypher =
        """
        MATCH (n:MeshNode)
        WHERE n.lastHeardAtUtc IS NOT NULL
          AND n.lastHeardAtUtc >= datetime($notBefore)
        RETURN
            n.nodeId AS nodeId,
            coalesce(n.degree, 0) AS degree,
            n.componentId AS componentId,
            coalesce(n.isBridgeNode, false) AS isBridgeNode
        ORDER BY degree DESC,
                 coalesce(n.longName, n.shortName, n.nodeId) ASC,
                 n.nodeId ASC
        LIMIT $maxNodes
        """;

    private const string GetNodeAnalyticsWithBrokerCypher =
        """
        MATCH (n:MeshNode)
        WHERE n.lastHeardAtUtc IS NOT NULL
          AND n.lastHeardAtUtc >= datetime($notBefore)
          AND n.brokerServer = $brokerServer
        RETURN
            n.nodeId AS nodeId,
            coalesce(n.degree, 0) AS degree,
            n.componentId AS componentId,
            coalesce(n.isBridgeNode, false) AS isBridgeNode
        ORDER BY degree DESC,
                 coalesce(n.longName, n.shortName, n.nodeId) ASC,
                 n.nodeId ASC
        LIMIT $maxNodes
        """;

    private readonly IDriver _driver;

    public Neo4jTopologyAnalyticsReadRepository(IDriver driver)
    {
        _driver = driver;
    }

    public Task<IReadOnlyCollection<TopologyNodeAnalytics>> GetNodeAnalyticsAsync(
        string? brokerServer,
        DateTimeOffset notBeforeUtc,
        int maxNodes,
        CancellationToken cancellationToken = default)
    {
        var hasBrokerFilter = !string.IsNullOrWhiteSpace(brokerServer);
        var cypher = hasBrokerFilter ? GetNodeAnalyticsWithBrokerCypher : GetNodeAnalyticsAllCypher;
        var parameters = CreateParameters(notBeforeUtc, maxNodes, hasBrokerFilter ? brokerServer : null);

        return RunQueryAsync(cypher, parameters, cancellationToken);
    }

    private async Task<IReadOnlyCollection<TopologyNodeAnalytics>> RunQueryAsync(
        string cypher,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        await using var session = _driver.AsyncSession();
        var cursor = await session.RunAsync(cypher, parameters);
        var results = new List<TopologyNodeAnalytics>();

        while (await cursor.FetchAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(MapNodeAnalytics(cursor.Current));
        }

        return results;
    }

    private static IReadOnlyDictionary<string, object> CreateParameters(
        DateTimeOffset notBeforeUtc,
        int maxNodes,
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

        if (maxNodes > 0)
        {
            parameters["maxNodes"] = maxNodes;
        }

        return parameters;
    }

    private static TopologyNodeAnalytics MapNodeAnalytics(IRecord record)
    {
        return new TopologyNodeAnalytics
        {
            NodeId = record["nodeId"].As<string>(),
            Degree = GetOptionalInt(record, "degree") ?? 0,
            ComponentId = GetOptionalLong(record, "componentId"),
            IsBridgeNode = GetOptionalBool(record, "isBridgeNode") ?? false
        };
    }

    private static int? GetOptionalInt(IRecord record, string key)
    {
        return record[key] switch
        {
            null => null,
            int value => value,
            long value => checked((int)value),
            string value when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue) => parsedValue,
            _ => null
        };
    }

    private static long? GetOptionalLong(IRecord record, string key)
    {
        return record[key] switch
        {
            null => null,
            long value => value,
            int value => value,
            string value when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue) => parsedValue,
            _ => null
        };
    }

    private static bool? GetOptionalBool(IRecord record, string key)
    {
        return record[key] switch
        {
            null => null,
            bool value => value,
            string value when bool.TryParse(value, out var parsedValue) => parsedValue,
            _ => null
        };
    }
}
