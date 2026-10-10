# Gym Management

[![CI](https://github.com/shahramdevops-rgb/Gym-Management/actions/workflows/ci.yml/badge.svg)](https://github.com/shahramdevops-rgb/Gym-Management/actions/workflows/ci.yml)

Internal management system for a single gym: members, plans, subscriptions, payments,
lockers, attendance, cafe and reporting. Closed system — no public access and no member
logins. The API is .NET 10 / PostgreSQL; the frontend is a Persian, right-to-left React app.

It runs a real gym: in use at the front desk since 2026-10-02, on one rented server in Iran.

| Document | What it answers |
|---|---|
| [docs/ROADMAP.md](docs/ROADMAP.md) | What is built and what is next |
| [docs/BUSINESS_RULES.md](docs/BUSINESS_RULES.md) | How the gym works — the source of truth for every rule |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Stack, packages, patterns and gotchas |
| [docs/adr/](docs/adr/README.md) | Why the big decisions were made |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Releasing, going back, server accounts, backups |

## What it does

- **The front desk.** A map of the 72 lockers: a free one opens check-in, a taken one shows who is
  inside, for how long and whether they owe anything. A plan or a single visit can be sold in the
  same box as the check-in. Guests, cardio-only visits, the cafe and small sales at the desk all
  hang off the visit.
- **Plans and money.** A plan is a number of sessions; its days and price follow from it. Payments
  can be partial, debt is calculated from named items and shown item by item, several items can be
  settled with one handover, and nothing about money is ever edited or deleted: mistakes are
  refunded or voided with a reason.
- **The office.** Expenses, cheques and instalments with reminders, staff accounts, the audit log
  of every change, a dashboard and a financial report, and SMS reminders (plan running out, few
  sessions left, birthdays, a cheque coming due).

## What is worth a look in the code

- **Rules in the domain, and again in the database.** Entities hold the rules
  (`subscription.ConsumeSession(today)`), and the database repeats every one it can state: unique
  and partial indexes, check constraints, an exclusion constraint so two plans never cover one day,
  `xmin` concurrency ([ADR 0007](docs/adr/0007-invariants-enforced-by-the-database.md)).
- **Failures are values.** Handlers return `Result`; each error has a stable code that the frontend
  turns into Persian, and a test fails if a code has no message
  ([ADR 0005](docs/adr/0005-business-failures-as-result-values.md)).
- **Two calendars, one storage.** Gregorian dates and UTC moments in the database, Jalali only in
  the browser, "today" from an injected clock in the gym's time zone
  ([ADR 0006](docs/adr/0006-gregorian-storage-jalali-display.md)).
- **Tests at every level.** Domain unit tests with no database; integration tests that boot the
  real app against a real Postgres in a container (never the EF Core InMemory provider); frontend
  tests; and a Playwright run of the front desk against the production images.
- **A demo made by the real code.** The screenshots below come from `admin seed-demo`, which
  replays three months of a made-up gym through the same handlers the desk uses, on a clock it
  moves itself ([ADR 0009](docs/adr/0009-demo-data-through-the-real-handlers.md)).

## Screenshots

From the made-up gym of `admin seed-demo`, never from the real one. `npm run screenshots` (in
`web/`) takes them again; they are refreshed before each release.

| | |
|---|---|
| ![The locker map at the front desk](docs/screenshots/desk-light.png) | ![Inside the gym](docs/screenshots/inside-light.png) |
| The front desk: the locker map, who is inside and who owes, birthdays and renewals | Who is inside right now, with their sessions |
| ![The dashboard](docs/screenshots/dashboard-light.png) | ![The dashboard's charts](docs/screenshots/dashboard-charts-light.png) |
| The dashboard, for the Owner: the month's money | Its charts |
| ![Members](docs/screenshots/members-light.png) | ![A member's profile](docs/screenshots/member-light.png) |
| Members, with their sessions and debt | A member's profile |
| ![History](docs/screenshots/history-light.png) | ![Cafe orders](docs/screenshots/cafe-light.png) |
| History: visits, payments and sales | Cafe orders |
| ![Cheques and instalments](docs/screenshots/payables-light.png) | |
| Cheques and instalments | |

<details>
<summary>The same screens in the dark theme</summary>

| | |
|---|---|
| ![The locker map, dark](docs/screenshots/desk-dark.png) | ![Inside the gym, dark](docs/screenshots/inside-dark.png) |
| ![The dashboard, dark](docs/screenshots/dashboard-dark.png) | ![The dashboard's charts, dark](docs/screenshots/dashboard-charts-dark.png) |
| ![Members, dark](docs/screenshots/members-dark.png) | ![A member's profile, dark](docs/screenshots/member-dark.png) |
| ![History, dark](docs/screenshots/history-dark.png) | ![Cafe orders, dark](docs/screenshots/cafe-dark.png) |
| ![Cheques and instalments, dark](docs/screenshots/payables-dark.png) | |

</details>

## Architecture

A modular monolith with Clean Architecture, organized by feature inside each layer
([ADR 0001](docs/adr/0001-modular-monolith-clean-architecture.md)).

```mermaid
flowchart LR
    Browser["Browser<br/>React · Persian RTL"] -->|"/api, /health"| Api
    subgraph Server["One deployable"]
        Api["Gym.Api<br/>Minimal APIs"] --> Application["Gym.Application<br/>use cases"]
        Application --> Domain["Gym.Domain<br/>entities and rules"]
        Infrastructure["Gym.Infrastructure<br/>EF Core · Identity · SMS · jobs"] -. implements .-> Application
        Api --> Infrastructure
    end
    Infrastructure --> Postgres[("PostgreSQL")]
    Api -. logs .-> Seq["Seq"]
```

Arrows are project references: Domain references nothing, so business rules are tested
without a database or a web server.

```
src/Gym.Domain/           entities, value objects, Result/Error
src/Gym.Application/      one folder per use case: Command, Validator, Handler, Response
src/Gym.Infrastructure/   EF Core context, migrations, interceptors
src/Gym.Api/              Program.cs, endpoints, error mapping
tests/                    domain unit tests; API integration tests (Testcontainers)
web/                      React frontend
```

## Prerequisites

- .NET SDK 10.0.401 or later (the exact band is pinned in `global.json`)
- Docker Desktop, running
- Node.js 24 (for the frontend in `web/`)

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

### Demo data

An empty local database can be filled with three months of a made-up gym: about sixty members,
visits, sales, debts, expenses, cheques and SMS, ending with people inside right now.

```bash
dotnet run --project src/Gym.Api                          # once, so the Owner is seeded (Seed:* in user-secrets)
dotnet run --project src/Gym.Api -- admin seed-demo       # then fill the database
```

It refuses a database that already holds anything, and in Production it also needs
`Demo:Allowed=true`, which only the screenshot run passes
([ADR 0009](docs/adr/0009-demo-data-through-the-real-handlers.md)). To start again:
`docker compose down -v`, `docker compose up -d`, apply the migrations, and run both commands again.

### Log levels

Levels live in `appsettings.json` (`Serilog` section), not in code, so they can be changed
without a rebuild. Development adds the Seq sink and turns on EF Core's SQL logging.
`/health` is logged at `Debug` on purpose — a probe polled every ten seconds is 8,640 events a
day that say nothing, and Seq stays readable without it.

## Frontend

The frontend lives in `web/`: Vite, React, TypeScript, Tailwind CSS and shadcn/ui. The UI is
Persian and right-to-left; dates are shown in the Jalali calendar.

```bash
cd web
npm ci                # first time, and after package-lock.json changes
npm run dev           # http://localhost:5173
npm run lint          # ESLint, including a rule that rejects physical left/right classes
npm test              # Vitest
npm run build         # type-check and production build
npm run format        # Prettier
```

`npm run dev` proxies `/api` and `/health` to the API on `http://localhost:5134`, so run the
API alongside it. The browser only ever talks to one origin, as it will in production.

### API types

`web/src/lib/api/schema.d.ts` is generated from the API's OpenAPI document and committed. After
changing any endpoint (with `docker compose up -d` running):

```bash
cd web
npm run gen:api
```

The script uses an API already listening on :5134; otherwise it starts one in the Development
profile, waits for the document, generates the types and stops it again. An API left running from
before the endpoint change serves an old document, so stop it first.

A mismatch between the frontend and the API then shows up as a type error in `npm run build`.

## Continuous integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every push to `main` and to
`task/**` branches, and on pull requests:

| Job | Steps |
|---|---|
| Backend | `dotnet restore`, `build` (Release, warnings are errors), `test` — integration tests start Postgres through Testcontainers on the runner's Docker |
| Frontend | `npm ci`, `lint`, `format:check`, `test`, `build` |

## Deployment

One Docker Compose stack (API, Postgres, Caddy) on a single rented Iranian VPS, with images built
on the development machine and copied over ([ADR 0003](docs/adr/0003-deployment-topology.md)).

- **Preparing a server**, once: [docs/SERVER-SETUP.md](docs/SERVER-SETUP.md).
- **Releasing, going back, server accounts, domains, backup and restore**:
  [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md), including the step-by-step release checklist.

```bash
deploy/release.sh gym@<server-ip>       # from the development machine
./server.sh rollback                    # on the server
```

**The server holds real data** since 2026-10-02: it is never wiped and nothing is restored over it,
and `./backup.sh run` comes before every release.
