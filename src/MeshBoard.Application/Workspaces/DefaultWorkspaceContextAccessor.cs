using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Workspaces;

namespace MeshBoard.Application.Workspaces;

internal sealed class DefaultWorkspaceContextAccessor : IWorkspaceContextAccessor
{
    public string GetWorkspaceId()
    {
        return WorkspaceConstants.DefaultWorkspaceId;
    }
}
