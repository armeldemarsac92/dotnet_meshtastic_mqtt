namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class WorkspaceRuntimeStatusSqlResponse
{
    public string WorkspaceId { get; set; } = string.Empty;

    public string? ActiveServerProfileId { get; set; }

    public string? ActiveServerName { get; set; }

    public string? ActiveServerAddress { get; set; }

    public int IsConnected { get; set; }

    public string? LastStatusMessage { get; set; }

    public string TopicFiltersJson { get; set; } = "[]";

    public string UpdatedAtUtc { get; set; } = string.Empty;
}
