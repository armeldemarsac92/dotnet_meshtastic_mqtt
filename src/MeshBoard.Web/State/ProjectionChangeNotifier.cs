using MeshBoard.Contracts.Realtime;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Web.State;

public sealed class ProjectionChangeNotifier
{
    private readonly ILogger<ProjectionChangeNotifier> _logger;

    public ProjectionChangeNotifier(ILogger<ProjectionChangeNotifier> logger)
    {
        _logger = logger;
    }

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
            try
            {
                await handler(projectionChange);
            }
            catch (ObjectDisposedException)
            {
                _logger.LogDebug(
                    "Skipped projection change handler because the target circuit was already disposed.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug(
                    "Skipped projection change handler because the target circuit cancellation was already requested.");
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Projection change handler failed while dispatching {ProjectionChangeKind} for workspace {WorkspaceId}.",
                    projectionChange.Kind,
                    projectionChange.WorkspaceId);
            }
        }
    }
}
