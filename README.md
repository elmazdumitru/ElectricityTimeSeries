# Electricity Market Time Series API

A small REST API built with C# and ASP.NET Core for managing electricity market time series data, such as production, consumption and cross-border flows between bidding zones.

> **Status:** the CRUD endpoints, filtering, validation, authentication and authorization, tests and CI are in place. Data is stored in memory through EF Core; moving to SQL Server is the planned next step.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`global.json` requires 10.0.100 or any later 10.0 SDK)
- [Docker](https://www.docker.com/) with Docker Compose, optional: only for running the API in a container

## Running the API

```powershell
dotnet run --project src/ElectricityTimeSeries.Api --launch-profile https
```

The API listens on `https://localhost:7244` and `http://localhost:5020`. In Visual Studio, select the `https` or `http` launch profile.

### Running in Docker

`docker-compose.yml` runs the API in a container, in the `Development` environment, at `http://localhost:5020`:

```powershell
docker compose up --detach --build
docker compose down
```

The container uses the same tokens as `dotnet run`: the host's user secrets folder, which holds the `dotnet user-jwts` signing key, is mounted read-only into the container. On Windows this works as is. On Linux or macOS, set `USER_SECRETS_PATH=~/.microsoft/usersecrets` first. Port 5020 is shared with the `http` launch profile, so stop one before starting the other.

In the Development environment, the OpenAPI document is served at `/openapi/v1.json`. A health endpoint is available at `/health`. Both are anonymous; every other endpoint requires a bearer token (see below).

### Getting a token for local development

The API uses JWT bearer tokens. Locally, tokens are issued by the [`dotnet user-jwts`](https://learn.microsoft.com/aspnet/core/security/authentication/jwt-authn) tool that ships with the .NET SDK, so no identity server is needed. The signing key is stored in your user secrets and the matching issuer/audience settings are already in `appsettings.Development.json`.

```powershell
# Token with full access (read + write)
dotnet user-jwts create --project src/ElectricityTimeSeries.Api --name dev-user --scope TimeSeries.Read --scope TimeSeries.Write

# Read-only token
dotnet user-jwts create --project src/ElectricityTimeSeries.Api --name reader --scope TimeSeries.Read
```

Send the printed token in the `Authorization` header:

```
Authorization: Bearer <token>
```

User secrets are per machine, so each developer runs the commands above once after cloning.

## API

| Method | URL | Scope | Result |
|---|---|---|---|
| `GET` | `/api/time-series?biddingZone=&type=&from=&to=` | `TimeSeries.Read` | `200` with the matching series |
| `GET` | `/api/time-series/{id}` | `TimeSeries.Read` | `200`, or `404` |
| `POST` | `/api/time-series` | `TimeSeries.Write` | `201` with a `Location` header |
| `PUT` | `/api/time-series/{id}` | `TimeSeries.Write` | `200` with the replaced series, or `404` |
| `DELETE` | `/api/time-series/{id}` | `TimeSeries.Write` | `204`, or `404` |

Example body for `POST` and `PUT`:

```json
{
  "type": "CrossBorderFlow",
  "biddingZone": "NO2",
  "toBiddingZone": "DK1",
  "resolution": "PT15M",
  "points": [
    { "timestamp": "2025-11-22T00:00:00Z", "value": 1250.5 },
    { "timestamp": "2025-11-22T00:15:00Z", "value": 1190.0 }
  ]
}
```

### Data model

A **time series** is a block of evenly spaced values for one bidding zone, for example "consumption in NO1 on 22 November, every 15 minutes". This matches how market data is usually exchanged: a whole block is sent, replaced or removed at once.

| Field | Meaning |
|---|---|
| `type` | `Production`, `Consumption` or `CrossBorderFlow` |
| `biddingZone` | The zone, for example `NO1`. For a flow, the zone the electricity leaves. |
| `toBiddingZone` | For a flow only: the zone the electricity goes to |
| `resolution` | How long each value lasts, as an ISO 8601 duration: `PT15M` or `PT1H` |
| `points` | The values. Each covers the period from its `timestamp` to `timestamp` + `resolution`. |
| `start`, `end` | Returned by the API: when the first point starts and the last point ends |

### Searching

- `biddingZone` matches series in that zone, and flows into or out of it.
- `from` (inclusive) and `to` (exclusive) return the series that have data in that interval, with **only the points inside it**. A point that partly overlaps the interval is included.
- All filters are optional and can be combined. Times must include a time zone (`Z` or an offset such as `%2B01:00`, where `%2B` is a URL-encoded `+`).

### Validation

Requests that break a rule return `400 Bad Request` with the list of invalid fields, in the Problem Details format. The rules:

- The bidding zones must be known (see [Assumptions](#assumptions)). A flow needs a different receiving zone; other types can't have one.
- The resolution must be 15 minutes or 1 hour.
- There must be between 1 and 35,136 points (one year of 15-minute values).
- Each point must start on a resolution boundary (for example 10:15, not 10:07) and follow the previous one by exactly one step: no gaps, duplicates or reordering.
- Production and consumption can't be negative. Flows can, as a negative value means the electricity flows the other way.
- In a search, `from` must be earlier than `to`.

## Solution structure

```
ElectricityTimeSeries/
├── ElectricityTimeSeries.slnx
├── global.json
├── Dockerfile                                 API container image
├── docker-compose.yml                         API in a container, for local development
├── .github/workflows/ci.yml                   GitHub Actions pipeline
├── src/
│   ├── ElectricityTimeSeries.Api/             Presentation: controllers, request/response contracts, startup wiring
│   ├── ElectricityTimeSeries.Core/            Domain + application: entities, business rules, services, abstractions
│   └── ElectricityTimeSeries.Infrastructure/  Data access: storage and repository implementations
└── tests/
    ├── ElectricityTimeSeries.UnitTests/               Isolated logic: business rules, authorization handler
    └── ElectricityTimeSeries.Api.IntegrationTests/    The API hosted in memory, with a fake identity provider
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
- **Authentication and authorization live in the Api layer.** They are HTTP concerns, so Core and Infrastructure stay unaware of them (see [Security](#security)).
- **The domain class is also the stored class.** There are two sets of classes, not three: request/response classes in Api, and domain classes in Core. EF Core saves the domain classes directly, and how they map to storage is described in Infrastructure (`TimeSeriesConfiguration`), so Core has no storage code. The only concession is a private constructor and private setters that EF Core uses when loading data.
- **The domain enforces its own rules.** A `TimeSeries` can only be created or changed through `Create` and `Update`, which check every business rule, so an invalid series can't exist. A broken rule throws `DomainValidationException`, which the Api turns into a `400` response.
- **EF Core with in-memory storage, for now.** The assignment allows in-memory storage. Using EF Core already means moving to SQL Server is a change inside Infrastructure only: replace `UseInMemoryDatabase` with `UseSqlServer` and add migrations. The column sizes, precision and indexes for that are already configured.
- **Simplified layering.** A full Clean Architecture template usually splits Core into separate Domain and Application projects. Given the scope of this API, they are merged into a single Core project, which keeps the solution easy to navigate while preserving the dependency rule.

## Security

**Authentication** uses JWT bearer tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`). The API validates each token's signature, issuer, audience and lifetime. Bearer options are bound from the `Authentication:Schemes:Bearer` configuration section, so moving from `dotnet user-jwts` to a real identity provider (Microsoft Entra ID, Keycloak, Auth0, …) is a configuration change only: set `Authority` and `ValidAudiences` for that provider.

**Authorization** is policy-based, driven by scopes in the token:

| Policy | Required scope | Applies to |
|---|---|---|
| `CanReadTimeSeries` | `TimeSeries.Read` | `GET` endpoints |
| `CanWriteTimeSeries` | `TimeSeries.Write` | `POST`, `PUT`, `DELETE` endpoints |

- **Scopes and policies are separate concepts.** Scopes (`Scopes`) are the identity provider's contract: the exact values issued in tokens. Policies (`Policies`) are the API's own authorization rules, which controllers reference with `[Authorize(Policy = ...)]`. A policy can later combine several requirements (for example a scope plus a role), or follow a renamed scope, without touching any controller.
- **Scope checks use a requirement and a handler**, the same pattern as the framework's built-in checks: `ScopeRequirement` states which scope is needed, and `ScopeAuthorizationHandler` (resolved from DI) evaluates it. The rule is defined once, reused by every policy, and unit testable without hosting the API.
- Read and write are separate because most consumers of market data (monitoring, analysis) only read, while only data-producing systems should change it. A token needs both scopes for full access.
- A **fallback policy** requires an authenticated caller on every endpoint by default. Anonymous endpoints (`/health`, the OpenAPI document) must opt out explicitly, so a new endpoint can't be exposed by accident.
- Scopes are read from the `scope` claim (OAuth 2.0 standard, used by `dotnet user-jwts`) and the `scp` claim (Microsoft Entra ID), including space-separated values.
- A missing or invalid token returns `401 Unauthorized`; a valid token without the required scope returns `403 Forbidden`. Both use the Problem Details format.

## Testing

The tests are split into two levels. Neither needs Docker, an identity provider or any local setup.

| Level | What it runs against | Tokens |
|---|---|---|
| **Unit** | Isolated classes: the time series rules, `HasScope()`, `ScopeAuthorizationHandler` | none |
| **Integration** | The full API hosted in memory with `WebApplicationFactory`, called over HTTP | signed by the test project with its own key pair |

```powershell
dotnet test
```

### Integration tests

- The API runs in a dedicated `Testing` environment, so user secrets and `appsettings.Development.json` are never loaded. The tests behave the same on every machine and in CI.
- The API is set up **as it is delivered**: it trusts an identity provider (`Authority` plus audience) and downloads that provider's public keys when it needs them. The only test-specific setting is where those downloads go: `FakeIdentityProvider`, which answers them inside the test process.
- Each test run creates a **public/private key pair** (`TestSigningKeys`). `TestTokenFactory` signs tokens with the private key; the API only ever sees the public key, published by the fake identity provider in the same format Keycloak or Entra ID use. Nothing secret is shared between the tests and the API.
- Requests go through the API's real token checks, so the tests also cover expired, wrongly addressed, wrongly signed and tampered tokens, and the trick of signing with the public key as if it were a password.
- The time series endpoints are tested with raw JSON requests, so the tests also pin down the public JSON format: field names, type names and `PT15M` durations. They cover each operation, searching, every validation rule, `404` for unknown ids, and that each endpoint needs the right scope.
- Each test host gets its own in-memory store. Tests in the same class share a host, so each test uses its own data and only checks the series it created.
- Authorization is tested through test-only endpoints (`AuthorizationProbeController`) that use the real policies. They are added only inside the test host and never ship with the API.

**Not covered by the integration tests:** the network connection to a real identity provider (its address, HTTPS, and the provider's own setup).

### Continuous integration

`.github/workflows/ci.yml` runs on every push to `main` and on every pull request. It restores and builds in Release, runs the unit and integration tests, and uploads the test results.

## Assumptions

- Access is granted per scope (read/write) for all time series. Finer-grained rules, such as restricting a client to specific bidding zones, are out of scope.
- **Bidding zones:** the Nordic zones (`NO1`–`NO5`, `SE1`–`SE4`, `FI`, `DK1`, `DK2`) and the neighbouring zones they trade with (`EE`, `LV`, `LT`, `PL`, `DE-LU`, `NL`, `GB`). Codes are case-insensitive and stored in upper case.
- **Units:** all values are in MW, the average power over each point's period.
- **Resolution:** 15 minutes (the Nordic market time unit since 2025) or 1 hour (older data).
- **Time zones:** clients may send any offset; times are stored and returned in UTC.
- **Overlaps are allowed:** several series may cover the same zone, type and time. The API doesn't decide which one is correct.
- **Last write wins:** two clients updating the same series at the same time aren't detected.
- **No paging:** a search returns every match. Clients are expected to narrow it with filters.
- **Data is lost on restart,** because storage is in memory.
