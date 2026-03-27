using System.Security.Claims;
using MeshBoard.Api.Middlewares.Validation;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MeshBoard.Api.Authentication;

internal static class AuthEndpoints
{
    private const string Tags = "Auth";
    private const string ContentType = "application/json";

    public static IEndpointRouteBuilder MapApiAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ApiRoutes.Auth.GetAntiforgery, GetAntiforgeryToken)
            .WithName("GetAntiforgeryToken")
            .Produces<AntiforgeryTokenResponse>()
            .AllowAnonymous()
            .WithTags(Tags);

        endpoints.MapPost(ApiRoutes.Auth.Register, RegisterUser)
            .WithName("RegisterUser")
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithValidator<RegisterUserRequest>()
            .Accepts<RegisterUserRequest>(ContentType)
            .Produces<AuthenticatedUserResponse>()
            .Produces(400)
            .Produces(409)
            .AllowAnonymous()
            .WithTags(Tags);

        endpoints.MapPost(ApiRoutes.Auth.Login, LoginUser)
            .WithName("LoginUser")
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithValidator<LoginUserRequest>()
            .Accepts<LoginUserRequest>(ContentType)
            .Produces<AuthenticatedUserResponse>()
            .Produces(400)
            .Produces(401)
            .AllowAnonymous()
            .WithTags(Tags);

        endpoints.MapPost(ApiRoutes.Auth.Logout, (Delegate)LogoutUser)
            .WithName("LogoutUser")
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .Produces(204)
            .Produces(400)
            .Produces(401)
            .RequireAuthorization()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.Auth.GetCurrentUser, GetCurrentUser)
            .WithName("GetCurrentUser")
            .Produces<AuthenticatedUserResponse>()
            .Produces(401)
            .RequireAuthorization()
            .WithTags(Tags);

        return endpoints;
    }

    private static IResult GetAntiforgeryToken(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Ok(tokens.ToAntiforgeryTokenResponse());
    }

    private static async Task<IResult> RegisterUser(
        HttpContext httpContext,
        RegisterUserRequest request,
        IUserAccountService userAccountService,
        CancellationToken cancellationToken)
    {
        var user = await userAccountService.RegisterAsync(request, cancellationToken);
        await SignInAsync(httpContext, user);
        return Results.Ok(user.ToAuthenticatedUserResponse(user.Id));
    }

    private static async Task<IResult> LoginUser(
        HttpContext httpContext,
        LoginUserRequest request,
        IUserAccountService userAccountService,
        CancellationToken cancellationToken)
    {
        var user = await userAccountService.ValidateCredentialsAsync(
            request.Username,
            request.Password,
            cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        await SignInAsync(httpContext, user);
        return Results.Ok(user.ToAuthenticatedUserResponse(user.Id));
    }

    private static async Task<IResult> LogoutUser(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUser(
        HttpContext httpContext,
        IUserAccountService userAccountService,
        CancellationToken cancellationToken)
    {
        var principal = httpContext.User;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var workspaceId = WorkspacePrincipalResolver.ResolveWorkspaceId(principal);

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(workspaceId))
        {
            return Results.Unauthorized();
        }

        var user = await userAccountService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Unauthorized();
        }

        return Results.Ok(user.ToAuthenticatedUserResponse(workspaceId));
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

    private static Task SignInAsync(HttpContext httpContext, AppUser user)
    {
        return httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AppUserPrincipalFactory.CreatePrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme),
            CreateAuthenticationProperties());
    }
}
