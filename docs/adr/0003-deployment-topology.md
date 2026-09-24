# 0003. Deploy as one Docker Compose stack on a single Iranian VPS

- Status: Accepted
- Date: 2026-09-22

## Context

The gym is a single location with one Owner and a few Staff. Members never log in: there is
no self-registration and no member-facing surface, so the only public endpoint is Staff login.
ADR 0001 already chose a modular monolith, which means there is exactly one deployable.

Three facts about where this will run shape the rest:

- **The server is rented in an Iranian data centre.** It has a public IP, so the application is
  reachable from the internet rather than from a gym LAN. It is 2 cores, 2 GB of RAM and 50 GB
  of disk.
- **The SMS provider will be Iranian** (Phase 10), so the host needs outbound HTTPS but nothing
  outside Iran is on the critical path.
- **No foreign cloud service is used by the application**, and none is wanted for operations
  either. Fonts and scripts are already self-hosted (`docs/ARCHITECTURE.md`), the frontend is
  same-origin by design (`web/src/lib/api/client.ts` uses `window.location.origin`), and dates
  come from `IGymCalendar` rather than any external service.

The binding constraint is RAM, not disk. Postgres, the API and Caddy together sit at roughly
1 GB, leaving about 1 GB of headroom on a 2 GB box.

## Decision

One Docker Compose stack on one host: `api`, `postgres`, `caddy`.

- **Caddy terminates TLS** with an automatically issued Let's Encrypt certificate for a real
  domain, serves the React build, and proxies `/api` and `/health` to the API. The browser
  therefore talks to one origin in production exactly as it does behind the Vite proxy in
  development, which is what the frontend already assumes.
- **Postgres publishes no host port.** It is reachable only over the Compose network.
- **Seq does not run in production.** Serilog writes to the console and Docker's `json-file`
  driver rotates it with `max-size`/`max-file`. Seq stays in `docker-compose.yml` for
  development, where it is genuinely useful.
- **Images are built on the development machine**, transferred with `docker save`/`docker load`,
  and the previous tag is kept on the server so a bad release rolls back with one command.
  That covers every image, not only ours: `postgres:18` and the `caddy` base come from Docker
  Hub, which may be unreachable from an Iranian data centre, so the first deploy carries them
  too (the API's `mcr.microsoft.com` and the frontend's npm and NuGet downloads only matter at
  build time, and that happens on the development machine).
- **Migrations run from an EF migration bundle** before the new API container starts, never at
  application startup.
- **Backups are two copies and no cloud.** A nightly `pg_dump -Fc` on the server, and the gym's
  own computer pulls the newest dump onto its disk and an attached flash drive on a schedule.

## Alternatives considered

- **Run Seq in production too.** It is the nicest way to read structured logs, and the project
  already depends on `Serilog.Sinks.Seq`. But Seq is a .NET application with its own storage
  engine and typically wants 500 MB–1 GB of RAM, which is essentially all the headroom on a 2 GB
  host. Rejected on memory, not on merit — see Consequences.
- **Build the image on the server.** Simpler to describe, and 50 GB of disk has room for the SDK
  image and the NuGet and npm caches. Rejected because `dotnet publish` and `npm run build` each
  comfortably exceed 1 GB of RAM, and running one beside a live Postgres invites the OOM killer
  to pick a process during a deploy.
- **Kubernetes, or separate hosts per service.** One gym, one deployable, a handful of
  concurrent users. Rejected as operational cost with no matching benefit.
- **Caddy's internal CA (`tls internal`) instead of a domain.** This is the right answer for a
  LAN-only box with no public name, and it was the first plan while the server was assumed to be
  on-premise. Once the server is in a data centre with a public IP, a real domain and Let's
  Encrypt cost less than installing a private root certificate on every machine that will ever
  need the app. Rejected.
- **Plain HTTP.** The refresh token cookie is `Secure` (`Gym.Api/Common/RefreshTokenCookie`), so
  plain HTTP would mean weakening authentication to suit the deployment. Rejected.
- **Off-site backups to object storage.** Automatic and genuinely off-site. Rejected because the
  Owner wants no external service and a second copy inside the gym meets the actual goal, which
  is not losing the data if the server is lost.

## Consequences

- There is no log search UI in production. Reading logs means `docker compose logs` on the
  server. If that becomes painful, the cheapest fix is more RAM plus Seq with `mem_limit: 512m`;
  this decision is the one to revisit, and it is revisitable without touching application code
  because the sink is configuration (`appsettings.json`), not code.
- `UseForwardedHeaders` stops being optional. `docs/ARCHITECTURE.md` records that behind Caddy
  the login rate limit partitions on Caddy's own address, putting every user in one bucket. On a
  LAN that was tolerable; on an internet-facing host it is a defect, so it moves from task 11.2
  into task 6.2.
- A release is a manual, scripted step from the development machine rather than a push to a
  branch. That is acceptable at one release per session and keeps the server clean, but it means
  the deploy scripts in `deploy/` are the only correct way to ship and have to be kept working.
- The runtime image must carry ICU and tzdata: `InvariantGlobalization=false` needs ICU for
  Persian formatting, and `Gym:TimeZone = Asia/Tehran` needs tzdata. The existing startup
  validation of `Gym:TimeZone` fails fast if either is missing, so a broken image cannot start
  and quietly serve wrong dates.
- The gym holds a dump file, not a running second database. Restoring it is a deliberate manual
  step, so the restore has to be rehearsed once and written down, otherwise the first real
  attempt happens on the worst day.
