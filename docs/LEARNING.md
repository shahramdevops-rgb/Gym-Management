# Learning Journal

Claude appends new concepts here after each task. Add your own notes and questions too.

Format:

## <task id> — <task name>
- **Concept:** one-sentence explanation
- **My notes:**

---

## 0.1 — Repository and solution skeleton

- **The dependency rule:** Clean Architecture is enforced by which project may reference which, so `Gym.Domain` has zero references and every arrow points inward — this is why an entity can never call EF Core.
- **`Directory.Build.props`:** MSBuild walks up from each `.csproj` and imports the nearest one, so `Nullable`, `ImplicitUsings` and `TreatWarningsAsErrors` are declared once and cannot drift per project.
- **Layered props files:** MSBuild imports only the *nearest* `Directory.Build.props`, so `tests/Directory.Build.props` must import the root one explicitly with `GetPathOfFileAbove` before adding its test-only settings.
- **Central Package Management:** `Directory.Packages.props` holds every version and `.csproj` files reference packages by name only, so two projects can never disagree on a version.
- **`global.json`:** pins the SDK band (`10.0.401` + `rollForward: latestPatch`) so this machine and CI compile with the same compiler instead of silently diverging.
- **Warnings as errors:** cheap on day one and expensive later, because a codebase with 200 tolerated warnings is one where nobody reads warnings at all.
- **Analyzer rules vs project conventions:** CA1707 forbids underscores in member names, which contradicts the mandated `Method_Scenario_ExpectedResult` test naming; the fix is to scope the rule off for `tests/**` in `.editorconfig`, not to rename the tests or silence the rule globally.
- **Architecture tests:** `Assembly.GetReferencedAssemblies()` turns the dependency rule into an executable assertion, so a forbidden reference fails the test run instead of surviving code review.
- **Trade-off behind those tests:** they read *compiled* references, so they are weak while assemblies are near-empty and get stronger as code lands; the airtight alternative (parsing `.csproj` files) is more brittle, and a dedicated library such as NetArchTest is not on the approved list.
- **Microsoft Testing Platform:** the .NET 10 SDK dropped the VSTest bridge, so xunit.v3 test projects are executables hosting their own runner and `global.json` opts `dotnet test` in with `"test": { "runner": "Microsoft.Testing.Platform" }`.
- **`.gitattributes` and `.editorconfig` must agree:** git on Windows converts to CRLF by default, which would silently contradict `end_of_line = lf`; `* text=auto eol=lf` settles it for both the repo and the working tree.
- **My notes:**

---

## 0.2 — Local infrastructure

- **Environment as code:** `docker-compose.yml` turns "install Postgres and Seq" into a file under version control, so a new machine, a teammate and CI all get byte-identical infrastructure instead of following a wiki page that drifted.
- **Running is not ready:** Postgres accepts TCP connections seconds before it will accept queries, so `healthcheck` exists to distinguish *started* from *usable*. Task 0.6 depends on this: Testcontainers waits for a readiness signal rather than sleeping a guessed number of seconds.
- **A health check can only use tools the image actually ships.** The Seq image has neither `curl` nor `wget`, and its bundled `seqcli` is broken, so the probe is a hand-written HTTP request through one of bash's `/dev/tcp` sockets. The trade-off against the simpler "is the port open" check: an open port only proves something bound it, while a `200 OK` on `/health` proves Seq is actually serving.
- **Named volumes outlive containers:** the container is disposable and the volume is not, which is what makes `docker compose down` safe and `down -v` the deliberate destructive one.
- **The `postgres:18` mount point:** this image keeps `PGDATA` in a versioned subdirectory, so the volume mounts at `/var/lib/postgresql`. Mounting `/var/lib/postgresql/data` — correct for older tags, and what most tutorials still show — leaves the real data directory inside the container's writable layer, so the data silently disappears on every recreate.
- **Splitting a connection string:** everything non-secret is committed in `appsettings.Development.json` and only the password lives in user-secrets, under a separate `Postgres:Password` key. A configuration *value* cannot have child keys, which is why the password gets its own key instead of being layered onto the connection string; `AddInfrastructure()` recombines them with `NpgsqlConnectionStringBuilder` in task 0.3.
- **User-secrets are a `UserSecretsId` plus a file outside the repository** (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`). They are a development convenience and nothing more — production reads environment variables, because a developer's home directory is not a secret store.
- **Fail loudly on missing configuration:** compose's `${VAR:?message}` stops with that message instead of starting a database with an empty password. The trade-off is one extra setup step (`cp .env.example .env`) against never having a working credential committed by default — and defaults have a way of surviving into staging.
- **Guard tests for configuration:** the same value written in two files will eventually disagree, so `DevelopmentSettingsTests` reads the repository's own files and fails if `.env.example` and the connection string drift apart, or if a password ever appears in a committed file. It is a cheap executable version of a code-review rule nobody remembers to apply.
- **`dotnet test` forwards unknown arguments to the test application.** Under Microsoft Testing Platform there is no VSTest wrapper to absorb MSBuild-style flags, so `dotnet test --nologo` hands `--nologo` to the test app, which rejects it and reports "Zero tests ran" with exit code 5 — a build-succeeded, no-tests-found result that looks exactly like a discovery failure. Plain `dotnet test` works. The lesson generalises: when a runner reports zero tests but the test executable run directly passes, suspect the arguments before suspecting the packages.
- **My notes:**

---

## 0.3 — Persistence foundation

- **`IAppDbContext` is the seam, not a repository:** Application declares the contract and Infrastructure implements it, so the dependency arrow still points inward even though EF Core is doing the work. The trade-off for dropping `IRepository<T>`: Application references the EF Core *abstractions* (`DbSet<T>`, `SaveChangesAsync`) and gains full LINQ, but never references a provider — a wrapper around something that is already a repository and a unit of work would only have to re-expose LINQ to stay useful, and would cost a layer of indirection for nothing.
- **Why version 7 GUIDs:** a v4 GUID is random, so every insert lands at a random point in the primary key's B-tree and fragments it. `Guid.CreateVersion7()` puts a millisecond timestamp in the high bits, so ids sort by creation time and inserts append to the end of the index — the write pattern of an `int` identity, but generated in C# before the row is saved, which matters for assigning ids to related objects in one unit of work.
- **A `SaveChangesInterceptor` turns a convention into a guarantee:** forty use cases each remembering to set `UpdatedAt` is forty chances to forget. One interceptor underneath `SaveChanges` is zero. The general lesson: when a rule must hold for *every* write, put it below the write, not beside it.
- **Writing through the change tracker preserves encapsulation:** `entry.Property(e => e.CreatedAt).CurrentValue = now` reaches a **private** setter, because EF Core writes properties through its own accessors rather than through C# visibility. So the audit fields did not have to be made public for the interceptor's benefit, and CLAUDE.md's "entities have private setters" stays literally true.
- **Insert is not update:** `CreatedAt` is stamped only for `Added` entries and `UpdatedAt` only for `Modified` ones. Re-stamping `CreatedAt` on every save is the classic bug here — it passes a naive one-save test and silently destroys history in production, which is why there is a dedicated test for it.
- **One timestamp per save, not per row:** `GetUtcNow()` is called once and shared, so everything written in the same transaction agrees about when it happened.
- **snake_case as a convention, not a per-property attribute:** Postgres folds unquoted identifiers to lower case, so a `CreatedAt` column would have to be quoted forever. `UseSnakeCaseNamingConvention()` renames the whole model at once. Watch out: it also renames EF's own `__EFMigrationsHistory` columns.
- **An empty first migration is still worth having:** there are no entities yet, so `InitialCreate` creates only `__EFMigrationsHistory` — but it also fixes `AppDbContextModelSnapshot` as the baseline every later migration is diffed against, so task 1.1's Identity tables become a clean delta instead of one enormous first migration.
- **A health check should answer "can I do my job", not "am I running":** `AddDbContextCheck<AppDbContext>()` makes `/health` return `503 Unhealthy` when Postgres is unreachable. A process-only probe would let an orchestrator route traffic to an API that cannot serve a single request. Verified by stopping the container and watching the endpoint flip to 503 and back.
- **Design-time is a separate execution context:** `dotnet ef` builds and runs the startup project's host to find `AppDbContext`, and it reads `Properties/launchSettings.json` for environment variables. Without an `ASPNETCORE_ENVIRONMENT=Development` profile there, the migration commands run as Production and never see `appsettings.Development.json` or user-secrets. The global `dotnet-ef` tool also carries its own version and must be at least as new as the EF Core packages.
- **Fail loudly at startup, not at the first query:** `AddInfrastructure` throws with the exact `dotnet user-secrets set` command when no password is configured, instead of letting the first request die with a bare authentication error — the same reasoning as `${VAR:?message}` in `docker-compose.yml`.
- **You can test EF Core without a database:** building a model and tracking changes happen entirely in memory; a connection is opened only when a query or `SaveChanges` actually runs. So column naming, type mappings and the interceptor's behaviour are all testable with the *real Npgsql provider* and no Docker — which is not the same thing as the banned InMemory provider, whose whole problem is that it is a *different* provider with different semantics. Real database behaviour (constraints, concurrency, transactions) still needs Testcontainers, which arrives in task 0.6.
- **`FakeTimeProvider` is why `DateTime.UtcNow` is banned:** the interceptor's clock is injected, so a test can pin "created on 21 March" and "updated on 22 March" and assert exact values instead of asserting that something is "roughly now".
- **My notes:**
