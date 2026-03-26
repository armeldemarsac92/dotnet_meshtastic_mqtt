namespace MeshBoard.Application.Abstractions.Authentication;

public interface ICurrentUserContextAccessor
{
    string GetUserId();
}
