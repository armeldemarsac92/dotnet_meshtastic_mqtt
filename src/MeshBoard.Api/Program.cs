using MeshBoard.Api.Authentication;
using MeshBoard.Api.Preferences;
using MeshBoard.Api.Realtime;
using MeshBoard.Application.Abstractions.Authentication;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.DependencyInjection;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApiApplicationServices();
builder.Services.AddProductPersistenceInfrastructure(builder.Configuration);
builder.Services.AddOptions<RealtimeSessionOptions>()
    .Bind(builder.Configuration.GetSection(RealtimeSessionOptions.SectionName));
builder.Services.AddOptions<RealtimeDownstreamBrokerOptions>()
    .Bind(builder.Configuration.GetSection(RealtimeDownstreamBrokerOptions.SectionName));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "MeshBoard.Api.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                if (WorkspacePrincipalResolver.ResolveWorkspaceId(context.Principal) is not null)
                {
                    return;
                }

                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
        };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "MeshBoard.Api.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.HeaderName = "X-CSRF-TOKEN";
});
builder.Services.AddScoped<ICurrentUserContextAccessor, HttpContextUserContextAccessor>();
builder.Services.AddScoped<IRealtimeSessionService, RealtimeSessionService>();
builder.Services.AddScoped<IWorkspaceContextAccessor, HttpContextWorkspaceContextAccessor>();

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapApiAuthEndpoints();
app.MapBrokerPreferenceEndpoints();
app.MapFavoritePreferenceEndpoints();
app.MapRealtimeSessionEndpoints();
app.MapVernemqWebhookEndpoints();

app.Run();

public partial class Program;
