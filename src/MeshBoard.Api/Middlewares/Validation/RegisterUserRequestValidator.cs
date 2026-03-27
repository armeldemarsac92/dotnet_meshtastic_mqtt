using System.Text.RegularExpressions;
using FluentValidation;
using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Api.Middlewares.Validation;

internal sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    private const int MaximumUsernameLength = 32;
    private const int MinimumPasswordLength = 8;
    private const int MaximumPasswordLength = 128;
    private const string UsernamePattern = "^[A-Za-z0-9](?:[A-Za-z0-9._-]{1,30}[A-Za-z0-9])?$";

    public RegisterUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .Must(HasUsernameValue)
            .WithMessage("Username is required.")
            .Must(HasValidUsernameLength)
            .WithMessage($"Username must be between 3 and {MaximumUsernameLength} characters.")
            .Must(HasValidUsernameFormat)
            .WithMessage("Username may contain letters, numbers, '.', '-', and '_', and must start and end with a letter or number.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(MinimumPasswordLength)
            .MaximumLength(MaximumPasswordLength);
    }

    private static bool HasUsernameValue(string? username)
    {
        return !string.IsNullOrWhiteSpace(username);
    }

    private static bool HasValidUsernameLength(string? username)
    {
        var trimmedUsername = username?.Trim();
        return trimmedUsername is not null && trimmedUsername.Length is >= 3 and <= MaximumUsernameLength;
    }

    private static bool HasValidUsernameFormat(string? username)
    {
        var trimmedUsername = username?.Trim();
        return !string.IsNullOrEmpty(trimmedUsername)
               && Regex.IsMatch(trimmedUsername, UsernamePattern, RegexOptions.CultureInvariant);
    }
}
