using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Client.Authentication;

public sealed class AuthSessionState
{
    public event Action? Changed;

    public AuthenticatedUserResponse? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public void Clear()
    {
        SetCurrentUser(null);
    }

    public void SetCurrentUser(AuthenticatedUserResponse? user)
    {
        CurrentUser = user;
        Changed?.Invoke();
    }
}
