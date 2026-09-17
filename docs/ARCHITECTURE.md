# Architecture

## Style
Modular monolith using Clean Architecture, organized by feature inside each layer.

```
Api  →  Application  →  Domain
          ↑
    Infrastructure (implements Application interfaces)
```

## Solution layout

```
GymManagement/
├── src/
│   ├── Gym.Domain/            Entities, value objects, domain errors. No dependencies.
│   │   ├── Common/            Entity base, Result, Error
│   │   └── <Feature>/         e.g. Members/, Subscriptions/, Attendance/
│   ├── Gym.Application/       Use cases. References Domain only.
│   │   ├── Common/            IAppDbContext, ICurrentUser, ISmsSender, ValidationFilter
│   │   └── <Feature>/<UseCase>/   Command, Validator, Handler, Response
│   ├── Gym.Infrastructure/    EF Core, Identity, SMS, Hangfire.
│   │   ├── Persistence/       AppDbContext, Configurations/, Migrations/, Interceptors/
│   │   ├── Identity/
│   │   ├── Sms/
│   │   └── Jobs/
│   └── Gym.Api/               Program.cs, Endpoints/, error mapping, auth setup
├── web/                       React frontend
├── tests/
│   ├── Gym.Domain.Tests/
│   └── Gym.Api.IntegrationTests/
├── docs/
├── docker-compose.yml
├── global.json
├── Directory.Build.props
└── Directory.Packages.props
```

Each layer exposes one DI extension: `AddApplication()`, `AddInfrastructure(configuration)`.

## Approved packages

Versions are pinned centrally in `Directory.Packages.props`. Ask before adding anything else.

| Purpose | Package |
|---|---|
| Runtime | .NET 10 SDK (pinned in global.json) |
| Database | Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore.Design |
| Database (Application layer) | Microsoft.EntityFrameworkCore — abstractions only, because `IAppDbContext` is declared in terms of `DbSet<T>`; no provider ever reaches Application |
| Naming | EFCore.NamingConventions (snake_case) |
| Identity | Microsoft.AspNetCore.Identity.EntityFrameworkCore |
| JWT | Microsoft.AspNetCore.Authentication.JwtBearer |
| Validation | FluentValidation, FluentValidation.DependencyInjectionExtensions |
| Logging | Serilog.AspNetCore, Serilog.Sinks.Seq |
| API docs | Microsoft.AspNetCore.OpenApi, Scalar.AspNetCore |
| Health checks | Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore |
| Phones | libphonenumber-csharp |
| Jobs | Hangfire.AspNetCore, Hangfire.PostgreSql |
| Tests | xunit.v3, Shouldly, NSubstitute, Microsoft.AspNetCore.Mvc.Testing, Testcontainers.PostgreSql, Respawn, Microsoft.Extensions.TimeProvider.Testing |

Frontend: Vite, React, TypeScript, React Router, TanStack Query, React Hook Form, Zod, shadcn/ui, Tailwind CSS, openapi-typescript + openapi-fetch, Recharts, Vitest + Testing Library, date-fns-jalali (Jalali date math), react-multi-date-picker (Persian calendar picker), Vazirmatn font (self-hosted). Playwright for end-to-end tests (Phase 11).

Infrastructure: Docker Compose (postgres:18, datalust/seq), GitHub Actions, Caddy.

Built-in features used instead of packages: rate limiting, `TimeProvider`, ProblemDetails, `IExceptionHandler`, `Guid.CreateVersion7()`.

## Patterns

### Use case
```csharp
// Application/Members/CreateMember/CreateMemberHandler.cs
public sealed class CreateMemberHandler(IAppDbContext db, IPhoneNormalizer phones, TimeProvider time)
{
    public async Task<Result<MemberResponse>> Handle(CreateMemberCommand command, CancellationToken ct)
    {
        // 1. normalize input  2. check rules  3. call domain  4. save  5. map to response
    }
}
```

### Endpoint
```csharp
// Api/Endpoints/MembersEndpoints.cs
var group = app.MapGroup("/api/members").WithTags("Members");

group.MapPost("/", async (CreateMemberCommand command, CreateMemberHandler handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult())
    .AddEndpointFilter<ValidationFilter<CreateMemberCommand>>()
    .RequireAuthorization(Policies.StaffOrOwner);
```

### Errors
`Error(Code, Description, ErrorType)`. Codes look like `Members.PhoneAlreadyExists`.

| ErrorType | HTTP |
|---|---|
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |
| BusinessRule | 422 |

Unhandled exceptions → 500 ProblemDetails with a trace id and no internal details.
`DbUpdateConcurrencyException` and Postgres unique violations (SQLSTATE 23505) are translated to 409 where they are expected.

### Persistence
- One `IEntityTypeConfiguration<T>` per entity in `Persistence/Configurations/`.
- Primary keys: `Guid` generated with `Guid.CreateVersion7()`.
- Base entity fields `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` are set by an interceptor, not by handlers.
- Money: `HasPrecision(18, 2)` (large enough for Rial and Toman amounts).
- Read queries use `AsNoTracking()` and project straight to response records.
- Lists are paged (`page`, `pageSize`, max 100).

### Testing
- Integration tests share one Postgres container per test run; Respawn resets data between tests.
- The test fixture applies migrations with `Database.MigrateAsync()`.
- Helper to create an authenticated client per role.

## Frontend (Persian, RTL)

```
web/src/
├── app/            Router, providers (QueryClient, DirectionProvider), layout shell
├── features/       One folder per feature: members/, subscriptions/, attendance/...
│   └── members/    api.ts (queries/mutations), components/, pages/, schemas.ts (Zod)
├── components/ui/  shadcn/ui components (adjusted for RTL)
├── lib/
│   ├── api/        Generated types (do not edit) and the openapi-fetch client
│   ├── format.ts   Persian digits, money, Jalali dates (Intl with fa-IR)
│   ├── normalize.ts  Persian/Arabic characters and digits
│   └── errors.ts   API error code → Persian message
└── main.tsx
```

- Direction: `<html lang="fa" dir="rtl">` plus Radix `DirectionProvider dir="rtl"`. After adding a shadcn component, replace any physical left/right classes with logical ones and check icons that should mirror (arrows, chevrons).
- Dates: API sends and receives ISO dates (`2026-09-17`) and UTC timestamps. Display with `Intl.DateTimeFormat('fa-IR', ...)`, which uses the Persian calendar. Jalali ranges such as "this month" are computed in the frontend and sent as Gregorian dates.
- Numbers and money: `Intl.NumberFormat('fa-IR')`. Inputs convert Persian and Arabic digits to English digits before validation.
- Text: normalize Arabic ي and ك to Persian ی and ک on input and before search.
- Server state lives in TanStack Query. No global state library.
- The access token is kept in memory only. The refresh token is an HttpOnly cookie handled by the browser.

## Gotchas
- `postgres:18` image: mount the volume at `/var/lib/postgresql`, not `/var/lib/postgresql/data`.
- Npgsql `timestamptz`: use `DateTimeOffset` with offset zero (or `DateTime` with `Kind = Utc`). Non-UTC values throw.
- `xmin` concurrency: a `uint Version` property configured with `.IsRowVersion()`.
- Do not use `MapIdentityApi()`: it adds a register endpoint and uses its own token format.
- Migrations are never applied automatically at app startup in production. Use an EF migration bundle during deployment.
- Testcontainers needs Docker running, locally and in CI.
- The .NET 10 SDK no longer runs Microsoft.Testing.Platform tests through VSTest. xunit.v3 hosts its own runner, so test projects set `OutputType=Exe` and `TestingPlatformDotnetTestSupport=true`, `global.json` carries `"test": { "runner": "Microsoft.Testing.Platform" }`, and neither `Microsoft.NET.Test.Sdk` nor `xunit.runner.visualstudio` is referenced. Without the `global.json` opt-in, `dotnet test` fails with "Testing with VSTest target is no longer supported".
- Font files and scripts are self-hosted in the build, never loaded from a public CDN.
- `UseSnakeCaseNamingConvention()` also rewrites EF's own `__EFMigrationsHistory` columns to `migration_id` and `product_version`. The table name keeps its original casing, so querying it by hand needs `SELECT migration_id ... FROM "__EFMigrationsHistory"`.
- The `dotnet ef` tool is a global tool with its own version; it must be at least as new as the EF Core packages, otherwise design-time commands fail. `dotnet tool update --global dotnet-ef`.
- `dotnet ef` reads `src/Gym.Api/Properties/launchSettings.json`. Without an `ASPNETCORE_ENVIRONMENT=Development` profile there, design-time commands run as Production and never load `appsettings.Development.json` or user-secrets.
- Partial unique indexes: `HasIndex(...).IsUnique().HasFilter("checked_out_at IS NULL")`. The filter uses snake_case column names.
