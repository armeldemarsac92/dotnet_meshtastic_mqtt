using System.Globalization;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.SQL.Requests;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class TopicPresetMapping
{
    public static UpsertTopicPresetSqlRequest ToSqlRequest(this SaveTopicPresetRequest request)
    {
        return new UpsertTopicPresetSqlRequest
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            TopicPattern = request.TopicPattern,
            IsDefault = request.IsDefault ? 1 : 0,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };
    }

    public static TopicPreset MapToTopicPreset(this TopicPresetSqlResponse response)
    {
        return new TopicPreset
        {
            Id = Guid.Parse(response.Id),
            Name = response.Name,
            TopicPattern = response.TopicPattern,
            IsDefault = response.IsDefault == 1,
            CreatedAtUtc = DateTimeOffset.Parse(response.CreatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }
}
