namespace MeshBoard.Client.Services;

public sealed class ViewModePreferenceService
{
    public bool NodesHistoryMode { get; private set; }
    public bool ChannelsHistoryMode { get; private set; }

    public event Action? Changed;

    public void SetNodesMode(bool historyMode)
    {
        NodesHistoryMode = historyMode;
        Changed?.Invoke();
    }

    public void SetChannelsMode(bool historyMode)
    {
        ChannelsHistoryMode = historyMode;
        Changed?.Invoke();
    }
}
