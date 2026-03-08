namespace MeshBoard.Contracts.Diagnostics;

public sealed class ActiveCircuitMetricSnapshot
{
    public int ActiveCircuitCount { get; set; }

    public int PeakActiveCircuitCount { get; set; }

    public long OpenedCircuitCount { get; set; }

    public long ClosedCircuitCount { get; set; }

    public DateTimeOffset? LastChangedAtUtc { get; set; }
}
