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

**Superseded by 6.5.6** (the Owner, 1405/07/05): the gym no longer sells from a list of plans.
Everything below was built and then removed; it stays here as the record of what existed.

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

Superseded in 6.5.5: the desk chooses the locker instead of a random pick, the no-locker warning
becomes a reserve place, the 72 lockers are fixed and seeded instead of created, and check-in moves
from search and profile to the lockers screen.

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
- [x] Both domains resolve to the server, CDN proxy off. It took two days and the fault was the
      same one twice: the nameservers recorded at the registry were not the ones actually
      serving the zone. `pasargadgymplus.com` came up on 2026-09-24 once its delegation was
      pointed at the pair that holds its zone. The `.ir` stayed `NXDOMAIN` for another day —
      IRNIC had `grass/tornado.parspack.net` on record, those servers did not answer for the
      domain, and a registry will not publish a delegation whose nameservers are lame. The
      provider's support moved it to `ocean/seedling.parspack.net` on 2026-09-25 and it
      resolved within the hour. Public resolvers are the only trustworthy check here: probing
      the nameserver IPs directly from a home connection returned contradictory answers twice
      and sent the diagnosis the wrong way both times.
      Two loose ends, neither urgent: the in-zone `NS` records still name `grass/tornado` while
      the registry delegates to `ocean/seedling`, and `www` has an A record that Caddy does not
      serve. Both are settled in task 6.5.0, along with `panel.`
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
- [x] Deploy, seed the Owner, change the password on first login
- [x] Persian smoke-test checklist: login → create member → sell subscription → take payment →
      check-in → locker shown → check-out → nightly job visible in Hangfire
- [x] `free -h` and `docker stats` after 24 hours — on 2 GB of RAM this is the number that matters
- [x] Deployment guide in the README
- [x] Reboot the server deliberately and watch the whole stack come back by itself. Only then
      decide whether to turn on `unattended-upgrades`' automatic reboot (04:00, gym closed)
- [ ] If `ghcr.io` turns out to be reachable from the server, move releases to a pull model in
      GitHub Actions. Not assumed: the save/load script is the baseline. Docker Hub *is*
      reachable from this data centre (checked 2026-09-24), so a registry pull is worth
      measuring against the ~1 GB `scp` — but only after go-live, and the save/load path stays

Done when: the front desk runs a real day on the deployed system.

Live since 2026-09-25 on `pasargadgymplus.com`, certificate issued by Let's Encrypt on the first
try. The Owner walked the Persian checklist on the deployed system and found nothing wrong. A
deliberate `sudo reboot` brought the whole stack back with no manual step — the browser shows
its own "site can't be reached" while the machine is down, which nothing on the machine can fix;
a friendly page during an API-only restart is a Phase 13 item.

Memory after 15 hours of real use, on 4 GB: Caddy 60.7 MiB of its 128 MiB limit (47%, down from
47.4% at boot — the limit stays), API 318 MiB of 768 MiB, Postgres 112 MiB of 1.5 GiB, 3.0 GiB
still available and **swap untouched at 0 B**. No container was killed and restarted. The
`unattended-upgrades` automatic reboot is still off; there is no reason to hurry it.

The one item left open is the `ghcr.io` question, which is an optimisation and deliberately not
a condition of go-live.

#### Real data from 1405/07/10 (2026-10-02): the server is now highly sensitive
Member registration (عضوگیری) starts with release **`20261002-1941-3059c81`** (previous:
`20261001-0400-0dc7f27`). Before it, the trial data was backed up (moved to `backups/trial/` on the
server) and wiped once, with the SQL kept in the README ("The server holds real data"). The Owner
starts clean: only the Owner account, every locker in service, both prices empty, the eight seeded
expense categories, no cafe products. The Owner re-enters the prices and the staff accounts, and
the staff add the cafe products.

From this release on, every member, payment and visit on the server is the gym's real record:
- **Never** wipe the server, truncate a table, or restore a dump over it
- **Never** write a migration that refuses or drops existing rows; it carries them forward
- `./backup.sh run` before **every** release, and check that the gym PC's copy is recent
- Every server step is written out, explained and confirmed first; nothing is run "to try"
- `./server.sh rollback` swaps the code only, never the schema. A rollback to a release older than
  the newest migration runs old code against a newer database, so think before using it. The first
  real release already includes `MakeMemberBirthDateRequired`, which `20261001-0400-0dc7f27` predates

#### First release over real data: `20261004-0511-2793a16` (2026-10-04)
Previous: `20261002-1941-3059c81`. It carries 6.5.25 to 6.5.32 (the history page, guest visits and
the guest debt list, settlements kept together, cardio-only visits, the miscellaneous and
multi-item shop sales) and the birth-date dropdowns, with seven migrations: `GuestVisits`,
`AddHistoryIndexes`, `AddPaymentSettlementId`, `AddAttendanceCardioOnly`, `AddMiscellaneousSale`,
`AddSubscriptionCreatedAtIndex`, `AllowGuestServiceCharges`. Each one only adds or loosens: new
columns are nullable or defaulted, the new checks hold for every old row, and the settlement
backfill only fills the new column. Tests before release: 214 domain, 1300 integration and 791
frontend, zero warnings.

How it went, as the procedure for the next one:
- Server state checked first: right `TAG`, API and Postgres healthy, 50 GB free
- `./backup.sh run` → `gym-20261004-050157.dump` (131 KB). The gym PC was out of reach, so the
  dump was copied to the developer's machine (`D:\GymBackups`, outside the repository) with `scp`
  and both copies' `sha256sum` compared equal. The gym PC's own pull still has to run.
- Row counts before the release: members 13, subscriptions 14, attendances 12, payments 13,
  service_charges 0, cafe_orders 0, expenses 0, products 28
- `deploy/release.sh gym@94.184.45.96` from Git Bash, with the key loaded once into `ssh-agent`

**Rollback warning:** a rollback to `3059c81` is safe only until the first guest visit or
miscellaneous sale is written. Old code cannot read an attendance or service charge without a
member, so after that the way out is a fix forward, not `./server.sh rollback`.

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

- [x] A record for `panel.` alongside the apex, same server, proxy off (done 2026-09-25, TTL 300)
- [x] A second Caddy site for the apex serving a static placeholder (`web/public-site`, copied
      into the image at `/srv-public`); the panel site keeps everything it has now plus
      `X-Robots-Tag: noindex`. A third block redirects `www.` as well — it has an A record
- [x] The `.com` redirects to the **public page**, not to the panel. Decided while building it,
      against what this task first said: whoever types the gym's second domain is a visitor, not
      a member of staff, and sending the public to a staff login form is the thing this task
      exists to stop. The cost is that staff who type the old address by habit reach the public
      page, which is why they are told the new one first
- [x] Smoke-tested locally on the real production stack with `DOMAIN=localhost`: the panel serves
      the app with `noindex` and `/health` through to the API, the public page serves its file and
      font and returns 404 for `/health`, and both `www.` and the second domain return 301 to the
      public page with the path kept
- [x] `DOMAIN=panel.pasargadgymplus.ir`, `PUBLIC_DOMAIN=pasargadgymplus.ir` and
      `REDIRECT_DOMAIN=pasargadgymplus.com` in `/opt/gym/.env`, then `up -d`. Released the image
      first, on its own: with the old `.env` it changed nothing anybody could see, so a broken
      build would have shown up before the disruptive step rather than during it
- [x] The staff address is `https://panel.pasargadgymplus.ir`. The system is still in trial use,
      so the Owner chose not to wait for a quiet hour — being signed out costs a login

Done when: the gym's domain serves the gym, the application answers on its own host, and both are
on real certificates.

Switched on 2026-09-25 and verified from outside against the live server: the panel serves the
app (200, `<title>مدیریت باشگاه</title>`) with `X-Robots-Tag: noindex, nofollow`, the apex serves
the placeholder (200), `www.` and the `.com` both 301 to the apex, and all four names present a
valid Let's Encrypt chain. `/health` answers 200 on the panel and **404 on the public domain** —
the public page really is served from static files with no path to the API, which is the property
that keeps the gym's website up while the application is not.

`panel.pasargadgymplus.com` deliberately does not exist (NXDOMAIN). A second name for the panel
would need its own certificate and Caddy site for no gain, and would reintroduce exactly the
problem this task removed: a staff member who typed it would get a separate session and be signed
out of the other one.

---

## Phase 6.5 — Front desk follow-ups (found during the first real use, 2026-09-25)

These came out of the Owner using the deployed system for the first time. They are features,
not polish: the front desk meets all of them every day. Numbered 6.5 rather than folded into
Phase 7 because they should land before the cafe adds more to the same screens.

### 6.5.1 Lockers for the front desk
Rule change, decided by the Owner on 2026-09-25: a staff member could not open the lockers
screen at all, because `LockersEndpoints` puts the whole group behind `Policies.OwnerOnly`. But
the person who sees a broken locker is the one at the desk, not the Owner.

- [x] BUSINESS_RULES.md §0: split the "Plans, lockers setup, staff accounts" row. Creating a
      locker stays Owner-only; listing lockers and taking one out of / back into service become
      Staff too. Plans and staff accounts are unchanged
- [x] Split the endpoint group: `GET /` and `GET /{id}` and the two service endpoints allow
      Staff, `POST /` stays `OwnerOnly`. One explicit policy per endpoint, never a group default
      that quietly widens later
- [x] A column on the lockers screen naming the member who currently holds each locker, so
      "whose is locker 1?" is answered without opening attendance. Occupancy is derived from the
      open attendance and never stored (BUSINESS_RULES.md §6), so the member's name comes from
      the same join
- [x] The create button is hidden for Staff. The API enforces it too — the hidden button is
      about not offering what would fail, not about security
- [x] Tests: Staff can list and take out of service; Staff creating a locker is 403; the holder
      column is empty for a free locker and names the member for an occupied one

### 6.5.2 The "currently inside" board: one row, one line
Two problems on one screen. `ServiceChargeBox` is a 147-line component rendered inside a table
cell, and its amount / payment / void forms expand **in place**, so a row can triple in height.
The cafe will want the same slot, and a column per service does not scale.

- [x] A row is always one line. The هوازی cell shows a summary only (amount plus payment badge);
      every form moves into a side panel or dialog opened from that row
- [x] Session progress per row: used / total with a bar, in the shape of the reference design.
      An unlimited subscription shows "نامحدود" and no bar — a bar needs a denominator
- [x] Expiry date per row, so the desk sees an expiring subscription at check-in rather than
      after it lapses. **No status badge**, decided while building it: check-in refuses a
      subscription that is not usable today, so the badge would read "فعال" on every row of this
      board. What the desk cannot otherwise see is how close the subscription is to running out,
      so the row is marked instead — 3 or fewer sessions left, or 5 days to expiry
      (BUSINESS_RULES.md §7, thresholds chosen by the Owner). Status belongs on the members list,
      where expired and unsubscribed members appear together
- [x] `CurrentlyInsideResponse` carries none of this yet (member, locker, time, charges only):
      extend the projection rather than firing a query per row
- [x] Built the bar as a shared component (`components/SessionsBar.tsx`) for Phase 9 to reuse.
      The status badge already existed (`SubscriptionStatusBadge`) and was not duplicated
- [x] While here: the "عضو جدید" button on the member search screen has no colour, unlike the
      one on the members list. One primary-button component, used by both

Done when: a staff member can manage lockers without the Owner, and a row on the board never
grows taller than one line.

### 6.5.3 Single-session entry (تک‌جلسه‌ای): the rules and the API
Asked for by the Owner on 2026-09-25: someone walks in with no subscription and wants to pay for one
visit. Check-in refuses them today, because it requires an `Active` subscription. Decided with the
Owner to model a single visit as an **ordinary subscription** sold from one dedicated plan
(BUSINESS_RULES.md §3, §4). That keeps the attendance table, lockers, هوازی, debt, payments and
refunds working untouched — but it means the subscription must be made invisible to the five rules
that order a member's calendar, or selling a single visit will silently rewrite what the member
already bought.

- [x] BUSINESS_RULES.md §0, §3, §4, §7 and §12 written up before any code (planning session,
      2026-09-25)
- [x] `Plan.Kind` (`Membership` / `SingleSession`); validation forcing `DurationDays = 1` and
      `SessionCount = 1` for the single-session kind; partial unique index so a second
      single-session plan cannot exist
- [x] `Subscription.IsSingleSession`, snapshotted at sale. Migration: the column defaulting to
      `false` for existing rows, plus a check constraint tying the flag to `duration_days = 1` and
      `total_sessions = 1`
- [x] Migration: recreate the `SubscriptionConstraints.NoOverlap` exclusion constraint with
      `AND NOT is_single_session` in its `WHERE`. Hand-written SQL, like the original in
      `AddSubscriptions`
- [x] `SubscriptionSchedule.NextStartDate`: a single-session sale starts today and reads nothing.
      And the `EndDate >= today` query in `SubscriptionSeller` skips single-session rows, so a
      membership sold to someone who dropped in today still starts today instead of tomorrow
- [x] `SubscriptionSchedule.InEffectToday` and `Subscription.CloseExhaustedEarly`: membership rows
      only. A single-session sale closes nothing early and pulls no queued subscription forward
- [x] Freeze refuses a single-session subscription; unfreeze shifts queued memberships only
- [x] Renew reads the latest membership subscription and ignores single-session rows
- [x] Tests (domain): selling a single visit to a member with an `Exhausted` membership leaves that
      membership's `EndDate` alone; with a renewal queued, the renewal does not move; the start date
      is today whatever the calendar holds; freeze and renew are refused
- [x] Tests (integration): two single-session sales on the same day both succeed, and the member
      checks in twice with a check-out between; a single visit sold alongside a frozen membership is
      accepted; two overlapping **memberships** are still refused by the constraint; a second
      single-session plan is refused

Done when: a member with no subscription — or with a frozen, expired or exhausted one — can be sold a
single visit and checked in, and nothing about their own subscription changes. Closed 2026-09-26
with the whole suite green and zero warnings. Two things were decided while building and need
review: among active subscriptions a single visit is consumed before a membership (it is worth
nothing tomorrow, the membership's sessions keep), and both new column defaults were kept rather
than dropped, because every hand-written INSERT that predates them would otherwise fail.

6.5.4 was being built in the same working tree at the same time and took three things the entry
screen needed from this task's surface: `IsSingleSession` on `CurrentlyInsideResponse`, the `Kind`
filter on `GET /api/plans`, and the `npm run gen:api` regeneration. They belong to 6.5.4's list,
noted here so the split is not mistaken for something 6.5.3 forgot.

Follow-up, 2026-09-26: §3 says the Owner creates the single-session plan "from the plans screen",
and the entry screen tells the desk exactly that when it is missing — but the plan form had no
way to choose the kind, so the Owner could not create it at all. The create form now has a
"پلن تک‌جلسه‌ای (ورود آزاد)" box that locks the duration and sessions at 1 and sends
`kind: "SingleSession"`; it is switched off, with the reason, once that plan exists. The edit form
shows the kind but cannot change it, and the plan list marks the plan with a badge.

### 6.5.4 UI: the entry screen
The member search screen (`HomePage`) is already the desk's entry point and already has a check-in
button per row, but it dead-ends as soon as the person has no usable subscription. This finishes that
screen rather than adding a second place that searches members, which would leave staff choosing
which one to open.

- [x] One search box, as now, and one primary action per row: "ورود", exactly as before. **The
      state is not worked out in the browser** — the screen asks the API to check the person in and
      reacts to its answer. Deriving "can this person come in today" in React would be a second
      copy of BUSINESS_RULES.md §4 and §7 living where nobody maintains it, and it is the copy that
      would be wrong. Changed from the plan while building; the guarantee it was after is stronger
      this way, because the single-visit offer cannot appear for someone the API just let in
- [x] Found, nothing usable: the refusal opens a panel with the reason, "ورود تک‌جلسه‌ای" and its
      price as the primary action, and "فروش اشتراک" beside it
- [x] Not found: register the member on the same screen (name, phone, birth date, notes — the whole
      `MemberForm`), and the new member goes straight into check-in, whose refusal opens the same
      panel
- [x] The board shows "تک‌جلسه‌ای" instead of the session bar, and no "needs attention" mark
      (BUSINESS_RULES.md §7). `CurrentlyInsideResponse` carries `IsSingleSession` for it
- [x] When the single-session plan has not been created yet, or is inactive, the panel says which
      in Persian and offers no button. `GET /api/plans?kind=SingleSession` finds that one plan —
      there is exactly one and it can sit on any page, so paging was not a way to find it
- [x] `npm run gen:api`, which 6.5.3 deliberately left to this task, plus the two frontend fixtures
      that then needed `kind` and `isSingleSession`
- [x] Tests: all four cases reach check-in; a successful check-in never offers a single visit; an
      exhausted pack does; a refusal selling a visit would not fix (already inside) stays an
      ordinary error

Done when: a walk-in visitor is inside with a locker, paid for, without the desk opening a second
screen. Closed 2026-09-26: 922 backend tests and 403 frontend tests green, zero warnings. One
thing decided while building and needing review: the sale and the check-in are two requests, not
one transaction — if the sale lands and the check-in fails, the member has a paid visit for today
and the ordinary "ورود" button finishes the job, which is visible and recoverable.

### 6.5.5 The locker map: the desk chooses the locker
Rule change, decided by the Owner on 1405/07/05 (2026-09-27), with photos of the real lockers.
Until now check-in took a random free locker, the Owner created lockers one by one, and the
lockers screen was a paged table. Now the gym's 72 lockers are fixed, drawn exactly as they stand
(1–30 outside the changing room; 31–66 on the inside wall and 67–72 in a separate cabinet), and
the desk gives a member the locker it chooses, by clicking it, the way a cinema seat is booked.
Check-in happens **only** there. BUSINESS_RULES.md §1, §6 and §7 have the rules.

One task, not an API task followed by a UI task: check-in now needs a locker in its request and
the create-locker endpoint disappears, so an API change on its own would leave the running web
app broken.

- [x] BUSINESS_RULES.md §1, §6, §7 written up before any code (planning session, 2026-09-27)
- [x] **Fixed lockers.** `LockerSeed` (72 rows with fixed ids, 1–72) applied with `HasData`, like
      `ExpenseCategorySeed`; `Locker.Count = 72` in Domain; `ck_lockers_number_range` (1–72).
      The integration `DatabaseFixture` puts the lockers back after every Respawn reset, as it
      does for the expense categories. The server's test data is wiped before release (the
      Owner's call), so no existing rows need reconciling
- [x] **No creating lockers.** Remove `Application/Lockers/CreateLocker/*`, `POST /api/lockers`,
      `Locker.Create` / `CheckNumber`, the create-only errors (`Lockers.NumberInvalid`,
      `Lockers.NumberAlreadyExists`) and their Persian messages, and the DI registrations
- [x] **Check-in takes the chosen place.** A body `CheckInCommand(Guid? LockerId)` on
      `POST /api/members/{memberId}/attendance/check-in`; `null` asks for a reserve place.
      `CheckInHandler` drops `EF.Functions.Random()` and checks `Lockers.NotFound`,
      `Lockers.OutOfService`, `Attendance.LockerTaken`; with no locker, `Attendance.LockersStillFree`
      or the lowest free reserve place, else `Attendance.ReserveFull`. A unique violation on
      `OneOpenPerLocker` maps to `Attendance.LockerTaken` instead of the generic
      `ChangedConcurrently`
- [x] **Reserve places in the database.** `Attendance.ReserveSlot` (`smallint`, nullable), check
      constraint 1–15, partial unique index where `checked_out_at IS NULL`, and a check that an
      open visit has exactly one of `locker_id` / `reserve_slot`. `Attendance.CheckIn` takes one
      or the other
- [x] **Move to another locker.** `Attendance.MoveToLocker(lockerId)` (`NotOpen`, `SameLocker`;
      clears the reserve place) and `POST /api/attendance/{id}/move-locker` with a handler in
      `Application/Attendances/MoveLocker/` that checks the target like check-in does
- [x] Every new error code in `web/src/lib/errors.ts` (`ErrorCatalogTests` checks it), then
      `npm run gen:api`
- [x] **The map layout** (`features/lockers/layout.ts`): zones → groups (wall, free-standing
      cabinet) → cabinets → columns of three, top to bottom. Drawn `dir="ltr"` like the wall. A
      test that every number 1–72 appears exactly once
- [x] **`LockerMap`**, reusable: each locker a door-shaped button, green free, red occupied (the
      holder's name as its tooltip), grey hatched out of service; a legend with counts; a "pick"
      mode that enables free lockers only, for moving
- [x] **`LockersPage` rewritten.** All lockers (`PageSize=100`) and the currently-inside list
      (`PageSize=100`), both refreshing every 15 s, joined by locker number. At most 72 + 15 = 87
      visits are open at once, so one page always holds them all. The create form, `LockersTable`,
      `Pager` and `useCreateLocker` go
- [x] **Free locker → `LockerCheckInDialog`.** Search by name or mobile (`useMemberList`,
      debounced like `HomePage`); a member already inside is marked "داخل باشگاه — کمد n" and
      choosing them shows an error; confirm → check-in with `lockerId` → the locker large plus
      `VisitSummary`; no usable subscription → `SingleVisitOffer`, with `useSellSingleVisit`
      taking the `lockerId`; not found → register with `MemberForm` and carry on; and "خارج از
      سرویس" for the locker itself
- [x] **Occupied locker → `LockerVisitDialog`.** Member linked to the profile, time in,
      `SessionsBar`, `VisitSummary` (debt item by item), `ServiceChargeBox` (هوازی),
      `VisitCafeBox` (cafe), check-out and cancel through the existing `CheckInOutDialog`, and
      "جابه‌جایی کمد" through `LockerMap` in pick mode. Out-of-service locker → bring back into
      service. No location text in any box
- [x] **Reserve places**: one small control ("ورود بدون کمد n از ۱۵") opening 15 boxes. A used box
      shows the member's name and opens `LockerVisitDialog`; an empty one opens check-in only when
      every locker is full, and otherwise says why not
- [x] **Check-in leaves the other screens.** `CheckInOutDialog` loses its `checkIn` kind (that
      logic moves to `LockerCheckInDialog`); `HomePage` / `MembersTable` and `MemberProfilePage`
      lose the check-in button but keep the locker and check-out for someone inside; "ثبت این شخص"
      on the search screen registers and opens the profile. The "currently inside" board shows
      "رزرو" for a reserve place
- [x] Tests (domain): `MoveToLocker` on a closed visit, to the same locker, from a reserve place;
      `CheckIn` with a locker or a reserve place
- [x] Tests (integration): exactly 72 lockers 1–72 after migrating; the create endpoint is gone;
      check-in gets the chosen locker; occupied → `LockerTaken`; out of service →
      `OutOfService`; reserve while a locker is free → `LockersStillFree`; all full → reserve
      works and the sixteenth gets `ReserveFull`; two check-ins racing for one locker → one wins,
      one `LockerTaken`; move (ok, target taken, closed visit, reserve → locker); every new
      constraint refused by raw SQL. Rewrite the old tests that created lockers or expected a
      random pick or a no-locker warning (5.1, 5.2, 5.4, 6.5.1)
- [x] **The map is the first screen** (the Owner, 1405/07/05): the first item in the menu, named
      "ورود با کمد", and the page every user lands on after logging in. The member search screen
      that held "ورود به باشگاه" no longer checks anyone in, so it is renamed "جستجوی عضو" and moves
      down the menu. Links and redirects that assumed search was home (`paths.home`, the
      catch-all redirect, the post-login redirect) follow
- [x] **Staff use the whole map** (BUSINESS_RULES.md §6): the screen stays in every user's menu,
      and each new endpoint (check-in with a locker, move-locker) names a policy that allows Staff
      explicitly, never a group default (the lesson of 6.5.1)
- [x] Tests (integration, roles): a Staff user checks in with a chosen locker, takes a reserve
      place, moves a visit, and takes a locker out of service and back; none of it is 403
- [x] Tests (frontend): the layout; the map draws two zones and 72 doors with the right colours
      and labels; signed in as Staff, every action on the map is offered; a free locker opens the search; an inside member is marked and refused; check-in
      sends `lockerId`; the occupied box shows هوازی, cafe, check-out, cancel and move; the reserve
      places are tucked away and locked while a locker is free; `HomePage`, `MemberProfilePage`
      and `CurrentlyInsidePage` tests updated
- Mobile is out of scope on purpose: how the map opens on a phone is decided with 13.1

Done when: at the desk, clicking locker 12, finding the member and confirming checks them in with
locker 12; هوازی and a cafe item are added from the same locker; the visit moves to locker 40; and
check-out with the key ticked turns 40 green again — all from the lockers screen, with no locker
ever chosen at random.

Built (2026-09-27). Decided with the developer while building: responses say `usesReservePlace`
outright instead of the screen reading it off a missing locker (old closed visits have neither);
`attendances` got an `xmin` token, so a move racing a check-out is refused rather than moving a
closed visit (check-out, cancel and move answer `Attendance.ChangedConcurrently`); the map is `/`,
the member search moved to `/search`, and `/lockers` is gone (an old link lands on the map). The
"done when" scenario runs as Staff in `LockerMapStaffTests`. Done: 405 domain, 809 integration and
575 frontend tests pass.

### 6.5.6 Custom plans and two fixed prices (پلن شخصی و قیمت‌های ثابت)
Rule change, decided by the Owner on 1405/07/05 (2026-09-27). The gym stops selling from a list of
plans. Each member's plan is built at the desk — any number of days (1–365) and at least 5
sessions, no upper limit, the two unrelated — and every session costs the same, so a plan's price
is `sessions × SessionPrice`. A single visit (walk-in or guest) costs `SingleVisitPrice`. Both
prices are set by the Owner on a settings screen, so they follow inflation without a deployment and
the desk never types a price. BUSINESS_RULES.md §0, §1, §3 and §4 have the rules; this replaces
Phase 3 and the single-session plan of 6.5.3.

One task, not API then UI: selling a subscription changes its request body, so an API change alone
would break the running web app.

- [x] BUSINESS_RULES.md §0, §1, §3, §4 rewritten before any code (planning session, 2026-09-27).
      Proposed by Claude and approved with the plan: both prices start unset and selling is
      refused until the Owner sets them; renew sells the same days and sessions at today's price;
      Staff read the prices, only the Owner changes them; a plan reads as its numbers
- [x] **Domain.** `Plans/` deleted. `Pricing/PriceList` (one row, `TheId`, both prices nullable,
      `Update`, `CheckPrice`). `Subscription` loses `PlanId`; `TotalSessions` is required (no
      unlimited); `CreateMembership(days, sessions, sessionPrice, start)` and
      `CreateSingleVisit(price, today)` replace `Create(plan)`. `SubscriptionSchedule.StartDateFor`
      is gone: a single visit takes today and never sees the calendar
- [x] **Application.** `Plans/` and `PlanNames` deleted. `Pricing/GetPrices`,
      `Pricing/UpdatePrices` (version check + `xmin`). `AssignSubscriptionCommand(DurationDays,
      SessionCount)`; `Subscriptions/SellSingleVisit`; `SubscriptionSeller.SellMembershipAsync` /
      `SellSingleVisitAsync` share the lock and the save. Responses carry the numbers
      (`PlanSummary` on debt items and payment history) instead of a plan name
- [x] **Database.** Migration `CustomPlansAndPriceList`: drops `plans` and `subscriptions.plan_id`;
      `total_sessions` NOT NULL; `ck_subscriptions_total_sessions_range` becomes
      `is_single_session OR total_sessions >= 5`; `price_lists` with one seeded row, a check that
      its id is the fixed one, and non-negative prices. The integration fixture restores the row
      after each reset
- [x] **API.** `PlansEndpoints` deleted. `GET /api/pricing` (Staff and Owner), `PUT /api/pricing`
      (Owner); `POST /api/members/{id}/subscriptions/single-visit`. Each names its policy
- [x] **Web.** `features/plans` deleted with its route and menu item. «تنظیمات» (Owner only): two
      `MoneyField`s. The sale form takes days and sessions and shows `n جلسه × price = total` before
      confirming; the single-visit offer reads its price from the settings and posts to the new
      endpoint. `planLabel()` names a subscription «۳۰ روز · ۱۲ جلسه» or «تک‌جلسه‌ای» everywhere.
      The unlimited-session branches are gone. `npm run gen:api`
- [x] Tests (domain): price = sessions × rate, days do not change it, 4 sessions refused, 1,000
      allowed, days 0/366 refused, prices unset refused, overflow refused; single visit;
      `PriceList` rules
- [x] Tests (integration): pricing endpoints (Staff reads, Staff PUT 403, audit, stale version,
      field codes, seeded row, second row refused); sale at the current rate, price change leaves a
      past sale alone, renew at today's price, single visit sold at its price / refused while unset;
      every constraint by raw SQL. The ~25 files that inserted a `Plan` use `TestPlans` instead
- [x] Tests (frontend): settings page (Staff refused, save sends both prices and the version,
      stale version, empty price), sale form (digits, preview, fewer than 5, price unset), the
      single-visit offer, `planLabel`

**Release step:** the server's test data must be wiped before this migration runs — it refuses
subscriptions with fewer than 5 sessions or none (the Owner's call, as in 6.5.5). After release the
Owner opens «تنظیمات» and sets both prices; until then the desk can sell nothing.

Done when: the Owner sets 75,000 a session and 150,000 a visit; the desk sells «۴۵ روز · ۱۲ جلسه»
for 900,000 without typing a price; the Owner raises the session price and that sale still costs
900,000; a walk-in is let in from the locker map for 150,000.

Built (2026-09-28). Found while building: the settings form is keyed by `version`, so its "saved"
message lived one level up or it vanished with the rebuild. Done: 389 domain, 785 integration and
532 frontend tests pass, zero warnings.

### 6.5.7 Sell at the locker: the sale and the check-in in one step
Asked by the Owner, 1405/07/06 (2026-09-28). A member with no usable subscription had to be sold a
plan on their profile and then brought back to the map to choose the locker a second time. Now the
check-in box sells the plan (or the single visit) right under the refusal, and the same press checks
them in with the locker already clicked. The Owner's rule: a subscription sold at the locker always
comes with the locker — one person does these steps one after another, so a sale without the locker
is never what they meant. BUSINESS_RULES.md §7 *Check-in* and *Confirming at the front desk*.

- [x] BUSINESS_RULES.md §7 written first: the sale is part of the check-in's transaction; the plan
      form is offered only when a new plan would start today; a person registered in the box goes
      straight to the sale; payment is left to the visit's box
- [x] **Application.** `CheckInCommand` takes an optional `Sale` (`SingleVisit`, or `Membership`
      with days and sessions) and `CheckInValidator` checks it, reporting `durationDays` and
      `sessionCount` under the same codes as the profile's sale. `SubscriptionSeller` gains
      `AddMembershipAsync` / `AddSingleVisitAsync`, the same sale inside a transaction the caller
      holds; `CheckInHandler` sells, saves inside its transaction, then checks in, so a refused
      check-in (locker taken, plan not starting today) rolls the sale back. New code
      `Attendance.SaleInvalid`
- [x] **API.** The check-in endpoint validates its body. Found on the way: `ValidationFilter` threw
      (500) when the body was missing, because minimal APIs run filters even after binding failed;
      it now leaves the framework's 400
- [x] **Web.** `PlanForm` (days, sessions, price preview) shared by the profile's sale and the
      check-in box. `SaleAtCheckInOffer` (was `SingleVisitOffer`): single visit, and a plan form
      that opens in place when `canSellPlanForToday`; otherwise the profile link as before. Both
      sales go through `useCheckIn({ sale })`; the two-request `useSellSingleVisit` is gone. A
      person registered in the box goes straight to the offer. `npm run gen:api`
- [x] Tests (integration): plan sold and checked in with the chosen locker, starting today; after
      an expired plan; locker taken → 409 and nothing sold; behind a frozen plan → refused and
      nothing sold; exhausted on its first day → `NextStartsTomorrow` and nothing sold; price unset;
      field codes; inactive member; single visit sold and checked in, locker taken, numbers with a
      single visit, member already inside; cancel check-in keeps the plan; the filter's empty body
- [x] Tests (frontend): single visit in one request with the sale; plan sold in place with the
      price shown and the same locker; a refused sale shown in the form; a frozen plan leaves plans
      to the profile; a registered person goes straight to the sale

The standalone `POST /api/members/{id}/subscriptions/single-visit` stays (its tests pin the
single-session rules), but the web app no longer calls it.

Done when: a walk-in with no plan is sold «۳۰ روز · ۱۲ جلسه» from the locker they clicked and is
inside without the desk going back to the map; payment is collected later from that locker's box.

### 6.5.8 Cancel check-in: ask before cancelling هوازی and cafe
Asked by the Owner, 1405/07/06. Before this, cancel check-in always voided the visit's هوازی and
never touched its cafe orders (BUSINESS_RULES.md §7 *Gym services*, §8). Now the desk decides, one
purchase at a time. BUSINESS_RULES.md §7 *Cancel check-in*.
- [x] Nothing bought during the visit: one question, as before, then it is cancelled
- [x] هوازی or cafe on the visit: the box lists the هوازی and **each cafe order with its own
      tick** (the developer's answer to the open question: one per purchase, not one for both),
      with its amount and what was paid, all unticked. Unticked stays on the member's account.
      With anything ticked, a second question names what goes and tells the desk, briefly: hand
      back what you collected for it; nothing collected, nothing to do (plus the recorded figure
      when there is one). "بازگشت" returns to the ticks
- [x] **API.** `CancelCheckInCommand(bool? VoidCardio, Guid[]? CafeOrderIds)`, both required
      (`Attendance.CancelChoiceRequired`, 400); an order that is not a standing order of the visit
      refuses the whole cancellation (`Attendance.CafeOrderNotOnVisit`, 422). The handler voids
      and cancels in the check-in's transaction; the refund of a cafe order moved into
      `CafeOrderRefunder`, shared with cancelling at the till
- [x] **Web.** `CancelCheckInConfirm` reads the visit's purchases from the "inside" list, so the
      locker map, the board and the profile ask the same way; `useCancelCheckIn` sends the choice
      and refreshes debt, payments and cafe. `npm run gen:api`
- [x] BUSINESS_RULES.md §7 and §8 rewritten first
- [x] Tests (integration): هوازی ticked → voided and refunded; unticked → still owed; one order of
      two ticked → only it cancelled, card refund as card; an order of another visit or already
      cancelled → 422 and nothing changed; choice missing or an order named twice → 400; هوازی
      ticked with none → fine. Every existing cancel call now sends "keep everything"
- [x] Tests (frontend): nothing bought → no ticks, one question; each purchase its own unticked
      box; nothing ticked → no second question; ticks → second question with the refund reminder
      and only the ticked ones sent; "بازگشت" keeps the ticks; a refusal shown in Persian

Done when: a member who took a drink and used the treadmill is cancelled with only the treadmill
ticked; the desk is told to hand back what was collected for it, and the drink stays on the account.

Built (2026-09-28). Found while building: an API left running from an earlier session held the old
build on :5134, which would also have fed `gen:api` the old endpoint. Done: 389 domain, 809
integration and 540 frontend tests pass, zero warnings, lint and typecheck clean.

### 6.5.9 A frozen member who comes in is unfrozen
Asked by the Owner, 1405/07/06. Replaces "a frozen member is sold a single visit and the freeze is
not touched" (BUSINESS_RULES.md §4 *Freeze*).
- [x] Check-in on a frozen plan unfreezes it in the same transaction, by the ordinary unfreeze
      rules (frozen days added, the allowance respected, queued plans shifted), then uses a session.
      `SubscriptionSchedule.FrozenToResume` / `Unfreeze` (Domain) hold the rule; the Owner's
      `UnfreezeSubscriptionHandler` now calls the same `Unfreeze`
- [x] Anything usable today is still used first and the freeze is left alone: a single visit, or
      another plan active today
- [x] A plan that turns out expired once unfrozen: `Subscriptions.Expired`, nothing saved, still
      frozen for the Owner (proposed in planning, not objected to)
- [x] A check-in that carries a sale never unfreezes: it uses what it sold or is refused, so a plan
      sold at the locker is never queued behind one unfrozen a moment later
- [x] Cancel check-in gives the session back but leaves the plan unfrozen (the developer's answer);
      the Owner freezes it again, and freeze days add up, so none are lost
- [x] The box warns before confirming that the plan is frozen and will be unfrozen (the
      developer's answer, from the plan the profile card shows), and afterwards says it was, with
      the days added (`AttendanceResponse.UnfrozenDays`). The manual unfreeze stays Owner-only
- [x] BUSINESS_RULES.md §1, §4 and §7 rewritten first
- [x] Tests (domain): which frozen plan is resumed; the queue shifted by the added days, capped by
      the allowance; single visits and earlier plans untouched; same day moves nothing
- [x] Tests (integration, as Staff): unfrozen, extended and a session used; the queued plan
      shifted; the allowance cap; expired once unfrozen → 422 and still frozen; a single visit held
      or sold with the check-in is used and the freeze stays; a plan sold behind a frozen plan →
      refused and still frozen; cancel keeps it unfrozen
- [x] Tests (frontend): the warning before confirming and the notice after; no warning when a
      single visit is held for today; no word of a freeze on an ordinary check-in

Done when: a member whose plan the Owner froze walks in, the desk clicks a locker and is told the
plan is frozen, confirms, and the member is inside with a session used and the frozen days added to
the plan's end.

Built (2026-09-28): 398 domain, 817 integration and 542 frontend tests pass, zero warnings, lint and
build clean (the chunk-size note predates this task).

### 6.5.10 Who had a locker today (تاریخچه امروز کمد)
Asked by the developer, 1405/07/06. BUSINESS_RULES.md §6 *Who had a locker today*.
- [x] `GET /api/lockers/{id}/today`, both roles: the visits checked in to that locker since the
      gym's midnight, oldest first, with the member's name, check-in, check-out and cancellation.
      Not paged; `Lockers.NotFound` for an unknown locker
- [x] A visit counts for the locker it holds now; a moved visit is listed under its new locker
      only (the developer's answer: the old locker is only in the audit log)
- [x] Cancelled check-ins are listed, marked «لغو شده» (the developer's answer)
- [x] The free locker's box offers «تاریخچه امروز این کمد» before a member is chosen only; the
      button beside «بله، ورود ثبت شود» / «انصراف» was removed later (the developer's answer:
      once a member is chosen, the box is only about confirming). Each name links to the
      member's profile; «بازگشت» returns to the search. Not offered for a reserve place
- [x] BUSINESS_RULES.md §1 and §6 written first
- [x] Tests (integration, as Staff): two visits oldest first with names; a visit a minute before
      midnight left out; another locker's visit left out; a cancelled check-in listed; a moved visit
      only under its new locker; unknown locker 404; no token 401
- [x] Tests (frontend): the list with profile links, times and the cancelled mark, fetched only
      when asked; not offered at the confirmation; the empty
      state; no button on a reserve place; `formatTime`

Done when: the desk clicks a free locker, asks who had it today, sees this morning's members with
their times, and opens one of them's profile from the list.

### 6.5.11 Guest visit (ورود مهمان)
Asked by the developer, 1405/07/07 (2026-09-29). BUSINESS_RULES.md §1, §6, §7 *Guest visit*, §7 *Gym
services* and §8, written first (this session). Replaces marking a locker «خارج از سرویس» for a
relative who takes a key without paying.
- [x] BUSINESS_RULES.md §1, §6, §7 and §8 written first
- [x] Domain: `Attendance` gets `MemberId?`, `SubscriptionId?` and `GuestName?`, with new factories
      `CheckInGuest` and `CheckInGuestOnReservePlace`. The name is trimmed and required, at most
      `Member.FullNameMaxLength`, and normalized like a member's
- [x] Domain: `CafeOrder` may name a guest visit and no member, and stay unpaid. A walk-in with
      neither a member nor a visit is still paid in full
- [x] Migration `GuestVisits`:
      - `member_id` and `subscription_id` become nullable
      - new column `guest_name varchar(200)`
      - checks `(member_id IS NULL) <> (guest_name IS NULL)` and
        `(member_id IS NULL) = (subscription_id IS NULL)`
      - `ck_cafe_orders_visit_has_member` is dropped, not replaced: a check sees only its own row and
        cannot tell a guest's visit from a member's (agreed with the developer, 1405/07/10)
      - also `ck_attendances_guest_name_not_blank`
      - the existing partial unique indexes stay
- [x] `POST /api/attendance/guest-check-in` `{ guestName, lockerId? }`, `StaffOrOwner`:
      - the locker goes through `LockerChoice.CheckAsync`
      - with no locker, a reserve place under §6. Move the reserve-place pick out of
        `CheckInHandler` into a shared helper
      - the same unique-violation mapping as check-in
- [x] `POST /api/attendance/{id}/settle-guest` pays every unpaid order of a guest visit in one
      transaction, one payment per order, as `SettleMemberDebt` does
- [x] Check-out, and a cancel that leaves an unpaid order unticked, are refused on a guest visit
      with `Attendance.GuestHasUnpaidCafe`. Auto-checkout still closes the visit
- [x] هوازی on a guest visit is refused with `ServiceCharges.GuestVisit`
- [x] Places that assume a visit has a member or a subscription:
      - `AttendanceResponse`: member and subscription become nullable, plus `GuestName`
      - `CurrentlyInsideResponse`: the same; its projection's `.First()` becomes null-safe
      - `LockerResponse`: gains the guest's name. `IsOccupied` counts a member or a guest, and
        the holder's debt for a guest is the visit's unpaid cafe
      - `LockerVisitResponse`
      - `VisitCafeOrders` (`MemberId!.Value`)
      - `CancelCheckInHandler`: no member lock and no `RestoreSession` for a guest
      - `CreateCafeOrderHandler`: an open guest visit with no member makes a guest order
      - the cafe order list shows the guest's name and filters unpaid guest orders
- [x] Fix `SetLockerOutOfServiceHandler.FindHolderAsync`. Its inner join to members would read a
      guest-held locker as free and take it out of service
- [x] `npm run gen:api`, then the frontend:
      - `useGuestCheckIn`
      - in `LockerCheckInDialog`, the «ورود مهمان» button leads to a full-name step and then the
        result, for a locker and for a reserve place
      - a `"guest"` state in `lockerState.ts`, drawn in the colour of a new `--guest` token in
        `index.css` (light and dark), with its own legend line and the «بدهکار» ribbon for
        unpaid cafe
- [x] A guest variant of `LockerVisitDialog`:
      - the name and «مهمان», with no profile link
      - no sessions and no هوازی
      - the cafe box with «تسویه یکجا»
      - check-out disabled until everything is paid; move and cancel as for a member
- [x] `CheckInOutDialog` and `CancelCheckInConfirm` handle a member or a guest. The till can pick a
      guest who is inside. `CurrentlyInsideTable`, `ReservePlaces` and `LockerTodayHistory` show
      the guest's name without a link. New error codes go in `lib/errors.ts`
- [x] Tests (domain): guest factories and name validation; a guest visit's cafe order
- [x] Tests (integration, as Staff):
      - guest check-in on a locker and on a reserve place
      - refusals: locker taken, out of service, lockers still free, reserve full, blank or long name
      - the database checks
      - move, and auto-close with unpaid orders kept
      - check-out and cancel refused while unpaid, allowed after `settle-guest`
      - هوازی refused
      - an order from the till joins the guest visit
      - the guest shown on the map, the board and today's history
      - out of service refused while a guest holds the locker
- [x] Tests (frontend):
      - guest check-in from a locker and from a reserve place
      - the guest colour, label and ribbon
      - the guest's box: pay everything, then check out
      - a guest row on the board
      - picking a guest at the till
- [x] Found on the way (1405/07/10):
      - `IAppDbContext.LockAttendanceAsync`: a guest has no member row to lock, so the visit's row
        serializes their check-out, cancel and «تسویه یکجا» against an order or a payment for them
      - `TodayByHour` leaves guests out and `LockerUsage` counts them (§7 *Guest visit*)
      - `LockerResponse.OccupiedByMemberDebt` is now `HolderDebt`, since it covers a guest too
      - the cafe order list's `unpaidGuest` filter, «فقط پرداخت‌نشده — مهمان» on the orders page
      - a cafe order refreshes the locker map, so «بدهکار» follows it at once

Done when: a relative walks in, the desk clicks a free locker, chooses «ورود مهمان» and types their
name. The locker turns the guest colour with the name on it. The relative buys a drink from the till
under their name, and the locker shows «بدهکار». At the door, the desk settles the drink, takes the
key and checks them out.

### 6.5.12 The desk panel: birthdays and renewal opportunities (تولد، فرصت تمدید)
Asked by the developer, 1405/07/07. BUSINESS_RULES.md §6 *The desk panel*.
- [x] BUSINESS_RULES.md §1 and §6 written first (with 6.5.13–6.5.15's rules)
- [x] `CurrentlyInsideResponse` gains `MemberBirthDate` and `HasQueuedRenewal` (a non-cancelled,
      non-frozen, non-single-session subscription of the same member starting after today: the SQL
      form of `Upcoming`), still one SQL statement
- [x] Frontend: the thresholds and `daysUntil` move to `features/attendance/renewal.ts` with
      `renewalDue`; `isJalaliBirthday` in `lib/format.ts` (30 Esfand falls back to 29 Esfand in a
      common year)
- [x] `DeskPanel` in the map's empty top-right corner: «تولدت مبارک» and «فرصت تمدید», each hidden
      when empty. Pointing at an entry blinks its locker; clicking opens the locker's box
- [x] Tests (integration): birth date returned; queued renewal true, false once cancelled, false
      with none
- [x] Tests (frontend): `isJalaliBirthday`, `renewalDue`; the panel's lists, hidden when empty,
      blink on hover, box on click

Done when: a member with two sessions left walks in, and the desk sees their name under
«فرصت تمدید», points at it, sees locker 19 blink, and tells them to renew.

### 6.5.13 Long stay bar on the door (خیلی وقته داخله)
BUSINESS_RULES.md §6 *Long stay*. Frontend only: `checkedInAt` is already in the visits list.
- [x] `stayProgress(checkedInAt, now)` in `features/lockers/longStay.ts`: the fill from 0 to 1
      over 3 hours and `isLong` from 3 hours; a check-in ahead of the browser's clock reads as 0
- [x] `useNow(60_000)` in `lib/useNow.ts`: one timer on `LockersPage`, which builds the progress
      per locker from the visits list and passes it to `LockerMap` (`stays`) and `ReservePlaces`
      (`now`). The move dialog's map passes none and shows no bar
- [x] `StayBar`: a thin bar along the bottom of an occupied door and a used reserve place, filling
      from the right in the door's red, wholly the warning colour from 3 hours
- [x] The door's accessible label (and its hover title) says «بیش از ۳ ساعت»; so does a reserve
      place's. Nothing is written on the door
- [x] Tests (frontend): `stayProgress` at 0, 1.5 h, 2 h 59 m, 3 h, 5 h and a clock behind the
      server; the page with fake timers: half full at 1.5 h, turning to the warning when a minute
      passes at 2 h 59 m (no new request), the same warning on a reserve place

Done when: a member checked in at 9:00 is still on locker 14 at noon, and the desk sees the bar
along its door turn orange without anything being written over the name.
(Its colours were changed in 6.5.16: green to red as it fills, and a faint blink once long.)

Built (2026-09-29): 1234 backend (domain and integration) and 596 frontend tests pass, zero
warnings, lint and build clean.

### 6.5.14 Today by hour, under the map (ورود امروز ساعت به ساعت)
BUSINESS_RULES.md §6 *Today by hour*.
- [x] BUSINESS_RULES.md §6 first: the average is over only the days of the 4 that had a check-in
      (a closed day is left out, not counted as zero), the developer's choice
- [x] Migration `AddAttendanceCheckedInAtIndex`: `ix_attendances_checked_in_at` (shared with 6.5.15)
- [x] `GET /api/attendance/today-by-hour`, both roles (`TodayByHourHandler`): 24 hours in the
      gym's time zone, today's count and the same weekday's average over the previous 4 weeks,
      plus how many days the average covers; cancelled excluded. Five small range queries, the
      moments put into hours in C#
- [x] `TodayByHourChart` under the reserve places, plain SVG: a bar for today, a dashed line at
      the average, hours right to left, the current hour marked, only the busy span of hours; a
      hidden table for a screen reader. Polls every minute and refreshes on any check-in or cancel
- [x] Tests (integration): hours bucketed in the gym's zone around midnight; cancelled excluded
      from today and the average; the average over open days only, ignoring week 5 and another
      weekday; no past days; both roles and 401. (Frontend) the busy span, `gymHour`, the labels,
      no average, nothing yet, a failed request, refetch after a check-in

Done when: at 18:10 on a Wednesday the desk sees today's bar at 18 above the dashed line of the
last four Wednesdays, and knows the evening is busier than usual.

Built (2026-09-29): 1256 backend (398 domain, 858 integration) and 634 frontend tests pass, zero
warnings, lint, type check and build clean.

### 6.5.15 Locker usage map (نقشهٔ استفادهٔ کمدها)
BUSINESS_RULES.md §6 *Locker usage map*.
- [x] BUSINESS_RULES.md §6 first: 7 days is today and the 6 before, 30 picked by default, five
      shades of one colour relative to the most used locker, amber for unused, the lock kept, and
      what the view leaves out (the developer's answers, 1405/07/07)
- [x] `GET /api/lockers/usage?days=7|30|90` (default 30, anything else 400), both roles
      (`LockerUsageHandler`): every locker with a correlated count of its visits between the gym's
      midnight at the start of the period and the one after today, cancelled excluded, counted for
      the locker they hold now. One SQL statement; no migration (6.5.14's index serves it)
- [x] `usageLevel` in `features/lockers/usage.ts`; `--door-use` and `--door-unused` tokens in both
      palettes; `usageDoorClass` in `doorStyle.ts`
- [x] «نقشهٔ استفاده» switch beside the name search; `UsageLegend` (period picker, the five shades
      with the most uses, unused) replaces the counts; `LockerMap`'s `usage` draws `UsageDoor`s, a
      picture rather than a button. The desk panel, the name search and the reserve places go while
      it is on. Read only while on, not polled, the previous period kept while the next loads
- [x] Tests (integration): counts and every locker listed, the 7-day edges on the gym's clock, 30
      and 90 reaching back, cancelled excluded (inserted and through the API), a moved visit, the
      default, 400 for other days, both roles and 401. (Frontend) `usageLevel`; the shades, counts
      and labels; nothing of who is inside and nothing clickable; the period; switching back; not
      asked for while off; a failed request

Done when: the Owner turns on «نقشهٔ استفاده», picks ۹۰ روز, sees locker 67 dashed amber with
«استفاده نشده» while its neighbours are deep violet, and goes to look at its door.

Built (2026-09-29): 1270 backend (398 domain, 872 integration) and 645 frontend tests pass, zero
warnings, lint, type check and build clean.

### 6.5.16 The desk screen's new look, dark (ظاهر تیره صفحه ورود با کمد)
BUSINESS_RULES.md §6 *The desk screen's look*, *Finding a member on the map*, and the changed
*Long stay* and debtor rules. Frontend only, no new package. The first piece of Phase 13.2: the
dark palette is defined once for the whole app, and only this screen switches it on for now.
- [x] BUSINESS_RULES.md §6 written first: the look, the name search, the debtor tag and the
      green-to-red long-stay bar
- [x] `.dark` tokens in `index.css` (the shadcn set plus the door's own); `useDarkScreen()` puts
      `dark` on `<html>` while the screen is open, so its boxes (portalled dialogs) are dark too
- [x] Doors as in the developer's sketch, a hover lift, a one-time pulse on a changed door; the
      cabinet frames go, a wider gap between cabinets keeps them apart
- [x] `DebtorTag` replaces the corner band; `StayBar` mixes green into red by the fill and blinks
      faintly once long, still under `motion-reduce`
- [x] The title alone at the top; the stats strip (`LockerStats`, now the map's legend) with the
      name search at the other end of its row; the desk
      panel restyled; the reserve places drawn as doors from the same classes (`doorStyle.ts`).
      The 72-mark strip and the "last refreshed" mark were tried and dropped
- [x] The clock (time and the date in words) moved to the top of the side menu (`SidebarClock`),
      on every screen
- [x] Room kept for 6.5.14 (the chart goes under the reserve-places row) and 6.5.15 (its switch
      goes on the stats strip); every colour comes from tokens so both read in the dark
- [x] Tests (frontend): the dark class on and off with the screen; the bar's mix at half and full
      and the blink only when long; name search matches, fades the rest, finds nobody by a
      number, normalizes ي/ك and the half-space, opens the reserve places; the changed-door pulse

Built (2026-09-29): 613 frontend tests pass; lint, type check and build clean. Backend untouched.

### 6.5.17 A birthday door celebrates (جشن تولد روی کمد)
BUSINESS_RULES.md §6 *The desk panel*, changed: the birthday member's door is no longer left
plain. Frontend only.
- [x] BUSINESS_RULES.md §6 changed first
- [x] `Celebration`: a turning ring of party colours (a conic gradient on a registered
      `--party-angle`, masked to the edge) and falling confetti, behind the number and name; a
      soft pink glow on the door; still under `motion-reduce`
- [x] The door's label (and hover title) says «امروز تولدش است»; a used reserve place celebrates too
- [x] The panel's birthday list stays plain, like the renewal list, with «امروز تولدشه» beside
      each (the ring and confetti were tried on it and removed by the developer: the door is enough)
- [x] Tests (frontend): the birthday door celebrates and says so, another door does not; a
      reserve place celebrates too

Built (2026-09-29): 614 frontend tests pass; lint, type check and build clean.

### 6.5.18 A plan's days follow its sessions (روزهای پلن از تعداد جلسات)
Rule change, decided by the Owner on 1405/07/07 (2026-09-29). The desk no longer types a plan's
days: it types the sessions and the days follow — 5 to 10 sessions last 30 days, 11 to 20 last 45,
21 to 140 last 70. Fewer than 5 or more than 140 is refused. The price is unchanged: sessions ×
the session price. BUSINESS_RULES.md §3 and §4 have the rules. This replaces the 6.5.6 rule that
days were 1 to 365 and had nothing to do with the sessions.

- [x] BUSINESS_RULES.md §3, §4, §7 changed first. Decided with the developer: renew sells the
      same sessions for the days today's table gives; the database enforces the table too, since
      nothing has been released and the server holds no real sales
- [x] **Domain.** `Subscription.DurationTable` (the one copy of the table), `MaxSessionCount = 140`,
      `DurationDaysFor(sessions)`; `CreateMembership(member, sessions, price, start)` loses its days.
      `MaxDurationDays` and `Subscriptions.DurationInvalid` are gone; `Subscriptions.SessionCountTooHigh`
      is new
- [x] **Application / API.** `AssignSubscriptionCommand(SessionCount)` and `CheckInSale(Kind,
      SessionCount)`: `durationDays` is no longer in either request body (an old client that
      still sends it has it ignored). Renew reads only the latest plan's sessions
- [x] **Database.** Migration `PlanDaysFollowSessions`: `ck_subscriptions_total_sessions_range`
      becomes 5 to 140; `ck_subscriptions_duration_days_range` is replaced by
      `ck_subscriptions_duration_days_for_sessions`, whose `CASE` is written from `DurationTable`
- [x] **Web.** `PlanForm`: the sessions box first; the days box beside it fills itself in and is
      locked; `planDaysFor` mirrors the table for display only. `npm run gen:api`
- [x] Tests (domain): days at every boundary (5, 10, 11, 20, 21, 140), 4 and 141 refused, price
      unchanged; helpers moved from 12 sessions to 10 so they keep their 30-day dates
- [x] Tests (integration): assign and check-in-with-sale send only sessions and get the table's
      days; 141 refused on the field; days sent by an old client ignored; renew; both constraints
      by raw SQL. Raw-SQL rows in other tests moved from 30 days · 12 sessions to 30 · 10
- [x] Tests (frontend): the days box fills itself for every boundary and is disabled; only
      `sessionCount` is sent from the profile and from the locker; more than 140 refused

**Release step:** the server holds only test data; wipe it before this migration runs, as for
6.5.6, or the new constraint refuses the old rows. (Locally, `TRUNCATE subscriptions CASCADE`
was enough; members, users, lockers and prices were kept.)

Done when: the desk types ۱۲ sessions and sees «۴۵ روز» filled in and the price, cannot change
the days, and the member's plan reads «۱۲ جلسه - ۴۵ روزه» (sessions first, asked by the developer while
building; every screen, through `planLabel`).

**Released** from `0514e28` on 2026-09-29, together with everything since `7644b75` (the expenses
screen, released 2026-09-27 but not recorded here): 6.5.5 to 6.5.18 and four migrations. The
server's trial data was wiped first (README, *Deployment*), the wipe and all migrations were
rehearsed on a throwaway Postgres beforehand, and the local trial data was then restored onto the
server.

### 6.5.19 One member list, and a roomier locker box (فهرست اعضا با جستجو، کادر کمد اشغال)
Asked by the developer on 1405/07/07 (2026-09-29), from a screenshot of an occupied locker's box.
Web only; no API change. BUSINESS_RULES.md §6 and §7 updated.

- [x] **The member search screen is gone.** `/search`, its menu item and `MemberSearchPage` are
      removed; an old `/search` link lands on «اعضا». The member list has the same box above it:
      the search sits in the URL beside the status filter and the page (`/members?q=…&status=…`),
      a search nobody matches offers «ثبت این شخص» with the name or phone already typed, and
      someone inside can be checked out from their row
- [x] **Occupied locker box.** The sessions bar moves up beside the name, where the header was
      empty, with «N جلسه مانده» under it. «تاریخچه امروز این کمد» joins the buttons at the
      bottom (not for a reserve place), with «بازگشت» back to the visit
- [x] **The plan box.** «اعتبار تا» becomes «دوره اعتبار»: first and last day («… تا …») and the
      days left, red at the desk panel's 5-day renewal threshold. It takes the place of «جلسات
      باقی‌مانده», which the locker box drops (its header shows the sessions); after a check-in and
      before a check-out the sessions line stays, below the period
- [x] Tests (frontend): the search tests moved to `MembersPage.test.tsx` (plus search with a status
      filter, paging keeps the search, the old link); locker box: sessions in the header, the
      period and its days left, the history and back, none for a reserve place; the menu has no
      «جستجوی عضو»

Done when: «اعضا» finds a member by name or phone and registers one nobody matches, and an
occupied locker's box shows «۴ از ۱۲» beside the name, «۱۴۰۵/۰۶/۱۰ تا ۱۴۰۵/۰۷/۰۸ · ۵ روز مانده»
next to the plan, and opens the locker's day from its own button.

### 6.5.20 Debtors filter on the member list (فیلتر بدهکاران)
Asked by the developer on 1405/07/07 (2026-09-29). BUSINESS_RULES.md §2 updated. No migration.

- [x] **API.** `GET /api/members?debtorsOnly=true` lists only members who owe something. Debt is
      calculated, never stored (§5), so the filter is a condition in the query
      (`MemberDebt.OwesSomething`: some non-cancelled subscription, non-voided charge or
      non-cancelled cafe order whose price is above its net paid), applied before the count and
      the paging. It combines with `isActive` and the search
- [x] **Web.** A «بدهکار» toggle beside «همه / فعال / غیرفعال», kept in the URL as `debt=1`;
      pressing it again clears only it. An empty result says «عضو بدهکاری نیست.»
- [x] Tests (integration): partly paid and unpaid listed, fully paid, no subscription and a
      cancelled one left out; paging counts debtors only; with `isActive=false`; with a search;
      a هوازی charge alone lists the member and a voided one does not; an unpaid cafe order
      lists the member and a cancelled one does not
- [x] Tests (frontend): the toggle sends `DebtorsOnly` and writes `?debt=1`; with a status both are
      sent and pressing again keeps the status; the empty message

Done when: pressing «بدهکار» on «اعضا» leaves only the rows with an amount owed, the count above
the table counts only them, and «غیرفعال» + «بدهکار» lists inactive members who still owe.

### 6.5.21 One theme, light (یک تم، روشن)
Asked by the developer on 1405/07/08 (2026-09-30). BUSINESS_RULES.md §6 *The desk screen's look*
updated. Frontend only; undoes the dark half of 6.5.16.

- [x] The locker map no longer turns the app dark; `useDarkScreen` is deleted, so no screen does
- [x] The `.dark` tokens stay in `index.css`, unused, ready for Phase 13.2 if the whole app goes
      dark (the class would go on `<html>`, not on one screen)
- [x] Tests (frontend): the map leaves `<html>` without the `dark` class

Done when: the first page (the locker map) and its dialogs are light like every other screen.

### 6.5.22 A dark theme switch (دکمهٔ تم تیره)
Asked by the developer on 1405/07/09 (2026-10-01). BUSINESS_RULES.md §14 written first. Frontend
only, no new package. The choice is kept per device (localStorage), not per user, so no backend.

- [x] `lib/theme.ts`: read the saved theme (light when none, or storage blocked), apply it as the
      `dark` class on `<html>` and save it; `useTheme()` for the button
- [x] `ThemeToggle` in the header beside «خروج»: a moon / sun button named «تم تیره», `aria-pressed`
- [x] An inline script in `index.html` puts the saved theme on before the first paint (no CSP on
      the server to block it)
- [x] The Jalali calendar's own white and black pointed at the tokens under `.dark`, unlayered so
      they beat the library's runtime styles
- [x] Tests (frontend): first visit light; the button turns `<html>` dark and back and saves each;
      opened dark shows it pressed; saved theme read with nothing, dark, junk and blocked storage;
      blocked storage still switches the screen

Done when: pressing the moon turns every screen, its dialogs and the calendar dark; a reload keeps
it dark with no white flash; another browser still opens light.

### 6.5.23 A queued plan's dates read as provisional (تاریخ اشتراک در صف)
Asked by the developer on 1405/07/10 (2026-10-02). BUSINESS_RULES.md §4 updated. Frontend only;
the queueing rule itself was already right (checked: sessions running out pull the queued plan
forward at the next check-in, a freeze pushes it later).

- [x] `isQueuedBehindAnother`: upcoming, and a live membership on the same history page ends the
      day before it starts. Not found on the page → plain dates, still correct for today
- [x] History row: start «بعد از پلن قبلی» with «فعلاً <date>» under it, end «N روز از شروع»; the
      row's action panel says the same
- [x] Renew notice: «بعد از پایان پلن فعلی شروع می‌شود (فعلاً از …) و N روز اعتبار دارد»
- [x] «تاریخ فروش» column in the history: the subscription's `CreatedAt`, already stored and
      returned, recorded at sale whether or not anything was paid. Who sold it is not recorded;
      not needed for now (developer, same day)
- [x] Tests (frontend): queued behind a live plan; behind a cancelled one, a gap, a single visit or
      off the page → not queued; the profile row shows the new texts and no end date; an unpaid
      sale shows its sale time

Done when: a member with an active plan and a renewal sees the renewal's row read «بعد از پلن
قبلی» instead of a fixed start and end.

### 6.5.24 Birth date required (تاریخ تولد اجباری)
Asked by the developer on 1405/07/10 (2026-10-02), for the birthday SMS to come. BUSINESS_RULES.md
§2 updated (was optional since 2.4). No member existed anywhere yet, so nothing to backfill.

- [x] `Member.BirthDate` is `DateOnly` (not nullable); `Create`/`Update` take a `DateOnly`. The
      commands keep `DateOnly?` so a missing value is `Members.BirthDateRequired` (validator, and
      the handler again) instead of 0001-01-01; `MemberResponse.BirthDate` is not nullable
- [x] Migration `MakeMemberBirthDateRequired`: `birth_date SET NOT NULL`, check constraint without
      the `IS NULL` branch. EF's scaffolded `defaultValue: 0001-01-01` removed, so a row without a
      date makes the migration fail instead of getting an invented one
- [x] Form: label «تاریخ تولد» (no «اختیاری»), empty refused before sending with «تاریخ تولد را
      وارد کنید.», the same on edit; the check-in dialog's register form is the same form.
      `CurrentlyInsideResponse.MemberBirthDate` stays nullable: a guest has none
- [x] Tests: domain (a corrected date replaces the old one); integration (create without a date →
      400 `Members.BirthDateRequired`, clearing it on edit → 400 and the old date kept, a `NULL`
      inserted directly → `23502`); frontend (blank on create and on edit is refused before
      sending, both register-from-search flows type a date)

- [x] Typing in the date box, beside the calendar (same day, after the developer found the box
      "could not be filled"). Cause: `toIsoDate` accepted only `/` or `-`, so `13700512` or
      `1370.05.12` was taken while typing and silently emptied on blur. Now `withDateSlashes` adds
      the slashes to digits as they are typed (only while the text grows, so backspace still
      deletes one), `toIsoDate` takes any common separator (`.`, space, comma, `÷`, `٫`, `،`,
      backslash), and the box asks phones for the number pad (`inputMode="numeric"`)
- [x] Tests (frontend): every separator parses; the slash mask step by step, and what it leaves
      alone; digits typed one by one end up `۱۳۷۰/۰۵/۱۲` and commit; backspace over a slash does
      not put it back; a date typed with dots survives blur

Done when: no member can be registered or saved without a birth date, by the form, the API or the
database.
Staff can also type a birth date by hand, with or without slashes, instead of picking it.

### 6.5.25 The gym's history (تاریخچه)
Asked by the developer on 1405/07/10 (2026-10-02). Built the same day on its own branch
(`task/6.5.25-gym-history`); the guest rows followed once 6.5.11 was merged. Each member's own history is
already on the profile, and the cafe already has its order history (`/cafe/orders`, 7.3). What is
missing is the history of the whole gym: who came in, what was paid, what هوازی was sold.
This page is a list of rows, not totals or charts. Those belong to Phase 9.

Decided with the developer, same day. Write these rules into BUSINESS_RULES.md first, in §12 and
the §1 *Permissions* table:
- One «تاریخچه» page with three sections: ورود و خروج، پرداخت‌ها، هوازی. The cafe keeps its own
  page and is linked from here
- **Who recorded it** is shown on every row: who took the payment (`Payment.ReceivedByUserId`),
  who recorded the هوازی (`ServiceCharge.RecordedByUserId`), who checked the member in
  (`CreatedBy`). The nightly auto-checkout has no user and shows as «خودکار»
- **Staff and payments:** Staff see only today and the 3 days before it (today is 1405/07/10 →
  07/07 to 07/10), in the gym's time zone. The API enforces it: a range from Staff that reaches
  earlier is refused with a stable error code. The Owner has no limit
- **Attendance and هوازی:** no date limit for either role
- **Filters:** a Jalali date range and a member search in every section. Payments also filter by
  method and by source (subscription, هوازی, cafe)
- **Cancelled, voided, refunds:** listed and marked, never hidden, as the cafe's order history does
- **Guests** (ورود مهمان) are listed in the attendance section, marked «مهمان», with no profile
  link. They still count nowhere in the Phase 9 reports

Tasks:
- [x] BUSINESS_RULES.md §12 *History* and three rows of the §1 *Permissions* table, written first.
      Defaults Claude chose are marked there "pending review": every section opens on today, the
      member filter is a member chosen by name or mobile, 20 rows a page
- [x] API (`Application/History/`, `Api/Endpoints/HistoryEndpoints.cs`): `GET /api/attendance`,
      `GET /api/payments`, `GET /api/service-charges`, each with `from`/`to`, `memberId`, paging,
      newest first, and the recorder's full name. Payments also take `method` and `source`
      (`PaymentTargetKind`). Who recorded it comes from the new `IUserNames` (Infrastructure reads
      `users.full_name` for the whole page in one query, ADR 0002)
- [x] Which day a row belongs to: a check-in by `CheckedInAt`, a payment by `PaidAt` (both turned
      into a moment range with `IGymCalendar.StartOfDayUtc`), a هوازی by `ChargedOn`
- [x] Marked, never hidden: cancelled and auto-closed visits; a refund (`Kind`, `Reason`); a payment
      whose item was later cancelled or voided (`TargetUndone`); a voided هوازی with its reason and
      who voided it
- [x] Staff's 3-day limit on payments: `PaymentHistoryWindow` (Domain) against `IGymCalendar.Today()`,
      the role from the new `ICurrentUser.IsOwner`. A range starting earlier, or with no start, is
      403 `Payments.HistoryTooFarBack`
- [x] Indexes: `attendances.checked_in_at` already existed (6.5.14); migration `AddHistoryIndexes`
      adds `ix_payments_paid_at` and `ix_service_charges_charged_on`. Indexes only, no row is touched
- [x] Web: `/history` («تاریخچه» in the menu, both roles), three tabs, filters in the URL (`tab`,
      `from`, `to`, `member`, `method`, `source`, `page`). A date missing from the URL is today; one
      present but empty was cleared and is no bound. Staff on payments are told the window, and a
      range outside it is refused under the date box without asking the API. Member names link to
      the profile; the cafe's order history is linked from the header
- [x] Tests (domain): the window for Staff and the Owner, and no start for Staff
- [x] Tests (integration): every filter; cancelled, auto-closed, voided and refunded rows marked;
      Staff asking for 4 days ago and with no start refused, 3 days ago allowed, the Owner allowed
      both; the recorded-by and voided-by names; 401 without signing in
- [x] Tests (frontend): each section, the filters in the URL and sent, a cleared date, a backwards
      range, Staff's limited range, switching tabs keeps the filters, choosing a member
- [x] Guest rows, after merging 6.5.11 into the branch: `HistoryAttendanceResponse` has a nullable
      `MemberId` and the `GuestName`; a payment for a guest's cafe order carries the guest's name
      from the order's visit. `WhoCell` shows a member link, a guest's name marked «مهمان», or
      «مشتری آزاد». Tests: both rows, in the API and on the page

Done when: the Owner can see every check-in, payment and هوازی of any past day with who recorded
it, and Staff can see payments from the last 3 days and every check-in and هوازی.
Closed 2026-10-03, with the guest rows, after merging 6.5.11: 1363 backend tests and 736 frontend
tests green, zero warnings (the build's chunk-size note predates this task).

### 6.5.26 A settlement's rows together in the payment history (ردیف‌های تسویه یکجا کنار هم)
Asked by the developer on 1405/07/11 (2026-10-03), from a screenshot: three payments made by one
bank transfer read as three unrelated rows. Reviews the open point left by 7.5 (no record ties the
rows of one settlement together). BUSINESS_RULES.md §5 *Settling several items at once*.

Decided with the developer, same day: a `SettlementId` column rather than grouping on the shared
moment at read time; a heading row with the total and the items one step in under it; in both
payment histories.
- [x] Domain: `Payment.SettlementId` and `JoinSettlement`, which refuses a refund, an empty id and a
      second settlement (a bug if hit, so it throws)
- [x] Both settle handlers (member debt, a guest's cafe) give every row of one handover one id
- [x] Migration `AddPaymentSettlementId`: the nullable column, a partial index on it, the check
      constraint `ck_payments_settlement_payment_only`, and a backfill for the settlements already
      written (payments sharing moment, method and staff member, two or more). The backfill's ids
      come from a `MATERIALIZED` CTE: as a plain subquery Postgres re-ran `gen_random_uuid()` per
      joined row and gave every row its own id, which the first local run showed
- [x] `GET /api/payments` and `GET /api/members/{id}/payments`: each row carries `Settlement`
      (id, the whole handover's total and item count), whatever the filters let through
- [x] Web: `groupBySettlement` gathers neighbouring rows of one settlement; `PaymentLogTable` and
      the profile's `PaymentHistoryTable` show a heading row and the items one step in
- [x] Tests: `JoinSettlement` (domain); one id per handover and a different one per settlement,
      the constraint, the guest's settlement, both histories with and without a filter
      (integration); the grouping and both tables (frontend)

Closed 2026-10-03: 1372 backend tests and 741 frontend tests green, zero warnings. Checked on a
local copy before release: the backfill turned the 4 settlements already written into 4 ids and
left the 5 payments taken on their own without one.

### 6.5.27 Cardio-only visit (ورود فقط هوازی)
Asked by the developer on 1405/07/11 (2026-10-03): a member with a plan comes in only for the
treadmill. They hold a locker and may use everything, but no session is consumed, and the locker
is yellow. BUSINESS_RULES.md §7 *Cardio-only visit*, with §1, §6 and the check-out and
auto-checkout lines.

Decided with the developer, same day: the member must hold an `Active` (or frozen) membership;
check-out is refused until a هوازی amount is recorded (paid or left as debt), and the nightly job
leaves such a visit open while it has none; counted as attendance; both roles; no minutes field.
The visit keeps the plan's id, so the board reads the plan as usual, and a new
`is_cardio_only` column says no session came from it.
- [x] Domain: `Attendance.IsCardioOnly`, `CheckInCardioOnly` / `...OnReservePlace`, check-out refused
      without a cardio charge (`Attendance.CardioChargeMissing`)
- [x] Migration `AddAttendanceCardioOnly`: the column, default `false` for every existing row
- [x] `POST /api/members/{id}/attendance/cardio-only-check-in`: the plan rule, the place rule, no sale, no
      unfreeze, no queue move
- [x] Check-out and auto-checkout read the visit's cardio charge; cancel check-in gives no session back
- [x] Responses carry `IsCardioOnly`: the visit, the locker map, the board, the locker's today
      history, the gym's history
- [x] Web: the second button in the check-in box, the yellow locker and its legend line, the
      disabled check-out with its reason, «فقط هوازی» marks on every list
- [x] Tests: domain, integration (each rule above and the new check constraint), frontend

Closed 2026-10-03: 1408 backend tests and 749 frontend tests green, zero warnings. Not released
yet: the migration only adds a column (default `false`) and a check every existing row passes.

### 6.5.28 Miscellaneous sale from the locker (فروش متفرقه)
Asked by the developer on 1405/07/11 (2026-10-03), from a screenshot of the locker box: a third
button beside «مبلغ هوازی» and «خرید بوفه» for something sold that the system does not know, with
its name, quantity, price and how it was paid. BUSINESS_RULES.md §7 *Miscellaneous sale*.

Decided with the developer, same day: card, transfer, cash or «به حساب عضو»; members only; any
number per visit; its own source («متفرقه») in the debt, histories and reports. Built as a second
`ServiceChargeKind` rather than a new table, so payments, debt, «تسویه یکجا», voids and the
histories needed no fourth payment target.
- [x] Domain: `ServiceChargeKind.Miscellaneous`, `Description` / `Quantity` / `UnitPrice`,
      `ServiceCharge.RecordMiscellaneous`; `Record` refuses the kind and `ChangeAmount` refuses a sale
      (`ServiceCharges.MiscellaneousNotEditable`)
- [x] Migration `AddMiscellaneousSale`: three nullable columns, the check constraint
      `ck_service_charges_miscellaneous`, and the one-per-visit index narrowed to `kind = 'Cardio'`.
      Every existing row is هوازی with the new columns null, so all of them pass
- [x] `POST /api/attendance/{id}/service-charges/miscellaneous`: the sale and, when a method is
      given, its full payment in one save
- [x] Cancel check-in takes `MiscellaneousSaleIds`, ticked one by one like the cafe orders
      (`Attendance.MiscellaneousSaleNotOnVisit`)
- [x] Responses: the sale's fields on the charge, the debt item (`Sale`), both payment histories
      (`ServiceDescription`) and the gym's service-charge history; `GET /api/payments` takes
      `ServiceKind` to tell هوازی and متفرقه apart
- [x] Web: «فروش متفرقه» in the locker box with its form (name, quantity, `MoneyField` unit price,
      total, payment choice, the money confirmation) and its list (pay, void); labels «متفرقه: …» in
      the debt and both payment histories; the history's «بابت» filter and its «هوازی و متفرقه» tab;
      a tick per sale in the cancel box
- [x] Tests: domain, integration (recording each way, each refusal, the constraint, void with
      refund, cancel check-in, settlement order, history filter, the cardio-only visit), frontend

Closed 2026-10-03: 1444 backend tests (457 domain, 987 integration) and 755 frontend tests green,
zero warnings. Not released yet: the migration adds three nullable columns and a check that every
existing row (all هوازی) passes, and narrows an index filter.

### 6.5.29 Purchase tiles in the locker box, «فروشگاه» and «آنالیز»
Asked by the developer on 1405/07/11 (2026-10-03), from a screenshot of the locker box: drop the
headings «هوازی / بوفه / متفرقه» above the buttons, keep one icon-and-name tile per purchase, each
with a background of its own; rename «مبلغ هوازی» → «هوازی», «خرید بوفه» → «بوفه», «فروش متفرقه»
→ «فروشگاه» (everywhere, decided with the developer); and add «آنالیز» with the rules of
«فروشگاه». Corrected by the developer on 1405/07/12, before release: a sale takes no money when it
is recorded (it is debt, paid afterwards), آنالیز is only a price, and فروشگاه takes several items
with ▲/▼ for the quantity. BUSINESS_RULES.md §7 *Sale at the desk*.
- [x] Domain: `ServiceChargeKind.Analysis`, a single amount through `ServiceCharge.Record`;
      `RecordMiscellaneous` → `RecordShopItem`; `ServiceCharge.IsSaleKind` / `IsSale` (never edited,
      any number per visit); `ServiceCharges.SaleNotEditable`, `Attendance.SaleNotOnVisit`,
      `ServiceCharges.ShopItemsRequired`, `ServiceCharges.TooManyShopItems`
- [x] No migration: آنالیز has no name, quantity or price, so `ck_service_charges_miscellaneous`
      already describes it and the one-per-visit index already covers only هوازی
- [x] API: آنالیز through `POST /api/attendance/{id}/service-charges` (kind `Analysis`);
      `POST …/service-charges/miscellaneous` → `POST …/service-charges/shop` with `Items`, no
      payment, all items in one save; cancel check-in's `MiscellaneousSaleIds` → `SaleIds`
- [x] Web: `PurchaseTile` (icon, name, total and status; `--tile-*` colours in light and dark);
      four tiles in the member's locker box, the cafe tile in the guest's; `SaleBox` for both kinds,
      `ShopSaleForm` (lines, ▲/▼ quantity, «افزودن کالای دیگر», no payment) and the هوازی amount form
      for آنالیز; «فروشگاه» and «آنالیز» in labels, the «بابت» filter, the history tab and the cancel
      box's ticks; `zodResolver` nests errors by path so a list of lines can show its own errors
- [x] Tests: domain (shop items, آنالیز as an amount, neither editable), integration (one and two
      shop items, one bad item saves nothing, آنالیز, the constraint, cancelling both, the payment
      filter for each kind), frontend (tiles, the stepper, adding and removing lines, posting with
      no payment, آنالیز's single field, each tile's own total, cancel ticks, the history filter)

Closed 2026-10-04: 1454 backend tests (459 domain, 995 integration) and 760 frontend tests green,
production build clean, zero warnings. Not released yet; no migration.

### 6.5.30 Sales in the history (فروش‌ها در تاریخچه)
Asked by the developer on 1405/07/12 (2026-10-04): make the history page more useful for the
Owner. Each kind of sale on its own (هوازی، فروشگاه، آنالیز، plans single-session or long-term), all
of them together, and one button to split paid from unpaid. Rows only: the income section the
Owner will get later is Phase 9. BUSINESS_RULES.md §12 *Sales in the history* and a §1 row.

Decided with the developer, same day: cafe orders included, with their own section; a partly paid
sale is «پرداخت نشده»; Owner only; no totals.
- [x] BUSINESS_RULES.md §12 *Sales in the history* and the §1 *Permissions* row, written first
- [x] API (`Application/History/ListSales/`): `GET /api/sales?from&to&memberId&source&paid&page`,
      Owner policy. `SaleSource` (`Subscription`, `Cardio`, `Miscellaneous`, `Analysis`,
      `CafeOrder`) and `SalePaidFilter` (`Paid`, `Unpaid`). One `UNION ALL` of five branches
      (plans, the three charge kinds, cafe orders), each with its net paid as a correlated sum, so
      the paid filter, the count and the paging all run in the database. What each row says is read
      afterwards for the page only. A plan by its `CreatedAt`, a charge by `ChargedOn`, a cafe order
      by `OrderedOn`; cancelled and voided rows only when no paid filter is chosen
- [x] Migration `AddSubscriptionCreatedAtIndex`: one index, no row touched
- [x] Web: the Owner's tabs ورود و خروج، پرداخت‌ها، همهٔ فروش‌ها، فروش پلن، هوازی، فروشگاه، آنالیز،
      بوفه (the combined «هوازی، فروشگاه و آنالیز» stays for Staff only); «وضعیت پرداخت» همه /
      پرداخت شده / پرداخت نشده in the URL (`paid`); `SalesLogTable` with the plan, the shop item ×
      quantity or the cafe order's lines, payment status, who recorded it, and cancelled/voided
      marks with the reason
- [x] Tests: integration (all five kinds in one list, each source, paid/unpaid with partial, free
      and cancelled plans, a voided charge in neither, each kind's own day, member, guest cafe
      order, Staff 403, bad filters 400, 401); frontend (both roles' tabs, Staff never asks, every
      row type, each tab's source, the paid button in the URL and the request)

Closed 2026-10-04: 1466 backend tests (459 domain, 1007 integration) and 765 frontend tests green,
lint and production build clean, zero warnings. Not released yet: the migration adds one index and
touches no row.

### 6.5.31 Services for guests, and the guest debt list (خدمات مهمان، بدهی مهمان‌ها)
Decided by the developer on 1405/07/12 (2026-10-04): a guest may use every service, under the
cafe's guest rule. هوازی is no longer free for a guest: the desk types its price, or records none.
No guest account; what midnight leaves unpaid goes to one list, «بدهی مهمان‌ها».
BUSINESS_RULES.md §7 *Guest visit*, *Gym services*, *Sale at the desk*, §6, §12 and the §1 row.
- [x] BUSINESS_RULES.md, written first
- [x] Domain: `ServiceCharge.MemberId` nullable (`Record` / `RecordShopItem` take `null` on a guest's
      visit); `Attendance.CheckOut`/`Cancel` refuse on any unpaid purchase,
      `Attendance.GuestHasUnpaidPurchases` replaces `Attendance.GuestHasUnpaidCafe`;
      `ServiceCharges.GuestVisit` removed
- [x] Migration `AllowGuestServiceCharges`: `service_charges.member_id` nullable, no row touched
- [x] Application: هوازی and sales on a guest visit (`GuestVisitLock`: lock, then ask "still
      open?"); a guest charge's payment, change and void lock the visit instead of a member
      (`ServiceChargeLock`); `GuestPurchases` (cafe + charges) behind check-out, cancel, the
      locker's «بدهکار» and «تسویه یکجا» (`SettleGuestVisit`); guest names on charges in the
      history; `GET /api/guest-debts` (Staff or Owner, paged)
- [x] Web: the four tiles in a guest's box, «تسویه یکجا» over everything, cancel check-in's guest
      check, the «بدهی مهمان‌ها» page and menu item, «مهمان» on guest charges in the history; the
      service-charge mutations also refresh the map and the debt list
- [x] Tests: domain (a guest's charge has no member), integration (`GuestPurchasesEndpointTests`,
      `GuestDebtsEndpointTests`, the three old "guest refused" tests now succeed), frontend (the guest
      box's tiles and full settle, cancel with a guest's هوازی, the debt list, the menu, the history)

Closed 2026-10-04: 1514 backend tests (462 domain, 1052 integration) and 780 frontend tests green,
lint and production build clean, zero warnings. Not released yet: the migration only drops NOT NULL
on `service_charges.member_id` and touches no row.

### 6.5.32 Totals in the history (جمع در تاریخچه)
Asked by the developer on 1405/07/12 (2026-10-04): every history section where money changes hands
ends with what it comes to, for everything the filters let through. BUSINESS_RULES.md §12 *Totals
in the history* and a §1 row; the 6.5.30 "no totals" is lifted for these figures only.

Decided with the developer, same day: the sales sections show «مبلغ»، «دریافتی»، «مانده»;
cancelled and voided sales count toward none; «پرداخت‌ها» shows «دریافتی»، «بازگشت»، «خالص»;
Owner only.
- [x] BUSINESS_RULES.md §12 *Totals in the history* and the §1 row, written first
- [x] API: `GET /api/sales/totals` and `GET /api/payments/totals`, Owner policy, the list's filters
      without the page. Each list and its totals share one filtered query (`SalesQuery`,
      `PaymentsQuery`), so the two can never disagree on which rows count; the sums run in the
      database
- [x] Web: `HistoryTotals` under the table of every sales section and of «پرداخت‌ها», Owner only
- [x] Tests: integration (each figure, partial and free plans, cancelled and voided left out, each
      filter, refunds, Staff 403, bad filters 400); frontend (Owner sees them, Staff never ask)

Closed 2026-10-04: 1514 backend tests (462 domain, 1052 integration) and 780 frontend tests green,
lint and production build clean, zero warnings. No migration: nothing to release beyond the code.

---

## Phase 7 — Cafe / POS

Rule change, decided by the Owner on 2026-09-25 as this phase started: **the gym does not count
stock.** The tasks below used to carry a StockMovement ledger, a `StockQuantity` column and a
"not enough left" refusal; all of it is gone (BUSINESS_RULES.md §8). The cafe is a price list and
a till. What the gym spends on restocking is an expense in Phase 8, and gross profit is worked out
in the Phase 9 reports — the same money is never counted twice.

### 7.1 Products and categories
- [x] `ProductCategory`: name (unique, normalized), renameable, switched on and off, deletable
      only while no product uses it
- [x] `Product`: name (unique across the cafe, normalized), category, price, `IsActive`.
      Switched off, never deleted, because orders point at it
- [x] Price follows the `Plan.Price` money rules exactly: `numeric(18,2)`, at most 2 decimals,
      refused rather than rounded
- [x] Endpoints: the whole cafe is readable and writable by both roles (§1, changed with this
      phase). One explicit policy per endpoint, never a group default
- [x] Sellable = switched on *and* in a category that is switched on, so the till and the product
      search on an order never offer a `ناموجود` item. `GET /products?isActive=true` asks
      that question; the management screens ask for everything and see both flags
- [x] Tests: staff can do everything in the cafe; a duplicate name is 409; a category with
      products cannot be deleted; a stale `Version` is refused; switching a category off takes
      its products out of the sellable list

Done when: the front desk can shape the menu and the price list without the Owner, and nothing
`ناموجود` can be rung up. Closed 2026-09-26; the cafe endpoint tests and the domain tests
are green.

### 7.2 Orders
- [x] CafeOrder and CafeOrderItem with `ProductName` and `UnitPrice` snapshots. An order is never
      edited: a mistake is cancelled with a reason and rung up again (§5, §8), so there is no
      method and no endpoint to change a line
- [x] Transactional create order: order, items, and the payment when it is paid there and then
- [x] Optional member link
- [x] Order on a member's account: created unpaid or part-paid under that member's name, settled
      later with ordinary payments through `POST /api/cafe/orders/{id}/payments`, counted in their
      debt (BUSINESS_RULES.md §8, §5 *Member debt*). A walk-in order with no member is paid in
      full at creation
- [x] `Payment.CafeOrderId` finally set: the third target has existed since task 4.4 and nothing
      wrote it until now. `PaymentTargetKind`, `PaymentLedger` and both `MemberDebt` paths gained
      the cafe alongside subscriptions and service charges
- [x] A product that is not sellable — its own switch or its category's — is refused with
      `Products.Inactive`. Which of the two switches said so is not the till's problem
- [x] Tests: price snapshot unchanged after product edit; an unpaid order on account appears in
      the member's debt breakdown and on the member list's row; a walk-in order must be paid in
      full; instalments add up to Paid; four parallel payments leave exactly one

Done when: the front desk can ring up a sale, on the counter or on a member's account, and the
money lands where §5 says it should. Closed 2026-09-26: 968 backend tests and 403 frontend tests
green, zero warnings. Decided while building and needing review: an inactive member may still buy
from the cafe (§2 only stops check-in and new subscriptions — they are buying water, not being let
in), and two lines of the same product are refused rather than merged, because a receipt reading
as two purchases of one thing is a till mistake worth catching at the till.

### 7.3 Cancellation and history
- [x] Cancel order (Staff or Owner — §8, decided by the Owner 1405/07/03; this line used to say
      Owner): `POST /api/cafe/orders/{id}/cancel`, reason required, a refund per payment method
      still in credit for whatever was paid, written in the same transaction
- [x] A payment asks "is the order cancelled?" again under the member lock, so a cancellation that
      lands while a payment waits is seen and the payment refused
- [x] Order history `GET /api/cafe/orders?memberId&from&to` and member purchase history
      `GET /api/members/{memberId}/cafe-orders`, newest first, cancelled orders included and marked
- [x] A member's payment history now includes cafe payments and refunds (7.2 had left them out)
- [x] Tests: unpaid on account leaves the debt with no refund; paid walk-in refunded in full; cash
      and card refunded separately; cancelling twice is 422; four parallel cancels refund once;
      cancel racing a payment never leaves money on a cancelled order; history filters by member
      and inclusive date range

Done when: a sale rung up by mistake can be taken back at the desk with the money returned the way
it came, and every order a member ever placed can be listed. Closed 2026-09-26: 987 backend tests
and 424 frontend tests green, zero warnings. Found while building and left for its own task:
`RegisterServiceChargePaymentHandler` checks "is the charge voided?" before it takes the member
lock, the same gap this task closed for cafe orders.

### 7.4 UI: cafe
- [x] POS screen (`/cafe`): product grid of sellable items only, grouped by category, with a name
      filter; a cart where a second tap raises the quantity rather than adding a line; the
      customer is a walk-in or a member found by name or phone (`?member=` in the URL, so the
      member profile's "خرید از بوفه" opens the till with them chosen). A walk-in pays the whole
      total; a member pays all, part or none of it, and the rest goes on their account
- [x] Products and categories on one screen (`/cafe/menu`), both roles: add, rename, موجود /
      ناموجود, delete a category, edit a product with its `Version`. A product that is switched on
      in a switched-off category says why the till will not offer it
- [x] Order history (`/cafe/orders`) with a Jalali from/to range in the URL; member profile
      purchases tab. Rows offer only what the API would accept: a payment while something is owed
      on a live order (settling an order on account — decided with the developer, since 7.2 built
      the endpoint and no screen reached it) and a cancellation with a required reason, warning
      that whatever was paid goes back the way it came
- [x] Tests: the till asks only for sellable products; a walk-in sends the whole total; a member
      with the amount cleared sends no payment; more than the total is refused before sending;
      a backwards date range asks the API nothing; a cancelled order offers no actions

Done when: the front desk can ring up, settle, cancel and look back over cafe sales, and shape the
menu, without leaving the browser. Closed 2026-09-26: 987 backend tests and 489 frontend tests
green, zero warnings (the build's chunk-size note predates this task, see LEARNING 2.4). Left for
its own task: the payment form is now copied three times (subscription, هوازی charge, cafe order)
with only the endpoint differing — the point at which `lib/money.ts` was extracted for the same
reason.

#### 7.4 follow-up — the cafe on the "currently inside" board (asked by the developer, 1405/07/04)
- [x] A بوفه column beside هوازی: purchases go on the member's account, tied to the open visit
      (`CafeOrder.AttendanceId`: nullable, foreign key, check constraint), in a dialog so the row
      stays one line
- [x] Check-out lists what the visit bought, and the debt by source: plan, هوازی, cafe
- [x] `GET /api/cafe/orders?attendanceId=`; the board's rows carry their visit's standing orders,
      batched per page like the service charges
- [x] A failure with an empty body (an API older than the route answers 405) is no longer shown as
      "the server could not be reached"
- [x] A page that fails while rendering shows a Persian message inside the frame (`PageError`)
      instead of React Router's screen replacing the whole app; the board reads a row without
      `cafeOrders` (an older API) as "bought nothing"
- [x] Tests: the visit must be open and the member's own; the board shows standing orders only;
      the list filters by visit; the board sends `attendanceId` and no payment; check-out shows
      the purchases and the three sources

Closed 2026-09-26: 995 backend tests and 500 frontend tests green, zero warnings.

#### 7.4 follow-up — saying "saved" at the desk (asked by the developer, 1405/07/04)
- [x] Adding a cafe purchase on the board ends on a success step listing what the server saved:
      each line with quantity and price, and the total (`DialogSuccess`, the same shape check-in
      and check-out already used)
- [x] Every هوازی form (record, change amount, payment, void) ends on a success step too; one
      dialog serves every state of the charge, so the step survives the refetch behind it
- [x] Shorter board buttons: «خرید بوفه» and «مبلغ هوازی» (the word «افزودن» dropped)
- [x] Tests: the cafe step lists the saved lines and closes; the هوازی step shows the amount and
      survives the charge appearing behind it; a void says so

The "unexpected error" on the order history and in the check-out box was not a code bug: an API
started before those routes existed was still holding ports 5134/7134, which also made every new
`dotnet run` fail with "address already in use". Closed 2026-09-26: 501 frontend tests green,
lint and type check clean; no backend change.

### 7.5 Settle a member's debt in one step (تسویه یکجا) (asked by the developer, 1405/07/04)
A single visit, هوازی and a cafe purchase are three items, each with its own payment form.
BUSINESS_RULES.md §5 *Settling several items at once*.
- [x] `SettlementAllocator` (Domain): spreads one amount over the ticked items, cafe → هوازی →
      subscription, oldest first within a kind; refuses more than their total
- [x] `POST /api/members/{memberId}/settlements`, both roles: under the member lock, re-reads the
      debt, refuses with `Settlements.DebtChanged` if any ticked item's outstanding differs from
      what the desk sent, then one `Payment` per item in one transaction
- [x] «تسویه یکجا» in the check-out box and on the member profile's debt card: items ticked, the
      amount filled with their total, one method, a success step listing what each item received
- [x] Tests: allocation order and partial amounts (domain); full and partial settlement, a stale
      item, an unknown or foreign item, overpayment, and two settlements racing (integration); the
      form's ticks, amount and success step (frontend)

Closed 2026-09-26: 1017 backend tests and 510 frontend tests green, zero warnings (the build's
chunk-size note predates this task). Two choices are Claude's and wait for review
(BUSINESS_RULES.md §5): oldest first within one kind, and no `SettlementId` tying the rows of one
settlement together. The second was reviewed and reversed in 6.5.26.

---

## Phase 8 — Expenses

### 8.1 Expenses API
- [x] ExpenseCategory table with seed data (Persian display names): eight rows written by the
      migration (`HasData`, fixed ids), added to and renamed by the Owner, never deleted or
      switched off (BUSINESS_RULES.md §9, details decided with the developer 1405/07/05)
- [x] Expense entity; register, edit, void (Owner): `POST /api/expenses`, `PUT /api/expenses/{id}`
      with `Version`, `POST /api/expenses/{id}/void` with a required reason. Not dated after the
      gym's today; a voided expense is final
- [x] `GET /api/expenses?from&to&categoryId&includeVoided`, newest first, with `TotalAmount` over
      every matching row, voided ones left out; `GET /api/expenses/{id}`; categories at
      `/api/expenses/categories`. Owner only, reading included
- [x] Tests: voided expenses excluded from totals; the total covers every page; inclusive date
      range; staff refused everywhere; stale `Version`; editing or voiding a voided expense is 422;
      four parallel voids leave one; an edit is audited; the migration seeds the eight categories

Done when: the Owner can write down, correct and void what the gym spends, and ask what went out
in a date range. Closed 2026-09-27: 1183 backend tests and 550 frontend tests green, zero
warnings. The test fixture now restores migration-seeded rows after each Respawn reset
(docs/ARCHITECTURE.md). Chosen by Claude and open to review: voided expenses are listed by default
(marked), as the cafe's order history does; the total never counts them.

### 8.2 UI: expenses
- [x] `/expenses`, Owner only (route behind `RequireRole`, nav item hidden from staff): list
      newest first with a Jalali from/to range and a category filter, all in the URL; voided rows
      marked with when and why, and offering no actions
- [x] The API's `TotalAmount` above the table: every page the filter matches, voided ones left
      out, and the page says so
- [x] Record and edit through one form (`MoneyField`, category, Jalali date defaulting to today,
      description, optional reference); edit sends its `Version`. Void with a required reason,
      warning that a voided expense is final
- [x] Categories on the same page (decided with the developer 1405/07/05, since 8.1 built the
      endpoints and no screen reached them): add and rename with `Version`; no delete, no switch
- [x] Tests: staff see no menu item and no list; the total and voided rows; filters sent as
      `From`/`To`/`CategoryId`; a backwards range asks the API nothing; a new expense sends decimal
      text and today; a future date, zero amount and missing description are refused before
      sending; an edit sends its version and shows a concurrent change; void needs a reason; a
      taken category name lands under the box; a rename sends its version

Done when: the Owner can write down, correct, void and look back over what the gym spends, and
shape the category list, without leaving the browser. Closed 2026-09-27: 1183 backend tests and 576
frontend tests green, zero warnings (the build's chunk-size note predates this task). `dateFromParams` moved from the cafe's order history to `lib/searchParams.ts` on its
second caller.

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

### 11.4 Opening hours: no check-in while the gym is closed (PENDING)
Rule decided on 2026-09-26 (BUSINESS_RULES.md §0, §7 *Opening hours*), deliberately left until here
because the developer builds and tests at night. Do not start it earlier unless asked.
- [ ] `Gym:OpeningTime` = 06:00 setting next to `Gym:ClosingTime`
- [ ] Check-in between `Gym:ClosingTime` and `Gym:OpeningTime` (gym time zone, window crosses midnight) is refused with a stable error code, mapped to a Persian message
- [ ] Decide with the developer before coding: how local development at night stays unblocked (no Owner override: §7 *Opening hours*)
- [ ] Tests: domain tests for the window edges (23:59, 00:00, 05:59, 06:00) with `FakeTimeProvider`; an integration test for the refusal

Done when: a check-in at 00:30 Tehran time is refused, one at 06:00 succeeds, and the front desk shows the Persian message.

### 11.5 Password policy after NIST (asked by the developer, 1405/07/04)
The old rule (8 characters, a letter and a digit) was too weak for a panel on the public internet.
BUSINESS_RULES.md §1 *Password policy*. Done on branch `task/11.5-password-policy-and-lockout`.
- [x] `PasswordPolicy` moves to Domain: 12–128 characters, English only, no user name, no
      repetition or keyboard run, not on the blocklist; no composition rules
- [x] Blocklist: the SecLists 100,000 most common passwords plus the gym's own words, as an
      embedded resource (`CommonPasswords.txt`)
- [x] `PasswordPolicyValidator` replaces `LetterAndDigitPasswordValidator`; Identity refusals keep
      the policy's error code
- [x] A login whose password fails the current policy sets `MustChangePassword`
- [x] UI: live checklist under every new-password field, show/hide button, Persian-keyboard warning
- [x] Tests: every rule (domain and frontend); the old-policy login, the codes from change password
      and create staff (integration); the checklist, the server's blocklist error, the warning and
      the eye button (frontend)

Done when: a password like `Football2024!` is refused with a Persian message under the field, and
a user whose password predates the policy is sent to change it at the next login.

### 11.6 Lockout that an attacker cannot turn against the gym (asked by the developer, 1405/07/04)
Anyone who knows a user name can keep that account locked by sending five wrong passwords every
15 minutes. BUSINESS_RULES.md §1 *Lockout*, docs/adr/0004-password-policy-and-lockout.md.
- [x] Trusted devices: a browser that logged in successfully gets a device cookie; failed attempts
      from unknown devices lock only unknown devices, and a trusted device has its own count
- [x] Owner: «باز کردن قفل» for a locked staff account, without a password reset
- [x] Server console: `./server.sh unlock <user>`, `./server.sh set-password <user>`,
      `./server.sh rename <old> <new>` (the Owner's user name is `Owner`, easy to guess)
- [x] Guessable user names (admin, owner, manager, …) refused for new accounts and renames
- [x] Tests: an attacker's lockout leaves a trusted device working; a trusted device locks only
      itself; a planted cookie is never trusted; change password and reset forget devices; unlock
      (integration); the console commands in process; the unlock button (frontend)
- [x] Released as 20260927-0310-9eb8f63 (2026-09-27); the Owner's user name was changed on the
      server with `./server.sh rename`, which also proved the console commands work in production

Done when: five wrong passwords from a browser without the device cookie lock the account for
unknown browsers only, the front-desk PC still logs in, and `./server.sh unlock` opens both doors.

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
- [x] Decide whether the whole app gets a dark theme: yes, as a switch in the header, light by
      default (6.5.22)
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

---

## Future — Group visits (not in MVP)
One person arrives with ten friends and does not want to hand over ten phone numbers. Recorded here
rather than in 6.5.3 because the current model blocks it twice over: a subscription belongs to exactly
one member, and a member holds one open visit at a time
(`ux_attendances_one_open_per_member`). So one buyer cannot hold ten visits, and ten members would
need ten unique Iranian mobile numbers (BUSINESS_RULES.md §2). Postponed by the Owner on 2026-09-25 —
until then each visitor buys their own single visit under their own name.

- [ ] A group ticket: one sale of quantity N to the buyer, and N visits carrying only a sequence
      number ("مهمان ۲ از علی رضایی") and a locker
- [ ] A visit with no subscription of its own — `Attendance.SubscriptionId` is required today
- [ ] The one-open-visit-per-member index becomes conditional on guest visits
- [ ] Check the whole group out in one action. With no names, the locker number is the desk's only
      handle, so a friend who leaves without checking out has to be findable by locker

