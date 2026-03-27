# Plan: Align MeshBoard Server with AGENT_CSHARP_STYLE.md

## Context

The AGENT_CSHARP_STYLE.md defines an explicit, layered, pragmatic C# architecture style. The codebase already follows it well in foundational areas (assembly splits, DI centralization, thin Program.cs, route constants in Contracts, Dapper repos, service orchestration, consumer thin-ness). However, several intermediate-level patterns from the style guide are missing or incomplete. This plan addresses those gaps in priority order.

The style guide itself says: *"When local code is inconsistent, preserve the nearest feature's precedent instead of forcing a global cleanup."* So we focus on additive improvements, not wholesale rewrites.

---

## Gap Analysis Summary

| # | Gap | Severity | Style Guide Section |
|---|-----|----------|-------------------|
| 1 | No centralized exception handlers - catch blocks duplicated in endpoints | HIGH | Exception Boundary Style |
| 2 | No endpoint metadata (`.WithName`, `.Produces<T>`, `.WithTags`) | HIGH | Metadata Style |
| 3 | No FluentValidation - validation scattered in services | HIGH | Validation Style |
| 4 | No `MapApiEndpoints()` aggregator | MEDIUM | Endpoint Manifest Pattern |
| 5 | Endpoint classes named `*EndpointMappings` instead of `*Endpoints` | MEDIUM | Static Endpoint Class Pattern |
| 6 | Health endpoint uses inline route string | LOW | Route Ownership |
| 7 | Antiforgery validation manual per-handler instead of filter/middleware | MEDIUM | Validation Style |
| 8 | Repository folder is flat (11 files) instead of grouped by feature | LOW | Repository Assembly Layout |

---

## Step 1: Centralized Exception Handlers

**Goal:** Remove try/catch from endpoint handlers; translate typed exceptions to ProblemDetails at the HTTP boundary via `IExceptionHandler` implementations.

**Files to create:**
- `src/MeshBoard.Api/Middlewares/ExceptionHandlers/NotFoundExceptionHandler.cs`
- `src/MeshBoard.Api/Middlewares/ExceptionHandlers/BadRequestExceptionHandler.cs`
- `src/MeshBoard.Api/Middlewares/ExceptionHandlers/ConflictExceptionHandler.cs`
- `src/MeshBoard.Api/Middlewares/ExceptionHandlers/GlobalExceptionHandler.cs`

**Files to modify:**
- `src/MeshBoard.Api/Program.cs` — register handlers with `builder.Services.AddExceptionHandler<T>()`, ensure `app.UseExceptionHandler()` is present
- `src/MeshBoard.Api/Authentication/ApiAuthEndpointMappings.cs` — remove try/catch blocks, let exceptions propagate
- `src/MeshBoard.Api/Preferences/BrokerPreferenceEndpointMappings.cs` — remove try/catch blocks
- Remove private `CreateProblemDetails` helpers from both endpoint files

**Pattern (per handler):**
```csharp
internal sealed class NotFoundExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not NotFoundException notFoundException) return false;
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = notFoundException.Message
        }, cancellationToken);
        return true;
    }
}
```

**Acceptance criteria:**
- All typed exceptions from services are caught by centralized handlers
- No try/catch for domain exceptions remains in endpoint files
- `ProblemDetails` creation is centralized in handlers only
- `dotnet build` passes

---

## Step 2: Endpoint Metadata

**Goal:** Add `.WithName()`, `.Produces<T>()`, `.Produces()`, and `.WithTags()` chains to all endpoint mappings per the style guide's Metadata Style section.

**Files to modify:**
- `src/MeshBoard.Api/Public/PublicCollectorEndpointMappings.cs` — add metadata to all 10 endpoints
- `src/MeshBoard.Api/Authentication/ApiAuthEndpointMappings.cs` — add metadata to all 4 endpoints
- `src/MeshBoard.Api/Preferences/BrokerPreferenceEndpointMappings.cs` — add metadata to all 5 endpoints
- `src/MeshBoard.Api/Preferences/FavoritePreferenceEndpointMappings.cs` — add metadata
- `src/MeshBoard.Api/Realtime/RealtimeSessionEndpointMappings.cs` — add metadata
- `src/MeshBoard.Api/Realtime/VernemqWebhookEndpointMappings.cs` — add metadata

**Pattern:**
```csharp
endpoints.MapGet(ApiRoutes.PublicCollector.GetServers, ...)
    .WithName("GetCollectorServers")
    .Produces<IReadOnlyCollection<CollectorServerSummary>>(200)
    .WithTags("PublicCollector");
```

**Acceptance criteria:**
- Every endpoint has `.WithName()`, `.Produces<T>()` (or `.Produces(statusCode)` for error codes), and `.WithTags()`
- Names are unique across the API surface
- Tags group endpoints by feature
- `dotnet build` passes

---

## Step 3: FluentValidation Setup

**Goal:** Add FluentValidation with endpoint filters for request validation, replacing manual validation in services.

**Files to create:**
- `src/MeshBoard.Api/Middlewares/Validation/ValidationFilter.cs` — generic endpoint filter that runs `IValidator<T>`
- `src/MeshBoard.Api/Middlewares/Validation/SaveBrokerServerProfileRequestValidator.cs`
- `src/MeshBoard.Api/Middlewares/Validation/SaveBrokerPreferenceRequestValidator.cs`
- `src/MeshBoard.Api/Middlewares/Validation/RegisterUserRequestValidator.cs`
- `src/MeshBoard.Api/Middlewares/Validation/LoginUserRequestValidator.cs`
- `src/MeshBoard.Api/Middlewares/Validation/SaveFavoriteNodeRequestValidator.cs`

**Files to modify:**
- `src/MeshBoard.Api/MeshBoard.Api.csproj` — add `FluentValidation.DependencyInjectionExtensions` NuGet
- `src/MeshBoard.Api/Program.cs` — register validators with `AddValidatorsFromAssemblyContaining<Program>()`
- Endpoint files — add `.WithValidator<T>()` (or `.AddEndpointFilter<ValidationFilter<T>>()`) to body endpoints
- `src/MeshBoard.Application/Services/BrokerServerProfileService.cs` — remove manual null/empty/range checks that validators now handle

**Validation filter pattern:**
```csharp
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null) return await next(context);
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null) return await next(context);
        var result = await validator.ValidateAsync(argument);
        if (!result.IsValid) return Results.ValidationProblem(result.ToDictionary());
        return await next(context);
    }
}
```

**Extension method:**
```csharp
public static RouteHandlerBuilder WithValidator<T>(this RouteHandlerBuilder builder) where T : class
    => builder.AddEndpointFilter<ValidationFilter<T>>();
```

**Acceptance criteria:**
- FluentValidation NuGet installed and validators registered
- All request DTOs with body payloads have a validator
- Endpoints use `.WithValidator<T>()` in the metadata chain
- Service-layer manual validation removed where validators now cover it
- Validation failures return `ValidationProblem` (RFC 7807)
- `dotnet build` passes

---

## Step 4: MapApiEndpoints() Aggregator

**Goal:** Create a single endpoint manifest method that acts as the authoritative HTTP surface list.

**Files to create:**
- `src/MeshBoard.Api/Extensions/EndpointExtensions.cs`

**Files to modify:**
- `src/MeshBoard.Api/Program.cs` — replace individual `MapXEndpoints()` calls with single `app.MapApiEndpoints()`

**Pattern:**
```csharp
public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Health, () => Results.Ok(new { status = "ok" }));
        app.MapApiAuthEndpoints();
        app.MapBrokerPreferenceEndpoints();
        app.MapFavoritePreferenceEndpoints();
        app.MapPublicCollectorEndpoints();
        app.MapRealtimeSessionEndpoints();
        app.MapVernemqWebhookEndpoints();
        return app;
    }
}
```

**Acceptance criteria:**
- `Program.cs` uses a single `app.MapApiEndpoints()` call
- Aggregator is the sole place that declares the endpoint surface
- `dotnet build` passes

---

## Step 5: Rename Endpoint Classes

**Goal:** Align class names with the style guide's `<Feature>Endpoints` convention.

**Files to rename (class + file):**
- `ApiAuthEndpointMappings` → `AuthEndpoints` (file: `AuthEndpoints.cs`)
- `BrokerPreferenceEndpointMappings` → `BrokerPreferenceEndpoints`
- `FavoritePreferenceEndpointMappings` → `FavoritePreferenceEndpoints`
- `PublicCollectorEndpointMappings` → `PublicCollectorEndpoints`
- `RealtimeSessionEndpointMappings` → `RealtimeSessionEndpoints`
- `VernemqWebhookEndpointMappings` → `VernemqWebhookEndpoints`

**Files to modify:**
- `src/MeshBoard.Api/Extensions/EndpointExtensions.cs` — update using/references
- `src/MeshBoard.Api/Program.cs` — update using statements if needed

**Notes:**
- Extension method names (`MapXEndpoints`) stay the same — only the containing class name changes
- Add `private const string Tags = "..."` and `private const string ContentType = "application/json"` to each endpoint class where applicable

**Acceptance criteria:**
- All endpoint classes follow `<Feature>Endpoints` naming
- Extension method names unchanged
- `dotnet build` passes

---

## Step 6: Add Health Route Constant

**Goal:** Move the inline `/api/health` string to `ApiRoutes`.

**Files to modify:**
- `src/MeshBoard.Contracts/Api/ApiRoutes.cs` — add `public const string Health = $"{ApiBase}/health";`
- `src/MeshBoard.Api/Extensions/EndpointExtensions.cs` (or `Program.cs` depending on Step 4 completion) — use `ApiRoutes.Health`

**Acceptance criteria:**
- No inline route strings remain in Program.cs or endpoint files
- `dotnet build` passes

---

## Step 7: Antiforgery as Endpoint Filter

**Goal:** Replace per-handler `await antiforgery.ValidateRequestAsync(httpContext)` with a reusable endpoint filter.

**Files to create:**
- `src/MeshBoard.Api/Middlewares/Validation/AntiforgeryValidationFilter.cs`

**Files to modify:**
- Endpoint files that currently call `antiforgery.ValidateRequestAsync` manually — remove manual calls, add `.AddEndpointFilter<AntiforgeryValidationFilter>()` to those endpoints
- Remove `IAntiforgery` and `HttpContext` from handler signatures where they were only needed for antiforgery

**Pattern:**
```csharp
internal sealed class AntiforgeryValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        await antiforgery.ValidateRequestAsync(context.HttpContext);
        return await next(context);
    }
}
```

**Acceptance criteria:**
- No manual antiforgery validation in handler bodies
- All state-changing endpoints use the filter
- Handler signatures are slimmer (no IAntiforgery, HttpContext only when actually needed)
- `dotnet build` passes

---

## Step 8: Group Repositories by Feature (Optional)

**Goal:** Restructure flat `Repositories/` into `Repository/<Feature>/` subfolders.

**Current structure:**
```
Repositories/
  CollectorBrokerServerProfileRepository.cs
  CollectorChannelResolver.cs
  CollectorNodeRepository.cs
  ... (11 files)
```

**Proposed structure:**
```
Repository/
  Collector/
    CollectorNodeRepository.cs
    CollectorChannelResolver.cs
    CollectorMessageRepository.cs
    CollectorNeighborLinkRepository.cs
    CollectorPacketRollupRepository.cs
    CollectorDiscoveredTopicRepository.cs
    CollectorBrokerServerProfileRepository.cs
    CollectorReadRepository.cs
  Product/
    ProductBrokerServerProfileRepository.cs
    FavoriteNodeRepository.cs
    UserAccountRepository.cs
```

**Note:** This is a low-priority rename. Only proceed if the user confirms. Namespace changes will ripple to DI registrations.

**Acceptance criteria:**
- Repositories grouped by domain
- Namespaces updated to match new folder paths
- All DI registrations still resolve correctly
- `dotnet build` passes

---

## Execution Order

Steps 1-4 are the highest value. Steps 5-7 are medium. Step 8 is optional.

Recommended execution batches:
1. **Batch A (foundation):** Step 1 (exception handlers) + Step 4 (aggregator) + Step 6 (health route)
2. **Batch B (metadata):** Step 2 (endpoint metadata) + Step 5 (rename classes)
3. **Batch C (validation):** Step 3 (FluentValidation) + Step 7 (antiforgery filter)
4. **Batch D (optional):** Step 8 (repository folders)

Each batch produces a build-clean commit.

---

## Verification

After each batch:
1. `dotnet build` — must pass with no new warnings
2. `dotnet test` — existing tests must pass
3. Manual review: endpoint files should be noticeably thinner (no try/catch, no manual validation, no antiforgery boilerplate)
4. Swagger UI (if available): endpoints should show names, tags, and response types

---

## Files Summary

**Critical files to modify:**
- `src/MeshBoard.Api/Program.cs`
- `src/MeshBoard.Api/Authentication/ApiAuthEndpointMappings.cs`
- `src/MeshBoard.Api/Preferences/BrokerPreferenceEndpointMappings.cs`
- `src/MeshBoard.Api/Preferences/FavoritePreferenceEndpointMappings.cs`
- `src/MeshBoard.Api/Public/PublicCollectorEndpointMappings.cs`
- `src/MeshBoard.Api/Realtime/RealtimeSessionEndpointMappings.cs`
- `src/MeshBoard.Api/Realtime/VernemqWebhookEndpointMappings.cs`
- `src/MeshBoard.Contracts/Api/ApiRoutes.cs`
- `src/MeshBoard.Application/Services/BrokerServerProfileService.cs`
- `src/MeshBoard.Api/MeshBoard.Api.csproj`

**New files to create:**
- `src/MeshBoard.Api/Middlewares/ExceptionHandlers/NotFoundExceptionHandler.cs`
- `src/MeshBoard.Api/Middlewares/ExceptionHandlers/BadRequestExceptionHandler.cs`
- `src/MeshBoard.Api/Middlewares/ExceptionHandlers/ConflictExceptionHandler.cs`
- `src/MeshBoard.Api/Middlewares/ExceptionHandlers/GlobalExceptionHandler.cs`
- `src/MeshBoard.Api/Middlewares/Validation/ValidationFilter.cs`
- `src/MeshBoard.Api/Middlewares/Validation/AntiforgeryValidationFilter.cs`
- `src/MeshBoard.Api/Middlewares/Validation/*RequestValidator.cs` (5-6 files)
- `src/MeshBoard.Api/Extensions/EndpointExtensions.cs`
