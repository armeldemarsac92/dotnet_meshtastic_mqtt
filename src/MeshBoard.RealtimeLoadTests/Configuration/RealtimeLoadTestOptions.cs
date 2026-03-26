using MeshBoard.RealtimeLoadTests.Load;

namespace MeshBoard.RealtimeLoadTests.Configuration;

public sealed class RealtimeLoadTestOptions
{
    public const string SectionName = "RealtimeLoadTests";

    public string ApiBaseUrl { get; set; } = "https://localhost:7281";

    public string Username { get; set; } = "meshboard-loadtest";

    public string Password { get; set; } = "password-123";

    public bool AutoRegisterIfMissing { get; set; } = true;

    public string Scenario { get; set; } = RealtimeLoadScenario.ConnectBurst.ToConfigValue();

    public int ClientCount { get; set; } = 25;

    public int MaxConcurrency { get; set; } = 10;

    public int HoldDurationSeconds { get; set; } = 15;

    public int ReconnectIterations { get; set; } = 3;

    public int DelayBetweenReconnectMilliseconds { get; set; } = 250;

    public int ConnectTimeoutSeconds { get; set; } = 15;

    public string? TopicFilterOverride { get; set; }

    public string ReportOutputDirectory { get; set; } = "artifacts/realtime-load-tests";

    public bool AllowInsecureTls { get; set; }

    public Uri GetApiBaseUri()
    {
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("RealtimeLoadTests:ApiBaseUrl must be an absolute URI.");
        }

        return uri;
    }

    public RealtimeLoadScenario GetScenario()
    {
        return RealtimeLoadScenarioExtensions.Parse(Scenario);
    }

    public void Validate()
    {
        _ = GetApiBaseUri();
        _ = GetScenario();

        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException("RealtimeLoadTests:Username is required.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("RealtimeLoadTests:Password is required.");
        }

        if (ClientCount <= 0)
        {
            throw new InvalidOperationException("RealtimeLoadTests:ClientCount must be greater than zero.");
        }

        if (MaxConcurrency <= 0)
        {
            throw new InvalidOperationException("RealtimeLoadTests:MaxConcurrency must be greater than zero.");
        }

        if (ConnectTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("RealtimeLoadTests:ConnectTimeoutSeconds must be greater than zero.");
        }

        if (ReconnectIterations <= 0)
        {
            throw new InvalidOperationException("RealtimeLoadTests:ReconnectIterations must be greater than zero.");
        }

        if (HoldDurationSeconds < 0)
        {
            throw new InvalidOperationException("RealtimeLoadTests:HoldDurationSeconds must not be negative.");
        }

        if (DelayBetweenReconnectMilliseconds < 0)
        {
            throw new InvalidOperationException("RealtimeLoadTests:DelayBetweenReconnectMilliseconds must not be negative.");
        }

        if (string.IsNullOrWhiteSpace(ReportOutputDirectory))
        {
            throw new InvalidOperationException("RealtimeLoadTests:ReportOutputDirectory is required.");
        }
    }
}
