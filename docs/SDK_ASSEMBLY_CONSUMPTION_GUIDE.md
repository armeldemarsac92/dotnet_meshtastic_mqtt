# SDK Assembly Consumption Guide

## Purpose

This repository consumes internal and external HTTP APIs through dedicated SDK assemblies.

The SDK is the transport boundary. It owns the Refit endpoint contracts, shared DTO exposure, and HTTP client registration. Business behavior stays outside the SDK in the consuming app or worker.

This rule is mandatory for the refactor described in `docs/ARCHITECTURE_REFACTOR_ROADMAP.md`.

## Required Pattern

The standard pattern is:

1. Put API contracts in an SDK project.
   Each SDK exposes thin Refit interfaces in `API/` and shared request/response DTOs or references shared contracts.
2. Put HTTP registration in the same SDK project.
   Each SDK exposes one DI extension in `DI/` that registers the Refit clients, base URL, and any cross-cutting HTTP concerns.
3. Keep business behavior out of the SDK.
   Consumers inject the SDK interfaces into their own repositories or services, where they add logging, caching, null-on-404 behavior, retries, and error translation.
4. Call the repository or service from the app boundary.
   Startup registers the SDK first, then the consumer-specific services.

## Mandatory Rules

- Use one SDK assembly per consumed HTTP API surface.
- Use `Refit` plus `Refit.HttpClientFactory` in the SDK assembly.
- Keep endpoint interfaces thin.
- Keep HTTP registration inside the SDK.
- Keep business logic, caching, logging, and policy translation outside the SDK.
- Do not define Refit endpoint interfaces directly inside UI projects, workers, or application service projects.
- Do not inject `HttpClient` into business services when the call should go through an SDK boundary.

## SDK Structure

The default SDK layout is:

```text
src/
  MyCompany.SomeApi.SDK/
    API/
    DI/
```

Recommended responsibilities:

- `API/`
  - Refit interfaces
  - route constants when they logically belong to the consumed API
- `DI/`
  - one service collection extension
  - base URL lookup
  - delegating handlers
  - auth/header wiring
  - resilience defaults

If contracts are already shared elsewhere, the SDK should reference them rather than duplicate them.

## Endpoint Interface Rules

Endpoint interfaces should only describe:

- HTTP verb via Refit attributes like `[Get]`, `[Post]`, `[Put]`, `[Delete]`
- route template or route constant
- method parameters
- request/response DTO types

They must not contain:

- logging
- retries
- caching
- fallback flow control
- domain-specific translation logic

Example:

```csharp
using Refit;

namespace MyCompany.Payments.SDK.API;

public interface IInvoiceEndpoints
{
    [Get("/api/invoices/{invoiceId}")]
    Task<InvoiceDto> GetById(long invoiceId);

    [Post("/api/invoices")]
    Task<InvoiceDto> CreateInvoice(CreateInvoiceRequest request);
}
```

## DI Registration Rules

Each SDK exposes one DI extension that owns HTTP registration.

Example:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace MyCompany.Payments.SDK.DI;

public static class PaymentsServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentsApi(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["networking:PaymentsApiHost"]
            ?? throw new InvalidOperationException("PaymentsApiHost is missing.");

        services.AddRefitClient<IInvoiceEndpoints>()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri(baseUrl));

        return services;
    }
}
```

If the API needs auth, shared headers, resilience, or certificate overrides, add that here rather than in each consumer.

## Consumer Wrapper Rules

Consumers should usually wrap SDK interfaces in a local repository or service.

That wrapper is where the real behavior belongs:

- structured logging
- null-on-404 behavior where acceptable
- remote error translation
- use-case-specific retries
- caching
- feature-specific fallback behavior

Example:

```csharp
using Refit;

public class InvoiceRepository
{
    private readonly IInvoiceEndpoints _invoiceApi;
    private readonly ILogger<InvoiceRepository> _logger;

    public InvoiceRepository(IInvoiceEndpoints invoiceApi, ILogger<InvoiceRepository> logger)
    {
        _invoiceApi = invoiceApi;
        _logger = logger;
    }

    public async Task<InvoiceDto?> GetById(long invoiceId)
    {
        try
        {
            return await _invoiceApi.GetById(invoiceId);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (ApiException ex)
        {
            _logger.LogError("Remote API error while loading invoice {invoiceId}: {content}", invoiceId, ex.Content);
            throw;
        }
    }
}
```

## Startup Composition Rules

The consuming app should register:

1. the SDK extension
2. the local wrapper or repository
3. higher-level services that depend on the wrapper

Business services should depend on the wrapper or higher-level abstraction, not on `HttpClient`.

## MeshBoard Refactor Application

For this repository, the migration plan must follow this structure:

- `MeshBoard.Api`
  - owns the HTTP server endpoints
- `MeshBoard.Api.SDK`
  - owns Refit endpoint interfaces for `MeshBoard.Api`
  - owns HTTP/DI registration for browser and worker consumers
- `MeshBoard.Client`
  - consumes `MeshBoard.Api.SDK`
  - wraps SDK interfaces in client-local services that manage session state, antiforgery flow, caching, or UI-oriented translation
- future workers or tools
  - also consume `MeshBoard.Api.SDK` through their own local wrappers when needed

Examples for MeshBoard:

- auth endpoints belong in `MeshBoard.Api.SDK/API/`
- broker preference endpoints belong in `MeshBoard.Api.SDK/API/`
- topic preset endpoints belong in `MeshBoard.Api.SDK/API/`
- favorites endpoints belong in `MeshBoard.Api.SDK/API/`
- SDK registration belongs in `MeshBoard.Api.SDK/DI/`
- browser session state and antiforgery token caching belong in `MeshBoard.Client`, not in the SDK

## Practical Notes

- If route constants need to be shared, prefer a shared contracts or routes project over making the SDK depend on the API server project.
- Returning `null` for specific remote failures is acceptable only when the caller is explicitly designed for that behavior.
- Keep the SDK small and transport-focused. If logic becomes domain-specific, move it into the consumer wrapper.
