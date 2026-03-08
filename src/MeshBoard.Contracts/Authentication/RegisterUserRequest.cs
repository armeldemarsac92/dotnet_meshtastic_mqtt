namespace MeshBoard.Contracts.Authentication;

public sealed class RegisterUserRequest
{
    public required string Username { get; set; }

    public required string Password { get; set; }
}
