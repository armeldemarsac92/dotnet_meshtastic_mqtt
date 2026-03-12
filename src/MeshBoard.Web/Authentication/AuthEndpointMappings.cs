using System.Security.Claims;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace MeshBoard.Web.Authentication;

internal static class AuthEndpointMappings
{
    public static IEndpointRouteBuilder MapUserAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/auth/login",
                async Task<IResult> (
                    HttpContext httpContext,
                    [FromForm] LoginFormModel form,
                    IUserAccountService userAccountService,
                    CancellationToken cancellationToken) =>
                {
                    var user = await userAccountService.ValidateCredentialsAsync(
                        form.Username ?? string.Empty,
                        form.Password ?? string.Empty,
                        cancellationToken);

                    if (user is null)
                    {
                        return Results.LocalRedirect(
                            BuildLoginRedirect(
                                form.ReturnUrl,
                                "Invalid username or password.",
                                form.Username));
                    }

                    await httpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        CreatePrincipal(user),
                        CreateAuthenticationProperties());

                    return Results.LocalRedirect(ToSafeReturnUrl(form.ReturnUrl));
                })
            .DisableAntiforgery();

        endpoints.MapPost(
                "/auth/register",
                async Task<IResult> (
                    HttpContext httpContext,
                    [FromForm] RegisterFormModel form,
                    IUserAccountService userAccountService,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var user = await userAccountService.RegisterAsync(
                            new RegisterUserRequest
                            {
                                Username = form.Username ?? string.Empty,
                                Password = form.Password ?? string.Empty
                            },
                            cancellationToken);

                        await httpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            CreatePrincipal(user),
                            CreateAuthenticationProperties());

                        return Results.LocalRedirect(ToSafeReturnUrl(form.ReturnUrl));
                    }
                    catch (BadRequestException exception)
                    {
                        return Results.LocalRedirect(
                            BuildRegisterRedirect(form.ReturnUrl, exception.Message, form.Username));
                    }
                    catch (ConflictException exception)
                    {
                        return Results.LocalRedirect(
                            BuildRegisterRedirect(form.ReturnUrl, exception.Message, form.Username));
                    }
                })
            .DisableAntiforgery();

        endpoints.MapPost(
                "/auth/logout",
                async Task<IResult> (
                    HttpContext httpContext,
                    [FromForm] LogoutFormModel form) =>
                {
                    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return Results.LocalRedirect(ToSafeReturnUrl(form.ReturnUrl, "/login"));
                })
            .DisableAntiforgery();

        return endpoints;
    }

    private static ClaimsPrincipal CreatePrincipal(AppUser user)
    {
        return AppUserPrincipalFactory.CreatePrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static AuthenticationProperties CreateAuthenticationProperties()
    {
        return new AuthenticationProperties
        {
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14),
            IsPersistent = true
        };
    }

    private static string BuildLoginRedirect(string? returnUrl, string error, string? username)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["error"] = error
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            parameters["username"] = username.Trim();
        }

        var safeReturnUrl = ToSafeReturnUrl(returnUrl);
        if (!string.Equals(safeReturnUrl, "/", StringComparison.Ordinal))
        {
            parameters["returnUrl"] = safeReturnUrl;
        }

        return BuildLocalUrl("/login", parameters);
    }

    private static string BuildRegisterRedirect(string? returnUrl, string error, string? username)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["error"] = error
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            parameters["username"] = username.Trim();
        }

        var safeReturnUrl = ToSafeReturnUrl(returnUrl);
        if (!string.Equals(safeReturnUrl, "/", StringComparison.Ordinal))
        {
            parameters["returnUrl"] = safeReturnUrl;
        }

        return BuildLocalUrl("/register", parameters);
    }

    internal static string ToSafeReturnUrl(string? returnUrl, string fallback = "/")
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return fallback;
        }

        var trimmedReturnUrl = returnUrl.Trim();
        if (!trimmedReturnUrl.StartsWith("/", StringComparison.Ordinal) ||
            trimmedReturnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return fallback;
        }

        return trimmedReturnUrl;
    }

    private static string BuildLocalUrl(string path, IReadOnlyDictionary<string, string?> parameters)
    {
        var query = QueryString.Create(
            parameters
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                .Select(entry => new KeyValuePair<string, string?>(entry.Key, entry.Value)));

        return $"{path}{query}";
    }

    internal sealed class LoginFormModel
    {
        public string? Password { get; set; }

        public string? ReturnUrl { get; set; }

        public string? Username { get; set; }
    }

    internal sealed class LogoutFormModel
    {
        public string? ReturnUrl { get; set; }
    }

    internal sealed class RegisterFormModel
    {
        public string? Password { get; set; }

        public string? ReturnUrl { get; set; }

        public string? Username { get; set; }
    }
}
