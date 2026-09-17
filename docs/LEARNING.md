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
