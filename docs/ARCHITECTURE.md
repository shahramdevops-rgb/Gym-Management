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

Infrastructure: Docker Compose, GitHub Actions, Caddy.

- Development: `docker-compose.yml` runs postgres:18 and datalust/seq.
- Production: `docker-compose.prod.yml` runs the API, postgres:18 and Caddy on one Iranian VPS.
  No Seq — 2 GB of RAM has no room for it, so Serilog writes to the console and Docker's
  `json-file` driver rotates it. Shape and reasoning: `docs/adr/0003-deployment-topology.md`.

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
- Lists are paged (`page`, `pageSize`, max 100), ordered by a column plus `Id` as a tie-breaker so no row repeats or disappears between pages.
- "Today" comes from `IGymCalendar.Today()` (`Gym:TimeZone`, validated at startup). Domain methods take that
  `DateOnly` as a parameter and never read a clock.
- Use cases that change one member's subscriptions run inside a transaction and call
  `db.LockMemberAsync(memberId)` (`SELECT … FOR UPDATE` on the member row) before reading them, so concurrent
  requests for the same member take turns instead of racing. The exclusion constraint
  `ex_subscriptions_no_overlap` (btree_gist, written by hand in the `AddSubscriptions` migration) is the safety net.
- "Contains" searches (`LIKE '%…%'`) use a GIN trigram index (`HasMethod("gin").HasOperators("gin_trgm_ops")`). The `pg_trgm` extension ships with Postgres and is enabled in `AppDbContext` with `HasPostgresExtension`. User input is escaped (`%`, `_`, `\`) before it goes into a pattern.

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
- Auth lives in `features/auth/`: `session.ts` holds the access token in a module (read with `useSessionState`),
  `lib/api/authFetch.ts` adds the bearer token to every `api` call and, on a 401, runs one shared refresh and retries
  once. `restoreSession()` in `main.tsx` turns the refresh cookie back into a session after a reload.
- Route guards: `RequireAuth` (signed in; users with a temporary password are sent to change-password) and
  `RequireRole` (for Owner-only pages). The navigation in `AppShell` lists only items the user's roles allow. The API
  enforces all of this again; the guards only avoid showing screens that would fail.
- Forms: React Hook Form with Zod schemas through `lib/forms.ts` (`zodResolver`, `applyServerErrors`). Server field
  errors land under their field; a code can be routed to a field (`Auth.CurrentPasswordIncorrect` → current password).
- Every API error code has a Persian message in `lib/errors.ts`. `ErrorCatalogTests` (.NET) collects every code the API
  defines by reflection and fails when one is missing there.
- List state lives in the URL (`/?q=علی&page=2`, `/members?status=inactive`), read with `useSearchParams`, so reload,
  back and a shared link return to the same results. Typing updates the URL with `replace`; paging and filters push.
- Search boxes debounce the call (`lib/useDebouncedCallback.ts`) in the change handler, not a value in an effect.
- Query keys start with the feature (`["members", "list", filter]`, `["members", "detail", id]`). A mutation writes
  the server's answer into the detail entry and invalidates the lists.
- A form fed by the server is keyed by the entity's `version`, so fresh data rebuilds it with the new values.
- Money goes to the API as a decimal string (`"price": "1500000.50"`), never a JS number: the allowed range has more
  digits than a float holds exactly, and ASP.NET reads a JSON string into `decimal` directly. Number inputs stay text
  in the form, are validated after digit normalization, and are converted only when sent (`features/plans/schemas.ts`).
- Phone numbers and other LTR runs inside RTL text are wrapped in `dir="ltr"`, or their digit groups display reversed.

## Gotchas
- `postgres:18` image: mount the volume at `/var/lib/postgresql`, not `/var/lib/postgresql/data`.
- Npgsql `timestamptz`: use `DateTimeOffset` with offset zero (or `DateTime` with `Kind = Utc`). Non-UTC values throw.
- `xmin` concurrency: a `uint Version` property configured with `.IsRowVersion()`.
- Do not use `MapIdentityApi()`: it adds a register endpoint and uses its own token format.
- Migrations are never applied automatically at app startup in production. Use an EF migration bundle during deployment.
- A consequence of that, and the one that actually bites: after pulling changes, run
  `dotnet ef database update` before running the app. Nothing applies migrations for you, and the app starts
  perfectly happily against an old schema — the first symptom is an unexplained 500 on whatever screen writes
  to the changed table (a dropped column that is still `NOT NULL`, a new column that does not exist yet), which
  looks like a code bug and is not. `/health` now reports this as `Unhealthy` and names the missing migrations
  (`PendingMigrationsHealthCheck`), so the Persian status page answers the question before anyone has to guess.
- The production runtime image must carry both ICU and tzdata. `InvariantGlobalization=false` (Directory.Build.props)
  needs ICU to format Persian dates and digits, and `Gym:TimeZone = Asia/Tehran` needs tzdata to resolve at all.
  The Debian-based `mcr.microsoft.com/dotnet/aspnet` image carries both; an Alpine variant needs `icu-libs` and
  `tzdata` installed explicitly. The startup validation of `Gym:TimeZone` is the check — an image missing either
  fails to start rather than serving wrong dates quietly.
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
- A **deferred** constraint is checked at `COMMIT`, and its error comes from the commit as a bare
  `PostgresException`, not wrapped in `DbUpdateException`. Deferred by default, the subscription exclusion
  constraint also made parallel inserts deadlock at commit (40P01), each waiting for the other's row. It is
  `DEFERRABLE INITIALLY IMMEDIATE`; a transaction that must pass through a moment of overlap runs
  `SET CONSTRAINTS ex_subscriptions_no_overlap DEFERRED` and handles the error at `CommitAsync`.
- `ON DELETE RESTRICT` reports SQLSTATE 23001 (`restrict_violation`), not 23503 (`foreign_key_violation`).
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
  before deploying; tracked for task 6.2. It moved there from task 11.2 when the server turned out to be an
  internet-facing VPS rather than a machine on the gym's own network (ADR 0003): sharing one rate-limit bucket
  is a defect once the login endpoint is publicly reachable, not a later cleanup.
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
- Two `SaveChangesInterceptor`s run in order: `AuditableEntityInterceptor` stamps the audit fields, then
  `AuditLogInterceptor` adds an `AuditLog` row per changed entity in the same save. Sensitive and noisy properties are
  excluded by name in `AuditLogInterceptor.ExcludedProperties`; a new secret column must use one of those names or be
  added there.
- `audit_logs` is append-only through a trigger created with `migrationBuilder.Sql` in the `AddAuditLog` migration. EF
  has no model API for triggers, so it is not in the snapshot: a later migration will not see it, and dropping or
  renaming the table must handle the trigger by hand.
- Respawn empties tables with `TRUNCATE`, which row-level triggers ignore, so the append-only trigger does not break the
  test reset. `AuditLogTests.Reset_BetweenTests_EmptiesTheAuditLogDespiteTheTrigger` guards that.
- The audit log records keys generated in C#. An entity with a database-generated key makes `AuditLogInterceptor` throw
  instead of recording a placeholder id; give such an entity a client-generated key.
- `openapi-fetch` is created with `baseUrl: window.location.origin`, not `"/"`: outside a browser (Vitest) the `Request`
  constructor rejects relative URLs.
- Frontend tests stub `fetch` with `test/mockApi.ts`, a router keyed by `"METHOD /path"`. Everything above fetch
  (openapi-fetch, `authFetch`, TanStack Query, React Router) runs for real. `renderApp(url, { session })` starts signed in
  or out; `test/setup.ts` resets the session store after each test because it lives outside React.
- `[AsParameters]` query records name their OpenAPI parameters after the C# record (`Page`, `PageSize`); the frontend
  uses those names so the generated types check them.
- shadcn's `CardTitle` is a `div`. A card title that is the page's title gets `role="heading"` and an `aria-level`.
- Editing tools and shells can turn a `\u064A` escape into the real character. To keep a `\u` escape in a source file,
  write it with a script that builds the backslash explicitly (Python `chr(92)`), then check the bytes.
- Git Bash on Windows can mangle Persian command-line arguments on their way to `curl` (they arrive as `?`). Send
  Persian request bodies from a UTF-8 file with `--data-binary @file`.
- Failed-login counting is one atomic SQL UPDATE (`UserAuthenticator.RecordFailedAttemptAsync`), not Identity's
  `AccessFailedAsync`, which reads, adds in C# and saves with a concurrency check: parallel wrong passwords all counted
  once and the rest answered 500. The update leaves `ConcurrencyStamp` alone, and as a bulk update it bypasses the
  change tracker and the audit interceptor. Code that already holds the tracked `User` sees stale counter values.
- Identity `UpdateAsync` returns `ConcurrencyFailure` instead of throwing. Staff actions map it to 409
  `Staff.ChangedConcurrently`; anything else from Identity is unexpected and throws.
- `RefreshHandler` retries a family revocation after `DbUpdateConcurrencyException` (clearing the tracker each time), so a
  reuse racing with a rotation still revokes the family and never answers 500.
- `authFetch` refreshes only on a 401 whose code is `Auth.Unauthenticated` (the token was rejected). Other 401s come from
  handlers with a valid token; `Auth.UserInactive` signs the tab out. Refresh runs under the Web Lock `gym-refresh`, so
  tabs sharing the cookie refresh one at a time, and a refresh that returns another user's token signs the tab out.
- `clearCacheWhenUserChanges` (in `app/queryClient.ts`) empties the TanStack Query cache on every sign-out and whenever
  the token's `sub` changes, not only on the logout button.
- Persian text rules (§13) live once on the backend, in `Gym.Domain/Common/Text/PersianText` (`Normalize`,
  `NormalizeDigits`); the frontend's `lib/normalize.ts` mirrors them for input. Special characters in C# are written as
  numeric code points (`(char)0x064A`), not `\u` escapes: the editing tools turn an escape into the invisible or
  look-alike character itself. Tests build such inputs from code points too.
- Phone numbers go through `IPhoneNormalizer` (Infrastructure: `LibPhoneNumberNormalizer`, region `Gym:PhoneDefaultRegion`
  = `IR`) before saving or searching, and are stored as E.164. A check constraint rejects anything else, so the unique
  index compares like with like.
- `AppDbContext.SaveChangesAsync` turns a Postgres unique violation (23505) into Application's `UniqueConstraintException`
  (a `DbUpdateException` naming the index). Handlers check for duplicates first for a friendly answer, then catch this
  for the race only the index can decide. Index names a handler reacts to are constants (`MemberConstraints`) shared
  with the configuration.
- Editable entities return their `Version` (`xmin`) and take it back on update. The handler refuses a mismatch
  (someone saved while the form was open), and `xmin` on save covers the moment between read and write.

- `UseSnakeCaseNamingConvention()` overwrites an index name given as the second argument of `HasIndex(…, "name")`
  (it produced `ix_members_phone_number1`). A second index on the same column needs `.HasDatabaseName(...)` too.
- Postgres `timestamptz` keeps microseconds; .NET ticks are 100 ns. A response built from the in-memory entity right
  after a save can carry a digit the stored value lost, so tests compare timestamps with a 1 µs tolerance.
- Tools can silently turn a `\u064A`-style escape into the character it names, in a heredoc or a script as well as
  in an editor. A script that must write the escape builds the backslash with `String.fromCharCode(92)`, and the
  result is checked by code point, since grep and the screen cannot tell ي from ی.
- React 19 builds `FormData` from a submitted form. In tests, `fireEvent.submit` must target the `<form>`
  (`getByRole("search")`), not an input inside it, or jsdom throws an unhandled error.
