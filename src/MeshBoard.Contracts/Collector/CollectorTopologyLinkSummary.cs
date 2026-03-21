namespace MeshBoard.Contracts.Collector;

public sealed class CollectorTopologyLinkSummary
{
    public string SourceNodeId { get; set; } = string.Empty;

    public string TargetNodeId { get; set; } = string.Empty;

    public string? SourceShortName { get; set; }

    public string? SourceLongName { get; set; }

    public string? TargetShortName { get; set; }

    public string? TargetLongName { get; set; }

    public int ObservationCount { get; set; }

    public float? AverageSnrDb { get; set; }

    public float? MaxSnrDb { get; set; }

    public float? LastSnrDb { get; set; }
}
