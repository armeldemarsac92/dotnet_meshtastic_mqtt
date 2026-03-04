using System.Globalization;
using MeshBoard.Contracts.Favorites;
using MeshBoard.Infrastructure.Persistence.SQL.Requests;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class FavoriteNodeMapping
{
    public static UpsertFavoriteNodeSqlRequest ToSqlRequest(this SaveFavoriteNodeRequest request)
    {
        return new UpsertFavoriteNodeSqlRequest
        {
            Id = Guid.NewGuid().ToString(),
            NodeId = request.NodeId,
            ShortName = request.ShortName,
            LongName = request.LongName,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };
    }

    public static FavoriteNode MapToFavoriteNode(this FavoriteNodeSqlResponse response)
    {
        return new FavoriteNode
        {
            Id = Guid.Parse(response.Id),
            NodeId = response.NodeId,
            ShortName = response.ShortName,
            LongName = response.LongName,
            CreatedAtUtc = DateTimeOffset.Parse(response.CreatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }
}
