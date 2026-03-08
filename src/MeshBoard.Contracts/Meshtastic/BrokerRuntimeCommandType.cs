namespace MeshBoard.Contracts.Meshtastic;

public enum BrokerRuntimeCommandType
{
    EnsureConnected = 1,
    ReconcileActiveProfile = 2,
    ResetAndReconnectActiveProfile = 3,
    Publish = 4,
    SubscribeEphemeral = 5,
    UnsubscribeEphemeral = 6
}
