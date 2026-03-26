namespace MeshBoard.Collector.TopologyAnalyst.Configuration;

public sealed class TopologyAnalysisOptions
{
    public const string SectionName = "TopologyAnalysis";

    public int ScheduleIntervalSeconds { get; set; } = 300;

    public string GraphProjectionName { get; set; } = "topologyProjection";
}
