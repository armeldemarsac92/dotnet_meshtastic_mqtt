using System.Text;
using MeshBoard.RealtimeLoadTests.Configuration;

namespace MeshBoard.RealtimeLoadTests.Load;

public sealed record RealtimeLoadRunSummary(
    RealtimeLoadScenario Scenario,
    int ClientCount,
    int MaxConcurrency,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    IReadOnlyList<RealtimeLoadMetricSummary> Metrics)
{
    public TimeSpan Duration => FinishedAtUtc - StartedAtUtc;

    public static RealtimeLoadRunSummary Create(
        RealtimeLoadScenario scenario,
        RealtimeLoadTestOptions options,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        IReadOnlyCollection<RealtimeLoadSample> samples)
    {
        var metrics = samples
            .GroupBy(sample => sample.Operation, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(BuildMetric)
            .ToArray();

        return new RealtimeLoadRunSummary(
            scenario,
            options.ClientCount,
            options.MaxConcurrency,
            startedAtUtc,
            finishedAtUtc,
            metrics);
    }

    public string ToConsoleText(string reportPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Realtime load scenario: {Scenario.ToConfigValue()}");
        builder.AppendLine($"Clients: {ClientCount}; max concurrency: {MaxConcurrency}; duration: {Duration}");

        foreach (var metric in Metrics)
        {
            builder.AppendLine(
                $"- {metric.Operation}: attempted={metric.Attempted}, succeeded={metric.Succeeded}, failed={metric.Failed}, successRate={metric.SuccessRate:P1}, meanMs={metric.MeanLatencyMs:F1}, p95Ms={metric.P95LatencyMs:F1}, maxMs={metric.MaxLatencyMs:F1}");

            foreach (var failureReason in metric.FailureReasons)
            {
                builder.AppendLine($"  failure: {failureReason}");
            }
        }

        builder.AppendLine($"Report: {reportPath}");
        return builder.ToString();
    }

    private static RealtimeLoadMetricSummary BuildMetric(IGrouping<string, RealtimeLoadSample> group)
    {
        var attempts = group.Count();
        var succeeded = group.Count(sample => sample.IsSuccess);
        var failed = attempts - succeeded;
        var latencies = group.Select(sample => sample.DurationMs).OrderBy(value => value).ToArray();
        var failureReasons = group
            .Where(sample => !sample.IsSuccess && !string.IsNullOrWhiteSpace(sample.FailureReason))
            .Select(sample => sample.FailureReason!)
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToArray();

        return new RealtimeLoadMetricSummary(
            group.Key,
            attempts,
            succeeded,
            failed,
            latencies.Length == 0 ? 0 : latencies[0],
            latencies.Length == 0 ? 0 : latencies.Average(),
            latencies.Length == 0 ? 0 : latencies[^1],
            GetPercentile(latencies, 0.50),
            GetPercentile(latencies, 0.95),
            GetPercentile(latencies, 0.99),
            failureReasons);
    }

    private static double GetPercentile(double[] orderedValues, double percentile)
    {
        if (orderedValues.Length == 0)
        {
            return 0;
        }

        if (orderedValues.Length == 1)
        {
            return orderedValues[0];
        }

        var index = (int)Math.Ceiling(percentile * orderedValues.Length) - 1;
        return orderedValues[Math.Clamp(index, 0, orderedValues.Length - 1)];
    }
}
