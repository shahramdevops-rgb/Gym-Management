# Gym Management — Roadmap

How to use this file:
- Each task (for example `0.1`) is sized for one Claude Code session.
- Start a task with `/next-task 0.1`. Run `/clear` between tasks.
- A task is finished only when its "Done when" check passes and its tests are green.
- From Phase 1 on, every phase ends with a UI task, so each feature is visible in the Persian app as soon as its API is done.

Milestones:
- **MVP 1 (Phases 0–6):** front desk runs live: members, plans, subscriptions, payments, lockers, attendance.
- **MVP 2 (Phases 7–9):** cafe, expenses, owner dashboard.
- **Complete (Phases 10–12):** SMS, audit UI, hardening, portfolio polish.

---

## Phase 0 — Foundation

### 0.1 Repository and solution skeleton
- [x] `git init`, `.gitignore` (dotnet and node), `.editorconfig`, `global.json` pinning .NET 10
- [x] Solution with Gym.Domain, Gym.Application, Gym.Infrastructure, Gym.Api
- [x] Test projects: Gym.Domain.Tests, Gym.Api.IntegrationTests
- [x] Project references following the dependency rule
- [x] `Directory.Build.props`: nullable enabled, implicit usings, warnings as errors
- [x] `Directory.Packages.props`: central package management

Done when: `dotnet build` succeeds with zero warnings.

### 0.2 Local infrastructure
- [x] `docker-compose.yml`: postgres:18 (named volume, health check) and Seq
- [x] Connection string in `appsettings.Development.json` without the password; password in user-secrets
- [x] Short README section: how to start local infrastructure

Done when: `docker compose up -d` shows both containers healthy.

### 0.3 Persistence foundation
- [x] `IAppDbContext` in Application; `AppDbContext` in Infrastructure
- [x] Npgsql and snake_case naming
- [x] Base `Entity` (Guid v7 id, CreatedAt/By, UpdatedAt/By)
- [x] Interceptor setting audit timestamps (user id stubbed until Phase 1)
- [x] Initial migration applied
- [x] `/health` endpoint including a database check

Done when: the migration applies and `/health` returns healthy.

### 0.4 API plumbing
- [x] `AddApplication()` and `AddInfrastructure()` extension methods
- [x] Serilog: console and Seq, request logging, correlation id
- [x] OpenAPI document and Scalar UI (Development only)
- [x] `TimeProvider.System` registered
- [x] CORS policy for the Vite dev server (Development only)

Done when: Scalar opens and request logs appear in Seq.

### 0.5 Errors and validation
- [x] `Result`, `Result<T>`, `Error`, `ErrorType` in Domain/Common
- [x] `ToHttpResult()` mapping to ProblemDetails (includes the error `code`)
- [x] Global `IExceptionHandler`
- [x] FluentValidation and a generic `ValidationFilter<T>` (field errors include error codes)
- [x] Unit tests for Result and error mapping

Done when: an invalid request returns a 400 ProblemDetails with field errors and codes.

### 0.6 Test harness
- [x] Domain tests: xUnit v3 and Shouldly
- [x] Integration fixture: WebApplicationFactory, Testcontainers Postgres, migrations, Respawn
- [x] First integration test: `/health` returns 200

Done when: `dotnet test` is green with Docker running.

### 0.7 Frontend skeleton (Persian, RTL)
- [x] Vite + React + TypeScript in `web/`, ESLint, Prettier, Vitest
- [x] `<html lang="fa" dir="rtl">`, self-hosted Vazirmatn font
- [x] Tailwind CSS and shadcn/ui, Radix `DirectionProvider` set to RTL
- [x] React Router and TanStack Query
- [x] Generated API types (openapi-typescript) and client (openapi-fetch); npm script to regenerate
- [x] Dev proxy `/api` → API
- [x] `lib/format.ts`: Persian digits, money, Jalali date display; `lib/normalize.ts`: Persian/Arabic character and digit normalization; unit tests for both
- [x] RTL app shell (header, right-side navigation) with a status page calling `/health`

Done when: `npm run dev` shows a Persian RTL page reporting the server health, and `npm test` passes.

### 0.8 CI and first decision record
- [x] GitHub Actions: backend restore, build, test; frontend lint, test, build
- [x] `docs/adr/0001-modular-monolith-clean-architecture.md`
- [x] README skeleton

Done when: CI passes on GitHub.

---

## Phase 1 — Identity, Access and Audit

### 1.1 Identity and Owner seeding
- [x] `User : IdentityUser<Guid>` with `IsActive`, `MustChangePassword`, `FullName`
- [x] Roles Owner and Staff
- [x] Idempotent Owner seeding from configuration
- [x] Tests: seeding twice creates one Owner

### 1.2 Login and access tokens
- [x] Login endpoint with lockout
- [x] JWT access token (15 minutes)
- [x] Rate limiting on login
- [x] Tests: wrong password, lockout, inactive user rejected

### 1.3 Refresh tokens and logout
- [x] RefreshToken entity (hash, family, expiry, revoked, replaced by)
- [x] Refresh endpoint with rotation and reuse detection
- [x] HttpOnly cookie
- [x] Logout revokes the token
- [x] Tests: rotation works, reused token revokes the family

### 1.4 Current user, policies, change password
- [x] `ICurrentUser`
- [x] Policies `OwnerOnly`, `StaffOrOwner`
- [x] Change password endpoint
- [x] Forced password change gate
- [x] Interceptor fills CreatedBy/UpdatedBy
- [x] Tests: user who must change password is blocked from other endpoints

### 1.5 Staff management API
- [x] Owner: create staff, deactivate (revokes tokens), reactivate, reset password
- [x] Tests: staff cannot create staff; deactivated staff cannot refresh

### 1.6 Audit log
- [x] AuditLog entity and table
- [x] Audit `SaveChangesInterceptor` with sensitive-field exclusions
- [x] Tests: update writes old and new values; password hash never appears

### 1.7 UI: login and staff
- [x] Login page
- [x] Access token in memory, silent refresh on 401, logout
- [x] Forced change-password screen
- [x] Route guards by role; navigation shows only allowed items
- [x] Error code → Persian message map (`lib/errors.ts`)
- [x] Owner: staff list, create, deactivate, reset password

Done when: you can log in as the seeded Owner in the Persian app, change the password, and create a staff account.

---

## Phase 2 — Members

### 2.1 Member creation and update
- [x] Member entity and configuration, including `NormalizedFullName` for search
- [x] Phone normalization service (libphonenumber, accepts Persian and Arabic digits)
- [x] Persian text normalizer (Arabic ي/ك → Persian ی/ک, spaces, zero-width non-joiner)
- [x] Unique phone index
- [x] Create and update endpoints with validation
- [x] Tests: the same phone in different formats (including Persian digits) is rejected as a duplicate

### 2.2 Member queries and lifecycle
- [x] Deactivate and reactivate
- [x] Get by id
- [x] Paged list
- [x] Search by partial name and by phone
- [x] Tests: searching "علي" finds "علی"; phone search normalizes input

### 2.3 UI: members
- [x] Home: search by phone or name
- [x] Member list with paging
- [x] Create and edit member form (Zod validation, Persian messages)
- [x] Member profile page (basic info; later phases add sections)

Done when: staff can find, create, and edit members in the app.

---

## Phase 3 — Plans

### 3.1 Plans API
- [x] Plan entity (duration, session count or unlimited, price, active)
- [x] Create, update, activate, deactivate (Owner only), list
- [x] Validation rules
- [x] Tests: staff cannot create plans; inactive plans cannot be sold

### 3.2 UI: plans
- [x] Owner plans screen: list, create, edit, activate/deactivate

---

## Phase 4 — Subscriptions and Payments

> Tasks 4.4-4.6 are **done and tested on branch `task/4.4-payments`** (commits 6a63b24,
> 66a8f85, 116ee65) but that branch was never merged: `main` stopped at 4.3 (`ae09fec`) and
> the whole Phase 5 chain branched from the same commit. So the payment entity, the refund
> endpoints and the member profile's subscription and payment sections are missing from the
> Phase 5 branches even though the work exists. Merging that branch back is a task of its
> own: the EF migration chain forked after `AddSubscriptions`, and `Program.cs`,
> `MemberProfilePage.tsx`, `router.tsx` and `paths.ts` were changed on both sides.

### 4.1 Subscription domain model
- [x] Subscription entity with snapshot fields
- [x] `ConsumeSession`, `RestoreSession`, `Freeze`, `Unfreeze`, `Cancel`
- [x] Calculated status and remaining sessions
- [x] Extensive unit tests with FakeTimeProvider (dates, freeze, unlimited plans, status precedence)

### 4.2 Assign and renew
- [x] Assign endpoint (starts today or queued)
- [x] Renew endpoint
- [x] `xmin` concurrency token and session check constraint
- [x] Tests: renewal before expiry is queued; inactive plan or member rejected

### 4.3 Freeze, unfreeze, cancel endpoints
- [x] Owner-only endpoints
- [x] Unfreeze shifts queued subscriptions
- [x] Tests: max freeze days enforced; queued subscription shifted

### 4.4 Payments
- [x] Payment entity with the one-target check constraint
- [x] Register payment (partial allowed, overpayment rejected)
- [x] Calculated payment status
- [x] Tests: Unpaid → Partial → Paid

### 4.5 Refunds and member history
- [x] Refund and void (Owner only)
- [x] Member subscription history
- [x] Member payment history
- [x] Tests: refund cannot exceed net paid; status recalculates

### 4.6 UI: subscriptions and payments
- [x] Member profile: current subscription card (status, Jalali dates, sessions left, payment status)
- [x] Assign and renew subscription dialog
- [x] Register payment dialog
- [x] Subscription and payment history tabs
- [x] Owner: freeze, unfreeze, cancel, refund actions

Done when: staff can sell a subscription and take payment from the member profile.

---

## Phase 5 — Lockers and Attendance

### 5.1 Lockers API
- [x] Locker entity, Owner setup endpoints
- [x] Locker list with derived occupancy
- [x] Tests: cannot put an occupied locker out of service

### 5.2 Check-in
- [x] Attendance entity with both partial unique indexes
- [x] Transactional check-in use case
- [x] No-locker warning
- [x] Tests: expired, frozen, exhausted, inactive member, already inside

### 5.3 Check-out, cancel, lists
- [x] Check-out
- [x] Cancel check-in within the window
- [x] "Currently inside" list with locker numbers
- [x] Attendance history by member and by date range
- [x] Tests: cancel restores the session; cancel after the window fails

### 5.4 Concurrency tests
- [x] Two parallel check-ins for the same member: one succeeds, one gets 409, one session consumed
- [x] Race for the last free locker: no locker assigned twice
- [x] Fix any issues found

### 5.5 Background jobs
- [x] Hangfire with PostgreSQL storage, dashboard restricted to Owner (Development-only for now — see docs/LEARNING.md 5.5)
- [x] Nightly auto-checkout job
- [x] Tests: job closes open attendances and frees lockers

### 5.6 UI: front desk
- [x] One-click check-in from search results and profile, showing the locker number and warnings
- [x] Check-out and cancel check-in
- [x] "Currently inside" board (auto-refresh)
- [x] Member attendance history tab (a stacked section, not a `<Tabs>` widget — see docs/LEARNING.md 5.6)
- [x] Owner lockers screen with live occupancy

Done when: the full front desk flow works in the app: search → check-in → locker shown → check-out.
Verified against the real API (dev server + Vite proxy, unmocked backend) and with 23 new Testing
Library tests that render the real components; not verified with an interactive browser click-through
in this session — see docs/LEARNING.md 5.6.

---

## Phase 6 — First Deployment (MVP 1 live)

Shape and reasoning: `docs/adr/0003-deployment-topology.md`. One Docker Compose stack
(API, Postgres, Caddy) on one rented Iranian VPS — 2 cores, 2 GB RAM, 50 GB disk — reachable
from the internet over a real domain. The binding constraint is RAM, not disk.

### 6.0 Deployment decisions and prerequisites
- [ ] `docs/adr/0003-deployment-topology.md`
- [ ] Server provisioned: Ubuntu LTS, 2 cores, 2 GB RAM, 50 GB disk, 2 GB swapfile
- [ ] Domain registered, its A record pointing at the server
- [ ] SSH key-only login, `ufw` allowing 22/80/443 only, fail2ban
- [ ] Outbound HTTPS left open: the Phase 10 SMS panel is called from this host
- [ ] `timedatectl` reports a synchronised clock — JWT validation uses `ClockSkew = TimeSpan.Zero`,
      so clock drift rejects valid tokens

Done when: `ssh` with a key works, `ufw status` shows only 22/80/443, and the clock is synchronised.

### 6.1 Production image and compose
- [ ] Multi-stage `src/Gym.Api/Dockerfile`: SDK build stage, `mcr.microsoft.com/dotnet/aspnet:10.0`
      runtime, `USER $APP_UID`
- [ ] Frontend built in its own stage (`npm ci && npm run build`); Caddy serves the output
- [ ] `Asia/Tehran` resolves inside the runtime image: `InvariantGlobalization=false` needs ICU and
      `Gym:TimeZone` needs tzdata. The existing startup validation of `Gym:TimeZone` is the check —
      a container missing either cannot start
- [ ] `docker-compose.prod.yml`: `api`, `postgres`, `caddy`. Postgres publishes **no** host port.
      `restart: unless-stopped`, `mem_limit` per service, `logging: json-file` with
      `max-size: 10m` and `max-file: 3`
- [ ] No Seq in production (ADR 0003): Serilog writes to the console and Docker rotates it
- [ ] `Caddyfile`: automatic HTTPS, serves the React build, proxies `/api` and `/health`,
      security headers, `basic_auth` in front of `/hangfire`
- [ ] Postgres tuned for 2 GB: `shared_buffers=256MB`, `effective_cache_size=768MB`,
      `max_connections=50`

Done when: the stack comes up on the server, the Persian app loads over HTTPS with a valid
certificate, and `/health` reports healthy.

### 6.2 Release process
- [ ] Images built on the development machine, not the server: `docker compose build`,
      `docker save`, `scp`, `docker load`. Not a disk limit — 2 GB of RAM cannot run
      `dotnet publish` or `npm run build` beside a live Postgres
- [ ] The previous image tag kept on the server, so a bad release rolls back with one command
- [ ] `dotnet ef migrations bundle --self-contained -r linux-x64`, copied and run before the new
      API container starts (migrations never run at application startup — see ARCHITECTURE.md)
- [ ] Secrets as environment variables from a root-owned `/opt/gym/.env` (mode 600):
      `ConnectionStrings__Postgres`, `Jwt__SigningKey`, `POSTGRES_PASSWORD`,
      `Seed__OwnerUserName`, `Seed__OwnerPassword`
- [ ] `AllowedHosts` set to the real domain instead of `*`
- [ ] `UseForwardedHeaders` with Caddy as the known proxy — moved forward from task 11.2. Behind
      Caddy the login rate limit otherwise partitions on Caddy's own address and every user shares
      one bucket; on an internet-facing host that is a defect, not a future cleanup
- [ ] `deploy/` scripts so a release is one command from the development machine

Done when: a code change reaches the server, migrations included, by running one script.

### 6.3 Backups
- [ ] Nightly `pg_dump -Fc` on the server into `/opt/gym/backups`, 60 daily copies kept
- [ ] The gym's computer **pulls** the newest dump on a schedule (Windows Task Scheduler, `scp`,
      a read-only SSH key) onto its own disk and onto an attached flash drive. Pull, not push:
      the gym machine is behind NAT and the server cannot reach it
- [ ] `/opt/gym/.env` copied once, separately, kept by the Owner — not on the shared flash drive
      with the daily dumps
- [ ] Restore rehearsed into a scratch database on the development machine and written into the
      README. What the gym holds is a dump file, not a running second database, so the restore
      step is the part that has to be proven

Done when: a dump taken on the server restores into a scratch database and the app runs against it.

### 6.4 Go live
- [ ] Deploy, seed the Owner, change the password on first login
- [ ] Persian smoke-test checklist: login → create member → sell subscription → take payment →
      check-in → locker shown → check-out → nightly job visible in Hangfire
- [ ] `free -h` and `docker stats` after 24 hours — on 2 GB of RAM this is the number that matters
- [ ] Deployment guide in the README
- [ ] If `ghcr.io` turns out to be reachable from the server, move releases to a pull model in
      GitHub Actions. Not assumed: the save/load script is the baseline

Done when: the front desk runs a real day on the deployed system.

---

## Phase 7 — Cafe / POS

### 7.1 Products and stock
- [ ] Categories and products (Owner)
- [ ] StockMovement ledger, non-negative stock constraint
- [ ] Stock purchase and adjustment with reason

### 7.2 Orders
- [ ] CafeOrder and CafeOrderItem with snapshots
- [ ] Transactional create order: stock, movements, payment
- [ ] Optional member link
- [ ] Tests: insufficient stock rejected; price snapshot unchanged after product edit

### 7.3 Cancellation and history
- [ ] Cancel order (Owner): stock restored, refund created
- [ ] Order history and member purchase history

### 7.4 UI: cafe
- [ ] POS screen: product grid, cart, payment
- [ ] Owner: products, categories, stock adjustments
- [ ] Order history; member profile purchases tab

---

## Phase 8 — Expenses

### 8.1 Expenses API
- [ ] ExpenseCategory table with seed data (Persian display names)
- [ ] Expense entity; register, edit, void (Owner)
- [ ] Tests: voided expenses excluded from totals

### 8.2 UI: expenses
- [ ] List with Jalali date filters, create, edit, void

---

## Phase 9 — Dashboard and Reports

### 9.1 Financial reports API
- [ ] Date range handling in the gym's time zone
- [ ] Revenue by source and method; expenses by category; net profit
- [ ] Indexes for report queries

### 9.2 Operational reports API
- [ ] Attendance per day and by hour
- [ ] Active, expiring soon, low-session subscriptions
- [ ] Top cafe products

### 9.3 UI: dashboard
- [ ] Summary cards and charts
- [ ] Jalali range presets (today, this week, this Jalali month, custom)

---

## Phase 10 — SMS Notifications

### 10.1 Notification model
- [ ] `ISmsSender`, `FakeSmsSender`. Define it template-first (a template id plus named
      parameters), not as "send this string": Iranian panels generally require a pre-approved
      template for service messages, so a free-text signature would have to be rewritten in 10.3.
      See BUSINESS_RULES.md §10
- [ ] Notification entity with unique (subscription, type)
- [ ] Persian message templates; count SMS parts (Unicode messages are shorter per part)

### 10.2 Reminder jobs
- [ ] Daily reminder job with thresholds
- [ ] Quiet hours
- [ ] Retry with backoff, Failed status
- [ ] Tests: running the job twice sends nothing twice

### 10.3 Real provider and UI
- [ ] Real SMS provider implementation
- [ ] Manual resend (Owner)
- [ ] SMS history screen

---

## Phase 11 — Audit UI and Hardening

### 11.1 Audit UI
- [ ] Audit query endpoint (entity, user, date filters)
- [ ] Audit log screen

### 11.2 Security review
- [ ] CORS, security headers, rate limits, cookie settings, secrets, OWASP checklist

### 11.3 Performance and logging review
- [ ] N+1 queries, missing indexes
- [ ] Review real production logs and error handling
- [ ] Playwright end-to-end test for the front desk flow

---

## Phase 12 — Portfolio Polish
- [ ] README with architecture diagram and screenshots
- [ ] Complete ADRs
- [ ] Demo seed data
- [ ] Deployment guide

---

## Future — QR / Scanner (not in MVP)
- [ ] QR token design
- [ ] Generate and revoke QR credential
- [ ] QR identification endpoint
- [ ] Scanner integration
- [ ] Fast check-in and check-out
- [ ] Security and rate limiting
- [ ] Keep the phone and name fallback
