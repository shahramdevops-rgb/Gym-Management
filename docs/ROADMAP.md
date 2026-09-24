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

### 2.4 Member birth date (and the first Jalali date input)
Added 1405/06/31. Rules: BUSINESS_RULES.md §2 *Birth date*, §13. Depends on nothing; can be done first.
This task brings the app's **first Jalali date input and its first Jalali→Gregorian conversion** — today
`web/src/lib/format.ts` only converts one way, for display. Both `react-multi-date-picker` and
`date-fns-jalali` are already on the approved list in ARCHITECTURE.md but are not installed yet.
- [x] `Member.BirthDate` (`DateOnly?`), validated inside the entity against the gym's today: not in the future (`Members.BirthDateInFuture`), not over 120 years ago (`Members.BirthDateTooOld`). `Create`/`Update` take `today`; the handler passes `IGymCalendar.Today()`
- [x] Migration `AddMemberBirthDate`: `date NULL` by convention, plus a check constraint with a **fixed** lower bound (`birth_date IS NULL OR birth_date >= DATE '1900-01-01'`) — `CURRENT_DATE` is not immutable and Postgres refuses it in a CHECK
- [x] Create and update commands, validators, and `MemberResponse` (record, `Projection` and `From`; the new parameter goes before the defaulted `HasUnpaidSubscription`)
- [x] Jalali↔ISO conversion added to `web/src/lib/format.ts` (the file that owns date translation), with `date-fns-jalali`
- [x] Shared `JalaliDateField` in `web/src/components/FormField.tsx` using `react-multi-date-picker`: Persian calendar, RTL, clearable, holds an ISO value, accepts Persian and English digits
- [x] Member create and edit forms, and the birth date shown on the member profile. Not searchable, not in the list
- [x] `npm run gen:api` after the API is up
- [x] Tests: future date and the 120-year edge rejected; empty stays `null`; create and edit through the API with and without a date; a conversion test (۱۳۷۰/۰۵/۱۲ → `1991-08-03`)

Done when: staff can record a birth date with a Persian calendar, leave it empty, and see it on the profile.

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

> Tasks 4.4-4.6 were finished on branch `task/4.4-payments` but that branch sat unmerged:
> `main` stopped at 4.3 (`ae09fec`) and the whole Phase 5 chain branched from the same
> commit, so the member profile had no subscription card and no payment history even
> though the code existed. Merged into the Phase 5 line in `51b93aa`. The migration chain
> forked after `AddSubscriptions` and reconverges in timestamp order; the integration
> tests apply every migration to a fresh Postgres, which is what proves the chain is sound.
> `main` itself is still at 4.3 and has yet to receive any of this.

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

### 4.7 Open accounts: member debt, and who may cancel or refund
Added 1405/06/31 after the developer reported the subscription row's buttons and then decided the
gym runs open accounts. Rules: BUSINESS_RULES.md §0, §4 *Cancel*, §5 *Member debt*, §7.
- [x] Member debt calculated from non-cancelled subscriptions (`Price − net paid` per item), with a per-item breakdown endpoint
- [x] Member profile: the total, opening into the item-by-item breakdown; debt shown in the members list
- [x] Check-in returns the outstanding total; the front desk shows it as a warning and still records the visit
- [x] Cancel refused unless `UsedSessions = 0` and the status is Upcoming/Active/Frozen (`Subscriptions.AlreadyUsed`)
- [x] Refund refused once a session has been used (`Payments.RefundAfterUse`)
- [x] Subscription history row shows only the actions that are possible: no buttons at all on a finished, settled subscription; "ثبت پرداخت" stays for as long as anything is owed, whatever the status
- [x] Tests: debt adds up across several subscriptions and ignores cancelled ones; cancel and refund refusals; a finished unpaid subscription still takes a payment

Done when: the front desk can see what a member owes, broken down by item, take money against any
of it, and the actions that are no longer allowed are gone from the screen rather than failing when
pressed.

### 4.8 One money field for the whole app
Added 1405/06/31. Rules: BUSINESS_RULES.md §13. Frontend only, no API change. Worth doing before 5.7,
whose cardio amount uses this field. `MoneyField` in `web/src/components/FormField.tsx` already groups
digits as you type; what is missing is the amount in words, an empty-allowed variant, and the two
forms that still do their own thing.
- [x] `amountInPersianWords` in `web/src/lib/format.ts`, hand-written (no package): «پانصد هزار تومان». Tests for zero, single digits, 1,000, 500,000, millions and the billion boundary
- [x] `MoneyField` shows the words under the input (wired into `aria-describedby`), and gains an optional (empty-allowed) mode that says «بدون مبلغ». It is controlled now, like `JalaliDateField`: a field that shows what the amount *means* has to know what the amount is
- [x] `PlanForm` moves from a plain `FormField` to `MoneyField`, so every typed amount behaves the same. The plan edit form now shows a stored `900000` as ۹۰۰٬۰۰۰ instead of a raw run of zeros
- [x] `normalizePrice` (plans) and `normalizeAmount` (payments) collapse into `normalizeMoney` in `web/src/lib/money.ts` — the deliberate duplication documented in `payments/schemas.ts` ends here, because the cardio field is the third caller. `priceProblem` and `amountProblem` stay apart: they answer to different error codes
- [x] Amounts stay strings end to end **on the way in and on screen**: `formatMoney` formats the decimal string without a `Number()` round trip, `subtractMoney` does "price − net paid" exactly in `bigint` hundredths, and `isPositiveMoney` replaces the `Number(debt) > 0` comparisons. The one gap left is not frontend-fixable: the API serializes `decimal` as a JSON number, so `JSON.parse` has already made it a double before any screen sees it — see docs/LEARNING.md 4.8
- [x] Tests: words under each money input, an empty optional amount submits as null, plan price still round-trips Persian digits and separators

Done when: every place an amount is typed looks and behaves the same, and the amount in words appears
under it. Done: 375 frontend tests pass (`npm test`), 49 of them new across `lib/money.test.ts`,
`lib/format.test.ts` and `components/FormField.test.tsx`.

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

### 5.7 Cardio (هوازی) and gym service charges
Added 1405/06/31. Rules: BUSINESS_RULES.md §7 *Gym services*, §5, §12. Depends on 4.7 (a service
charge is the third thing a member can owe money for) and reads better after 4.8 (the amount uses the
shared money field). Built before 4.7 it still works, but the amount will not appear in any debt total.
4.7 and 4.8 are both done, so the optional «مبلغ هوازی» box is `<MoneyField />` and the amount it
submits goes through `normalizeMoney`.
- [x] `ServiceCharge` entity and `service_charges` table: `MemberId`, `AttendanceId`, `Kind` (enum, only `Cardio` today), `Amount` (`numeric(18,2)`, > 0), `ChargedOn` (`DateOnly`), `RecordedByUserId`, void fields (`VoidedAt`, `VoidReason`, `VoidedByUserId`), `xmin`
- [x] Partial unique index `(attendance_id, kind) WHERE voided_at IS NULL`: one live charge per visit per kind
- [x] Record and change the amount only while the visit is open and nothing has been paid against it; after check-out or the first payment, only void-with-a-reason. Staff or Owner
- [x] Cancelling a check-in voids that visit's charges with a reason
- [x] `Payment` gains `ServiceChargeId`; the one-target check constraint becomes "exactly one of subscription, cafe order, service charge" (migration on `payments`)
- [x] Member debt and its breakdown include non-voided service charges
- [x] UI: an optional «مبلغ هوازی» field on the open visit in the member profile and on the "currently inside" board; a هوازی column in the attendance history
- [x] Tests: charge refused on a closed or cancelled visit; second live charge for the same visit refused; cancel check-in voids it; an unpaid charge shows in the member's debt; a paid charge cannot be edited, only voided

Decided during the task (BUSINESS_RULES.md §5, §7): voiding a charge that has been paid **refunds**
the money in the same transaction, one refund per payment method in credit — the developer reviewed
and rejected a wallet, so the gym cannot hold money against a name. Voiding is **Staff or Owner**, a
documented exception to §1. There is no service-charge refund endpoint: a correction is a void plus a
fresh charge. "Revenue by source gains services" is left to Phase 9, which is where reports are built.

Done when: staff can put a treadmill amount on a member who is inside the gym, the member owes it
until it is paid, and a charge entered by mistake is voided rather than erased. Done: 772 backend
tests and 387 frontend tests pass.

---

## Phase 6 — First Deployment (MVP 1 live)

Shape and reasoning: `docs/adr/0003-deployment-topology.md`. One Docker Compose stack
(API, Postgres, Caddy) on one rented Iranian VPS, reachable from the internet over a real domain.
The binding constraint is RAM, not disk. The ADR was written against 2 cores / 2 GB / 50 GB; the
server actually rented (2026-09-24) is 2 cores, 4 GB and 60 GB, which changes only the memory
values in `.env`.

### 6.0 Deployment decisions and prerequisites

Written up step by step, with the reasoning, in `docs/SERVER-SETUP.md`.

- [x] `docs/adr/0003-deployment-topology.md`
- [x] Server provisioned: Ubuntu 26.04.1 LTS, 2 cores, 4 GB RAM, 60 GB disk, 2 GB swapfile
      (`vm.swappiness=10`)
- [x] Deploy user `gym` (sudo, in the `docker` group)
- [x] `/opt/gym` and `.env` (mode 600, owned by `gym`) with the 4 GB memory values
- [x] Docker Engine and the Compose v2 plugin from Docker's own repository
- [ ] Domain registered, its A record pointing at the server — `pasargadgymplus.ir` (canonical)
      and `pasargadgymplus.com` (redirected to it by Caddy, `REDIRECT_DOMAIN`). Cause found
      2026-09-24: the zone holding the A record lives on the provider's CDN nameservers
      (`grass/tornado.parspack.net`) while both domains are delegated at the registry to
      `ns1/ns2.parspack.co`, which do not hold it and answer `REFUSED`. The `.ir` is not
      published in the `.ir` zone at all, which is consistent — a registry will not publish a
      delegation whose nameservers do not answer for the domain. Fixed by changing the
      nameservers at the registry to the two the zone is actually served from, with no glue
      records (they are out-of-domain names) and the CDN proxy left off
      (`docs/SERVER-SETUP.md`, step 9). `pasargadgymplus.com` now resolves to the server, with
      the proxy confirmed off by the A record being the server's own address. The `.ir` is
      waiting on IRNIC to publish the new delegation
- [x] SSH key-only login (root login off, `KbdInteractiveAuthentication` off too), `ufw`
      allowing 22/80/443 only, fail2ban reading the systemd journal
- [x] Outbound HTTPS left open: the Phase 10 SMS panel is called from this host
- [x] `timedatectl` reports a synchronised clock — JWT validation uses `ClockSkew = TimeSpan.Zero`,
      so clock drift rejects valid tokens. Host timezone set to `Asia/Tehran` so `backup.sh`
      filenames and logs agree with the gym's day
- [x] Inbound 80 confirmed reachable from outside (some providers block it until an identity
      check), so Let's Encrypt validation will work once DNS resolves

Done when: `ssh` with a key works, `ufw status` shows only 22/80/443, and the clock is synchronised.

All three verified on the server on 2026-09-24. The one item still open is DNS, which depends on
the provider serving the zones; it is not a blocker for anything except the certificate, and the
certificate is 6.4's first step.

### 6.1 Production image and compose
- [x] Multi-stage `src/Gym.Api/Dockerfile`: SDK build stage, `mcr.microsoft.com/dotnet/aspnet:10.0`
      runtime, `USER $APP_UID`
- [x] Frontend built in its own stage (`npm ci && npm run build`); Caddy serves the output
      (`web/Dockerfile`)
- [x] `Asia/Tehran` resolves inside the runtime image: `InvariantGlobalization=false` needs ICU and
      `Gym:TimeZone` needs tzdata. The existing startup validation of `Gym:TimeZone` is the check —
      a container missing either cannot start
- [x] `docker-compose.prod.yml`: `api`, `postgres`, `caddy`. Postgres publishes **no** host port.
      `restart: unless-stopped`, `mem_limit` per service, `logging: json-file` with
      `max-size: 10m` and `max-file: 3`
- [x] No Seq in production (ADR 0003): Serilog writes to the console and Docker rotates it
- [x] `Caddyfile` (`deploy/Caddyfile`): automatic HTTPS, serves the React build, proxies `/api` and
      `/health`, security headers. No `basic_auth` for `/hangfire`: the dashboard is
      Development-only (Program.cs), so nothing is proxied to it. An Owner-only dashboard is its
      own task, and 6.4's "job visible in Hangfire" is checked in the logs or the database instead
- [x] Postgres tuned for 2 GB: `shared_buffers=256MB`, `effective_cache_size=768MB`,
      `max_connections=50`. The sizes come from `.env`; `deploy/env.example` lists the 4 GB values
- [x] Hangfire `WorkerCount = 2`: the default 20 held 24 of those 50 connections while idle

Done when: the stack comes up on the server, the Persian app loads over HTTPS with a valid
certificate, and `/health` reports healthy.

Verified locally (2026-09-24) with `DOMAIN=localhost`: all three containers healthy, `/health` 200
through Caddy, deep links served by the SPA, `/assets/*` cached as immutable, login sets the Secure
refresh cookie, `/hangfire` and `/openapi` reach only the SPA, and the stack idles at about 225 MB.
Still to do on the real server: Let's Encrypt issuance (needs 80/443 inbound and Let's Encrypt
reachable from the data centre).

### 6.2 Release process
- [x] Images built on the development machine, not the server: `docker build`, `docker save`,
      `scp`, `docker load` (`deploy/release.sh`). Not a disk limit — 2 GB of RAM cannot run
      `dotnet publish` or `npm run build` beside a live Postgres
- [x] The previous image tag kept on the server, so a bad release rolls back with one command
      (`./server.sh rollback`; the newest three images are kept)
- [x] `dotnet ef migrations bundle --self-contained -r linux-x64`, copied and run before the new
      API container starts (migrations never run at application startup — see ARCHITECTURE.md).
      Runs as the `migrate` service of `docker-compose.prod.yml`
- [x] Secrets as environment variables from `/opt/gym/.env` (mode 600, **owned by the user you
      deploy as** — Compose reads it as that user and a release rewrites its `TAG` line).
      `server.sh` refuses a file others can read, or one it cannot read and write, and names the
      `chown` to run. Creating the file on the server is a 6.0 step
- [x] `AllowedHosts` set to the real domain instead of `*`
- [x] `UseForwardedHeaders` with Caddy as the known proxy — moved forward from task 11.2. Behind
      Caddy the login rate limit otherwise partitions on Caddy's own address and every user shares
      one bucket; on an internet-facing host that is a defect, not a future cleanup
      (`ForwardedHeadersConfiguration`, tested in `ForwardedHeadersTests`)
- [x] `deploy/` scripts so a release is one command from the development machine

Done when: a code change reaches the server, migrations included, by running one script.

Rehearsed locally (2026-09-24) with stand-ins for `ssh` and `scp`: release, second release,
rollback, and a rollback with nothing recorded, against a real Compose stack. Not yet proven
against the real server: the ssh/scp transfer and a `.env` with a real mode 600. That is 6.4's
first deploy, so the "Done when" line is confirmed there, not here.

### 6.3 Backups
- [x] Nightly `pg_dump -Fc` on the server into `/opt/gym/backups`, 120 daily copies kept
      (`deploy/backup.sh run`; each dump is checked with `pg_restore --list` before it is kept).
      Installing the cron line is a server step (README, "Backup and restore")
- [x] The gym's computer **pulls** the newest dump on a schedule (Windows Task Scheduler, `scp`,
      a read-only SSH key) onto its own disk and onto an attached flash drive. Pull, not push:
      the gym machine is behind NAT and the server cannot reach it (`deploy/pull-backup.ps1`).
      Creating the `gymbackup` account and registering the task are one-time setup steps in the README
- [x] `/opt/gym/.env` copied once, separately, kept by the Owner — not on the shared flash drive
      with the daily dumps (written into the README; the copying itself happens at go-live, 6.4)
- [x] Restore rehearsed into a scratch database and written into the README. What the gym holds
      is a dump file, not a running second database, so the restore step is the part that has to
      be proven (`backup.sh restore-scratch` and `restore`)

Done when: a dump taken on the server restores into a scratch database and the app runs against it.

Rehearsed locally (2026-09-24) on a real Compose stack: three members created through the API,
a dump taken, the members destroyed, `restore --yes` run, and the three members, the changed
owner password and a healthy `/health` came back. `restore-scratch` loaded the same dump into a
separate database (3 members, 16 migrations). `pull-backup.ps1` ran on Windows PowerShell 5.1 with
stand-ins for `ssh`/`scp`. Still to prove on the real server: cron, the `gymbackup` account and
key, and the real ssh pull. Not rehearsed: restoring a dump older than the current migrations
(the script runs the bundle afterwards, but that path was not exercised).

### 6.4 Go live
- [ ] Deploy, seed the Owner, change the password on first login
- [ ] Persian smoke-test checklist: login → create member → sell subscription → take payment →
      check-in → locker shown → check-out → nightly job visible in Hangfire
- [ ] `free -h` and `docker stats` after 24 hours — on 2 GB of RAM this is the number that matters
- [ ] Deployment guide in the README
- [ ] Reboot the server deliberately and watch the whole stack come back by itself. Only then
      decide whether to turn on `unattended-upgrades`' automatic reboot (04:00, gym closed)
- [ ] If `ghcr.io` turns out to be reachable from the server, move releases to a pull model in
      GitHub Actions. Not assumed: the save/load script is the baseline. Docker Hub *is*
      reachable from this data centre (checked 2026-09-24), so a registry pull is worth
      measuring against the ~1 GB `scp` — but only after go-live, and the save/load path stays

Done when: the front desk runs a real day on the deployed system.

### 6.5.0 Panel subdomain and a public placeholder

Decided 2026-09-25, and it has to land **before** the canonical domain moves to the `.ir`. Today
the bare domain answers with the staff login form, which is the wrong front door for a gym and
gets indexed by search engines. The panel belongs on its own host:

```
pasargadgymplus.ir         the gym's public face (a placeholder page until there is a real site)
panel.pasargadgymplus.ir   this application
```

A subdomain rather than a path, for four reasons: the refresh cookie stays off the public host;
Caddy can give the panel its own rate limits or an IP allowlist without touching the public
site; the public site can be static files that keep working while the API is down; and the
panel gets `X-Robots-Tag: noindex` on its own. This is separation, not security — the real
protections are the login rate limit, lockout, fail2ban and the firewall, all already in place.

Timing is the whole point: switching the canonical domain signs every user out and makes staff
learn a new address, so the move to the `.ir` and the move to `panel.` happen in the same
change. One disruption, not two.

- [ ] A record for `panel.` alongside the apex, same server, proxy off
- [ ] A second Caddy site for the apex serving a static placeholder; the panel site keeps
      everything it has now plus `X-Robots-Tag: noindex`
- [ ] `DOMAIN=panel.pasargadgymplus.ir` in `/opt/gym/.env` (it also sets `AllowedHosts`);
      `REDIRECT_DOMAIN` keeps sending the `.com` to the panel until there is a public site
- [ ] Tell the staff the new address before the switch, and do it before opening time

---

## Phase 6.5 — Front desk follow-ups (found during the first real use, 2026-09-25)

These came out of the Owner using the deployed system for the first time. They are features,
not polish: the front desk meets both of them every day. Numbered 6.5 rather than folded into
Phase 7 because they should land before the cafe adds more to the same screens.

### 6.5.1 Lockers for the front desk
Rule change, decided by the Owner on 2026-09-25: a staff member could not open the lockers
screen at all, because `LockersEndpoints` puts the whole group behind `Policies.OwnerOnly`. But
the person who sees a broken locker is the one at the desk, not the Owner.

- [ ] BUSINESS_RULES.md §0: split the "Plans, lockers setup, staff accounts" row. Creating a
      locker stays Owner-only; listing lockers and taking one out of / back into service become
      Staff too. Plans and staff accounts are unchanged
- [ ] Split the endpoint group: `GET /` and `GET /{id}` and the two service endpoints allow
      Staff, `POST /` stays `OwnerOnly`. One explicit policy per endpoint, never a group default
      that quietly widens later
- [ ] A column on the lockers screen naming the member who currently holds each locker, so
      "whose is locker 1?" is answered without opening attendance. Occupancy is derived from the
      open attendance and never stored (BUSINESS_RULES.md §6), so the member's name comes from
      the same join
- [ ] The create button is hidden for Staff. The API enforces it too — the hidden button is
      about not offering what would fail, not about security
- [ ] Tests: Staff can list and take out of service; Staff creating a locker is 403; the holder
      column is empty for a free locker and names the member for an occupied one

### 6.5.2 The "currently inside" board: one row, one line
Two problems on one screen. `ServiceChargeBox` is a 147-line component rendered inside a table
cell, and its amount / payment / void forms expand **in place**, so a row can triple in height.
The cafe will want the same slot, and a column per service does not scale.

- [ ] A row is always one line. The هوازی cell shows a summary only (amount plus payment badge);
      every form moves into a side panel or dialog opened from that row
- [ ] Session progress per row: used / total with a bar, in the shape of the reference design.
      An unlimited subscription shows "نامحدود" and no bar — a bar needs a denominator
- [ ] Status badge and expiry date per row, so the desk sees an expiring subscription at
      check-in rather than after it lapses
- [ ] `CurrentlyInsideResponse` carries none of this yet (member, locker, time, charges only):
      extend the projection rather than firing a query per row
- [ ] Build the bar and the badge as shared components: Phase 9's dashboard needs both, and a
      second copy would drift from the first
- [ ] While here: the "عضو جدید" button on the member search screen has no colour, unlike the
      one on the members list. One primary-button component, used by both

Done when: a staff member can manage lockers without the Owner, and a row on the board never
grows taller than one line.

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
- [ ] Order on a member's account: created unpaid under that member's name, settled later with ordinary payments, counted in their debt (BUSINESS_RULES.md §8, §5 *Member debt*). A walk-in order with no member is paid in full at creation
- [ ] Tests: insufficient stock rejected; price snapshot unchanged after product edit; an unpaid order on account appears in the member's debt breakdown

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

## Phase 13 — Look, feel and mobile

Deliberately last, and deliberately in one pass. Phases 7 to 9 add whole screens; restyling
before they exist means restyling them twice, while a shared set of components built here would
be built against screens that do not exist yet. The exception is 6.5.2, which builds the bar,
the badge and the primary button early because the board needs them anyway.

### 13.1 Mobile
Not cosmetic. The reception PC is a Windows desktop, but the gym loses power: during an outage
the desk has to keep working from a phone, and the Owner checks the gym from home on one.

- [ ] Check-in, check-out and the board are fully usable on a phone — the flows that cannot wait
      for the power to come back
- [ ] Tables become cards below the breakpoint instead of scrolling sideways
- [ ] Forms and dialogs open full-screen on a phone, including the service-charge panel from 6.5.2
- [ ] Touch targets, and the Jalali date picker and MoneyField on a real phone keyboard

### 13.2 One visual language
- [ ] Colour, typography and spacing tokens; every screen built from the same components
- [ ] The Persian font renders numbers and text consistently across screens
- [ ] Empty states, loading states and error states that look deliberate

### 13.3 The public site
- [ ] Replace the placeholder on the apex with a real page for the gym: hours, address, contact,
      services. Static, no API
- [ ] Confirm the panel stays out of search results

---

## Future — QR / Scanner (not in MVP)
- [ ] QR token design
- [ ] Generate and revoke QR credential
- [ ] QR identification endpoint
- [ ] Scanner integration
- [ ] Fast check-in and check-out
- [ ] Security and rate limiting
- [ ] Keep the phone and name fallback
