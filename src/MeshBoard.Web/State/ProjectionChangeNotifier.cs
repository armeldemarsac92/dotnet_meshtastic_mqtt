using MeshBoard.Contracts.Realtime;

namespace MeshBoard.Web.State;

public sealed class ProjectionChangeNotifier
{
    public event Func<ProjectionChangeEvent, Task>? Changed;

    public async Task NotifyChangedAsync(ProjectionChangeEvent projectionChange)
    {
        ArgumentNullException.ThrowIfNull(projectionChange);

        var handlers = Changed;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<ProjectionChangeEvent, Task>>())
        {
            await handler(projectionChange);
        }
    }
}
