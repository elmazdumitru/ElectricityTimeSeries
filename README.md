# Electricity Market Time Series API

A small REST API built with C# and ASP.NET Core for managing electricity market time series data, such as production, consumption and cross-border flows between bidding zones.

> **Status:** the solution structure and architecture are in place. The domain model, CRUD endpoints, storage and automated tests are in progress.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned in `global.json`, rolls forward to the latest 10.0 feature band)

## Running the API

```powershell
dotnet build
dotnet run --project src/ElectricityTimeSeries.Api
```

In the Development environment, the OpenAPI document is served at `/openapi/v1.json`.

## Solution structure

```
ElectricityTimeSeries/
├── ElectricityTimeSeries.slnx
├── global.json
└── src/
    ├── ElectricityTimeSeries.Api/             Presentation: controllers, request/response contracts, startup wiring
    ├── ElectricityTimeSeries.Core/            Domain + application: entities, business rules, services, abstractions
    └── ElectricityTimeSeries.Infrastructure/  Data access: storage and repository implementations
```

## Architecture

The solution follows **Clean Architecture** principles, built on the **Dependency Inversion Principle**: source code dependencies point inward, towards the business logic.

```
        ┌──────────► Core ◄──────────┐
       Api                    Infrastructure ──► storage
        └─ (Program.cs only) ────────┘
```

| Project | Responsibility | Depends on |
|---|---|---|
| **Core** | Domain entities, business rules, validation, application services, and the interfaces it needs from the outside world (for example `ITimeSeriesRepository`) | nothing |
| **Infrastructure** | Implements the Core interfaces against a concrete storage technology | Core |
| **Api** | HTTP endpoints, request/response contracts, error handling, and composing the application at startup | Core, Infrastructure (startup wiring only) |

### Key design decisions

- **Core owns the abstractions.** Core defines what it needs (for example a repository interface), and Infrastructure provides it. Business logic has no knowledge of how or where data is stored, so it can be unit tested without a database, and the storage technology can be replaced without touching Core or the API.
- **The API does not know about storage.** Controllers depend only on Core services and types. All Infrastructure types are `internal`; the only public member is the `AddInfrastructure()` registration method. Api references Infrastructure solely so that `Program.cs` can act as the *composition root*, the single place where implementations are wired to abstractions. The compiler prevents any other use.
- **Each layer registers its own services** through an extension method (`AddCore()`, `AddInfrastructure()`), which keeps `Program.cs` short and keeps each layer's internals private.
- **API contracts are separate from domain entities.** Request and response classes live in the Api project, so the public HTTP contract can change independently of the domain model and the storage model.
- **Consistent error responses.** Errors are returned as RFC 9457 Problem Details (`AddProblemDetails`, `UseExceptionHandler`, `UseStatusCodePages`).
- **Simplified layering.** A full Clean Architecture template usually splits Core into separate Domain and Application projects. Given the scope of this API, they are merged into a single Core project, which keeps the solution easy to navigate while preserving the dependency rule.

## Assumptions

- The API is a self-contained service without authentication or authorization; these are out of scope for the case.
- Further assumptions about the data model (time resolution, units, time zones) will be documented here as the model is implemented.
