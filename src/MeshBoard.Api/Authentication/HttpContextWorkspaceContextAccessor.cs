using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Api.Authentication;

public sealed class HttpContextWorkspaceContextAccessor : IWorkspaceContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextWorkspaceContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetWorkspaceId()
    {
        return WorkspacePrincipalResolver.ResolveWorkspaceId(_httpContextAccessor.HttpContext?.User) ??
            throw new InvalidOperationException("No authenticated workspace context is available for the current request.");
    }
}
