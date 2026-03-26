using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MeshBoard.Collector.Ingress.Observability;

internal sealed class IngressPublisherHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = IngressObservability.CreateHealthSnapshot();
        var data = new Dictionary<string, object>
        {
            ["consecutive_failure_count"] = snapshot.ConsecutiveFailureCount,
            ["last_lag_ms"] = snapshot.LastLagMilliseconds,
            ["last_success_at_utc"] = snapshot.LastSuccessAtUtc?.ToString("O") ?? string.Empty,
            ["last_failure_at_utc"] = snapshot.LastFailureAtUtc?.ToString("O") ?? string.Empty,
            ["last_failure_topic"] = snapshot.LastFailureTopic
        };

        var result = snapshot.ConsecutiveFailureCount > 0
            ? new HealthCheckResult(
                HealthStatus.Degraded,
                "Recent Kafka raw packet publish failures were detected.",
                null,
                data)
            : new HealthCheckResult(
                HealthStatus.Healthy,
                "Kafka raw packet publisher is healthy.",
                null,
                data);

        return Task.FromResult(result);
    }
}
