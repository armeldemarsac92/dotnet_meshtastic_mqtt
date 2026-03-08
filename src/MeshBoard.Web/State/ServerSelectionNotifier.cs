namespace MeshBoard.Web.State;

public sealed class ServerSelectionNotifier
{
    public event Func<string, Task>? Changed;

    public async Task NotifyChangedAsync(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        var handlers = Changed;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<string, Task>>())
        {
            await handler(workspaceId);
        }
    }
}
