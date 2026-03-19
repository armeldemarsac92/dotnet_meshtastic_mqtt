namespace MeshBoard.RealtimeLoadTests.Load;

public sealed record RealtimeLoadMetricSummary(
    string Operation,
    int Attempted,
    int Succeeded,
    int Failed,
    double MinLatencyMs,
    double MeanLatencyMs,
    double MaxLatencyMs,
    double P50LatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    IReadOnlyList<string> FailureReasons)
{
    public double SuccessRate => Attempted == 0 ? 0 : (double)Succeeded / Attempted;
}
