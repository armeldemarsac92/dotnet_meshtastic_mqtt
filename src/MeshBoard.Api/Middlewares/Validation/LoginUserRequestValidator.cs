using FluentValidation;
using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Api.Middlewares.Validation;

internal sealed class LoginUserRequestValidator : AbstractValidator<LoginUserRequest>
{
    public LoginUserRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
