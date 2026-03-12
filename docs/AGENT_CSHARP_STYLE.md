# C# Architecture And Coding Style

This document captures the recurring architectural decisions and coding conventions inferred from this repository so another agent can extend a different project in the same style.

The conventions below are inferred from the current codebase, not from a formatter or `.editorconfig`. They should be treated as the default unless the target project already has a stronger local convention.

## Architectural Baseline

Build services as small, focused projects with explicit responsibilities.

- `App.Contracts`: shared contracts, request/response DTOs, mappings, config types, exceptions, queue messages, and observability helpers.
- `App.Seeqr.API` / `App.Seeqr.Auth` / `App.Vinted.API`: HTTP entrypoints built with Minimal APIs plus extension-based bootstrap.
- `App.Seeqr.Repository`: Dapper-based persistence layer with repository interfaces, implementations, SQL query classes, and a shared DB context / unit of work.
- `App.*.SDK`: typed API clients used by workers or other services.
- `App.Worker.*`: queue-driven background processes that orchestrate external APIs and internal APIs.

Prefer clear boundaries between layers:

1. Endpoint layer handles HTTP concerns only.
2. Validator layer enforces request shape and basic rules.
3. Service layer owns business logic, orchestration, transactions, and queue publishing.
4. Repository layer owns persistence calls.
5. SQL lives in dedicated query classes, not inline in services.
6. Mapping lives in static extension classes, not inside endpoints or repositories.

Do not collapse these layers into a single handler or introduce framework-heavy abstractions unless the target codebase already does that.

## Request Flow Pattern

The dominant flow is:

`Route constant -> endpoint mapping -> validator -> service -> repository -> SQL -> SQL response mapping -> API response`

For writes, the usual flow is:

`Request DTO -> ToSqlRequest() mapper -> service transaction -> repository write -> repository read-back -> MapToResponse()`

That means:

- Endpoints should stay thin.
- Services should own write transaction boundaries.
- Repositories should return SQL-shaped DTOs.
- Mapping should convert between transport models and persistence models explicitly.

## Project And File Organization

Use file-scoped namespaces consistently. The repository is almost entirely using:

```csharp
namespace App.Seeqr.API.Services;
```

Organize files by feature or concern, not by technical mega-folders alone. Typical patterns:

- `Endpoints/<Feature>Endpoints.cs`
- `Services/I<Feature>Service.cs`
- `Repository/<Feature>/<Feature>Repository.cs`
- `Repository/<Feature>/I<Feature>Repository.cs`
- `Contracts/.../Request/<Feature>/...`
- `Contracts/.../Response/<Feature>/...`
- `Contracts/.../Mapping/...`
- `Middlewares/Validation/<Feature>/...`
- `Repository/SQL/<Feature>Queries.cs`

Common local convention in this codebase:

- Service interface and implementation are often colocated in the same file.
- Repository interface and implementation are usually split into separate files.
- Endpoint classes are static.
- DI registration is grouped into extension methods.

Follow the existing pattern of the target layer instead of enforcing one rule everywhere.

## API Style

### Minimal APIs

HTTP APIs are defined through static endpoint classes with one `Map<Feature>Endpoints` method per feature.

Use this shape:

- Static endpoint class.
- Constants for tag names and repeated content types.
- Named route handlers as private or internal static methods.
- Authorization declared directly on mapped endpoints.
- Response metadata declared with `.Produces(...)`, `.Accepts(...)`, `.WithTags(...)`, `.WithName(...)`.

Keep endpoint handlers thin. A handler should mostly:

1. Accept route/query/body parameters plus a service and `CancellationToken`.
2. Call the service.
3. Return `Results.Ok`, `Results.Created`, or `Results.NoContent`.

Do not move business rules, transactions, or raw SQL into endpoint methods.

### Route Definitions

Centralize routes in static route classes with nested feature classes and string constants.

Prefer this style:

```csharp
public static class Routes
{
    private const string ApiBase = "/api";

    public static class Brands
    {
        public const string GetById = $"{ApiBase}/brand/{{brandId}}";
    }
}
```

Avoid hardcoding route strings in the endpoint mapping body.

## Validation Style

Use FluentValidation.

- One validator class per request type.
- Keep validators focused on shape, nullability, range, length, and simple format checks.
- Register validators by assembly scan.
- Apply validation through an endpoint filter such as `WithValidator<T>()`.

Validation rules are generally written in a straightforward chain style:

```csharp
RuleFor(x => x.Name)
    .NotEmpty()
    .MaximumLength(100);
```

Use conditional validation with `When(...)` for optional fields.

## Service Layer Style

Services are the main orchestration layer.

Responsibilities that belong in services:

- Business rules
- Repository coordination
- Queue publishing
- Transaction boundaries
- Logging around intent and outcome
- Converting missing data into domain exceptions

Service conventions used in this codebase:

- Interface first, often followed immediately by the implementation in the same file.
- Constructor injection with explicit private readonly fields.
- Method names are verb based: `Get...`, `Create...`, `Update...`, `Delete...`, `Like...`, `Check...`.
- Service method names usually do not use the `Async` suffix even when they are asynchronous.
- Repository methods usually do use the `Async` suffix.
- `CancellationToken` is threaded through nearly every async API.

### Transaction Pattern

For write operations, the usual shape is:

```csharp
await _unitOfWork.BeginTransactionAsync(cancellationToken);
try
{
    // write
    // optional read-back
    await _unitOfWork.CommitAsync(cancellationToken);
}
catch
{
    await _unitOfWork.RollbackAsync(cancellationToken);
    throw;
}
```

If an update or delete affects `0` rows, throw `NotFoundException` after rolling back.

### Error Handling Pattern

Business methods throw domain-specific exceptions such as:

- `NotFoundException`
- `BadRequestException`
- `ConflictException`
- database-specific exceptions derived from `DatabaseException`

Do not build HTTP problem responses inside services. Throw typed exceptions and let exception handlers translate them at the boundary.

### Logging Pattern

Use structured logging aggressively.

- Log intent before the operation.
- Log success after the operation.
- Log warnings for recoverable or expected misses.
- Log errors inside catch blocks with identifiers in the message template.

Prefer:

```csharp
_logger.LogInformation("Attempting to get brand: {BrandId}", brandId);
```

Over interpolated strings, except where the existing code already mixes them in.

## Repository Layer Style

Repositories are thin Dapper wrappers over SQL query classes.

Responsibilities that belong in repositories:

- Execute one SQL command or query.
- Bind parameters.
- Return SQL response DTOs or primitive values.
- Log query intent.

Repository conventions:

- Constructor injection of `IDbContext` and `ILogger<T>`.
- Public methods are async and normally end with `Async`.
- A repository method should usually correspond to one query constant.
- Repositories do not contain business decisions beyond simple query execution concerns.

Typical repository shape:

```csharp
public async Task<BrandSQLResponse?> GetByIdAsync(long brandId, CancellationToken cancellationToken = default)
{
    _logger.LogInformation("Attempting to fetch brand with ID: {BrandId}", brandId);
    return await _dbContext.QueryFirstOrDefaultAsync<BrandSQLResponse>(
        BrandQueries.GetBrandById,
        new { BrandId = brandId },
        cancellationToken);
}
```

## SQL Organization

Store SQL in static query classes such as `BrandQueries`, `ItemQueries`, `SubscriptionQueries`.

Conventions:

- One query class per feature or aggregate.
- Each query is exposed as a `public static string` property.
- Use verbatim multi-line SQL strings.
- Keep SQL readable and database-native.
- Parameter names match C# request/anonymous object names.

Do not inline SQL in services or endpoint files.

## DB Context And Unit Of Work

Persistence is built around a shared Dapper context that also implements `IUnitOfWork`.

Patterns to preserve:

- One shared context handles connection lifecycle.
- The same context type can be exposed as both `IDbContext` and `IUnitOfWork`.
- Query helpers wrap Dapper methods such as `QueryAsync`, `QuerySingleAsync`, `ExecuteAsync`.
- Database exceptions are normalized through an exception handler before leaving the persistence layer.
- Query execution time is logged.

This is a pragmatic Dapper architecture, not an ORM-centric one. Avoid introducing EF-style tracking assumptions into code written in this style.

## Contracts And DTO Style

Use explicit DTO classes for transport and persistence models.

Patterns seen repeatedly:

- Request DTOs are mutable classes with `get; set;`.
- Required inputs use `required`.
- Optional inputs use nullable reference or nullable value types.
- SQL response DTOs use `[Column("...")]` where needed.
- API response models are separate from SQL response models.

Prefer explicit classes over clever record-heavy designs if you are trying to match this repository's style.

Examples:

- API request DTO: `CreateBrandRequest`
- SQL request DTO: `CreateBrandSQLRequest`
- SQL response DTO: `BrandSQLResponse`
- API response DTO: `Brand`

## Mapping Style

Mapping is explicit and separated into static extension classes.

Patterns:

- `ToSqlRequest()` for transport-to-persistence mapping
- `MapToX()` for persistence-to-domain/API mapping
- `MapToXs()` for list mapping

Keep mapping dumb. It should mostly copy fields and perform simple shape translation.

Do not hide mapping logic inside constructors, repository methods, or endpoint handlers.

## Dependency Injection And Bootstrap Style

Application bootstrapping is intentionally thin.

Patterns to preserve:

- `Program.cs` sets up host-wide concerns only.
- Service registration lives in extension classes such as `AddApiServices`, `AddDbConnection`, `AddAuth`, `AddCache`.
- Endpoint aggregation lives in one extension like `MapApiEndpoints()`.
- Serilog, OpenTelemetry, MassTransit, Swagger, auth, caching, and DB registration happen centrally during startup.

Naming style:

- `AddX(...)` for service registration extensions
- `MapXEndpoints(...)` for endpoint groups
- sometimes `ServiceCollectionExtensions` is declared as `partial` across several DI files

## Queue And Worker Style

Background processing is queue-driven through MassTransit.

Patterns:

- Workers are small console hosts.
- Consumers are focused and do little more than orchestrate calls.
- Shared queue message contracts live in `App.Contracts.Queues`.
- Queue messages are explicit types, often derived from a shared `BaseTask`.
- Side effects such as data refreshes are triggered by publishing messages from services.

Worker consumers should stay thin:

1. Read the message.
2. Log the task identifier.
3. Fetch required external/internal data.
4. Delegate transformation or persistence to another service.
5. Log completion.

## External API Client Style

Typed HTTP clients are wrapped behind endpoint interfaces or service classes.

Patterns:

- Refit is used for typed HTTP APIs.
- SDK projects expose endpoint interfaces consumed by workers or other apps.
- External API wrapper services catch `ApiException` explicitly.
- Recoverable remote failures may return `null` or empty results instead of crashing the worker immediately.
- Retry loops are implemented imperatively when needed.

This style favors pragmatic wrappers over deep custom client frameworks.

The required structure is:

- SDK assembly owns Refit endpoint interfaces.
- SDK assembly owns `HttpClient` and DI registration.
- Consumer app owns wrapper services or repositories.
- Business behavior stays out of the SDK.

Do not place Refit endpoint interfaces directly inside the consuming UI, worker, or service project when the call belongs to a reusable API boundary.

## Exception Boundary Style

Exception handlers translate domain and infrastructure exceptions into HTTP responses.

Patterns:

- One handler per category where useful: not found, validation, database, bad request, conflict, global.
- Use ASP.NET Core exception handling middleware, not try/catch in every endpoint.
- Problem details responses are built in exception handlers.

Keep exception translation centralized.

## Naming Conventions

Use descriptive, explicit names over shortened abstractions.

- Interfaces: `IItemService`, `IBrandRepository`, `IDbContext`
- Implementations: `ItemService`, `BrandRepository`, `DapperContext`
- Request DTOs: `CreateBrandRequest`, `UpdateItemAiDataRequest`
- SQL DTOs: `CreateBrandSQLRequest`, `ItemSummarySQLResponse`
- Mapping classes: `BrandRequestMapping`, `ItemMapping`
- Query classes: `BrandQueries`, `ItemQueries`

Method names are direct and business-readable. Prefer `GetBrandByVintId` over a generic `Fetch`.

## Style Guardrails For Another Agent

If you are writing a new project in this style:

- Use Dapper plus explicit SQL, not repository methods backed by hidden ORM behavior.
- Keep endpoints thin and static.
- Put orchestration into services.
- Keep mapping explicit.
- Use typed exceptions and centralized exception handlers.
- Register dependencies through extension methods.
- Thread `CancellationToken` through async flows.
- Use structured logging with identifiers in templates.
- Prefer mutable DTO classes for requests and responses.
- Keep queue messages and cross-service contracts in a dedicated shared project.

Avoid these deviations unless the target project already requires them:

- MediatR/CQRS layers for simple CRUD and orchestration
- fat controllers or fat endpoints
- EF Core entity tracking as the main data model
- inline SQL in services
- hiding business behavior inside mapping or repository classes
- unstructured `Console.WriteLine` logging instead of `ILogger`

## Semantics To Mirror Carefully

The codebase is consistent in structure, but not every feature uses identical response semantics.

Examples:

- Some list queries return an empty list when nothing is found.
- Some list queries throw `NotFoundException`.
- Some remote-client methods swallow recoverable API failures and return `null`.

When extending a project in this style, match the precedent of the local feature area instead of forcing one universal rule.

## Suggested Build Recipe For New Features

When adding a feature in this style, follow this order:

1. Add route constants.
2. Add request and response DTOs in `App.Contracts`.
3. Add request-to-SQL and SQL-to-response mapping extensions.
4. Add SQL request/response models if needed.
5. Add SQL query constants.
6. Add repository interface and implementation.
7. Add service interface and implementation.
8. Add validator classes.
9. Add endpoint mapping class and handlers.
10. Register the service and repository through DI extensions.
11. Add exception handling only if the feature introduces a new exception category.
12. If the feature triggers background work, publish a queue message from the service layer.

## Short Style Summary

This coding style is pragmatic layered C#:

- Minimal API at the edge
- FluentValidation for request validation
- service-oriented orchestration
- Dapper repositories with explicit SQL
- static mapping extensions
- typed shared contracts
- queue-based workers for async processing
- centralized exception translation
- structured logging and observability from startup

If another agent follows those constraints, the result should feel close to how this repository is designed and how you currently write C#.
