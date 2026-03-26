using System.Security.Claims;
using MeshBoard.Application.Abstractions.Authentication;

namespace MeshBoard.Api.Authentication;

public sealed class HttpContextUserContextAccessor : ICurrentUserContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextUserContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetUserId()
    {
        return _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            throw new InvalidOperationException("No authenticated user context is available for the current request.");
    }
}
