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
│   │   ├── Common/            IAppDbContext, ICurrentUser, ISmsSender
│   │   └── <Feature>/<UseCase>/   Command, Validator, Handler, Response
│   ├── Gym.Infrastructure/    EF Core, Identity, SMS, Hangfire.
│   │   ├── Persistence/       AppDbContext, Configurations/, Migrations/, Interceptors/
│   │   ├── Identity/
│   │   ├── Sms/
│   │   └── Jobs/
│   └── Gym.Api/               Program.cs, Endpoints/, auth setup
│       ├── Common/            ResultExtensions (error mapping), ProblemDetails contract
│       └── Filters/           ValidationFilter<T>
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

Frontend supporting packages (dependencies of the tools above, approved in task 0.7): `@radix-ui/react-direction` (Radix `DirectionProvider`), `@radix-ui/react-slot`, `class-variance-authority`, `clsx`, `tailwind-merge` and `lucide-react` (shadcn/ui), `@tailwindcss/vite`, `jsdom` and `@testing-library/jest-dom` (Vitest), ESLint with `typescript-eslint`, `eslint-plugin-react-hooks`, `eslint-plugin-react-refresh`, `eslint-config-prettier` and `globals`, Prettier, `@types/node` (types for `vite.config.ts`).

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

Every failure is an RFC 9457 ProblemDetails. `Gym.Api/Common/ResultExtensions.ToHttpResult()` is the only
place that chooses a status code, and `ProblemDetailsFields` names the extension fields the frontend reads:

```jsonc
// 409 — a Result failure
{ "title": "Conflict with the current state.", "status": 409,
  "detail": "Another member already uses that phone number.",   // English, for developers
  "code": "Members.PhoneAlreadyExists",                          // the contract lib/errors.ts maps
  "correlationId": "cb34971d8df84af3ac6f02b6bec50926" }          // same value as X-Correlation-Id

// 400 — ValidationFilter<T>. `errors` is keyed by JSON property name; each entry carries a code,
// not just a sentence, so the frontend can show a Persian message per field.
{ "title": "Validation failed.", "status": 400, "code": "General.ValidationFailed",
  "errors": { "phoneNumber": [ { "code": "Members.PhoneTooShort",
                                 "description": "Phone number is too short." } ] } }
```

Validators are FluentValidation `AbstractValidator<T>` classes in Application, registered by an assembly scan in
`AddApplication()`; every rule sets `.WithErrorCode("Feature.Reason")`. `ValidationFilter<T>` itself lives in
Gym.Api, because `IEndpointFilter` is an ASP.NET Core type and Application must stay host-free.

Unhandled exceptions → 500 ProblemDetails (`GlobalExceptionHandler`) with the correlation id and no internal
details — no message, no type name, no stack trace.
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

The harness lives in `tests/Gym.Api.IntegrationTests/Infrastructure/`:

| Type | Job |
|---|---|
| `GymApiFactory` | `WebApplicationFactory<Program>` running the real app in environment `Testing` |
| `DatabaseFixture` | starts the `postgres:18` container, migrates, owns the Respawner, hands out clients and scopes |
| `DatabaseCollectionDefinition` | the xUnit collection all database-backed classes join |
| `DatabaseTestBase` | resets the database before each test |

A database test is `[Collection(DatabaseCollectionDefinition.Name)]` plus `: DatabaseTestBase(fixture)`.
A **collection** fixture, not an assembly fixture, so the tests that need no database (result mapping, the
validation filter, the middleware) never wait for Docker; the container starts with the first database test
and is removed when the collection finishes. Tests inside the collection run serially, which is required
rather than incidental: they share one database, and a reset would delete a parallel test's rows.

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
- Serilog sinks in `appsettings*.json` are keyed objects (`"WriteTo": { "Console": {...} }`), not JSON arrays. Configuration files merge by key and arrays merge by index, so an array would make `appsettings.Development.json` able to add a sink only by counting entries in the base file.
- Serilog sinks default to the machine's culture. This project runs with `InvariantGlobalization=false` so the app can format Persian dates, so every sink passes `formatProvider` explicitly (`System.Globalization.CultureInfo::InvariantCulture` in configuration) — otherwise a machine set to `fa-IR` writes log timestamps in Persian digits and no log query matches them.
- `HttpResponseFeature.OnStarting` is a no-op on a plain `DefaultHttpContext`, so a middleware test that asserts on a deferred response header passes whether or not the middleware did anything. Tests use `RecordingResponseFeature` and fire the callbacks explicitly.
- A switch expression over an enum still needs a default arm: C# allows any underlying value, so covering every
  declared member silences CS8509 but raises CS8524. `ResultExtensions` throws in the default arm and a test walks
  `Enum.GetValues<ErrorType>()`, because with warnings-as-errors the alternative is an unbuildable file.
- CA1716 rejects type names that are keywords in another .NET language (`Error` is one in VB). Disabled in
  `.editorconfig` with a rationale: this is a C# application, not a published library.
- `AddProblemDetails()` already adds a `traceId` extension holding the whole W3C traceparent. The API adds
  `correlationId` as well, holding just the trace id — the same string `X-Correlation-Id` returns, which is what a
  user can actually read off a screen and quote.
- `dotnet test` needs Docker running from task 0.6 on, locally and in CI. The container is Testcontainers'
  own throwaway `postgres:18` on a random port, never the `gym-postgres` container from docker-compose.yml,
  so a test run cannot touch development data. Testcontainers also starts a `ryuk` reaper container that
  removes the rest if the test process is killed; it exits on its own shortly after the run.
- `WebApplicationFactory` cannot override configuration that `Program.cs` reads **before** `builder.Build()`.
  With minimal hosting, `AddInfrastructure(builder.Configuration)` has already run by the time the factory's
  `ConfigureAppConfiguration` delta is applied, so the app starts with no connection string at all. Pass such
  values as environment variables (`ConnectionStrings__Postgres`) before the factory is constructed — which is
  also how production supplies them.
- `Respawner.CreateAsync` throws "No tables found" against a schema whose only table is the ignored
  `__EFMigrationsHistory`. The fixture builds it lazily for that reason. Since task 1.1 there are always
  tables, so this only matters if the model is ever emptied again.
- Respawn also empties the Identity `roles` table. Tests that need the Owner and Staff roles get them by
  running `IdentitySeeder.SeedOwnerAsync`, not by assuming they survive from the migration.
- Respawn deletes rows, not tables, so `__EFMigrationsHistory` must be in `TablesToIgnore` — otherwise the
  next run finds a fully migrated database that believes it has never been migrated.
- Partial unique indexes: `HasIndex(...).IsUnique().HasFilter("checked_out_at IS NULL")`. The filter uses snake_case column names.
- The shadcn CLI reads the `@/` alias from the **root** `web/tsconfig.json`, not `tsconfig.app.json`. Without `paths` there, `npx shadcn add` writes into a literal `web/@/` folder and installs an unrelated npm package named `cn`. It also imports Slot from the all-in-one `radix-ui` package; this project uses `@radix-ui/react-slot`, so fix that import after adding a component.
- `/health` is mapped at the API root, not under `/api`, and is not in the OpenAPI document. The Vite proxy has a separate rule for it and the frontend reads it with plain `fetch`. An unhealthy server answers 503 with the body `Unhealthy`, which is a report, not a failed request.
- Write invisible and look-alike characters (ZWNJ, Arabic ي/ك) as `\u` escapes in source. ESLint rejects literal ones (`no-irregular-whitespace`), and a ZWJ inside a regex character class trips `no-misleading-character-class` even when escaped, so use an alternation there.
- `IdentityDbContext.OnModelCreating` names the Identity tables `AspNetUsers`, `AspNetRoles` and so on.
  `AppDbContext` calls `base.OnModelCreating` first and applies our configurations after it, otherwise
  `ToTable("users")` is silently overwritten. Identity's own index names (`UserNameIndex`, `RoleNameIndex`,
  `EmailIndex`) are explicit, so the snake_case convention leaves them as they are.
- Identity reports failures as an `IdentityResult`, not an exception. Always check `Succeeded`.
  `UserManager.CreateAsync` and `AddToRoleAsync` each save separately, so wrap them in a transaction when
  both must happen together.
- The Owner seeder runs in `Program.cs` after `builder.Build()`, which `WebApplicationFactory` reaches before
  the test fixture migrates. That is why the seeder checks `Seed:*` configuration before any database
  call, and why the Testing environment has no `Seed` section.
- Every endpoint needs an authenticated user unless it says `.AllowAnonymous()`: `AddJwtAuthentication` sets a
  fallback authorization policy, so an endpoint that forgets its policy fails closed (401), not open. `/health`,
  login and the dev-only OpenAPI/Scalar routes are anonymous on purpose.
- JWT validation uses `ClockSkew = TimeSpan.Zero`. The default five minutes would make a 15-minute token live 20.
  `MapInboundClaims = false` keeps the claim names `sub` and `role` instead of long XML-namespace URIs.
- `Jwt:SigningKey` is a secret of at least 32 bytes. Locally: `dotnet user-secrets set "Jwt:SigningKey" "<random>"
  --project src/Gym.Api`. In production: `Jwt__SigningKey`. The API refuses to start without it.
- The login rate limit partitions by `RemoteIpAddress`. Behind Caddy that is Caddy's address for every request, so
  all users would share one bucket. Configure forwarded headers (`UseForwardedHeaders` with Caddy as a known proxy)
  before deploying; tracked for task 11.2.
- The integration test host raises the login rate limit (`RateLimiting__Login__PermitLimit`), because every test
  client shares one address. A test that needs different settings uses `DatabaseFixture.CreateClient(settings)`,
  which builds a separate host with its own singletons.
- Identity's `UserManager` methods take no `CancellationToken`. Check the token once before starting the work.
- The refresh token cookie is `Secure`, `HttpOnly`, `SameSite=Strict`, `Path=/api/auth`, written only through
  `Gym.Api/Common/RefreshTokenCookie`. Clearing a cookie needs the same name and path it was set with. In development
  the Vite proxy makes the API same-origin, so the browser sends the cookie; `http://localhost` counts as secure.
- The integration test client does not keep cookies (`HandleCookies = false`): a cookie jar drops `Secure` cookies
  over the test server's plain HTTP. Tests read `Set-Cookie` and send `Cookie` themselves (`Auth/RefreshCookies.cs`).
- `IAppDbContext` exposes `ChangeTracker` for one reason: after a `DbUpdateConcurrencyException` the tracked entities
  hold values that never reached the database, and `ChangeTracker.Clear()` lets the handler load fresh rows in the
  same request (`RefreshHandler`).
- Expired and revoked `refresh_tokens` rows are never deleted by the app yet. A cleanup job belongs with Hangfire.
- Authorization policies live in `Gym.Api/Authorization/Policies.cs`: `OwnerOnly`, `StaffOrOwner`,
  `PasswordChangeAllowed`. Every policy except `PasswordChangeAllowed` includes `PasswordChangedRequirement` (the forced
  password change gate), and so does the fallback. `EndpointAuthorizationTests` fails if an endpoint declares neither a
  policy nor `AllowAnonymous()`.
- Authorization failures are ProblemDetails too (`ProblemDetailsAuthorizationResultHandler`): 401 `Auth.Unauthenticated`,
  403 `Auth.PasswordChangeRequired` when the gate failed, 403 `Auth.Forbidden` otherwise.
- `ICurrentUser` (Application) is implemented in Gym.Api from the `sub` claim through `IHttpContextAccessor`. It is a
  singleton so the singleton audit interceptor can use it; the accessor resolves the current request on every call.
  Code that builds the lower layers without the Api host (tests) must register its own `ICurrentUser`.
- `AuditableEntityInterceptorTests` drive the interceptor without a database. To test a *modified* entity, start from
  `AddAndSaveAsync`, which accepts the changes; otherwise the entity is still `Added` and gets insert stamps again.
- A use case that saves through both `UserManager` and `IAppDbContext` wraps them in `IAppDbContext.BeginTransactionAsync`;
  both use the same scoped context. Anything that must survive a rollback, like a failed password attempt, happens before
  the transaction starts.
- Paged lists return `PagedResponse<T>` (`Items`, `Page`, `PageSize`, `TotalCount`) and validate their query with
  `PagingRules.ValidPage()` / `ValidPageSize()`. Query-string records bind with `[AsParameters]`, and
  `ValidationFilter<T>` validates them like a body.
- Endpoints that share a policy put it on the `MapGroup` (`StaffEndpoints`), so a new endpoint in the group cannot be
  added without it.
- User names and passwords have one rule each in Application (`UserNamePolicy`, `PasswordPolicy`), used by both the
  FluentValidation rules (`PasswordRules.ValidNewPassword`) and Identity's options, so the form and the database agree.

