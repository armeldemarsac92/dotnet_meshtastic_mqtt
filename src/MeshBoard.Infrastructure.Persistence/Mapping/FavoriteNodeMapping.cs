using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MeshBoard.Contracts.Favorites;
using MeshBoard.Infrastructure.Persistence.SQL.Requests;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class FavoriteNodeMapping
{
    public static UpsertFavoriteNodeSqlRequest ToSqlRequest(
        this SaveFavoriteNodeRequest request,
        string workspaceId)
    {
        return new UpsertFavoriteNodeSqlRequest
        {
            Id = Guid.NewGuid().ToString(),
            WorkspaceId = workspaceId,
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
            Id = ParseOrDeriveGuid(response.Id),
            NodeId = response.NodeId,
            ShortName = response.ShortName,
            LongName = response.LongName,
            CreatedAtUtc = ParseOrDefault(response.CreatedAtUtc)
        };
    }

    private static Guid ParseOrDeriveGuid(string? value)
    {
        if (Guid.TryParse(value, out var parsedGuid))
        {
            return parsedGuid;
        }

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return new Guid(hash);
    }

    private static DateTimeOffset ParseOrDefault(string value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedValue)
            ? parsedValue
            : DateTimeOffset.UnixEpoch;
    }
}
