using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Requests;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class NeighborLinkRepository : INeighborLinkRepository
{
    private readonly IDbContext _dbContext;

    public NeighborLinkRepository(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertAsync(
        string workspaceId,
        IReadOnlyList<NeighborLinkRecord> links,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || links.Count == 0)
        {
            return;
        }

        var normalizedWorkspaceId = workspaceId.Trim();

        foreach (var link in CanonicalizeLinks(links))
        {
            await _dbContext.ExecuteAsync(
                NeighborLinkQueries.UpsertNeighborLink,
                new UpsertNeighborLinkSqlRequest
                {
                    WorkspaceId = normalizedWorkspaceId,
                    SourceNodeId = link.SourceNodeId,
                    TargetNodeId = link.TargetNodeId,
                    SnrDb = link.SnrDb,
                    LastSeenAtUtc = link.LastSeenAtUtc.ToString("O")
                },
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<NeighborLinkRecord>> GetActiveLinksAsync(
        string workspaceId,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return [];
        }

        var responses = await _dbContext.QueryAsync<NeighborLinkSqlResponse>(
            NeighborLinkQueries.SelectActiveNeighborLinks,
            new
            {
                WorkspaceId = workspaceId.Trim(),
                NotBeforeUtc = notBeforeUtc.ToString("O")
            },
            cancellationToken);

        return responses
            .Select(response => new NeighborLinkRecord
            {
                SourceNodeId = response.SourceNodeId,
                TargetNodeId = response.TargetNodeId,
                SnrDb = response.SnrDb,
                LastSeenAtUtc = DateTimeOffset.Parse(response.LastSeenAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind)
            })
            .ToArray();
    }

    private static IReadOnlyList<NeighborLinkRecord> CanonicalizeLinks(IReadOnlyList<NeighborLinkRecord> links)
    {
        var canonicalLinks = new Dictionary<string, NeighborLinkRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            if (string.IsNullOrWhiteSpace(link.SourceNodeId) ||
                string.IsNullOrWhiteSpace(link.TargetNodeId) ||
                string.Equals(link.SourceNodeId, link.TargetNodeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sourceNodeId = link.SourceNodeId.Trim();
            var targetNodeId = link.TargetNodeId.Trim();
            var sourceFirst = StringComparer.OrdinalIgnoreCase.Compare(sourceNodeId, targetNodeId) <= 0;
            var canonical = new NeighborLinkRecord
            {
                SourceNodeId = sourceFirst ? sourceNodeId : targetNodeId,
                TargetNodeId = sourceFirst ? targetNodeId : sourceNodeId,
                SnrDb = link.SnrDb,
                LastSeenAtUtc = link.LastSeenAtUtc
            };
            var key = $"{canonical.SourceNodeId}|{canonical.TargetNodeId}";

            if (!canonicalLinks.TryGetValue(key, out var existing))
            {
                canonicalLinks[key] = canonical;
                continue;
            }

            var isIncomingLatest = canonical.LastSeenAtUtc >= existing.LastSeenAtUtc;
            canonicalLinks[key] = new NeighborLinkRecord
            {
                SourceNodeId = existing.SourceNodeId,
                TargetNodeId = existing.TargetNodeId,
                SnrDb = isIncomingLatest
                    ? canonical.SnrDb ?? existing.SnrDb
                    : existing.SnrDb ?? canonical.SnrDb,
                LastSeenAtUtc = isIncomingLatest
                    ? canonical.LastSeenAtUtc
                    : existing.LastSeenAtUtc
            };
        }

        return canonicalLinks.Values.ToArray();
    }
}
