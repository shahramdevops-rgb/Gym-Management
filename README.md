# Gym Management

Internal management system for a single gym: members, plans, subscriptions, payments,
lockers, attendance, cafe and reporting. Closed system — no public access and no member
logins. The API is .NET 10 / PostgreSQL; the frontend is a Persian, right-to-left React app.

See [docs/ROADMAP.md](docs/ROADMAP.md) for what is built and what is next,
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the stack and
[docs/BUSINESS_RULES.md](docs/BUSINESS_RULES.md) for how the gym works.

> This README is a skeleton. Task 0.8 fills in the architecture diagram, screenshots and
> the deployment guide.

## Prerequisites

- .NET SDK 10.0.401 or later (the exact band is pinned in `global.json`)
- Docker Desktop, running

## Local infrastructure

Postgres and Seq run in containers defined by `docker-compose.yml`.

**First time only** — create your local environment file:

```bash
cp .env.example .env
```

Then open `.env` and set `POSTGRES_PASSWORD` to any value you like. `.env` is git-ignored;
`.env.example` is the committed template. Compose refuses to start if a value is missing,
rather than quietly creating a database with an empty password.

**Start and stop:**

```bash
docker compose up -d      # start in the background
docker compose ps         # both services should report (healthy)
docker compose logs -f    # follow the logs
docker compose down       # stop, keeping the data
docker compose down -v    # stop and delete the data volumes
```

`docker compose up -d` returns as soon as the containers are *created*; the health checks
take a few more seconds to report `healthy`. Wait for `docker compose ps` to show both.

| Service | URL | Notes |
|---|---|---|
| Postgres | `localhost:5432` | database and user both `gym` by default |
| Seq (UI) | <http://localhost:8081> | structured logs; wired up in task 0.4 |
| Seq (ingestion) | `http://localhost:5341` | where Serilog will send events |

Ports are configurable in `.env` (`POSTGRES_PORT`, `SEQ_UI_PORT`, `SEQ_INGESTION_PORT`) if
something already listens on them.

### The database password

The password is deliberately split in two so that no credential is ever committed:

| Half | Where it lives | Committed? |
|---|---|---|
| host, port, database, username | `src/Gym.Api/appsettings.Development.json` | yes |
| password | .NET user-secrets, outside the repository | no |

After setting `POSTGRES_PASSWORD` in `.env`, give the API the **same** value:

```bash
dotnet user-secrets set "Postgres:Password" "<the value from .env>" --project src/Gym.Api
```

If you change `POSTGRES_USER` or `POSTGRES_DB` in `.env`, update the connection string in
`appsettings.Development.json` to match — a test fails if the two drift apart. Changing the
user or database name also requires `docker compose down -v`, because Postgres only creates
them when the data volume is empty.

In production nothing here applies: configuration comes from environment variables.

## Database schema

The schema lives in EF Core migrations under `src/Gym.Infrastructure/Persistence/Migrations`.
The `dotnet ef` tool must be installed once and kept at least as new as the EF Core packages:

```bash
dotnet tool install --global dotnet-ef   # or: dotnet tool update --global dotnet-ef
```

```bash
# apply every migration to the local database
dotnet ef database update --project src/Gym.Infrastructure --startup-project src/Gym.Api

# add a migration after changing the model
dotnet ef migrations add <Name> --project src/Gym.Infrastructure --startup-project src/Gym.Api --output-dir Persistence/Migrations
```

Both commands need Postgres running and the `Postgres:Password` user-secret set. They pick up
`Development` configuration from `src/Gym.Api/Properties/launchSettings.json`, which is why
they can see `appsettings.Development.json` and user-secrets at all.

Migrations are never applied automatically at startup in production; task 6.2 runs a migration
bundle during deployment instead.

## Build and test

```bash
dotnet build                          # solution; warnings are errors
dotnet test                           # all tests
dotnet test tests/Gym.Domain.Tests    # domain tests only
dotnet run --project src/Gym.Api      # run the API
```

Integration tests start their own Postgres container through Testcontainers, so Docker must
be running for `dotnet test` (from task 0.6 on).

## Running the API

```bash
dotnet run --project src/Gym.Api      # http://localhost:5134
```

| Endpoint | What it is |
|---|---|
| <http://localhost:5134/health> | `Healthy` only when the database is reachable too — stop Postgres and it returns `503 Unhealthy` |
| <http://localhost:5134/scalar/v1> | API explorer (Development only) |
| <http://localhost:5134/openapi/v1.json> | the OpenAPI document the explorer renders, and the input to `npm run gen:api` |
| <http://localhost:8081> | Seq — structured logs from the running API |

Every response carries an `X-Correlation-Id` header holding the request's W3C trace id. The
same value is attached to every log event as `CorrelationId`, so pasting it into Seq's filter
box shows everything that one request did:

```
CorrelationId = 'cc8367be7ab726497ac6a73977b0aac5'
```

If the frontend sends a `traceparent` header, its trace id is reused rather than replaced, so
one id covers both sides of the call.

### Log levels

Levels live in `appsettings.json` (`Serilog` section), not in code, so they can be changed
without a rebuild. Development adds the Seq sink and turns on EF Core's SQL logging.
`/health` is logged at `Debug` on purpose — a probe polled every ten seconds is 8,640 events a
day that say nothing, and Seq stays readable without it.
