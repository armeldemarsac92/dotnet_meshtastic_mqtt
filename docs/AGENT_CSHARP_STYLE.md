# C# Architecture And Coding Style

This guide is meant to be reusable on future projects. It captures a specific style of writing C# applications: explicit, layered, pragmatic, and organized around deployment boundaries rather than framework fashion.

The goal is not maximum abstraction. The goal is code that is easy to extend without guessing where things belong.

Core preferences:

- keep boundaries obvious
- keep HTTP concerns at the edge
- keep mapping explicit
- keep SQL visible
- keep orchestration in services
- keep cross-process contracts shared
- keep startup composition centralized

When local code is inconsistent, preserve the nearest feature's precedent instead of forcing a global cleanup.

## Core Principles

Optimize for these qualities first:

- explicitness over cleverness
- feature-local consistency over framework purity
- thin endpoints and repositories, heavier services
- transport models separated from persistence models
- shared route contracts between servers and SDKs
- pragmatic repetition over hidden magic

Avoid introducing abstraction layers that hide behavior unless the project already depends on them heavily.

## Assembly Naming Conventions

Assembly names follow a very literal pattern:

`<Root>.<Subject>.<Role>`

The last segment should tell you why the assembly exists.

Common roles:

- `Contracts`
- `API`
- `Auth`
- `Repository`
- `SDK`
- `Core`
- `Console`
- `Consumer`
- `Lambdas`

Common shapes:

- `<Root>.Contracts`
- `<Root>.<Domain>.API`
- `<Root>.<Domain>.Auth`
- `<Root>.<Domain>.Repository`
- `<Root>.<Provider>.SDK`
- `<Root>.Worker.<Capability>.Core`
- `<Root>.Worker.<Capability>.Console`
- `<Root>.<Platform>.Lambdas`

What this naming style implies:

- the root prefix stays stable across the solution
- middle segments describe a domain, provider, or capability
- the last segment describes runtime role or deployment role
- names stay literal and boring on purpose

Avoid vague assembly names like:

- `Common`
- `Shared`
- `Helpers`
- `Infrastructure` when it mixes unrelated concerns
- `Core` unless it actually means reusable logic separated from a host

## How Assemblies Are Split

Assemblies are split by runtime boundary and responsibility, not by theoretical purity.

The dominant split is:

- one shared contracts assembly
- one or more HTTP entrypoint assemblies
- one repository/persistence assembly
- one SDK assembly per reusable external or internal HTTP boundary
- one host assembly per deployed worker or function
- one reusable core assembly behind a worker host when the worker does real application logic

### Shared Contracts Assembly

Put these in the contracts assembly:

- API request and response DTOs
- SQL request and response DTOs
- mapping extensions
- config models
- queue message contracts
- domain-level exception types
- database models shared across boundaries
- observability helpers shared by multiple hosts

This assembly exists so other assemblies can communicate without reaching into each other's implementation folders.

### API Assemblies

Create a dedicated API assembly per HTTP boundary.

Typical reasons to split APIs:

- main application API
- authentication API
- integration-facing API
- public-facing API versus internal-facing API

Do not combine unrelated HTTP boundaries just because they both happen to use ASP.NET Core.

### Repository Assembly

Keep database access in its own assembly when persistence is shared by multiple hosts or when you want SQL and DB concerns isolated from HTTP and worker composition.

This assembly should own:

- Dapper or DB context abstractions
- repository interfaces and implementations
- SQL query constants
- persistence-specific exception normalization

### SDK Assemblies

If an HTTP boundary is reused by workers, other services, or background jobs, put the typed client into its own SDK assembly.

This assembly should own:

- Refit interfaces or typed HTTP contracts
- HTTP client registration
- auth handlers for outgoing requests
- resilience and timeout setup
- route constants if they are not already shared from the server assembly

Do not scatter reusable HTTP client definitions across workers or APIs.

### Worker Split: Core Versus Host

When a worker has meaningful logic, split it into:

- `<Root>.Worker.<Capability>.Core`
- `<Root>.Worker.<Capability>.Console`

The `Core` assembly owns:

- orchestration services
- worker-local repositories
- DI extensions
- transformation logic

The `Console` assembly owns:

- `Program.cs`
- host startup
- configuration
- wiring the core assembly into a runnable executable

If the host is trivial, keep it trivial. Do not leak business logic upward into the host.

### What Not To Split

Do not create a new assembly for every feature.

A new feature usually belongs as a folder inside an existing layer assembly unless one of these changes:

- deployment boundary
- public contract boundary
- external provider boundary
- persistence ownership boundary

Most features should become folders. Few features should become assemblies.

## Folder Organization

Folder layout depends on the assembly's job. Do not force one identical folder tree everywhere.

### API Assembly Layout

A typical API assembly uses folders like:

- `Endpoints/`
- `Routes/`
- `Services/`
- `Middlewares/Validation/`
- `Middlewares/ExceptionHandlers/`
- `Middlewares/Logging/`
- `DI/` or `Extensions/`
- `Utils/`
- `Caching/` when output caching is part of the HTTP surface

Rules:

- endpoint files are grouped by feature
- services sit close to the API surface and own orchestration
- route constants live away from endpoint registration
- middleware folders are grouped by middleware concern, not by feature
- composition helpers live in `DI/` or `Extensions/`, but stay consistent inside the assembly

### Auth-Oriented API Layout

An auth-focused API often looks similar to a normal API assembly, but may lean more on:

- `Extensions/` instead of `DI/`
- `Endpoints/`
- `Routes/`
- `Services/`
- `Middlewares/`
- `Consumer/` if auth events trigger async work

The important pattern is not the exact folder name. It is that auth stays its own boundary instead of being mixed into unrelated API features.

### Repository Assembly Layout

A typical repository assembly uses folders like:

- `Context/`
- `Repository/<Feature>/`
- `SQL/`
- `DI/`
- `Config/`
- `Utils/`

Rules:

- connection and transaction logic live under `Context/`
- repositories are grouped by feature under `Repository/`
- raw SQL lives in `SQL/`
- DB-specific helpers and exception translation live in `Utils/`
- database registration stays in `DI/`

Prefer `Repository/<Feature>/` over one flat directory with dozens of repository files.

### Contracts Assembly Layout

The contracts assembly is usually split first by concern or boundary, then by model role.

Common top-level folders:

- `Config/`
- `Exceptions/`
- `Queues/`
- `Observability/`
- `Database/`
- `SQL/`
- `<PublicApi>/`
- `<ExternalApi>/`
- `<Auth>/`
- `<Ai>/`

Within a boundary-specific folder, common subfolders are:

- `Request/`
- `Response/`
- `Mapping/`

Within `SQL/`, common subfolders are:

- `Request/`
- `Response/`
- `Mapping/`

Do not dump every DTO into one `Models/` folder.

### SDK Assembly Layout

A typical SDK assembly uses folders like:

- `API/`
- `DI/`
- `Routes/` when it owns route constants
- `Services/` only if wrapper behavior truly belongs in the SDK
- `Repository/` only if the SDK intentionally exposes a provider-specific repository abstraction

Keep SDKs small. Their job is transport setup and typed access, not business orchestration.

### Worker Core Layout

A worker core assembly typically uses:

- `Services/`
- `Repositories/`
- `DI/`

This is intentionally lean. The worker core should be easy to scan quickly.

## Namespace Style

Use file-scoped namespaces.

```csharp
namespace Product.Catalog.API.Endpoints;
```

Namespace structure should mirror assembly and folder structure closely. If the folders are clear, namespaces should usually be clear without extra creativity.

## File Organization Rules

Common patterns:

- endpoint class per feature
- route class per HTTP boundary, with nested feature classes
- service interface and implementation often colocated in the same file
- repository interface and implementation usually split
- static mapping class per feature or model family
- validator class per request type

There is no need to normalize everything to one file-per-type rule. Match the convention of the current layer.

## Dominant Request Flow

The expected boundary line is:

`endpoint -> validator -> service -> repository -> SQL -> SQL response mapper -> API response`

For writes, the common flow is:

`request DTO -> ToSqlRequest() -> service transaction -> repository write -> optional read-back -> MapToResponse()`

That shape matters more than theoretical architectural terminology.

## Endpoint Management

This is one of the strongest patterns in the style.

An endpoint is usually managed across several linked places:

1. route constant in `Routes` or `ApiRoutes`
2. endpoint mapping in a static `*Endpoints` class
3. endpoint aggregation in `MapApiEndpoints()`
4. service method behind the handler
5. request and response mapping in the contracts assembly
6. SDK Refit interface reusing the same route constant when the API is shared

If you add or change an endpoint, think in terms of this whole chain.

### Route Ownership

Routes are centralized in static classes.

Common shapes:

- `Routes`
- `ApiRoutes`

The local project decides the exact container, but the principles stay the same:

- route strings are constants
- route groups are represented as nested static classes
- the API base prefix is shared
- endpoints do not own raw route strings

Typical shape:

```csharp
public static class Routes
{
    private const string ApiBase = "/api";

    public static class Brands
    {
        public const string GetById = $"{ApiBase}/brand/{{brandId}}";
        public const string GetAll = $"{ApiBase}/brands";
        public const string Create = $"{ApiBase}/brand";
    }
}
```

Important style points:

- do not hardcode route strings in `MapGet`, `MapPost`, and friends
- singular and plural route nouns may be intentionally asymmetrical
- route names should reflect the feature vocabulary plainly
- preserve local route style instead of trying to re-restify everything

### Endpoint Manifest Pattern

Each API host has one aggregator that acts as the public endpoint manifest.

Typical shape:

- `MapApiEndpoints()`
- one `app.MapXEndpoints();` call per feature

This file is the authoritative list of the HTTP surface. Adding a new endpoint family should normally mean touching this manifest.

### Static Endpoint Class Pattern

A feature endpoint set usually looks like this:

- static class named `<Feature>Endpoints`
- one public `Map<Feature>Endpoints(this IEndpointRouteBuilder app)` method
- private `const string ContentType = "application/json"` when body endpoints exist
- private `const string Tags = "<FeaturePlural>"` for Swagger grouping
- private or internal static handlers defined below the map method

The mapping section is intentionally explicit. Do not hide it behind reflection, automatic scanning, or controller discovery.

### Metadata Style

Per-endpoint metadata is declared inline on the mapping chain.

Common metadata in order:

- `.WithName(...)`
- `.WithValidator<T>()` when applicable
- `.Accepts<T>(ContentType)` for JSON body endpoints
- `.Produces<T>(...)` and `.Produces(...)`
- `.RequireAuthorization(...)`
- `.CacheOutput(...)` when needed
- `.WithTags(Tags)`

Typical shape:

```csharp
app.MapPost(Routes.Brands.Create, CreateBrand)
    .WithName("CreateBrand")
    .WithValidator<CreateBrandRequest>()
    .Accepts<CreateBrandRequest>(ContentType)
    .Produces<Brand>(201)
    .Produces(400)
    .RequireAuthorization("worker")
    .WithTags(Tags);
```

Prefer this explicit chain over controller attributes or hidden group-wide defaults.

### Handler Signature Style

Minimal API handlers are deliberately thin. Their signatures usually follow this shape:

- simple route or query scalars first
- `HttpContext` when identity, form data, raw request body, or claims are needed
- `[AsParameters]` query object for complex filter sets
- request DTO for body payloads
- service dependency near the end
- `CancellationToken` last

Examples of recurring shapes:

- route scalar plus service: `long brandId, IBrandService brandService, CancellationToken token`
- claims-driven endpoint: `HttpContext context, IItemService itemService, CancellationToken token`
- filter endpoint: `[AsParameters] ItemFilterParams parameters`
- explicit body annotation when clarity helps: `[FromBody] UpdateItemQueryRequest request`

Keep handlers small enough that they mostly read as "extract transport data, call service, wrap result".

### Transport Concerns Stay In Endpoints

The endpoint layer is allowed to handle HTTP-specific quirks directly.

Typical examples:

- claim extraction from `HttpContext`
- `ReadFormAsync()` for multipart uploads
- webhook signature verification
- OAuth callback handling and cookie writing
- converting empty list results to `NoContent`
- choosing between two service methods from a query flag

These are transport concerns, so they belong here. Do not push them down into repositories or SQL layers.

### Claim And Identity Pattern

For APIs that need authenticated user context, the endpoint layer usually resolves claims into plain values first.

Typical helper methods:

- `GetUserIdFromClaims()`
- `GetUserRole()`
- `GetSubscribedCreators()`

The endpoint resolves the user or role once, then passes plain values such as `Guid userId` to the service.

That keeps:

- services free of raw `HttpContext`
- claim parsing close to the HTTP boundary
- authorization policy separate from claim extraction

### Authorization Style

Authorization is declared per endpoint, not globally at a route-group level.

Policy names are literal strings and often describe the exact role combinations needed.

Examples of style, not required exact names:

- `"creator"`
- `"worker"`
- `"creator or worker"`
- `"authenticated"`
- `"admin"`

Preserve the local policy naming of the application you are editing.

### Response Style

Handlers usually return `IResult`.

Typical rules:

- reads return `Results.Ok(...)`
- creates return `Results.Created(...)`
- command-style updates often return `Results.Ok(...)` with the updated payload
- deletes and side-effect-only commands return `Results.NoContent()`
- some list endpoints return `NoContent` when the result is empty
- some reads rely on services throwing `NotFoundException`

One subtle but recurring detail: created location headers are often short inline literals rather than values built from route constants.

Examples of style:

- `Results.Created($"/brand/{createdBrand.Id}", createdBrand)`
- `Results.Created($"/itemquery/{createdQuery.QueryId}", createdQuery)`

Follow the local feature precedent instead of introducing a generic link builder by default.

### Endpoint Return Semantics Are Feature-Local

There is no single universal rule for empty results.

Possible patterns:

- return an empty list
- return `NoContent`
- throw `NotFoundException`
- return `null` or swallow recoverable API failures in remote-facing layers

Do not normalize this globally unless that is the task. Match the feature you are extending.

### Caching And Special Endpoint Behaviors

Caching is declared directly on the endpoint when needed with `.CacheOutput(...)`.

Patterns:

- some read endpoints opt into short-lived cache
- some filter-driven endpoints explicitly opt out with `NoCache()`
- cache policy details live close to the API surface

This style prefers explicit endpoint-level caching decisions over hidden conventions.

### SDK Parity Is Part Of Endpoint Design

For reusable APIs, route constants are not only a server concern. They are shared with SDK clients.

The recurring pattern is:

- server owns the route constants
- server endpoint maps use those constants
- SDK Refit interfaces reference the same constants
- SDK DI registers one client per endpoint interface

That means endpoint work is often incomplete if you only add the server handler.

For a reusable API boundary, a finished feature often requires all of these:

1. add the route constant
2. map the endpoint in the server project
3. add the service call behind it
4. expose the same route through a Refit interface in the SDK
5. register the SDK interface if it is new

This shared-contract route management is a key style choice. Preserve it.

### What To Avoid In Endpoint Work

Avoid these unless the project already does them:

- controller classes for standard feature endpoints
- route groups as the main abstraction
- hardcoded route strings inside handler registration
- business logic embedded in handlers
- direct repository calls from endpoints
- AutoMapper-based request conversion in handlers
- passing `HttpContext` into services for normal CRUD logic

## Validation Style

Validation is done with FluentValidation and Minimal API endpoint filters.

Patterns:

- one validator class per request type
- validators grouped under a validation middleware folder
- validators registered with assembly scanning
- endpoints opt in with `.WithValidator<T>()`
- validation failures return `ValidationProblem`

Validator style is direct and imperative:

```csharp
RuleFor(x => x.Name)
    .NotEmpty()
    .MaximumLength(100);
```

Common rule patterns:

- `NotEmpty()`
- `MaximumLength(...)`
- `GreaterThan(...)`
- `Must(...)`
- `When(...)` for optional fields

Keep validators focused on request shape and basic correctness. Do not move business rules or repository lookups into them.

## Contracts And DTO Style

### DTO Separation

This style intentionally keeps multiple DTO layers even when the fields look similar.

Typical categories:

- API request DTOs
- API response DTOs
- SQL request DTOs
- SQL response DTOs
- queue contracts
- external API DTOs

Do not collapse them into one shared mega-model just because the properties overlap.

### Request DTO Style

Request DTOs are usually mutable classes with `get; set;`.

Common traits:

- explicit class, not record-heavy
- `required` on mandatory fields when appropriate
- nullable reference and value types for optional inputs
- property names use PascalCase in C#
- JSON field names are annotated when external shape matters

### Response DTO Style

API response DTOs commonly use `JsonPropertyName` to preserve snake_case on the wire while keeping PascalCase property names in C#.

Examples of the pattern:

- `[JsonPropertyName("item_id")]`
- `[JsonPropertyName("brand_website")]`
- `[JsonPropertyName("first_name")]`

Do not rely on serializer-wide naming magic when the models are already explicit.

### SQL DTO Style

SQL response models mirror the database shape closely.

Patterns:

- `[Column("...")]` attributes
- denormalized or DB-shaped property names mapped into cleaner API DTOs later
- JSON payload columns sometimes stored as raw strings and deserialized in mapping extensions

This is deliberate. SQL DTOs represent persistence shape, not API shape.

### Contract Naming Style

Naming is highly literal.

- `CreateBrandRequest`
- `UpdateBrandSQLRequest`
- `BrandSQLResponse`
- `PrivateCreatorProfile`
- `UpdateDataTask`

Choose names that describe the role of the type, not names that try to be generic across layers.

## Mapping Style

Mapping is explicit and static.

Patterns:

- request mappers live in a boundary-specific contracts mapping namespace
- SQL-to-API mappers live in a SQL mapping namespace
- method names are `ToSqlRequest()`, `MapToX()`, and `MapToXs()`
- collection mapping usually does `responses.Select(MapToX).ToList()`

Mapping rules:

- keep mapping obvious and field-based
- simple shaping is fine
- lightweight transformations are fine
- JSON deserialization of embedded fields is acceptable in mapping when that is the contract boundary
- do not hide business logic in mapping

Avoid:

- AutoMapper profiles
- constructors that silently perform cross-layer mapping
- repository methods returning already-HTTP-shaped models without an explicit mapping step

## Service Layer Style

Services are the orchestration layer and carry most of the business weight.

Responsibilities that belong in services:

- business rules
- repository coordination
- transaction boundaries
- queue publishing
- translating missing data into typed exceptions
- intent and outcome logging

### Service File Pattern

It is common for a service file to contain both:

- `public interface IFeatureService`
- `public class FeatureService : IFeatureService`

This is especially common in API-facing application layers. Preserve that pattern where it already exists.

### Constructor Injection Style

Services use explicit constructor injection with private readonly fields.

Typical dependencies:

- one or more repositories
- `IUnitOfWork`
- `ILogger<T>`
- `IPublishEndpoint` or other external boundary services

Avoid service locators and hidden ambient dependencies.

### Naming Style

Service method names are business-readable and usually do not use the `Async` suffix, even though they are asynchronous.

Examples:

- `GetBrandById`
- `InsertItem`
- `UpdateProfile`
- `DeleteItem`
- `LikeItem`

Repository methods, by contrast, usually do use `Async`.

### Transaction Pattern

Write methods generally own the transaction boundary:

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

Recurring details:

- set route-derived IDs onto SQL request models inside the service when needed
- if an update or delete affects `0` rows, rollback and throw `NotFoundException`
- read back the updated row before returning when the feature pattern does that

### Error Handling Pattern

Services throw typed exceptions rather than composing `ProblemDetails`.

Common exceptions:

- `NotFoundException`
- `BadRequestException`
- `ConflictException`
- specialized database exceptions derived from `DatabaseException`

Do not build HTTP error responses inside services.

### Queue Publishing Pattern

Services are allowed to publish side-effect messages after core business actions.

Common style:

- perform primary DB work
- commit the transaction
- publish follow-up work through MassTransit when appropriate
- log both the action and the publish intent

### Logging Style In Services

Use structured logging with identifiers in templates.

Preferred shape:

```csharp
_logger.LogInformation("Attempting to update brand: {BrandId}", brandId);
```

The service layer typically logs:

- intent before work
- success after commit
- warning for expected but notable situations
- error inside catch blocks with relevant identifiers

## Repository Layer Style

Repositories are thin Dapper wrappers, not business services.

Responsibilities:

- execute one SQL statement or query
- bind parameters
- return SQL DTOs or primitive values
- log query intent

### Repository Shape

Typical repository dependencies:

- `IDbContext`
- `ILogger<T>`

Typical method shape:

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

Rules to preserve:

- public methods are async and usually end with `Async`
- repository methods map closely to one query constant
- repositories do not enforce higher-level business semantics beyond data access concerns
- repositories return SQL-shaped models, not API responses

## SQL Organization

SQL is kept in dedicated static query classes.

Patterns:

- one query class per feature or aggregate
- `public static string` properties
- verbatim multiline SQL strings
- database-native SQL kept readable
- parameter names aligned with request DTO or anonymous object names

Typical query class names:

- `BrandQueries`
- `ItemQueries`
- `SubscriptionQueries`

Do not inline substantial SQL inside services or endpoints.

## DB Context And Unit Of Work

Persistence is built around a shared Dapper context that also acts as the unit of work.

Key patterns:

- one `DapperContext` implements both `IDbContext` and `IUnitOfWork`
- DI exposes the same scoped instance under both interfaces
- Dapper calls are wrapped in helper methods such as `QueryAsync`, `QuerySingleAsync`, `ExecuteAsync`
- query duration is logged
- exceptions are normalized through a dedicated Npgsql exception handler

This is a pragmatic Dapper architecture, not an EF-style identity map.

Do not introduce ORM tracking assumptions into code written in this style.

## Exception Boundary Style

Exception translation is centralized at the HTTP boundary.

Patterns:

- dedicated exception handlers per category where useful
- handlers implement ASP.NET Core exception handling abstractions
- `ProblemDetails` is created in handlers, not in services
- database exceptions are translated before they escape persistence

Common handler categories:

- not found
- validation
- bad request
- conflict
- database
- global fallback

Prefer adding or refining handlers rather than scattering `try/catch` blocks through endpoint code.

## Dependency Injection And Bootstrap Style

Startup is intentionally thin and centralized.

### Program.cs Pattern

`Program.cs` is where host-wide composition happens:

- Serilog
- Swagger
- auth
- exception handlers
- DB registration
- service registration
- validators
- problem details
- cache
- OpenTelemetry
- MassTransit
- middleware ordering
- `MapApiEndpoints()`

Do not move this orchestration into many hidden startup classes. Extension methods are the preferred decomposition mechanism.

### DI Extension Style

Service registration lives in small extension methods such as:

- `AddApiServices(...)`
- `AddDbConnection(...)`
- `AddAuth(...)`
- `AddCache(...)`
- `AddStripeServices(...)`

Naming rules:

- `AddX(...)` for service registration
- `MapXEndpoints(...)` for endpoint registration
- `MapApiEndpoints()` for the application-level endpoint manifest

It is common for DI helper classes to be declared as:

```csharp
public static partial class ServiceCollectionExtensions
```

and split across several files. Keep that when the project already uses it.

### Configuration Style

Configuration binding is explicit and fail-fast.

Typical pattern:

```csharp
var authConfiguration = configuration.GetSection("auth").Get<AuthConfiguration>()
    ?? throw new InvalidOperationException("Auth configuration not found");
```

Observed conventions:

- section names are lowercase strings such as `"auth"`, `"database"`, `"queue"`, `"cache"`, `"networking"`, `"stripe"`
- missing config should usually throw immediately during startup

This style does not prefer lazy configuration failure.

## SDK And External API Client Style

SDK assemblies are a real part of the architecture, not a side detail.

Patterns:

- Refit interfaces are split by feature
- SDK DI owns client registration
- auth and resilience concerns live in the SDK assembly
- consuming apps use the SDK rather than recreating raw HTTP calls
- route constants are shared between server and SDK where the API is internal or reusable

Typical structure:

- `API/`
- `DI/`
- `Routes/` when needed

The style is:

- one interface per resource area
- reuse shared route constants when possible
- keep client setup centralized
- add auth handlers and retry/timeouts in DI
- keep business behavior out of the SDK layer

Do not put reusable Refit interfaces directly in worker or API assemblies when they belong in an SDK boundary.

## Queue And Worker Style

Background work is queue-driven and explicit.

Patterns:

- queue messages live in the contracts assembly
- workers are small console hosts
- services publish follow-up tasks when needed
- consumers and worker services mostly orchestrate, log, fetch, and persist

Worker flow is usually:

1. receive a typed message
2. log intent with the relevant identifier
3. fetch or compose required data
4. delegate persistence or transformation
5. log completion

Keep workers thin and explicit.

## Naming Conventions

Naming is intentionally literal and domain-facing.

- interfaces: `IItemService`, `IBrandRepository`, `IDbContext`
- implementations: `ItemService`, `BrandRepository`, `DapperContext`
- route classes: `Routes`, `ApiRoutes`
- endpoint classes: `BrandEndpoints`, `ItemQueryEndpoints`
- mapping classes: `BrandRequestMapping`, `ItemMapping`
- SQL classes: `BrandQueries`, `ItemSummarySQLResponse`

Method names should say what they do in business terms. Favor `GetBrandByVintId` over vague names like `Fetch`.

## Semantics To Mirror Carefully

The structure may be consistent even when behavior details are not globally uniform.

Mirror the local feature area's behavior for:

- empty list handling
- `404` versus `204`
- whether a create returns a read-back or just the insert result
- whether a write publishes a message
- whether an endpoint requires a role-specific authorization policy
- whether claims are read from `HttpContext` or a direct `ClaimsPrincipal`

This style values consistency within a feature more than global doctrinal purity.

## Feature Recipe

When adding a normal API feature in this style, the safest order is:

1. add route constants
2. add API request and response DTOs in the contracts assembly
3. add SQL request and response DTOs if needed
4. add request and response mapping extensions
5. add SQL query constants
6. add repository interface and implementation
7. add service interface and implementation
8. add validator classes
9. add the static endpoint class and handlers
10. register the endpoint group in `MapApiEndpoints()`
11. register the service and repository through DI
12. if the API is shared internally, add or update the corresponding Refit SDK interface
13. if the feature triggers background work, publish the queue message from the service layer

For endpoint work specifically, the checklist is:

1. route constant
2. `MapXEndpoints()` entry
3. handler implementation
4. validator
5. service contract
6. request mapper
7. SDK interface parity when applicable

## Guardrails For Another Agent

If you want new code to feel like this style, favor these choices:

- Minimal APIs instead of controller-heavy patterns
- static endpoint classes
- centralized route constants
- Refit SDKs that reuse server route constants
- FluentValidation endpoint filters
- explicit mutable DTO classes
- static mapping extensions
- Dapper repositories with visible SQL
- services as the orchestration layer
- typed exceptions and centralized exception handlers
- extension-method-based DI and startup composition
- `CancellationToken` threaded through async boundaries
- structured logging with identifiers in templates

Avoid these unless the project already chose them:

- MediatR or CQRS layers for straightforward CRUD features
- AutoMapper as the default mapping mechanism
- inline SQL in endpoints or services
- EF Core tracking as the primary persistence model
- route strings duplicated across server and client
- giant shared DTOs reused across HTTP, persistence, and queue layers
- hidden convention-based endpoint registration

## Short Summary

This style is:

- explicit layered architecture
- Minimal APIs with static endpoint classes
- centralized route constants
- shared server and SDK route contracts
- assembly names based on domain plus role
- assembly splits based on runtime boundary
- folder layouts based on assembly job
- FluentValidation via endpoint filters
- services for orchestration and transactions
- Dapper repositories with named SQL classes
- static mapping extensions between DTO layers
- typed exceptions translated at the boundary
- queue-driven background work
- centralized DI, logging, and observability setup

If another agent mirrors those constraints, especially the assembly split, folder organization, and route-plus-endpoint-plus-SDK management pattern, the result will stay very close to this coding style while remaining reusable on future projects.
