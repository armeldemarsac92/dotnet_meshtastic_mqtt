using MeshBoard.Application.DependencyInjection;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Infrastructure.Meshtastic.DependencyInjection;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;
using MeshBoard.Web.Authentication;
using MeshBoard.Web.Components;
using MeshBoard.Web.State;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using MeshBoard.Contracts.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddMeshtasticInfrastructure(builder.Configuration);
builder.Services.AddPersistenceInfrastructure(builder.Configuration);
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
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
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IWorkspaceContextAccessor, AuthenticatedWorkspaceContextAccessor>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<ServerSelectionNotifier>();
builder.Services.AddSingleton<ProjectionChangeNotifier>();

if (builder.Configuration.GetValue<bool>("WebUiRuntime:EnableProjectionDispatch"))
{
    builder.Services.AddHostedService<ProjectionChangeDispatchHostedService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapUserAuthEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
