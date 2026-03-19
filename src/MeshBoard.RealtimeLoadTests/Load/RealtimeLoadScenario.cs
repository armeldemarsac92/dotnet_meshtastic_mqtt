namespace MeshBoard.RealtimeLoadTests.Load;

public enum RealtimeLoadScenario
{
    ConnectBurst = 1,
    ReconnectStorm = 2
}

public static class RealtimeLoadScenarioExtensions
{
    public static RealtimeLoadScenario Parse(string rawValue)
    {
        return rawValue.Trim().ToLowerInvariant() switch
        {
            "connect-burst" => RealtimeLoadScenario.ConnectBurst,
            "reconnect-storm" => RealtimeLoadScenario.ReconnectStorm,
            _ => throw new InvalidOperationException(
                $"RealtimeLoadTests:Scenario '{rawValue}' is unsupported. Use 'connect-burst' or 'reconnect-storm'.")
        };
    }

    public static string ToConfigValue(this RealtimeLoadScenario scenario)
    {
        return scenario switch
        {
            RealtimeLoadScenario.ConnectBurst => "connect-burst",
            RealtimeLoadScenario.ReconnectStorm => "reconnect-storm",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };
    }
}
