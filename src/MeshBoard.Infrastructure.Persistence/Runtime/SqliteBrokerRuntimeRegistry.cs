using System.Text.Json;
using Dapper;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Realtime;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Persistence.Runtime;

internal sealed class SqliteBrokerRuntimeRegistry : IBrokerRuntimeRegistry
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _connectionString;
    private readonly BrokerRuntimeSnapshot _defaultSnapshot;
    private readonly ILogger<SqliteBrokerRuntimeRegistry> _logger;

    public SqliteBrokerRuntimeRegistry(
        IOptions<BrokerOptions> brokerOptions,
        IOptions<PersistenceOptions> persistenceOptions,
        ILogger<SqliteBrokerRuntimeRegistry> logger)
    {
        var broker = brokerOptions.Value;
        var persistence = persistenceOptions.Value;

        if (string.IsNullOrWhiteSpace(persistence.ConnectionString))
        {
            throw new InvalidOperationException("The persistence connection string is not configured.");
        }

        _connectionString = persistence.ConnectionString;
        _logger = logger;
        _defaultSnapshot = new BrokerRuntimeSnapshot
        {
            ActiveServerName = "Default server",
            ActiveServerAddress = $"{broker.Host}:{broker.Port}",
            IsConnected = false,
            TopicFilters = []
        };
    }

    public BrokerRuntimeSnapshot GetSnapshot(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        using var connection = CreateConnection();
        connection.Open();

        var response = connection.QueryFirstOrDefault<WorkspaceRuntimeStatusSqlResponse>(
            WorkspaceRuntimeStatusQueries.GetByWorkspaceId,
            new { WorkspaceId = workspaceId });

        return response is null ? Clone(_defaultSnapshot) : Map(response);
    }

    public void UpdateSnapshot(string workspaceId, BrokerRuntimeSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(snapshot);

        using var connection = CreateConnection();
        connection.Open();

        var normalizedTopicFilters = snapshot.TopicFilters
            .Where(filter => !string.IsNullOrWhiteSpace(filter))
            .Select(filter => filter.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(filter => filter, StringComparer.Ordinal)
            .ToList();

        connection.Execute(
            WorkspaceRuntimeStatusQueries.Upsert,
            new
            {
                WorkspaceId = workspaceId,
                ActiveServerProfileId = snapshot.ActiveServerProfileId?.ToString(),
                ActiveServerName = snapshot.ActiveServerName,
                ActiveServerAddress = snapshot.ActiveServerAddress,
                IsConnected = snapshot.IsConnected ? 1 : 0,
                LastStatusMessage = snapshot.LastStatusMessage,
                TopicFiltersJson = JsonSerializer.Serialize(normalizedTopicFilters, JsonSerializerOptions),
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });

        connection.Execute(
            ProjectionChangeQueries.Insert,
            new
            {
                WorkspaceId = workspaceId,
                ChangeKind = ProjectionChangeKind.RuntimeStatusChanged.ToString(),
                EntityKey = (string?)null,
                OccurredAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    public RuntimePipelineSnapshot GetPipelineSnapshot(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        using var connection = CreateConnection();
        connection.Open();

        var response = connection.QueryFirstOrDefault<RuntimePipelineStatusSqlResponse>(
            RuntimePipelineStatusQueries.GetByWorkspaceId,
            new { WorkspaceId = workspaceId });

        if (response is null)
        {
            return new RuntimePipelineSnapshot();
        }

        return new RuntimePipelineSnapshot
        {
            InboundQueueCapacity = response.InboundQueueCapacity,
            InboundWorkerCount = response.InboundWorkerCount,
            InboundQueueDepth = response.InboundQueueDepth,
            InboundOldestMessageAgeMilliseconds = response.InboundOldestMessageAgeMilliseconds,
            InboundEnqueuedCount = response.InboundEnqueuedCount,
            InboundDequeuedCount = response.InboundDequeuedCount,
            InboundDroppedCount = response.InboundDroppedCount,
            UpdatedAtUtc = string.IsNullOrWhiteSpace(response.UpdatedAtUtc)
                ? null
                : DateTimeOffset.Parse(response.UpdatedAtUtc)
        };
    }

    public void UpdatePipelineSnapshot(string workspaceId, RuntimePipelineSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(snapshot);

        using var connection = CreateConnection();
        connection.Open();

        connection.Execute(
            RuntimePipelineStatusQueries.Upsert,
            new
            {
                WorkspaceId = workspaceId,
                snapshot.InboundQueueCapacity,
                snapshot.InboundWorkerCount,
                snapshot.InboundQueueDepth,
                snapshot.InboundOldestMessageAgeMilliseconds,
                snapshot.InboundEnqueuedCount,
                snapshot.InboundDequeuedCount,
                snapshot.InboundDroppedCount,
                UpdatedAtUtc = (snapshot.UpdatedAtUtc ?? DateTimeOffset.UtcNow).ToString("O")
            });
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private BrokerRuntimeSnapshot Map(WorkspaceRuntimeStatusSqlResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.ActiveServerProfileId) &&
            !Guid.TryParse(response.ActiveServerProfileId, out _))
        {
            _logger.LogWarning(
                "Ignoring invalid active server profile ID '{ActiveServerProfileId}' for workspace runtime snapshot {WorkspaceId}",
                response.ActiveServerProfileId,
                response.WorkspaceId);
        }

        Guid? activeServerProfileId = null;
        if (!string.IsNullOrWhiteSpace(response.ActiveServerProfileId) &&
            Guid.TryParse(response.ActiveServerProfileId, out var parsedId))
        {
            activeServerProfileId = parsedId;
        }

        return new BrokerRuntimeSnapshot
        {
            ActiveServerProfileId = activeServerProfileId,
            ActiveServerName = response.ActiveServerName ?? _defaultSnapshot.ActiveServerName,
            ActiveServerAddress = response.ActiveServerAddress ?? _defaultSnapshot.ActiveServerAddress,
            IsConnected = response.IsConnected != 0,
            LastStatusMessage = response.LastStatusMessage,
            TopicFilters = DeserializeTopicFilters(response.TopicFiltersJson)
        };
    }

    private List<string> DeserializeTopicFilters(string? topicFiltersJson)
    {
        if (string.IsNullOrWhiteSpace(topicFiltersJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(topicFiltersJson, JsonSerializerOptions) ?? [];
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Failed to deserialize runtime topic filters from persisted workspace runtime status.");
            return [];
        }
    }

    private static BrokerRuntimeSnapshot Clone(BrokerRuntimeSnapshot snapshot)
    {
        return new BrokerRuntimeSnapshot
        {
            ActiveServerProfileId = snapshot.ActiveServerProfileId,
            ActiveServerName = snapshot.ActiveServerName,
            ActiveServerAddress = snapshot.ActiveServerAddress,
            IsConnected = snapshot.IsConnected,
            LastStatusMessage = snapshot.LastStatusMessage,
            TopicFilters = [..snapshot.TopicFilters]
        };
    }
}
