using System.Diagnostics;
using MeshBoard.Collector.TopologyAnalyst.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace MeshBoard.Collector.TopologyAnalyst.Services;

public sealed class TopologyAnalysisService : ITopologyAnalysisService
{
    private const string DropProjectionCypher =
        """
        CALL gds.graph.drop($projectionName, false) YIELD graphName
        """;

    private const string CreateProjectionCypher =
        """
        CALL gds.graph.project(
          $projectionName,
          'MeshNode',
          { RADIO_LINK: { orientation: 'UNDIRECTED' } }
        ) YIELD graphName, nodeCount, relationshipCount
        """;

    private const string WriteDegreeCypher =
        """
        CALL gds.degree.write($projectionName, { writeProperty: 'degree' }) YIELD nodePropertiesWritten
        """;

    private const string WriteWeaklyConnectedComponentsCypher =
        """
        CALL gds.wcc.write($projectionName, { writeProperty: 'componentId' }) YIELD componentCount, nodePropertiesWritten
        """;

    private const string WriteArticulationPointsCypher =
        """
        CALL gds.articulationPoints.write($projectionName, { writeProperty: 'isBridgeNode' }) YIELD nodePropertiesWritten
        """;

    private readonly IDriver _driver;
    private readonly ILogger<TopologyAnalysisService> _logger;
    private readonly IOptions<TopologyAnalysisOptions> _options;

    public TopologyAnalysisService(
        IDriver driver,
        IOptions<TopologyAnalysisOptions> options,
        ILogger<TopologyAnalysisService> logger)
    {
        _driver = driver;
        _options = options;
        _logger = logger;
    }

    public async Task RunAnalysisAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var stopwatch = Stopwatch.StartNew();
        var parameters = CreateProjectionParameters(options.GraphProjectionName);
        Exception? analysisException = null;

        await using var session = _driver.AsyncSession();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteAsync(session, DropProjectionCypher, parameters, cancellationToken);

            var projectionRecord = await ExecuteSingleRecordAsync(
                session,
                CreateProjectionCypher,
                parameters,
                cancellationToken);

            _logger.LogInformation(
                "Created topology projection {ProjectionName} with {NodeCount} nodes and {RelationshipCount} relationships.",
                options.GraphProjectionName,
                projectionRecord["nodeCount"].As<long>(),
                projectionRecord["relationshipCount"].As<long>());

            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteAsync(session, WriteDegreeCypher, parameters, cancellationToken);

            var connectedComponentsRecord = await ExecuteSingleRecordAsync(
                session,
                WriteWeaklyConnectedComponentsCypher,
                parameters,
                cancellationToken);

            _logger.LogInformation(
                "Topology analysis projection {ProjectionName} identified {ComponentCount} connected components.",
                options.GraphProjectionName,
                connectedComponentsRecord["componentCount"].As<long>());

            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteAsync(session, WriteArticulationPointsCypher, parameters, cancellationToken);
        }
        catch (Exception exception)
        {
            analysisException = exception;
            _logger.LogError(
                exception,
                "Topology analysis run failed for projection {ProjectionName}.",
                options.GraphProjectionName);
            throw;
        }
        finally
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteAsync(session, DropProjectionCypher, parameters, cancellationToken);
            }
            catch (Exception dropException)
            {
                _logger.LogError(
                    dropException,
                    "Dropping topology projection {ProjectionName} failed.",
                    options.GraphProjectionName);

                if (analysisException is null)
                {
                    throw;
                }
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "Topology analysis run for projection {ProjectionName} completed in {ElapsedMilliseconds} ms.",
                    options.GraphProjectionName,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }

    private static IReadOnlyDictionary<string, object> CreateProjectionParameters(string projectionName)
    {
        return new Dictionary<string, object>
        {
            ["projectionName"] = projectionName
        };
    }

    private static async Task ExecuteAsync(
        IAsyncSession session,
        string cypher,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cursor = await session.RunAsync(cypher, parameters);
        cancellationToken.ThrowIfCancellationRequested();
        await cursor.ConsumeAsync();
    }

    private static async Task<IRecord> ExecuteSingleRecordAsync(
        IAsyncSession session,
        string cypher,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cursor = await session.RunAsync(cypher, parameters);
        cancellationToken.ThrowIfCancellationRequested();

        if (!await cursor.FetchAsync())
        {
            throw new InvalidOperationException("Expected a Neo4j result record but the query returned none.");
        }

        var record = cursor.Current;
        cancellationToken.ThrowIfCancellationRequested();
        await cursor.ConsumeAsync();
        return record;
    }
}
