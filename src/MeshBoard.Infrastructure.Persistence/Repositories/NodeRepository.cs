using System.Text;
using Dapper;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class NodeRepository : INodeRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<NodeRepository> _logger;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public NodeRepository(
        IDbContext dbContext,
        IWorkspaceContextAccessor workspaceContextAccessor,
        ILogger<NodeRepository> logger)
    {
        _dbContext = dbContext;
        _workspaceContextAccessor = workspaceContextAccessor;
        _logger = logger;
    }

    public Task<int> CountAsync(NodeQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to count nodes");

        return _dbContext.QueryFirstOrDefaultAsync<int>(
            NodeQueries.CountNodes,
            CreateQueryParameters(query, _workspaceContextAccessor.GetWorkspaceId()),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<NodeSummary>> GetPageAsync(
        NodeQuery query,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to fetch nodes with offset {Offset} and take {Take}", offset, take);

        var sqlBuilder = new StringBuilder(NodeQueries.SelectNodes)
            .AppendLine()
            .AppendLine(GetOrderByClause(query.SortBy))
            .AppendLine("LIMIT @Take OFFSET @Offset;");

        var parameters = CreateQueryParameters(query, _workspaceContextAccessor.GetWorkspaceId());
        parameters.Add("Take", take);
        parameters.Add("Offset", offset);

        var sqlResponses = await _dbContext.QueryAsync<NodeSqlResponse>(
            sqlBuilder.ToString(),
            parameters,
            cancellationToken);

        return sqlResponses.MapToNodes();
    }

    public async Task UpsertAsync(UpsertObservedNodeRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to upsert observed node: {NodeId}", request.NodeId);

        await _dbContext.ExecuteAsync(
            NodeQueries.UpsertNode,
            request.ToSqlRequest(),
            cancellationToken);
    }

    private static DynamicParameters CreateQueryParameters(NodeQuery query, string workspaceId)
    {
        var parameters = new DynamicParameters();
        var normalizedSearchText = query.SearchText.Trim();

        parameters.Add("WorkspaceId", workspaceId);
        parameters.Add("SearchText", normalizedSearchText);
        parameters.Add("SearchPattern", $"%{normalizedSearchText}%");
        parameters.Add("OnlyFavorites", query.OnlyFavorites ? 1 : 0);
        parameters.Add("OnlyWithLocation", query.OnlyWithLocation ? 1 : 0);
        parameters.Add("OnlyWithTelemetry", query.OnlyWithTelemetry ? 1 : 0);

        return parameters;
    }

    private static string GetOrderByClause(NodeSortOption sortBy)
    {
        return sortBy switch
        {
            NodeSortOption.NameAsc =>
                """
                ORDER BY COALESCE(n.long_name, n.short_name, n.node_id) COLLATE NOCASE ASC,
                         n.node_id COLLATE NOCASE ASC
                """,
            NodeSortOption.BatteryDesc =>
                """
                ORDER BY COALESCE(n.battery_level_percent, -1) DESC,
                         COALESCE(n.long_name, n.short_name, n.node_id) COLLATE NOCASE ASC
                """,
            _ =>
                """
                ORDER BY COALESCE(n.last_heard_at_utc, '') DESC,
                         COALESCE(n.long_name, n.short_name, n.node_id) COLLATE NOCASE ASC
                """
        };
    }
}
