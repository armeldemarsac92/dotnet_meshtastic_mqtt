namespace MeshBoard.Contracts.Authentication;

public sealed class LoginUserRequest
{
    public required string Username { get; set; }

    public required string Password { get; set; }
}
