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

Release order (decided by the developer on 1405/07/26, 2026-10-18):
1. **Done 2026-10-20:** the SMS release, 10.6 to 10.15, from `main`, as one complete version
   (`20261020-0246-099e551`, 10.6), Kavenegar live with every kind off until the Owner turns them on.
   **Done 2026-10-23:** 10.16 (sending again what Kavenegar cancelled), 10.17 (three changes to
   the SMS settings) and 10.18 (new wording for the absence, welcome and birthday SMS), as one
   small release from `main` (`20261023-2305-6bcb031`, 10.18).
2. Then Phase 13's look and mobile (13.1 and 13.2) as a release of its own. **Merged into `main`
   on 2026-10-23 (1405/08/01)**, asked by the developer, with the SMS work of 10.7 to 10.18 brought
   into the new look (*Merged with the SMS work* under Phase 13). **Done 2026-10-25 (1405/08/03),
   00:43:** released with 14.5 (the panel out of search results) as `20261025-0043-0d08196`,
   frontend only (*Release `20261025-0043-0d08196`* under Phase 13). The Docker/apt, rootless and
   edge nights keep their own nights.
3. The public site (Phase 14) has no link to the panel; it is built on its own branch and released
   on its own, when the developer chooses, never inside the SMS release.
4. **Restart done 2026-10-20, 03:21**, the same night as the SMS release, by the developer's choice
   once the release was seen healthy: `./backup.sh run` first, `sudo reboot`, back in about a
   minute with all three containers healthy on their own and `/var/run/reboot-required` gone.
   **Still open:** 19 packages to upgrade, Docker among them (`docker-ce`, `containerd.io`,
   `docker-compose-plugin`, …), which restarts every container: `./backup.sh run`, then
   `sudo apt upgrade`, then a reboot if asked, on a night of its own, not with a release.

## The panel is closed (1405/08/03, 2026-10-25)
**Closed by the developer** after the release `20261025-0043-0d08196`: every task of Phases 0 to
13 is done and on the server, with 14.5. From here the work is the public site (Phase 14). The
panel is opened again only for a new feature or a change the developer asks for; such a task gets
its own number in the phase it belongs to, plan first, as before. Releases keep the rules of
docs/DEPLOYMENT.md (late at night, `./backup.sh run` first).

Still open around the panel (none of it code, or waiting for someone else):
- **Gym PC backups:** `deploy/pull-backup.ps1` on the gym's computer, once it is set up (6.5,
  *First release over real data*). Until then the dumps are on the server and in `D:\GymBackups`
- **Server night:** the 19 apt upgrades with Docker (item 4 above)
- ~~**The Owner** fills in «تنظیمات پیامک»~~ **Done** by 1405/08/03 (the developer said so); the
  line's account still has to stay charged (10.6)
- **Optional:** releases by registry pull instead of `scp` (6.4); the edge Caddy shared with
  another site, only if that site moves onto this server
- ~~**GitHub history**~~ **Done 1405/08/03:** the `Co-Authored-By: Claude` (189) and
  `Claude-Session:` (14) lines removed from the old messages by rewriting `main`, files, authors and
  dates unchanged; every commit id changed, so ids written before that day are old ones:
  docs/git-history-map.md. Old history kept in `D:\gymbackups\gym-git-before-rewrite-20261025.bundle`.
  Commits from 2026-10-25 are `Shahram Azghandi <shahramdevops@gmail.com>`

Next: **members and trainers on the site (Phase 15):** 15.0 is done (ADR 0010 accepted
  1405/08/03); 15.1, 15.2, 15.3 (a and b), 15.4 (a, b and c) and 15.5 (a and b) are done on their
  branches, not merged or released (`task/15.4-pick-a-trainer` pushed 1405/08/08); the next step is
  **Release 1**. Phase 14's 14.1 to 14.3 still wait for the
  gym's trainers, links and photos; 14.4 closed with 15.3b

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
      Installing the cron line is a server step (docs/DEPLOYMENT.md, "Backup and restore")
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
a friendly page during an API-only restart is a Phase 13 item (done as a message in 13.2g).

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

#### Release `20261010-2323-3428fa7` (1405/07/18, 2026-10-10, late at night)
Previous: `20261006-0505-73e6c33`. It carries 6.5.34 to 6.5.36, Phase 10's code (10.1 to 10.5, the
provider still `Fake`, so nothing can be sent), 11.1, 11.2 and 11.3, with seven migrations:
`AddNotifications`, `AddSmsSettings`, `MemberLastVisitIndex`, `CardioOnlyWithoutPlan`,
`SendSmsAsText`, `AddSmsDeliveryCancelled`, `AddPaymentsCafeOrderForeignKey`. Released while the
SMS line waits for Kavenegar's registration, so seven migrations do not pile up for another week;
switching SMS on later is an `.env` change, not a release (10.6).
- Tests before it: 704 domain, 1480 integration, 1017 frontend, `npm run e2e` green; zero warnings
- Server checked first: `TAG` `73e6c33`, API and Postgres healthy, 50 GB free, 2.8 GiB available
- Read-only checks: no payment pointing to a missing cafe order (the new foreign key's condition),
  last migration `AddPayables`. Rows: members 78, subscriptions 78, attendances 176, payments 108,
  service_charges 9, cafe_orders 26, expenses 0, products 28, audit_logs 2590
- `./backup.sh run` → `gym-20261010-232136.dump` (335 KB), copied to `D:\GymBackups` with `scp`,
  both `sha256sum` equal
- After it: the panel and `/health` answer 200, the CSP, Permissions-Policy and
  Cross-Origin-Opener-Policy headers are served, no error in the API log, the seven migrations
  listed, every row count unchanged (audit_logs 2596: sign-ins), every SMS switch off

**Rollback warning:** `./server.sh rollback` to `73e6c33` would run old code against the new schema.
The way out of a problem in this release is a fix forward.

#### Menu marks for that release (asked by the developer the same night)
- [x] A menu item carries its own mark (`badge` with its text and an optional last day, replacing
      `newUntil`): «اعضا» «به‌روز شد» and «گزارش تغییرات» «جدید» through 1405/07/25 (2026-10-17);
      «پیامک‌ها» and «تنظیمات پیامک» «آزمایشی» with no date, until SMS goes live (10.6). The
      developer asked for «جدید آزمایشی»; measured in the real menu, «تنظیمات پیامک» then broke onto
      two lines, so the developer chose «آزمایشی». The expired marks on the dashboard and «چک و قسط»
      removed
- [x] Tests: the marks on 25 Mehr, only the SMS marks from 26 Mehr, Staff sees «به‌روز شد» and no
      Owner mark. 1018 frontend tests green, lint, `format:check` and production build pass
- [x] Released in `20261010-2339-7ebfb0d` (previous `20261010-2323-3428fa7`), the same night.
      `./backup.sh run` first → `gym-20261010-233932.dump`, copied to `D:\GymBackups`, both
      `sha256sum` equal. No migration. After it: the panel and `/health` answer 200, the live bundle
      carries the three marks and the 1405/07/25 date, no error in the API log, row counts unchanged
      but one new payment recorded between the two releases, the SMS switch off. A rollback to
      `3428fa7` is safe: the schema did not change between them

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
server's trial data was wiped first (docs/DEPLOYMENT.md, *Record: the go-live wipe*), the wipe and all migrations were
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
Released in `20261004-0511-2793a16` (2026-10-04).

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
Released in `20261004-0511-2793a16` (2026-10-04).

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
Released in `20261004-0511-2793a16` (2026-10-04).

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
Released in `20261004-0511-2793a16` (2026-10-04).

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
Released in `20261004-0511-2793a16` (2026-10-04).

### 6.5.33 Remove the guest debt list (حذف بدهی مهمان‌ها)
Asked by the developer on 1405/07/12 (2026-10-04): the «بدهی مهمان‌ها» page from 6.5.31 is not
worth keeping. A guest pays before leaving, and while inside their locker already shows what they
owe. Decided with the developer, same day: auto-checkout no longer closes a guest visit that still
owes anything; it stays open and «بدهکار» for the desk the next morning, like a cardio-only visit
with no amount. BUSINESS_RULES.md §7 *Auto-checkout*, *Guest visit*, *Opening hours*, and the §1 row.
- [x] BUSINESS_RULES.md, written first
- [x] API: `GET /api/guest-debts` and `ListGuestDebts` removed; `AutoCheckoutHandler` runs in a
      transaction, locks each guest visit and skips it while `GuestPurchases` finds anything unpaid
- [x] Web: the page, its route, path and menu item removed, and the three mutations no longer
      refresh its cache
- [x] Tests: `GuestDebtsEndpointTests` and the menu test removed; integration (an unpaid charge
      keeps the visit open and the locker owing, a paid guest beside an owing one, settled and
      checked out the next day; the guest cafe auto-checkout test now expects the visit open)

Closed 2026-10-04: 1509 backend tests and 785 frontend tests green, lint clean, zero warnings. No
migration: nothing to release beyond the code.

### 6.5.34 Member list by latest visit, with tags and a sessions column (ترتیب آخرین ورود، برچسب‌ها، ستون جلسات)
Asked by the developer on 1405/07/15 (2026-10-07). BUSINESS_RULES.md §2 updated (the rules were
decided with the developer the same day). One migration, an index only.

- [x] **Order.** `GET /api/members` sorts by each member's latest visit, cancelled check-ins left
      out, newest first; members who never came last, by name. A partial index
      `ix_attendances_member_id_checked_in_at` (`WHERE cancelled_at IS NULL`) serves it
- [x] **Domain.** `SubscriptionSchedule.CurrentPlan` (active, else frozen, else soonest queued, else
      the one that ended last; single visits left out), for the bar
- [x] **API.** Each row has `lastVisitWasSingleSession`, `planEnded` (active members only) and
      `plan` (total, used, remaining), batched for the page. Both tags are expressions in
      `MemberListRows`, which the filters `singleSessionOnly` and `planEndedOnly` read too, so a filter
      lists exactly the tagged members
- [x] **Web.** The «وضعیت» column is removed (asked by the developer during the task); «تک‌جلسه»
      (red) and «پلن تمام‌شده» sit beside the name, and a «جلسات» column shows the board's
      `SessionsBar`, «—» with no plan. «تک‌جلسه» and «پلن تمام‌شده» toggles beside «بدهکار», kept in
      the URL as `single=1` and `ended=1`
- [x] **50 rows a page** on «اعضا» and on every tab of «تاریخچه» (was 20; asked by the developer)
- [x] **Never both tags**: a member whose latest visit was a single visit and whose plan has ended
      shows only the more recent of the two (asked by the developer); the filters follow the tags
- [x] **History, sales:** «ثبت توسط» shows who sold a plan (its `CreatedBy`, filled for every past
      sale too); the «وضعیت» column is removed, a cancelled or voided sale is tagged beside what was
      sold with the reason under it; the «سفارش‌های بوفه» button above the history is removed (the
      menu and the cafe till keep their links)
- [x] Tests (domain): `CurrentPlan` for active, frozen, exhausted with a renewal, expired, cancelled
      and single visits only
- [x] Tests (integration): order by latest visit with never-visited last by name; a cancelled
      check-in does not count; the single-visit tag, and not after a later visit on a plan; sessions
      of an active plan; ended (expired, exhausted), renewed, frozen past its end, deactivated, only
      single visits; each filter lists and counts only the tagged members; a filter with a search; the
      more recent tag wins (plan expired then a single visit, plan used up then a single visit, a
      single visit then a plan that expired unused); a plan sale names who sold it
- [x] Tests (frontend): no status column, the API's order kept, both tags on their rows only, the bar
      and none without a plan; each new toggle sends its parameter, keeps the status and writes the
      URL; their empty messages; paging tests at 50 a page; the sales tab names who sold a plan and has
      no status column; no «سفارش‌های بوفه» link on the history

Done when: «اعضا» lists whoever came last at the top, 50 to a page, a walk-in reads a red
«تک‌جلسه», someone whose plan ran out with nothing after it reads «پلن تمام‌شده», each tag is a
filter, and every member with a plan shows «۴ از ۱۲» over a bar.

Tests not run yet: the developer asked to run them once, when the changes are done. Release needs
the migration `MemberLastVisitIndex` (an index only). Committed as `6242691`; its tests ran with
6.5.35's full suite. Released in `20261010-2323-3428fa7` (1405/07/18).

### 6.5.35 Cardio-only without a plan (ورود فقط هوازی بدون اشتراک)
Reported by the developer on 1405/07/15 (2026-10-07) as a bug: someone whose sessions or plan had
run out, or who never had one, could not come in for هوازی. It was the rule of 6.5.27 ("the member
must hold a plan"); the developer replaced it the same day. BUSINESS_RULES.md §1 and §7
*Cardio-only visit* updated. One migration, a check constraint only.

- [x] **Domain.** `SubscriptionSchedule.PlanForCardioOnly` returns `Subscription?`: the plan active
      today, else a frozen one, else `null`; it never refuses. `Attendance.CheckInCardioOnly*` take a
      `Guid?` plan
- [x] **Database.** `ck_attendances_subscription_with_member` becomes "a guest's visit has no plan; a
      member's has one unless it is cardio-only" (migration `CardioOnlyWithoutPlan`). Every existing
      row passes. Its `Down` would fail once a visit with no plan exists; a rollback leaves it
- [x] **Web.** «ورود فقط هوازی» under the sale offered after a refused check-in and after
      registering a new member, its «انصراف» back to that offer; the confirmation reads «جلسه‌ای کم
      نمی‌شود و اشتراک لازم نیست». The board shows «—» and no end date or expiry mark for a visit
      with no plan; the locker's box says «بدون اشتراک»
- [x] Tests (domain): no subscriptions, only a single visit, exhausted, exhausted with a queued plan
      (nothing moved), expired, only a queued plan all name no plan; a cardio-only visit with no plan
      is a member's and still needs its amount to check out
- [x] Tests (integration): no plan, only a single visit (left unused), no sessions left (left at
      10), expired, not started (start date kept) all come in on no plan; an inactive member is still
      refused, and one already inside; a no-plan visit needs its amount, then checks out leaving the
      debt; it can be cancelled; the board shows it with no plan values; an ordinary member's visit
      without a plan is rejected by the check constraint
- [x] Tests (frontend): after `NoSubscription`, `Expired` and `NoSessionsLeft` the offer has
      «ورود فقط هوازی» and it lets the member in with nothing sold; «انصراف» goes back to the offer;
      a newly registered member is offered it; the board's dash and no expiry; the box's «بدون اشتراک»

Done when: a member with no plan, or whose plan ran out, comes in for هوازی from the locker's box,
and leaves once the amount is recorded.

Release needs the migration `CardioOnlyWithoutPlan` (a check constraint only). Released in `20261010-2323-3428fa7` (1405/07/18).

### 6.5.36 A third sale at the desk, «متفرقه» (فروش متفرقه)
Asked by the developer on 1405/07/15 (2026-10-07): a copy of «آنالیز» under its own name, with
exactly its rules, recorded by both roles, its money someone else's. Its own tab in the history and
its own card on the dashboard. BUSINESS_RULES.md §7 *Sale at the desk* and §12 updated.

- [x] **Domain.** `ServiceChargeKind.Other`, a sale kind (`IsSaleKind`). Named `Other`, not
      `Miscellaneous`: that is فروشگاه, whose rows were written as «متفرقه» before 6.5.29
- [x] **No migration.** The kind is stored as text; `ck_service_charges_miscellaneous` already gives
      it no name, quantity or unit price, and the one-per-visit index is for هوازی alone.
      `has-pending-model-changes` confirms it
- [x] **API.** Recorded through `POST /api/attendance/{id}/service-charges` (kind `Other`); voided in
      cancel check-in; `SaleSource.Other` and `RevenueSource.Other`; the financial report keeps it
      out of «فروش», «دریافتی» and «سود خالص» and sends it alone as `otherByMethod`
- [x] **Web.** A fifth tile «متفرقه» (rose, `Package`) in the member's and the guest's box, the
      آنالیز form; the history's «متفرقه» tab, «بابت» option and the Staff section «هوازی، فروشگاه،
      آنالیز و متفرقه»; the dashboard's «دریافتی متفرقه» card, its bar in revenue by source, and
      «فروش (به غیر از آنالیز، فروشگاه و متفرقه)»
- [x] Tests (domain): price only, never edited, on a guest's visit
- [x] Tests (integration): recorded unpaid with no name, on a guest's visit, refused a name by the
      constraint, never edited, voided by cancel check-in with its money back, blocks a guest's
      check-out while unpaid, its own payments source, its own sales section and totals, out of
      «فروش» and «سود خالص», its own `otherByMethod` apart from «دریافتی» and the shop's card
- [x] Tests (frontend): five tiles, the price-only form posts `Other`, the tile's own total, its
      cancel tick, the history tab and «بابت», the dashboard card and bar

Done when: the desk records a «متفرقه» from the locker's box like an «آنالیز», and the Owner finds
it on its own tab of the history and its own card on the dashboard.

Release needs no migration. Released in `20261010-2323-3428fa7` (1405/07/18).

### 6.5.37 Cancelling a subscription gives the money back (لغو اشتراک با بازگشت پول)
Asked by the developer on 1405/07/22 (2026-10-14), after a cancelled plan at the gym kept its money
because the separate «استرداد» was never pressed. Cancelling now refunds everything paid, in the
same step, as a cafe order's cancel and a charge's void already did; there is no refund by hand any
more. A payment taken wrong is fixed by cancelling the subscription and selling a new one.
BUSINESS_RULES.md §1, §4 *Cancel*, §5 (and *Confirming money at the desk*) updated. Built on its own
branch from `main`, so it can be released before the Phase 13 look.

- [x] Released on 2026-10-15 in `20261015-1423-4f9347d`, with the public site (13.3)
- [x] **API.** `CancelSubscriptionHandler` takes the member lock, cancels, and writes one refund
      per payment method still in credit with the cancel's reason (`SubscriptionRefunder`, the twin
      of `CafeOrderRefunder`; `PaymentLedger.GetNetPaidByMethodForSubscriptionAsync`), in one
      transaction
- [x] **Refund endpoint removed.** `POST /api/subscriptions/{id}/refunds`, `RegisterRefund*` and
      the codes `Payments.RefundExceedsNetPaid` and `Payments.RefundAfterUse` are gone
- [x] **A cancelled subscription takes no more payments** (`Subscriptions.Cancelled`), read again
      under the member lock: with no refund of its own, money paid onto it could never come back.
      *Claude's decision, pending review*
- [x] **Demo.** `seed-demo`'s plan sold by mistake is put right with the cancel alone
- [x] **Web.** The «استرداد» button and `RegisterRefundForm` are gone; the cancel box says
      «… پرداخت‌شده برای این اشتراک، به همان روشی که پرداخت شده بود بازگردانده می‌شود» and the row
      «اشتراک لغو شد و … بازگردانده شد»; the payments list refreshes after a cancel. The money-out
      question of `ConfirmPaymentDialog` went with the form that used it
- [x] Tests (integration): nothing paid, paid in full with the reason, cash and card refunded
      separately, partly paid, a refused cancel refunds nothing, a cancelled plan refuses a payment,
      four cancels at once refund once, a cancel and a payment at once leave nothing paid, the old
      route answers 404. The history, totals, reports, debt and payments tests that refunded by hand
      now cancel, with the same figures
- [x] Tests (frontend): the cancel box names the money coming back, no «استرداد» button, the
      message after the cancel
- [x] **Before the release** (with the developer, each step confirmed): on the server, record the
      «استرداد» for the plan already cancelled at the gym with the old button, and check that no
      other cancelled subscription still has money on it, since the new version has no button to
      give it back. Settled with the release of 2026-10-15, as the developer confirmed on
      1405/07/26 (2026-10-18): the server's panel has had no «استرداد» button since then

Done when: the Owner cancels a paid plan and the money is back in «پرداخت‌ها» as «استرداد», with no
second step to forget.

Release needs no migration.

Found during this task, not fixed (its own small task): `DemoSeederTests.SeedDemo_EmptyDatabase_
FillsThreeMonthsUpToNow` fails on `main` as well since 1405/07/22. The seeder tries the freeze on one
day only (day 78 of 90), and with that day's seeded data no member qualifies (inside the gym, or no
active plan with sessions left), so nothing is frozen and the test's "one frozen plan" fails. Every
other backend test passes (2190 of 2191).

### 6.5.38 A plan has at most 30 sessions (سقف ۳۰ جلسه)
Decided by the Owner on 1405/08/03 (2026-10-25): a plan is sold with 5 to 30 sessions, not 5 to
140, and a larger number is refused with a friendly message. The days of the table do not change
(21 to 30 sessions still last 70). BUSINESS_RULES.md §3, §4 and §10 updated. The panel was closed
(Phase 15); this reopens it at the Owner's request, on its own branch from `main`, so it is
released without 15.1.

- [x] **Domain.** `Subscription.MaxSessionCount = 30`; the table's last row and the
      «جلسات رو به اتمام» limits follow from it (first row 5 to 29, second at most 30)
- [x] **Database.** `ck_subscriptions_total_sessions_range` is 5 to 30, except a cancelled plan,
      which may keep the old 140. Checked on the server first, read-only, with the developer: one
      plan had 123 sessions, typed by mistake, already cancelled, nothing paid. A sale is never
      rewritten or deleted, so the check makes room for it rather than the other way round. The
      SMS settings' row sizes come down to the new ceiling if above it (the server's 8 and 15
      stay). Migration `SubscriptionsAtMostThirtySessions`
- [x] **Web.** `planLimits.maxSessionCount = 30` and the SMS settings' limits;
      `Subscriptions.SessionCountTooHigh` reads «حداکثر ۳۰ جلسه می‌شود فروخت؛ لطفاً عددی از ۵ تا ۳۰
      بنویسید.», shown under the box before anything is sent
- [x] Tests (domain): 30 sells for 70 days, 31, 123 and 140 refused, the SMS rows' new limits
- [x] Tests (integration): 30 allowed and 31 refused by the API, the check-in sale and the
      database; a cancelled plan of 123 allowed, 141 refused even cancelled; the migration keeps the
      cancelled 123 and brings the SMS row sizes down
- [x] Tests (frontend): 30 fills 70 days, 31 is refused with the new message
- [x] **Released 1405/08/05 (2026-10-27), 02:20, as `20261027-0220-387aa1d`**, with 10.19 and
      10.20 (the developer, 1405/08/05, replacing "a night of its own"); the record is under 10.20
- [x] **Before the release:** the read-only check, the new constraint's own condition negated,
      found 0 rows of 289; then `./backup.sh run`

Done when: the desk types 31 and sees the friendly message, and nothing is sent.

Release needs a migration (check constraints only; no row is changed on the server).

#### Housekeeping before Phase 9 (2026-10-04)
Phase 6.5 closed with a check of the whole repository, so the next task starts clean:
- [x] Full suite on `6b3159e`: 1509 backend and 785 frontend tests green, `dotnet build` with zero
      warnings, lint and production build clean
- [x] `main` pushed; the merged branches `task/6.5.25-gym-history` (and its worktree
      `D:/GymManagement-history`) and `task/8.1-expenses-api` (local and on `origin`) deleted. Only
      `main` is left
- [x] 6.5.28 to 6.5.32 marked as released in `20261004-0511-2793a16`
- [x] 6.5.33 released in `20261006-0505-73e6c33` (1405/07/14), with 9.1 to 9.4 and their
      follow-ups
- [ ] The gym PC has not yet pulled its own copy of the `gym-20261004-050157.dump` backup
      (`deploy/pull-backup.ps1`); today it exists only on the server and in `D:\GymBackups`.
      Still to do on 1405/07/26 (2026-10-18), the developer confirmed: the gym PC is not set up yet
- Noted for 11.3: the production build warns that the main JS chunk is about 942 kB (Vite's hint
  is 500 kB); code splitting by route would fix it. Not an error. Done in 11.3: pages load when
  first opened, the main file is 436 kB

Next: **9.1 Financial reports API**, in its own session, plan first.

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

Planned with the developer on 1405/07/12 (2026-10-04). The dashboard is the Owner's: summary cards
compared with the range before, a «نیاز به اقدام» panel (plans ending this week with no renewal,
members who left in the last 30 days, members with a live plan who stopped coming, old debts,
cheques coming due), and charts (revenue by source per day with expenses, the cash drawer, expenses
by category, cafe gross profit, renewal rate, new members per month, attendance by weekday and hour,
top cafe products). Decided the same day: guests' money is revenue but guests are not attendance;
sales are shown beside money received; "running out" uses the board's thresholds (3 sessions, 5
days); a range is at most a year; the Owner sees money by staff member.

### 9.1 Financial reports API
BUSINESS_RULES.md §12 *Financial report*, *Receivables*.
- [x] Date range handling in the gym's time zone: required, inclusive, at most 366 days
- [x] `GET /api/reports/financial?from&to` (Owner): revenue by source (single-session apart from
      membership) and by method and by staff member; sales of the range; expenses by category; net
      profit; cafe gross profit; the same for the range before; day by day
- [x] `GET /api/reports/receivables` (Owner): what is owed now, by age
- [x] Indexes for report queries: `payments.paid_at`, `expenses.expense_date`,
      `subscriptions.created_at`, `service_charges.charged_on` and `cafe_orders.ordered_on` already
      exist; checked, nothing to add unless a query plan says otherwise
- [x] Tests: by source with single-session apart, by method in the desk's order, by staff
      member; a payment either side of the gym's midnight; voided expenses left out; net and
      cafe gross profit; the previous range; every day listed; sales beside revenue; a range
      missing, backwards or over 366 days; Staff refused; receivables by age at each edge and equal
      to the history's «مانده»

Closed 2026-10-04: 1528 backend tests and 785 frontend tests green, zero warnings, lint and
production build clean. No migration: the indexes were already there. `ExpenseCategory.CafePurchasingId`
now names the seeded «خرید بوفه» id in Domain, and the seed uses it (same Guid, so no migration).
Next: **9.2 Operational reports API**, in its own session.

### 9.2 Operational reports API
BUSINESS_RULES.md §12 *Operational reports*, *Needs attention*. Decided with the developer on
1405/07/12: "ending this week" uses the board's thresholds (3 sessions, 5 days); "no visit for a
while" is 10 days; a plan is renewed by a plan sold within 30 days of its end; a new member is
counted on their first membership plan's sale.
- [x] `GET /api/reports/attendance?from&to`: members' check-ins per day and by weekday × hour,
      the range before, distinct members
- [x] `GET /api/reports/subscriptions`: active, frozen, expiring soon, low-session plans today
- [x] `GET /api/reports/cafe-products?from&to`: the top 10 by quantity
- [x] `GET /api/reports/needs-attention`: running out with no renewal, left in the last 30 days,
      a live plan and no visit for 10 days, old debts (members, plus one figure for walk-ins and
      guests)
- [x] `GET /api/reports/members?from&to`: ended, renewed and waiting plans and new members, day by
      day (the dashboard groups them into Jalali months)
- [x] The range rules shared by every report (`ReportRangeValidator`); the thresholds named once
      (`ReportThresholds`)
- [x] Tests: each threshold at its edge; renewed, frozen, single-visit, inactive and cancelled
      cases left out; a visit either side of the gym's midnight; guests and cancelled check-ins
      not counted; renewal on day 30 and 31 and waiting; first plan after a cancelled one; top 10
      and cancelled orders; old debts equal to the receivables' oldest age; Staff refused; bad
      ranges refused

Closed 2026-10-04: 1555 backend tests (27 new) and 785 frontend tests green, zero warnings, lint
and production build clean. No migration and no new error code. `SaleRow` carries the buyer's
`MemberId` now, for the old-debt list. Pending review: the top 10 by quantity, "waiting" plans left
out of the renewal rate, and deactivated members kept in the old-debt list.
Next: **9.3 UI: dashboard**, in its own session.

### 9.3 UI: dashboard
BUSINESS_RULES.md §12 *Dashboard*. Decided with the developer on 1405/07/12: the presets end today
(«ماه قبل» is the whole month), the page opens on this month, time runs right to left in the
charts, and a card shows the figure before instead of a percent when the range before had nothing
or a loss.
- [x] `/dashboard`, Owner only, with its menu item «داشبورد»
- [x] Summary cards with the change since the range before: net revenue, expenses, net profit,
      sales, the cash drawer, cafe gross profit, visits; new members and the renewal rate
- [x] Today: plans active, frozen, ending soon, low on sessions; receivables by age
- [x] «نیاز به اقدام»: the four lists, each name linked to the profile, plus what walk-ins and
      guests owe; cheques wait for 9.4
- [x] Charts (Recharts 3 and its `react-is` peer): revenue and expenses by day (by Jalali month past
      62 days), new members and renewal rate by Jalali month; bar lists in HTML for revenue by
      source and by method, expenses by category and the top 10 cafe products; the weekday × hour
      table; money by staff member
- [x] Jalali range presets (today, this week, this Jalali month, last month, this year, custom),
      kept in the URL
- [x] Tests: each preset at its edges (Saturday, Friday, Farvardin, a leap Esfand, Nowruz), 366
      and 367 days, months either side of the 1st and of Nowruz, percent change from zero and from
      a loss, renewal rate with waiting plans; the page's requests, presets, refused ranges, cards,
      lists and Staff refused

Closed 2026-10-04: 1555 backend tests and 835 frontend tests (50 new) green, lint and production
build pass. No backend change, no migration. The dashboard is its own chunk (388 kB, Recharts
inside), so the desk screens do not download it. The build's "chunk larger than 500 kB" warning
for the main chunk (942 kB) was already there before this task: splitting the other pages by
route is left for a housekeeping task, not hidden by raising the limit.
Next: **9.4 Cheque reminders**, rules first, in its own session.

#### 9.3 follow-up: plans sold on the dashboard (asked by the developer, 1405/07/13)
Plans sold matter more to the Owner than new members. BUSINESS_RULES.md §12 *Financial report*,
*Dashboard*.
- [x] The financial report's `bySource` rows carry `sold`: how many of each were sold in the range,
      by the sale's own day, cancelled and voided left out (the rule «فروش» follows)
- [x] «درآمد به تفکیک منبع» lists the cafe above فروشگاه, and the plan and single-visit bars say
      how many were sold («۱۲ پلن فروخته شد»), inside the bar or just past its tip
- [x] New members explained as "members whose first membership plan was sold in this range / month"
- [x] Tests: plans and single visits counted apart, cancelled left out, a plan sold the day before
      and paid today counted in the range before only; the page's order, counts and wording

Closed 2026-10-05: 1690 backend tests and 887 frontend tests green, lint and build pass (the
known main-chunk size warning only). No migration, no new error code. The dashboard test file now
loads the lazy page once before its tests: its first test had run out of time on a busy machine.

#### 9.3 follow-up: a livelier dashboard (asked by the developer, 1405/07/14)
The dashboard read as black and white, the top most of all. Look only: the same figures, rules and
tests.
- [x] A coloured banner at the top: greeting, today's date, the range in words, the presets as pills
- [x] Each card, list and chart has its own accent and icon (`--tone-*` tokens with dark variants,
      `tone-soft` / `tone-ink` / `tone-solid` / `tone-bar` utilities); the change since the range
      before is a badge, still arrow + word + colour
- [x] Receivables in one card with a bar split by age; the renewal rate with a progress bar
- [x] «نیاز به اقدام»: a coloured edge, count badge and initials per list; an overdue cheque tinted
- [x] Charts: rounded gradient bars on a track; skeleton cards while the figures load
- [x] A loss is shown with its minus sign instead of «—» (`formatMoney` took no negatives); the
      profit cards are green above zero and red below
- [x] The banner is green, with the gym's logo (cut from a photo of its wall, white on
      transparency); a photo of the gym's athlete was tried and taken out, and the greeting and
      date line removed
- [x] «فروش» leaves فروشگاه and آنالیز out, and is named «فروش (به غیر از آنالیز و فروشگاه)»
      (BUSINESS_RULES.md §12 *Financial report*); their «sold» counts and the history's totals
      keep them
- [x] «سود خالص» = revenue without فروشگاه and آنالیز − every expense (§12); the revenue card is
      «درآمد ناخالص» and still counts every payment; «سود ناخالص بوفه» is «سود بوفه»
- [x] Cards «خرید پلن» and «تک‌جلسه‌ای»: the money received for each, compared with the range
      before, with how many were sold underneath
- [x] «نرخ تبدیل تک‌جلسه‌ای به پلن» (§12 *Operational reports*): new people only, each once, a plan
      within 30 days, those still waiting left out; `trials`, `trialsConverted`, `trialsWaiting`
      on the members report
- [x] «فروش» first, with «پرداخت‌شده» and «نسیه» in the card's empty corner (`salesPaid`,
      `salesOwed`); «درآمد ناخالص» and «صندوق نقدی» taken off
- [x] «دریافتی» for the nightly drawer check: card, transfer, cash, without فروشگاه and آنالیز
      (`receivedByMethod`); «دریافتی آنالیز و فروشگاه» apart (`shopAndAnalysisByMethod`)
- [x] «خرید پلن» and «تک‌جلسه‌ای» by the day of the sale (`soldAmount`): a single visit sold
      days before and paid today no longer shows as today's
- [x] «خرید پلن» and «تک‌جلسه‌ای» list «پرداخت‌شده» and «نسیه» like «فروش» (`soldPaid`, `soldOwed`)
- [x] «نرخ تمدید» in plain words: a sentence for what it measures, and «تمدید کردند»,
      «تمدید نکردند», «در انتظار» in its corner
- [x] «دریافتی بوفه» after «دریافتی آنالیز و فروشگاه»: the cafe's net money of the range, one
      figure without the methods (`bySource` Cafe `money.net`, no API change)
- [x] «دریافتی هوازی» after «دریافتی بوفه», the same card for هوازی (`bySource` Cardio)
- [x] «خرید پلن» and «تک‌جلسه‌ای» renamed «دریافتی پلن» and «دریافتی تک‌جلسه‌ای», after
      «دریافتی هوازی»; «نسیه» is «مانده» on the dashboard
- [x] «سود بوفه» removed, from the page and from the report (`cafeGrossProfit`; API types
      regenerated)

#### 9.3 follow-up: the plain dashboard for the handover (asked by the developer, 1405/07/14)
The dashboard goes to the Owner in its first, plain black-and-white look. The coloured design is
kept, not thrown away: it comes back in Phase 13.2 as it is, not redesigned.
- [x] Git tag `dashboard-colour-v1` on `1d08ce2`, pushed; the design and how to bring it back in
      `docs/design/dashboard-colour.md`
- [x] Plain again: no banner or logo, no tones, icons, progress bars or skeletons; the receivables
      as four cards; the chart, list and picker components as before the colour commit
- [x] Kept: every card, figure and text of the follow-ups above, the breakdown lines beside the
      figure, a profit green and a loss red with its minus sign (§12)
- [x] The breakdown takes the card's second half instead of a fixed 160px: «پرداخت‌شده» ran out of
      the sales card; a figure too wide for its line now drops under its label (checked with
      12 billion on 245px cards)
- [x] «داشبورد» and «چک و قسط» first reach the server in the release of 1405/07/14: the menu marks
      both «جدید» through 1405/07/15 (`newUntil` on the menu item, against `gymToday()`), so the
      Owner finds them without being told; from 1405/07/16 the mark is gone by itself
- [x] Released in `20261006-0505-73e6c33` (previous `20261004-0511-2793a16`), with everything since
      the last release: 6.5.33, 9.1 to 9.4 and their follow-ups. Before it: the full suite green
      (109 domain, 1590 integration, 926 frontend tests, zero warnings), `./backup.sh run`
      (`gym-20261006-050350.dump`, also copied to `D:\gymbackups`), the server checked at
      `AllowGuestServiceCharges` with no `cheques` table. Migrations `AddCheques` then
      `AddPayables` (which drops the never-used `cheques` and adds a nullable `payable_id` to
      `expenses`): no existing row touched. After it: the panel and `/health` answer 200 and the
      live bundle carries the «جدید» date

### 9.4 Cheque reminders (یادآور چک) (asked by the developer, 1405/07/12)
The gym pays for equipment with dated cheques. A cheque is not an expense in the system: on its
date the Owner records the expense by hand, in that month (§9). The system only reminds.
**Members never pay by cheque**, and no payment method changes.
Decided with the developer on 1405/07/12–13: amount, date, payee and description, all required (no
cheque number, no bank); 7 days before the date, one fixed number; the Owner marks «پاس شد», only
on or after the date, and an unmarked cheque past its date stays on the dashboard; edited while
pending, cancelled with a reason, both final, never deleted; Owner only; who gets the SMS is
decided in Phase 10.
- [x] Rules first: BUSINESS_RULES.md §9 *Cheques*, §12 *Needs attention* (cheques coming due) and a
      §1 row
- [x] `Cheque` entity (Domain) and the `cheques` table: check constraints for the amount, payee,
      description, the pass and cancel fields and never both, `xmin`, an index on `due_date`.
      Migration `AddCheques` adds the table only
- [x] `/api/cheques` (Owner): list by status with the pending total, get, register, edit (with
      `version`), `pass`, `cancel`; no DELETE
- [x] `GET /api/reports/needs-attention` gains `chequesDue` (`ReportThresholds.ChequeDueWithinDays`)
- [x] `/cheques` «چک‌ها», Owner menu only: status tabs in the URL, the pending total, add and edit
      forms (`MoneyField`, Jalali picker), «پاس شد» with an in-page confirmation that points to the
      expenses, cancel with a reason; «چک‌های نزدیک سررسید» on the dashboard
- [x] Tests: every field and limit, pass on the date and not the day before, pass and cancel final,
      reason required (domain); register, edit, stale version, pass, cancel, pass racing cancel,
      list order and totals, Staff refused, no DELETE, the database constraints, the dashboard list
      at day 7 and 8 with overdue, passed and cancelled cases (integration); the page, its forms,
      tabs, menu and the dashboard list (frontend)
- [x] The SMS itself needs Phase 10's `ISmsSender`; until then the reminder is on the dashboard only.
      Decided on 1405/07/14: it goes to the Owner's number, one SMS per cheque or instalment
      (BUSINESS_RULES.md §10); built in 10.3

Closed 2026-10-05: 1632 backend tests (77 new: 32 domain, 45 integration) and 870 frontend tests
(35 new) green, zero warnings, lint and production build pass. Migration `AddCheques` adds one new
table and touches no existing row: it is safe over the real data, and the release still starts with
`./backup.sh run`.

#### 9.4 follow-up: cheques and instalments (چک و قسط) (asked by the developer, 1405/07/13)
The developer changed the rule after 9.4 was committed and before it was released: a cheque **is**
an expense, and the system records it on payment; instalments join the same page. Decided with the
developer on 1405/07/13: the expense category is chosen on the form; the expense is dated the day
it is marked paid; each instalment is entered on its own like a cheque, with «قسط n از N» (both
required, N at most 360); an instalment can be paid before its date, a cheque cannot; one shared
list with a kind filter and the pending total split by kind; a payment marked by mistake goes back
to pending with a reason, which voids its expense, and that expense is locked on the expenses page.
- [x] Rules first: BUSINESS_RULES.md §9 *Cheques and instalments* (replaces *Cheques*), the
      expenses' lock, §12 *Cheques and instalments coming due*, §0 and §1 rows
- [x] `Cheque` renamed `Payable` with `Kind` (Cheque | Installment), `CategoryId`,
      `InstallmentNumber`/`InstallmentCount`; `MarkPaid` returns the expense, `RevertToPending`
      voids it; `Expense.PayableId`, refused edit and void when set (`Expenses.LinkedToPayable`)
- [x] `payables` table: check constraints for the kind, the instalment numbers by kind, the paid
      and cancelled fields; FK to the expense category; `expenses.payable_id` with a partial unique
      index (one standing expense per payment). Migration `AddPayables` drops the empty `cheques`
      table from `AddCheques` (never released, so no row anywhere to carry) and adds the new table
      and one nullable column on `expenses`
- [x] `/api/payables` (Owner): list by status and kind with the pending totals, get, register,
      edit, `pay`, `revert`, `cancel`; `/api/cheques` is gone. `needs-attention` sends `payablesDue`
- [x] `/payables` «چک و قسط», Owner menu: kind and status filters in the URL, the totals, the form
      with kind, category and «قسط n از N», «پاس شد»/«پرداخت شد», «برگشت به در انتظار», cancel;
      «چک و قسط نزدیک سررسید» on the dashboard; «از چک و قسط» on the expenses page, without edit
      or void
- [x] Tests: the instalment numbers, paying an instalment early and a cheque not, the expense
      written and its date, revert and pay again, the expense's lock (domain); pay writing the
      expense, racing pays, revert voiding it, the expense endpoints refusing it, list by kind and
      the split totals, the dashboard with an instalment, the new check constraints and the partial
      unique index (integration); the page, both forms, revert, the kind filter, the expenses badge
      and the dashboard list (frontend)

Closed 2026-10-05: 1689 backend tests (521 domain, 1168 integration) and 885 frontend tests green,
zero warnings, lint and production build pass. Migration `AddPayables` touches no existing row of
the real data (`cheques` was never released, and the new `expenses` column is nullable); the
release still starts with `./backup.sh run`.

#### Follow-up: every date chosen like a birth date (asked by the developer, 1405/07/13)
Every date field now works like the birth date at registration: three dropdowns (day, month by
name, year) instead of the typed box with a calendar. Decided with the developer on 1405/07/13: the
years run from three years after the current one back to ۱۴۰۴; each form keeps the starting value
it already had.
- [x] Rules first: BUSINESS_RULES.md §13 (every date in three dropdowns; the two year ranges)
- [x] `FormField.tsx`: one `DropdownDateField`; `JalaliDateField` (۱۴۰۴ to three years ahead, with
      «پاک کردن») and `BirthDateField` (۱۳۲۰ to ۱۴۰۰) wrap it. Used by the cheque and instalment
      form, the expense form, and the date filters of expenses, cafe orders, history and the
      dashboard's range
- [x] Removed what only the typed box needed: `react-multi-date-picker`, `react-date-object`, the
      calendar's CSS, and `toIsoDate`, `withDateSlashes` and `toJalaliInput` in `format.ts`
- [x] Tests: the field's year range, clear button and error wiring; `jalaliToIso` and
      `jalaliPartsOf` take over the conversion tests; page tests choose dates through
      `test/jalaliDate.ts` (renamed from `birthDate.ts`)

Closed 2026-10-05: frontend only, no API or database change. 843 frontend tests green (885
before: the typed box's and the removed helpers' tests went with them), lint and production build pass.

#### Follow-up: the date filters keep the calendar (asked by the developer, 1405/07/13)
After trying the dropdowns, the developer found them slow for filters and asked for the previous
field back there; the cheque, instalment and expense forms keep the dropdowns.
- [x] Rules first: BUSINESS_RULES.md §13, a filter's date as the exception
- [x] `JalaliCalendarField` in `FormField.tsx`: the previous typed box with a Persian calendar,
      restored as it was; used by «از تاریخ» and «تا تاریخ» on the history, expenses and cafe
      orders pages and in the dashboard's custom range
- [x] Restored `react-multi-date-picker`, `react-date-object`, the calendar's CSS, and `toIsoDate`,
      `withDateSlashes`, `toJalaliInput` in `format.ts`, with their tests
- [x] Housekeeping: 13 files that had missed Prettier formatted, so CI's `format:check` passes;
      CLAUDE.md now says to run it before a frontend commit

Closed 2026-10-05: frontend only. 896 frontend tests green, lint, `format:check` and production build pass.

#### Follow-up: the header alert for cheques and instalments (asked by the developer, 1405/07/14)
The dashboard's 7-day list is only seen when the dashboard is opened. The developer asked for an
alert in the header, on every page, once a payment is 5 days away. Decided with the developer on
1405/07/14: 5 days, today included; past their date included, in red; in the middle of the top bar.
- [x] Rules first: BUSINESS_RULES.md §9 *Cheques and instalments*, *The header alert*
- [x] `GET /api/payables/due-soon` (Owner): pending ones dated within 5 days or past their date,
      the earliest first, each with `daysLeft` from the gym's today (`ListPayablesDueSoonHandler`)
- [x] `PayablesDueAlert` in the header for the Owner: the nearest one, its days left and the count
      of the others; a dialog with every one and a link to «چک و قسط». Asks again every 5 minutes
      and on window focus; refreshed by every payable change
- [x] Tests: the 5-day edge, past their date, paid and cancelled left out, empty, the kind's name,
      Staff refused (integration); the pill, the count, red when overdue, the dialog, nothing when
      empty, Staff never asking (frontend)

Closed 2026-10-06: 1694 backend tests (521 domain, 1173 integration) and 902 frontend tests green,
zero warnings, lint, `format:check` and production build pass. No migration; a new read-only
endpoint only.

---

## Phase 10 — SMS Notifications

Decided with the developer on 1405/07/14 (2026-10-06), BUSINESS_RULES.md §10: four kinds
(subscription running out, few sessions left, birthday for every member, each cheque and instalment
to the Owner); the numbers and send times on an Owner-only settings page that starts empty and off,
separate from the desk's; Kavenegar's template method; the developer's Kavenegar account until
release, the Owner's after. Still open: §0.

### 10.1 Notification model and sender
- [x] `ISmsSender`, template-first: a template name plus named values (`token`, `token2`,
      `token3`, `token10`, `token20`), not "send this string" (§10 *Sending*). It answers one of five
      outcomes (`SmsSendResult`) and never throws for a failed send
- [x] `SmsTokens` (Domain): Kavenegar's limits checked when it is built (`%token` required, no space,
      5 or 8 spaces, 100 characters); `SmsTokens.Fit` cuts a long name or payee short (decided with
      the developer: simply cut)
- [x] `FakeSmsSender`, which only logs (the template and the number's last 4 digits); `Sms:Provider`
      chooses it, `Fake` when missing, an unknown name stops startup; the test host forces it
- [x] `Notification` entity: kind, recipient, status (`Pending`, `Sent`, `Failed`, `Unknown`),
      attempts, the provider's message id, cost (Rial), delivery, the template and its values (for
      the resend in 10.5); a unique index for each kind (per subscription and kind, per member and
      Jalali year, per cheque or instalment); check constraints for the event's ids per kind,
      "sent has the provider's id" and "delivery only when sent"; `xmin`
- [x] `docs/sms-templates.md`: the wording, agreed with the developer on 1405/07/14 («باشگاه
      پاسارگاد»; the end date, not the days left; two birthday templates, the day itself and
      «پیشاپیش»), their blanks and their part count

Closed 2026-10-06: 1768 backend tests (562 domain, 1206 integration; 69 new: 41 domain, 28
integration) and 926 frontend tests green, zero warnings, lint and `format:check` pass. Migration
`AddNotifications` adds one new table only; nothing sends yet (no job until 10.3).

### 10.2 SMS settings (تنظیمات پیامک)
- [x] One settings row: the on/off switches, numbers, send times, template names and the Owner's
      number; check constraints for every range and for "on means filled"; `xmin`
- [x] `GET`/`PUT /api/sms/settings` (Owner), audited; `Sms.SettingsIncomplete` and the range errors
- [x] The page «تنظیمات پیامک», Owner menu only: starts empty, everything off; a kind turns on only
      when it is filled
- [x] Rules first: BUSINESS_RULES.md §10, decided with the developer on 1405/07/14: a send time is on a
      quarter hour (08:00 to 22:00, both included); a kind that is off may be left or made empty
- [x] Tests: every range edge, the send window and quarters, template names, on means filled, the
      Owner's number for cheques (domain); get, save, Staff refused, stale version, two saves at once,
      the number normalized and refused, the audit row, each field error, every check constraint
      (integration); the page, the locked switches, the time boxes, saving and every refusal (frontend)

Closed 2026-10-06: 1869 backend tests (618 domain, 1251 integration; 101 new: 56 domain, 45
integration) and 950 frontend tests (24 new) green, zero warnings, lint, `format:check` and production
build pass. Migration `AddSmsSettings` adds one new table and its one row, everything empty and off;
no existing row is touched, and the release still starts with `./backup.sh run`. Nothing sends yet
(the jobs are 10.3).

### 10.3 The daily jobs
Decided with the developer on 1405/07/14 (BUSINESS_RULES.md §0 and §10 *The daily runs*): 3 tries,
waiting 1 minute then 5; a missed run is made the same day before 22:00; the birthday works as a
window (tomorrow to N days ahead, or the day itself for 0); a cheque whose date changes gets no
second SMS; a row a stopped run left `Pending` becomes `Unknown`.
- [x] Rules first: BUSINESS_RULES.md §0 (the three settings decided) and §10 (the birthday window,
      *The daily runs*)
- [x] One job per kind at its own send time, read from the settings (a change of time applies from
      the next run), nothing outside 08:00–22:00: `SendDailySmsHandler`, run by `DailySmsJob` as the
      Hangfire recurring jobs `sms-<Kind>`; `ISmsRunSchedule` moves or removes them when the settings
      page is saved; `[DisableConcurrentExecution]` keeps any two runs apart
- [x] Who gets what (§10 *The four kinds*): renewed members left out, birthday by the Jalali day
      for every member, one SMS per cheque and instalment (`SmsAudience`, `SmsCandidates`); the
      blanks written as the app shows them (`SmsValues`, `JalaliCalendar` on .NET's `PersianCalendar`)
- [x] Retry with backoff up to `Sms:MaxAttempts` (`Sms:RetryDelays`, `SmsRetrySchedule`), in rounds
      after the others; `Failed` at once for the codes that will not pass; `Unknown` never retried;
      credit used up stops the run; a leftover `Pending` row becomes `Unknown` (`MarkInterrupted`)
- [x] Tests: running a job twice sends nothing twice; each kind's edges (renewed, frozen,
      single-session, deactivated, 30 Esfand, paid and cancelled cheques); a setting changed between
      runs; every outcome, the rounds, the wait on the clock, the 22:00 limit, credit used up mid-run
      and during retries, the leftover row; the real Hangfire schedule; the settings save moving the runs

Closed 2026-10-06: 1991 backend tests (696 domain, 1295 integration; 122 new: 78 domain, 44
integration) green, zero warnings. Backend only: no endpoint shape changed, so the frontend and
`schema.d.ts` are untouched. No migration. The runs exist only once the Owner saves the settings page
with a kind on, and everything starts off, so a release sends nothing by itself; it still starts with
`./backup.sh run`.

### 10.4 Kavenegar
Decided with the developer on 1405/07/15 (2026-10-07): nothing may really be sent yet, since the
local database holds made-up numbers that may belong to real people; the developer has no API key
yet, and has not asked Kavenegar's support about the birthday template (§0 stays open).
- [x] `KavenegarSmsSender` (`verify/lookup`); the API key in user secrets locally and
      `Sms__Kavenegar__ApiKey` on the server, never in git. `Sms:Provider` stays `Fake` in
      appsettings.json; Kavenegar with no key stops the app at startup
- [x] Kavenegar's codes mapped to the outcomes of `SmsSendResult` (the blanks' limits are already
      checked by `SmsTokens`, task 10.1): 409 retry, 418 credit, any other code `Failed`; no
      connection is a retry, a request that left with no usable answer is `Unknown`
- [x] Cost kept with each message (already in 10.3's run); delivery asked for once a night at 23:30
      (`CheckSmsDeliveryHandler`, Hangfire job `notifications-delivery-check`, §10); the remaining
      credit (`account/info`, `GET /api/sms/credit`, a line on the settings page, «حالت آزمایشی» with
      the fake provider)
- [x] `Sms:AllowedReceptors`: in every environment but Production, real sending only to the listed
      numbers (`AllowListSmsSender`), and none when the list is empty; Production refuses a list
- [x] The API key never in the logs: the HTTP client's own loggers removed (`RemoveAllLoggers`), with
      a test that fails without it
- [x] Free text from the gym's dedicated line instead of templates (1405/07/16, 2026-10-08):
      Kavenegar refused every template because the gym's site was not up yet, so a line is being
      bought (not yet on 1405/07/17) and its support said to send with `sms/send`. Decided with the developer: the wording fixed in
      the code (`SmsText`, `docs/sms-texts.md`), the line's number in `Sms:Kavenegar:Sender`
      (configuration, not the settings page). `ISmsSender.SendAsync(SmsMessage(Receptor, Text))`;
      `Notification.Text` replaces the template name and the five values; the settings page loses
      the template name; the birthday text is picked by the date (the day itself or «پیشاپیش»); a
      long name or payee is still cut short. Kavenegar with no line, or one that is not digits, stops
      the app at startup. Codes named in §10 now: 411, 412, 413, 422 (424, 426 and 431 were the
      template method's). Migration `SendSmsAsText`: every message already written gets its text from
      its kind and values, then the old columns go; a kind that is on stays on. 2120 backend tests
      (695 domain, 1425 integration) and 987 frontend tests green, zero warnings, lint and
      `format:check` pass; the release still starts with `./backup.sh run`
- [x] A manual test with the developer's account and number. `account/info` checked on 1405/07/16
      (2026-10-08) with the developer's key: the settings page showed the account's 1,050,000 Rial as
      ۱۰۵٬۰۰۰ تومان. Still to do: the line's number in user secrets (`Sms:Kavenegar:Sender`; with
      `Sms:Provider` = Kavenegar the API does not start without it), then one message to the
      developer's own number through `Sms:AllowedReceptors`, with Kavenegar's Debug mode on (it
      accepts requests and sends nothing). Kavenegar's documentation could not be reached from the
      development machine, so the codes and statuses come from §10 and are checked in this test.
      First try, 1405/07/17 (2026-10-09), Debug mode off, from the developer's account with the only
      line it has, the shared international line `0018018949161`: Kavenegar answered **412** (the
      line not valid for the account), so nothing was sent and nothing charged. What it proved: the
      request with the key and the line reaches Kavenegar, 412 becomes `Failed` with its code and is
      not retried, and `Sms:AllowedReceptors` let only the developer's number through. Note for the
      repeat: on startup Hangfire makes the day's missed runs at once, for every kind the local
      settings page has on. Waits for the dedicated line (to be bought from 1405/07/18; it needs
      identity verification and a business licence), then the same test with that account's key
      and line. Second try the same evening, Debug off, with `2000660110` (the line in the sample
      code of Kavenegar's panel, not one the account owns): three resends from «پیامک‌ها», each one
      request and each answered **427**, which §10 did not name yet; nothing sent. The resend
      behaved as §10 says: one request per click, `Failed` with the code, never retried by itself.
      Kavenegar's documentation, reached on 1405/07/17: 427 is "this line needs an access level",
      as expected for a line the account does not own. The developer says the first line,
      `0018018949161`, shows as active in their panel, yet it got 412; the documentation's 407 says
      sending through international lines needs the server's IP in the account's security settings,
      so that may be the reason. Not looked into further: the test waits for the dedicated line.
      The dedicated line was bought on 1405/07/18 (2026-10-10); Kavenegar's registration of it may
      take about a week, then this test runs with that account's key and line.
      **Passed on 1405/07/26 (2026-10-18)**: Kavenegar registered the line `9981600378` (خدماتی,
      10 digits) on the same account, so the same key. Locally, `Sms:Kavenegar:Sender` set to it,
      `Sms:Provider` = Kavenegar, the master switch off first so Hangfire's missed runs made nothing
      («SMS or this kind is off»); the two `427` messages to the developer's number resent from
      «پیامک‌ها»: both `Sent`, with Kavenegar's message ids, 1,615 Rial each, and both reached the
      phone from `9981600378`, each followed by a line «لغو11» that Kavenegar added (next item).
      `notifications-delivery-check` triggered by hand from Hangfire: «asked about 2, 2 with a new
      delivery», both `Delivered`, so `sms/status` and its mapping work too
- [x] Kavenegar's opt-out line in the text (1405/07/26): every message from the line reached the
      phone ending in a line «لغو11», which Kavenegar adds anyway, so the system writes it itself
      (`SmsText.OptOut`, BUSINESS_RULES.md §10 *Sending*, `docs/sms-texts.md`) and the history holds
      what was sent; it shows the line on a line of its own. No migration: messages already written
      keep their text, and the server has none yet
- [x] Whether a member who blocked advertising SMS still gets the line's messages (§10 *Sending*):
      settled by the developer on 1405/07/18 — `sms/send` from the dedicated line reaches the gym's
      members with no limit and no block. No code change: a status 14 that still comes back shows
      in the delivery with its cost given back, as 10.4 built it
- [x] Kavenegar's codes and statuses checked against its documentation (1405/07/17, 2026-10-09; it
      could not be reached before). Decided with the developer: `451` (too many requests from the IP)
      may pass like `409`; status `13` becomes its own delivery, `Cancelled` («کاوه‌نگار لغو کرد»),
      instead of «به گوشی نرسید»; a blocked or cancelled message's cost becomes 0, since Kavenegar
      gives it back, and its delivery is final (`Notification.RecordDelivery`,
      `Notifications.DeliveryRefunded`, check `ck_notifications_refunded_has_no_cost`); the history
      says «هزینه برگشت داده شد». §10 names 401, 403, 407, 416, 420, 427, 429, 451 and 501 now, each
      with its Persian meaning on «پیامک‌ها». Migration `AddSmsDeliveryCancelled`: a blocked message
      recorded before costs 0 (carried forward, not refused). 2139 backend tests (704 domain, 1435
      integration) and 990 frontend tests green, zero warnings, lint, `format:check` and production
      build pass; the release still starts with `./backup.sh run`

### 10.5 SMS history and resend
Decided with the developer on 1405/07/16 (2026-10-08), BUSINESS_RULES.md §10 *The SMS history*,
*The credit warning* and *Resend*: exactly the same message (number, template, values); one request,
a failure that may pass is `Failed` at once; allowed while the switches are off, never outside
08:00–22:00; the warning while the latest `418` is newer than the latest message sent.
- [x] Rules first: BUSINESS_RULES.md §10, the four decisions above
- [x] `GET /api/sms/messages` (Owner): the latest first, filtered by date (the day it was written,
      Tehran), kind and status, paged, with the filter's cost in Toman (`ListSmsMessagesHandler`)
- [x] `POST /api/sms/messages/{id}/resend` (Owner): `Notification.PrepareResend` back to `Pending`,
      saved before the request like a run, the outcome recorded with the run's own methods
      (`ResendSmsHandler`); `Notifications.NotResendable`, `OutsideSendingHours`, `ChangedConcurrently`
- [x] The credit warning in `GET /api/sms/credit` (`creditUsedUp`), on both SMS pages
      (`SmsCreditLine`, moved out of the settings page)
- [x] The page «پیامک‌ها» (Owner menu, `/sms`): opens on the current Jalali month with its cost;
      each message with its kind, recipient, status, delivery, cost, Kavenegar's code in Persian,
      the template and its values; «ارسال دوباره» with a confirmation, which for `Unknown` warns it
      may have arrived
- [x] Tests: `PrepareResend` from every status (domain); the list's order, names, Tehran day edges,
      filters, the total across pages, the range error, Staff refused; each resend outcome, the
      number changed since, the switches off, the hours' edges, a second click while sending, two
      saves at once, a run starting mid-resend; the warning's cases (integration); the page, the
      filters keeping the month, the resend and its refusals, the warning, the menu (frontend)

Closed 2026-10-08: 2140 backend tests (715 domain, 1425 integration; 44 new: 8 domain, 36
integration) and 987 frontend tests (16 new) green, zero warnings, lint, `format:check` and production
build pass. No migration: the history reads the `notifications` table as 10.1 built it. A release
still starts with `./backup.sh run`.

### 10.6 Go live with the Owner's account
Every server step is proposed and confirmed first, with `./backup.sh run` before the release.
The code of 10.1 to 10.5 is on the server since `20261010-2323-3428fa7` (1405/07/18), with the
provider `Fake` and every kind off. Switching it on needs no release, but taking the menu's
«آزمایشی» mark off does (`smsTestBadge` in `AppShell.tsx`), so plan a small frontend release for
the same night.
**Waits for 10.7 to 10.13** (decided by the developer, 1405/07/26): no hurry, SMS goes live as one
complete version with every kind below, not the four kinds first.
- [ ] The account that holds the dedicated line charged (no templates to wait for since 1405/07/16;
      it has credit: the test of 1405/07/26 was paid from it)
- [x] `docker-compose.prod.yml` passes `Sms__Provider`, `Sms__Kavenegar__ApiKey` and
      `Sms__Kavenegar__Sender` to the API from `SMS_PROVIDER`, `SMS_KAVENEGAR_API_KEY` and
      `SMS_KAVENEGAR_SENDER`; left out of `.env`, the provider stays `Fake`, so a release sends
      nothing. `deploy/env.example` lists them (1405/07/17)
- [x] Its API key and the line's number in the server's `.env` (2026-10-20, 03:00, right after the
      release below; `.env.bak-20261020` kept beside it). Every switch was checked off first, read-only,
      so nothing could go out; `docker compose up -d` rebuilt only the API, healthy, and the header
      shows the credit. Whether the key is limited to the server's IP in Kavenegar is the
      developer's to check
- [x] The Owner fills in the settings page and turns on the kinds they want (done by 1405/08/03,
      2026-10-25, the developer said)
- [x] The menu's «آزمایشی» mark on «پیامک‌ها» and «تنظیمات پیامک» removed (1405/07/28), released
      with 10.7 to 10.15 as the SMS release
- [x] Released in `20261020-0246-099e551` (previous `20261016-1747-c6ff9ec`) on 2026-10-20 at
      02:46: 10.6's menu change and 10.7 to 10.15. Seven migrations, each only adding and off at
      the start (`AddSmsDailyReport`, `AddBirthdayVisit`, `AddSubscriptionReceipt`, `AddSmsWelcome`,
      `AddSmsDebtReminder`, `AddSmsAbsence`, `AddSmsBroadcast`), so a rollback to `c6ff9ec` leaves
      the new columns in place and is not harmless. Before it: `npm run e2e` passed, the README
      pictures retaken, `./backup.sh run` → `gym-20261020-023611.dump` (771 KB, after 712 KB the
      night before), copied to `D:\gymbackups` with the same size. The provider was `Fake` until
      the `.env` step above

### More SMS kinds (10.7 to 10.13)
Asked by the developer on 1405/07/26 (2026-10-18), after the line's first messages arrived. One task
each, in this order (10.7 first: the developer promised it to the Owner). What is common to all:
- Every kind has its row on «تنظیمات پیامک»: on/off, its numbers and a send time, starting empty
  and off like the first four (§10 *SMS settings*); the Owner turns any of them off at any time.
- Every text ends with «لغو11» (10.4). The wording is chosen by the developer and kept in
  `SmsText` and `docs/sms-texts.md`, except the broadcast (10.13), which the Owner writes.
- **Offers and discounts may be sent** (settled by the developer with Kavenegar on 1405/07/26): the
  members signed up in the gym's own customer club and were registered at the desk, so any
  message to them may go from the line. Goes into §10 *Sending* with the first of these tasks.
- Each task starts with its rules in BUSINESS_RULES.md §10 (rules first), and asks before
  inventing any of what is listed as open below.
- A migration only adds columns, off and empty, and new kinds: the server's real data is carried
  forward (CLAUDE.md).

#### 10.7 Daily report to the Owner (گزارش روزانه)
Decided: one SMS a day to the Owner with the date, the number of entries, the number of
subscriptions sold, and the day's whole takings (دخل): cash, card and bank transfer each on its own,
the cash line left out on a day with no cash.
Settled with the developer on 1405/07/27 (2026-10-19), BUSINESS_RULES.md §10 *The daily report*:
- [x] The money is the dashboard's «دریافتی» for the day: refunds taken off, old debts included,
      without فروشگاه, آنالیز and متفرقه; cash, card and transfer each on its line
- [x] Entries are the members' check-ins, cardio-only included, guests and cancelled ones left out
      (the dashboard's attendance); plans and single visits sold, each counted on its own
- [x] Sent for **the same day at 22:45**, fixed in the code; this kind's hours run until midnight
      (a retry, a missed run and a resend until 23:59), the other kinds keep 08:00–22:00. "After the
      last check-out" was considered and dropped (the system cannot know which one is the last)
- [x] Its own number on its row of the settings page, apart from the cheques'
      (`Sms.DailyReportPhoneInvalid` for a number that is not an Iranian mobile)
- [x] The wording (`docs/sms-texts.md`): the date without the year, amounts in thousands of Toman
      rounded to the nearest thousand (two SMS parts instead of three), no cash line on a day with
      no cash; a day with nothing is not sent
- [x] Rules, settings row, the run, tests, UI: `NotificationKind.DailyReport`, `DailyReportFigures`,
      `SmsText.ForDailyReport`, `DailyReportCandidate` (the figures from `GetFinancialReportHandler`
      and `GetAttendanceReportHandler` for one day, so the SMS says what the dashboard says),
      `SmsSettings.IsWithinSendingHours(kind, …)`, Hangfire job `sms-DailyReport` at `45 22 * * *`;
      migration `AddSmsDailyReport` (two columns on `sms_settings`, off and empty, `report_date` on
      `notifications` with the unique index `ix_notifications_one_daily_report_per_day`, the kind
      and event check constraints widened; no existing row is touched); the «گزارش روزانه» section
      on «تنظیمات پیامک» and its label on «پیامک‌ها»; the demo sends it at 22:45

Closed 2026-10-19: 2250 backend tests (739 domain, 1511 integration) and 1025 frontend tests green,
zero warnings, lint, `format:check` and production build pass. One migration, `AddSmsDailyReport`,
which only adds (the switch starts off, the numbers empty) and refuses no existing row. It goes out
with the SMS release (10.6), which still starts with `./backup.sh run`.

#### 10.8 Birthday: one text and a free visit (تولد و ورود رایگان)
Replaces the «Future — Free entry on the birthday» notes. Decided:
- One text instead of two, sent 1 to 7 days before the birthday (the days on the settings page, as
  now), telling the member that on their birthday the gym is free for them.
- On the birthday the visit uses **no session** of the plan, like a guest's; anything else (cafe,
  services such as cardio, desk sales) is paid as usual.
- A frozen member gets the SMS too. If they come, they are unfrozen as any frozen member who comes
  in (§4 *Freeze*).
Settled with the developer on 1405/07/27 (2026-10-19), BUSINESS_RULES.md §7 *Birthday visit* and §10:
- [x] 1–7 days only: never on the day itself, so the member can plan the visit; a 0 already saved
      becomes 1. Members greeted this year keep their row (one per member and year)
- [x] Only an **active member holding a plan** gets the SMS: a membership active, frozen or bought
      for later on the day it is written (not a single visit, not used up or ended); replaces "every
      member with a birth date, deactivated ones included"
- [x] Freezing stays as §4 says: **several times**, the days adding up (the developer, 1405/07/27)
- [x] The free visit is **automatic**: the first check-in on the Jalali birthday that lands on a
      membership plan uses no session (frozen ones are unfrozen as usual; a plan sold at the locker
      that day counts too, decided by the developer); a single visit is used as usual; the second
      visit that day uses a session; a cancelled one gives nothing back and frees the free visit
      again. No money moves, so nothing at zero is recorded; the visit is marked «تولد»
- [x] The wording (`docs/sms-texts.md`): «{name} عزیز، {date} روز تولد شماست؛ آن روز مهمان باشگاه
      پاسارگاد هستید و ورودتان رایگان است. پیشاپیش تولدتان مبارک!», the month and day without the year
- [x] Rules, migration, check-in, tests, UI: `BirthdayVisit.FreeYear`, `JalaliCalendar.IsBirthday`,
      `Attendance.BirthdayYear` and `ConsumedSession`, `SmsAudience.HoldsPlanForBirthday`, one
      `SmsText.ForBirthday`; migration `AddBirthdayVisit` (`birthday_year` on `attendances`, empty for
      every visit before, the unique index `ix_attendances_one_birthday_visit_per_member_and_year`,
      the check `ck_attendances_birthday_visit_on_plan`, the settings' range 1–7 with a 0 carried
      forward as 1); the check-in box says it before and after, «تولد» on the board, the locker's
      box and both histories; the settings page says 1–7

Closed 2026-10-19: 2283 backend tests (758 domain, 1525 integration) and 1029 frontend tests green,
zero warnings, lint, `format:check` and production build pass. One migration, `AddBirthdayVisit`,
which only adds (an empty column, an index, a check) and carries a saved 0 forward as 1, refusing
no existing row. It goes out with the SMS release (10.6), which still starts with `./backup.sh run`.

#### 10.9 Subscription receipt (رسید خرید اشتراک)
Decided: sent when the subscription is **fully paid**, so a member who got it has surely paid; a
subscription paid in parts gets it when the last part is paid.
Settled with the developer on 1405/07/27 (2026-10-19), BUSINESS_RULES.md §10 *The subscription receipt*:
- [x] What it says: the sessions, the dates **without the year**, and the price; a plan queued behind
      another gives its days and «پس از اشتراک فعلی» instead of dates. «مبلغ» and «پایان» were taken
      out so the usual text stays in two SMS parts (the queued one takes three)
- [x] Plans only: a single visit never gets one, nor a free plan
- [x] Sent at once after the payment, as a Hangfire background job, so the desk never waits for
      Kavenegar; a payment outside 08:00–22:00 has it sent **at 10:00 the next morning** (fixed)
- [x] Cancelled after the SMS: nothing more. Checked again when sent: switched off since, cancelled
      or the member deactivated (the last proposed by Claude, approved with the plan) sends nothing
- [x] Only payments made while the switch is on: turning it on later sends nothing for older ones
- [x] Rules, settings row (on/off only), the job, tests, UI: `NotificationKind.SubscriptionReceipt`,
      `SmsText.ForSubscriptionReceipt` and `ForQueuedSubscriptionReceipt`, `SmsAudience.EarnsReceipt`,
      `ReceiptStillDue` and `IsQueuedBehindAnother`, `SmsSettings.ReceiptDeferredUntil`;
      `SubscriptionReceipts` called after the commit of `RegisterPaymentHandler` and
      `SettleMemberDebtHandler`, `ISubscriptionReceiptScheduler` (Hangfire `Enqueue`/`Schedule`),
      `SendSubscriptionReceiptHandler` run by `Jobs/SubscriptionReceiptJob` one at a time, `SmsRequest`
      shared with the daily run; migration `AddSubscriptionReceipt` (one switch, off, and the kind and
      event check constraints widened; the existing index already allows one per subscription and
      kind; no existing row is touched); «رسید خرید اشتراک» on «تنظیمات پیامک», its label on
      «پیامک‌ها» and the audit screen; the demo sends them on its own clock

Closed 2026-10-19: 2339 backend tests (786 domain, 1553 integration) and 1032 frontend tests green,
zero warnings, lint, `format:check` and production build pass. One migration, `AddSubscriptionReceipt`,
which only adds (one switch, off) and widens two check constraints, refusing no existing row. It goes
out with the SMS release (10.6), which still starts with `./backup.sh run`.

#### 10.10 Welcome (خوش‌آمد)
Decided: once per member, when they buy their **first plan**; single-session visits never get it.
Settled with the developer on 1405/07/27 (2026-10-19), BUSINESS_RULES.md §10 *The welcome*:
- [x] Members who hold plans already never get it (their next plan is not their first); a member
      moved over from the paper notebook gets it with their first plan in the system
- [x] Sent **at the sale** of the first plan, paid or not, as its own kind: a first plan paid at once
      gets two SMS, the welcome and then its receipt. At once, or at 10:00 for a sale after 22:00
- [x] The wording: «{name} عزیز، به باشگاه پاسارگاد خوش آمدید!», one SMS part
- [x] No free-plan rule: a plan's price is its sessions × the session price (§3), and any first plan
      gets it whatever it cost
- [x] Rules, settings row (on/off only), the job, tests, UI: `NotificationKind.Welcome`,
      `SmsText.ForWelcome`, `SmsAudience.IsFirstPlan` (sold before it by `CreatedAt`, then id),
      `SmsSettings.DeferredToMorning` (was `ReceiptDeferredUntil`); `Welcomes` called after the commit
      of `SubscriptionSeller.SellMembershipAsync` (assign, renew) and of `CheckInHandler`,
      `IWelcomeScheduler`, `SendWelcomeHandler` run by `Jobs/WelcomeJob` one at a time; migration
      `AddSmsWelcome` (one switch, off, the unique index `ix_notifications_one_welcome_per_member`,
      the kind and event checks widened; no existing row is touched); «خوش‌آمد» on «تنظیمات پیامک»,
      its label on «پیامک‌ها» and the audit screen; the demo sends them on its own clock

Closed 2026-10-19: 2382 backend tests (799 domain, 1583 integration) and 1035 frontend tests green,
zero warnings, lint, `format:check` and production build pass. One migration, `AddSmsWelcome`,
which only adds (one switch, off, and a unique index) and widens two check constraints, refusing no
existing row. It goes out with the SMS release (10.6), which still starts with `./backup.sh run`.

#### 10.11 Debt reminder (یادآوری بدهی)
Decided: the Owner sets on the settings page the number of days and the minimum amount.
Settled with the developer on 1405/07/27 (2026-10-19), BUSINESS_RULES.md §10 *The debt reminder*:
- [x] The days count from the **oldest unpaid sale** (the receivables' ages), not the last payment;
      sent once it is at least that many days old (*N* = 1: yesterday's debt today)
- [x] **Once per debt**: no other until everything owed at the last reminder is paid; a debt that
      builds after that has its own (refined from "until the debt is zero", approved with the plan)
- [x] The amount is the member's **whole open account** (§5), newer sales included
- [x] **Deactivated members get it too**: money owed is owed; guests and walk-ins have no account
- [x] The wording (`docs/sms-texts.md`): «{name} عزیز، مانده حساب شما در باشگاه پاسارگاد {amount}
      تومان است. لطفاً در مراجعه بعدی تسویه بفرمایید. سپاس!», two parts
- [x] A daily run at the time chosen on the page (08:00–22:00, quarter hours); days 1–90; the
      minimum amount 1,000 Toman or more
- [x] Rules, settings row, the run, tests, UI: `NotificationKind.DebtReminder`, `Notification.DebtSince`,
      `OpenDebt`, `SmsAudience.IsDebtReminderDue`, `SmsText.ForDebtReminder`, the four
      `SmsSettings.DebtReminder*` fields with `CheckDebtReminderMinAmount` and
      `CheckDebtReminderComplete`; `DebtReminderCandidate` reads each member's debt from `SaleRows`
      (the receivables' query) and their latest reminder; Hangfire job `sms-DebtReminder` at the
      chosen time; migration `AddSmsDebtReminder` (four columns on `sms_settings`, off and empty,
      with their checks; `debt_since` on `notifications` with the unique index
      `ix_notifications_one_debt_reminder_per_member_and_debt`; the kind and event checks widened;
      no existing row is touched); «یادآوری بدهی» on «تنظیمات پیامک» with `MoneyField`, its label on
      «پیامک‌ها» and the audit screen; the demo turns it on (30 days, 200,000)

Closed 2026-10-19: 2443 backend tests (833 domain, 1610 integration) and 1051 frontend tests green,
zero warnings, lint, `format:check` and production build pass. One migration, `AddSmsDebtReminder`,
which only adds (four columns, off and empty, one column and a unique index) and widens two check
constraints, refusing no existing row. It goes out with the SMS release (10.6), which still starts
with `./backup.sh run`.

#### 10.12 Absence SMS (پیامک غیبت)
Moved here from «Future — Absence SMS». Decided: the Owner sets the number of days on the settings
page.
Settled with the developer on 1405/07/27 (2026-10-19), BUSINESS_RULES.md §10 *The absence SMS*:
- [x] Only an active member with a **plan usable today** (§4 `Active`): the members of «دیگر
      نمی‌آید» on «نیاز به اقدام»; not a frozen plan, a plan for later, an ended one or a single visit
- [x] **Once per absence**: nothing more until the member comes again; the next absence has its own
- [x] A member who **never came** on the plan is counted from the plan's start, as on the board
- [x] The wording (`docs/sms-texts.md`): «{name} عزیز، جای شما در باشگاه پاسارگاد خالی است!», one part
- [x] Days away **7 to 60** (proposed by Claude, approved with the plan), a send time 08:00–22:00 on
      a quarter hour; the board's own 10 days stay as they are
- [x] Rules, settings row, the run, tests, UI: `NotificationKind.Absence`, `Notification.AbsentSince`,
      `Absence.Since` and `DaysAway` (Domain/Attendances, now used by the board too, so both count
      the same way), `SmsAudience.IsAbsenceDue`, `SmsText.ForAbsence`, the three `SmsSettings.Absence*`
      fields (`Sms.AbsenceDaysOutOfRange`); `AbsenceCandidate` reads each plan's last visit and the
      member's latest absence SMS; Hangfire job `sms-Absence` at the chosen time; migration
      `AddSmsAbsence` (three columns on `sms_settings`, off and empty, with their checks;
      `absent_since` on `notifications` with the unique index
      `ix_notifications_one_absence_per_member_and_absence`; the kind and event checks widened; no
      existing row is touched); «پیامک غیبت» on «تنظیمات پیامک», its label on «پیامک‌ها» and the
      audit screen; the demo turns it on (14 days, 11:30)

Closed 2026-10-19: 2498 backend tests (868 domain, 1630 integration) and 1058 frontend tests green,
zero warnings, lint, `format:check` and production build pass. One migration, `AddSmsAbsence`,
which only adds (three columns, off and empty, one column and a unique index) and widens two check
constraints, refusing no existing row. It goes out with the SMS release (10.6), which still starts
with `./backup.sh run`.

#### 10.13 Broadcast (پیامک گروهی)
Decided: the Owner writes the text each time (e.g. the gym closed for a few hours, a piece of news);
the one kind not fixed in the code.
Settled with the developer on 1405/07/27 (2026-10-19), BUSINESS_RULES.md §10 *The broadcast*:
- [x] Who it goes to: the Owner chooses each time, «همه اعضای فعال» or «اعضای دارای پلن» (a plan
      usable today, the absence SMS's members); never a deactivated member, a guest or a walk-in
- [x] The text: «{name} عزیز، » before the Owner's own, «لغو11» after it; at most **3 parts** for
      every recipient, measured with the longest first name among them (`Sms.BroadcastTooLong`)
- [x] Every member's SMS now names them by the **first name only** («سید» skipped), replacing
      "the first 6 words" (`SmsText.FirstName`); messages written before keep their text
- [x] The page «پیامک گروهی» (Owner only, in the audit log): the text with its characters and
      parts as it is typed, then a confirmation with the recipients, the exact parts and the cost
      «حدود» (from the latest charged message) beside the credit left
- [x] Sent at once in the background, only 08:00–22:00; no chosen time. Its own switch on «تنظیمات
      پیامک», off at the start (`Sms.BroadcastOff`); one at a time (`Sms.BroadcastInProgress`)
- [x] Stopped (credit, hours, a switch, an error) → «متوقف شد» with its reason, and «ادامه ارسال»
      sends to those of the group with no message of it yet; a server restart continues it by itself
- [x] On «پیامک‌ها» one row per recipient, of the kind «پیامک گروهی»; the broadcast page lists each
      broadcast with its group, state, sent/failed/unknown and cost
- [x] Rules, migration, endpoints, tests, UI: `SmsBroadcast` (`Create`, `CheckLength`, `Finish`,
      `Stop`, `Resume`, `xmin`), `SmsParts` (70, then 67 a part; the frontend's `smsParts` counts the
      same), `SmsBroadcastErrors`, `NotificationKind.Broadcast`, `Notification.BroadcastId`;
      `SmsBatchSender` taken out of `SendDailySmsHandler` so a run and a broadcast write, send, retry
      and stop on credit the same way; `GET /api/sms/broadcasts/audiences`,
      `POST /api/sms/broadcasts/preview`, `POST /api/sms/broadcasts`,
      `POST /api/sms/broadcasts/{id}/resume`, `GET /api/sms/broadcasts`; Hangfire `BroadcastJob`
      (one at a time, no automatic retry); migration `AddSmsBroadcast` (the table `sms_broadcasts`
      with the partial unique index `ix_sms_broadcasts_one_sending`, `broadcast_enabled` on
      `sms_settings`, off; `broadcast_id` on `notifications` with
      `ix_notifications_one_per_broadcast_and_member`; the kind and event checks widened; no
      existing row is touched); the page, its menu item, the labels on «پیامک‌ها» and the audit
      screen; the demo turns it on
- [x] Review fixes: the switches and the hours are read again before every message, so 22:00 or
      a switch turned off stops a broadcast in the middle (`SmsBatchSender`'s `mayGoOn`, `Halted`);
      a broadcast saved but not queued says so on the page instead of «در حال ارسال»; the job
      measures the text again and stops with `TooLong` if a longer first name joined; at start
      `BroadcastRecoveryService` hands a broadcast still sending to its job again, and
      `StopBroadcastOnFailureFilter` stops one whose Hangfire job failed (`RecoverBroadcastsHandler`)

Closed 2026-10-19: 2602 backend tests (917 domain, 1685 integration) and 1084 frontend tests green,
zero warnings, lint, `format:check` and production build pass. `DemoSeederTests.SeedDemo_…` fails
when run late at night (after about 23:30 nobody is still inside, so no open visit), whatever the
code; it passed earlier the same evening. One migration, `AddSmsBroadcast`, which only adds (a
table, a column off, a column and a unique index) and widens two check constraints, refusing no
existing row. It goes out with the SMS release (10.6), which still starts with `./backup.sh run`.

### 10.14 The SMS credit in the header (اعتبار پیامک)
Asked by the developer on 1405/07/27 (2026-10-19): the panel warned only once the credit had run out,
and only on the SMS pages. Decided, BUSINESS_RULES.md §10 *The credit in the header*:
- [x] The Owner sees the credit on every page, beside the cheques alert, opening «تنظیمات پیامک»:
      the whole box flashing solid red below 1,000 Toman, red and blinking below 100,000, orange
      from 100,000 to 200,000, green above
- [x] Asked from Kavenegar when the panel opens and every 10 minutes (a send's answer carries only
      its cost, and the account is charged outside the panel, so the figure is never worked out);
      no answer keeps the last figure, in grey; none yet says «نامعلوم»; hidden in test mode
- [x] An SMS to two numbers below 100,000 was considered and dropped by the developer
- [x] `SmsCreditBadge` in the header, `useSmsCredit`'s `lastKnownToman` and `refetchInterval`
      (one cache entry with the SMS pages); no backend change, no migration

Closed 2026-10-19: frontend 1096 tests green, lint, `format:check` and production build pass. No
migration and no endpoint change. It goes out with the SMS release (10.6).

### 10.15 The SMS settings page: a card per kind with its message (تنظیمات پیامک)
Asked by the developer on 1405/07/28 (2026-10-20): the broadcast belongs under the SMS settings,
each kind should show the exact SMS it sends, and the page left most of a wide screen empty.
Decided, BUSINESS_RULES.md §10 *SMS settings*:
- [x] Each kind is a box in a grid (three a row on a wide screen, two, then one): its switch beside
      the title, its few fields only as wide as their numbers, the range inside the empty box, and
      at its foot the message drawn as on a phone, with its SMS parts
- [x] The texts come from `GET /api/sms/samples` (Owner), built by `SmsText` with a sample member
      and figures (`GetSmsSamplesHandler`), so the frontend keeps no copy of the wording
- [x] The kinds grouped: «پیامک به اعضا», «پیامک به شما (مدیر)» and «ارسال دستی» (the last two
      side by side on a wide screen); the master switch on a card of its own; «ذخیره» stays at the
      bottom of the screen while the settings scroll
- [x] The broadcast is one card: its switch, writing and sending one with a live preview (the
      group's longest first name), and the latest broadcast only while it is sending or stopped,
      with «ادامه ارسال» (`LatestBroadcast`); its own page, route, menu item and list of sent
      broadcasts removed (they are on «پیامک‌ها», which links back to the card, `#broadcast`)
- [x] Tests: the samples are the runs' own texts and parts, Owner only (integration); every card's
      preview, an unreadable preview, the grouping, the broadcast card (frontend)

Closed 2026-10-20: 2605 backend tests (917 domain, 1688 integration) and 1100 frontend tests green,
zero warnings, lint, `format:check` and production build pass. No migration; one new endpoint,
`GET /api/sms/samples`, which reads nothing from the database. It goes out with the SMS release (10.6).

### 10.16 Send again what Kavenegar cancelled (ارسال دوبارهٔ پیامک‌های لغوشده)
Found on 1405/07/30 (2026-10-22): «اعتبار پیامک: نامعلوم» on the server. The cause was the
server's DNS, only Google's (`8.8.8.8`, `8.8.4.4`), which from inside Iran sometimes does not answer
(`NameResolutionError` in the logs). Fixed on the server the same night, no code: ParsPack's
resolver `45.139.11.231` and Shecan's `178.22.122.100`, `185.51.200.2` beside Google's, in
`/etc/systemd/resolved.conf.d/iran-dns.conf`; the API reaches them through Docker's
`host(127.0.0.53)`. The logs then showed no SMS had failed for DNS, but «پیامک‌ها» showed many
«کاوه‌نگار لغو کرد» (13) between about 18:00 and 20:10, on 07/29 and 07/30, while the messages before
and after reached the phone. A ticket to Kavenegar about it is the developer's. Decided with the
developer on 1405/08/01, BUSINESS_RULES.md §10 *Sending*:
- [x] The Owner's resend also takes a sent message Kavenegar cancelled (`PrepareResend`); the same
      row, its last request's id, cost, moment and delivery cleared
- [x] Automatic resend (`ResendCancelledSmsHandler`, Hangfire every 15 minutes, `CancelledSmsResendJob`):
      asks Kavenegar about today's messages (`CheckSmsDeliveryHandler.CheckSentSinceAsync`), then sends
      each cancelled one again 30 minutes after its last request, at most 5 times
      (`PrepareAutoResend`, `auto_resends` with a check constraint), the same day, within the
      kind's hours, with the switches on; every kind
- [x] The nightly 23:30 check and the 15-minute one may meet: a conflict is logged and asked again
      next time instead of failing the job
- [x] «پیامک‌ها»: «ارسال دوباره» on a cancelled row, with «کاوه‌نگار این پیامک را لغو کرد و به گوشی
      نرسید» in the confirmation, and «N بار خودکار دوباره فرستاده شد» under the status
- [x] Migration `AddSmsAutoResends`: one column, 0 for every existing row
- [x] Tests: domain (resend and automatic resend, the limit), integration (every rule above, the
      schedule, the constraint, the Owner's resend of a cancelled message), frontend (the button,
      the warning, the count)

Release: with 10.17, from `main`, before Phase 13's look; it has a migration, so `./backup.sh run`
first.

### 10.17 Three changes to the SMS settings (تنظیمات پیامک)
Asked by the developer on 1405/08/01 (2026-10-23), to go out with 10.16. Decided, BUSINESS_RULES.md
§10:
- [x] The daily report's time is the Owner's: 08:00 to 23:45 on a quarter hour, an hour and a
      minute box on its card (the kinds' 22 has only :00; the report's 23 has every quarter). It was
      22:45, fixed in the code; the migration keeps 22:45 (`daily_report_send_time`, check
      constraint, `Sms.DailyReportSendTimeOutOfRange`)
- [x] «پایان اشتراک» also says the sessions left: «… به پایان می‌رسد و فقط ۱۲ جلسه از آن مانده
      است.» Still two SMS parts (`SmsText.ForRunningOut`, docs/sms-texts.md)
- [x] «جلسات رو به اتمام» by the plan's size (`LowSessionsSettings`): plans of 5 sessions to a first
      size (8 at first), to a second (15), and bigger, each row with its own number of sessions left,
      1 to 4. The migration renames the old number to the first row's and carries it into all
      three, capped at 4, and sets the sizes to 8 and 15; check constraints for every range and for
      "on means filled"
- [x] Tests: domain (the time's window, the rows' ranges, which row a plan falls in, the text),
      integration (each row's plans in a run, the endpoint's refusals under each box, the
      constraints, the migration carrying the old number forward), frontend (the rows named by
      the sizes typed, the report's hours, what is sent)

Release: with 10.16, as one release from `main`; both have migrations, so `./backup.sh run` first.

### 10.18 New wording for three SMS kinds (متن تازهٔ پیامک غیبت، خوش‌آمد و تولد)
Asked by the developer on 1405/08/01 (2026-10-23), to go out with 10.16 and 10.17, all three in
spoken Persian. BUSINESS_RULES.md §10, docs/sms-texts.md. Messages already written keep their text.
- [x] Absence: «{name} عزیز، جات توی باشگاه پاسارگاد خالیه! {days} روزه که باشگاه نیومدی و منتظر
      حضور گرمت هستیم!», two SMS parts (accepted by the developer; it was one). *{days}* is the
      member's real days away (`Absence.DaysAway`), not the settings' number (chosen by the
      developer); the sample on «تنظیمات پیامک» says ۱۲
- [x] Welcome: «درود بر {name} عزیز، به باشگاه پاسارگاد خوش اومدی، امیدوارم ساعات خوشی رو توی
      باشگاه داشته باشی», two SMS parts (it was one)
- [x] Birthday: «{name} عزیز سلام، {month/day} روز تولدته؛ اون روز مهمون باشگاه پاسارگاد هستی و
      ورودت رایگانه. پیشاپیش تولدت مبارک رفیق!!», still two parts
- [x] Tests: domain (each text, its parts, no zero days), integration (a member away longer than the
      setting reads the real days; the samples; the birthday run), frontend (the samples)

Found by the pre-release run on the same day, a 1st of the month, and fixed with it:
- [x] The dashboard opened with «امروز» lit instead of «این ماه» on every Jalali month's 1st, when
      the two are the same day (`presetOf`, §12 *Dashboard*): the default now wins a tie
- [x] `ServiceChargeBox`'s void test waited one second for its notice and sometimes ran out under
      the whole suite: three, as the other slow notices

No migration. Release: with 10.16 and 10.17, one release from `main`.

- [x] Released in `20261023-2305-6bcb031` (previous `20261020-0246-099e551`) on 1405/08/01
      (2026-10-23) at 23:05, the gym closed early that night: 10.16, 10.17 and 10.18. Two
      migrations (`AddSmsAutoResends`, `SmsSettingsLowSessionsRowsAndReportTime`); the second
      renames `low_sessions_threshold`, so a rollback to `099e551` is **not** safe (fix forward, or
      restore the dump below). Before it: every test green (965 domain, 1740 integration, 1110
      frontend), `npm run e2e` passed, the README pictures retaken; read-only, the low-sessions
      number was 1 (carried into all three rows) and absence, welcome and birthday were on;
      `./backup.sh run` → `gym-20261023-230118.dump` (994,906 bytes), copied to `D:\gymbackups`,
      the same `sha256sum` (`42a09964…2b7a67c8`). After it: all three containers on the new tag and
      healthy, `/health` 200, no error in the API's log, the row counts unchanged (226 members, 245
      subscriptions, 418 payments, 931 attendances, 121 notifications, 151 cafe orders),
      `auto_resends` 0 everywhere. The developer then set the low-sessions rows to 1, 2 and 3 and
      the daily report to 22:30 on the new page

### 10.19 Did the absence SMS work? (اثر پیامک غیبت)
Asked by the developer on 1405/08/04 (2026-10-26); reopens the panel. BUSINESS_RULES.md §12 *The
absence SMS's effect*. Branch `task/10.19-absence-sms-effect` from `main`, in its own worktree, so
it does not mix with 15.1.
- [x] Asked first (1405/08/04): came back = a visit within **14 days** of the message (not 7 or
      30); only messages sent and not given back by the provider; by the day of sending, inside
      the dashboard's range; the card shows the rate, the three counts, the average days to come
      back and the cost, and opens the list of names
- [x] Domain: `AbsenceSmsEffect` (which messages count, and each one's outcome), unit tests
- [x] `GET /api/reports/absence-sms?from&to` (Owner only): the counts, the average, the cost and
      the list; integration tests (the 14-day edge, waiting, a cancelled or cardio-only visit, a
      visit before the message, a failed or cancelled message, the range by the day of sending)
- [x] The card «اثر پیامک غیبت» beside the renewal and conversion rates, and its list in a dialog
      opened by «فهرست نام‌ها» (`StatCard` took an `action`); a visit on the day itself reads
      «همان روز آمد», the window's last day «امروز روز آخر است» (committed as `ad199ff`)

Second round, the same day, after the developer saw it running: "a new month empties it" and "a
plan that ends inside the window gives wrong data". Decided with the developer:
- [x] The window is **5 days** (14, then 7, then 5: the closer to the message, the more a visit is
      its answer)
- [x] **Not measurable** («قابل سنجش نیست»), apart from the rate: the plan held on the day of
      sending ends inside the window or was cancelled (known from that day, so it leans the
      figure neither way), or it was frozen inside the window before the member came. Rate = came
      back ÷ (came back + did not)
- [x] No control group (members left without the SMS): declined by the developer. Instead, how
      soon those who came back came, day 0 to day 5, as a sign of the message's effect
- [x] «اثر پیامک غیبت» in «نیاز به اقدام», under «بدهی قدیمی»: every message since the first,
      whatever the range (`GET /api/reports/absence-sms/overall`), with the counts, the cost per
      return, the days to come back and the same month by Jalali month; no names. The card in
      «خلاصهٔ بازه» stays, with the same rules (`AbsenceSmsEffectReader` serves both)

Third round, the same day, asked by the developer:
- [x] The card in «خلاصهٔ بازه» taken off: one place only. The report has no range any more
      (`GET /api/reports/absence-sms`; the range query, its validator, the reader and `/overall`
      removed; `StatCard`'s `action` with them)
- [x] «فهرست نام‌ها» moved to the box in «نیاز به اقدام»: the last **30 days** only, so it stays
      short (`AbsenceSmsNamesDialog`, counted back from the report's own today)
- [x] The box takes the place of «بدهی قدیمی», which moves to the end of the panel (`ListFrame`
      moved to a file of its own, shared by the panel and the box)

No migration: everything is read from `notifications`, `attendances` and `subscriptions`.
- [x] Released 1405/08/05 (2026-10-27) as `20261027-0220-387aa1d`, with 6.5.38 and 10.20; the
      record is under 10.20

### 10.20 Welcome back (خوش برگشتی)
Asked by the developer on 1405/08/04 (2026-10-26), right after 10.19; reopens the panel.
BUSINESS_RULES.md §10 *The welcome back*. Branch `task/10.20-welcome-back` from `main` (10.19 and
6.5.38 on it, not released yet).
- [x] Asked first (1405/08/04): **half an hour after the check-in**; on the first visit after the
      absence SMS **however late**; until midnight, not 22:00; **six texts in turn** for each
      member, not at random; a member who came back before the release gets nothing after it
- [x] Domain: `NotificationKind.WelcomeBack` (its event is the member and `AbsentSince`),
      `SmsText.WelcomeBackTexts` and `ForWelcomeBack`, `SmsSettings.WelcomeBackEnabled`, its hours
      until midnight and its moment (`WelcomeBackAt`), unit tests
- [x] Application: `WelcomeBacks` asked after the commit of `CheckInHandler` and
      `CardioOnlyCheckInHandler`; `IWelcomeBackScheduler`; `SendWelcomeBackHandler` checks it all
      again and picks the text by the member's turn; the settings' switch; integration tests
- [x] Infrastructure: `Jobs/WelcomeBackJob` one at a time; migration `AddSmsWelcomeBack` (one
      switch, off; the unique index `ix_notifications_one_welcome_back_per_member_and_absence`, named
      beside the absence SMS's on the same columns; the kind and event checks widened; no row touched)
- [x] UI: «خوش برگشتی» on «تنظیمات پیامک» with its six texts, its label on «پیامک‌ها» and the
      audit screen; the demo

Release: **with 10.19 and 6.5.38, one release from `main`** (the developer, 1405/08/05, replacing "a
night of its own"); it has a migration, so `./backup.sh run` first. The switch starts off: the Owner
turns it on.

#### Release `20261027-0220-387aa1d` (1405/08/05, 2026-10-27, 02:20)
6.5.38, 10.19 and 10.20 from `main`, with 14.2's map of the streets (merged into `main` with 15.0's
docs before 6.5.38, so it went out to the public site too). 15.1 and 13.5 stay on their branches.
- [x] Before: domain 1018, integration 1808 (`DemoSeederTests.SeedDemo_EmptyDatabase_FillsThreeMonthsUpToNow`
      failed once just after midnight, an older flake, green again), frontend 1202; lint, format and
      build clean; `npm run e2e` 7/7; `npm run screenshots` retaken (`387aa1d`)
- [x] The server before: `20261025-0344-23fdcdd`, all healthy, 51 GB free, 2.9 GB memory available;
      rows 258 members, 289 subscriptions, 507 payments, 1256 attendances, 225 notifications; no plan
      the 30-session constraint refuses (0 rows)
- [x] `./backup.sh run` → `gym-20261027-021839.dump` (1247072 bytes), copied to `D:\gymbackups`,
      sha256 `0c17718116f03940d5f2dc98c1b8e3237434d1039fb6a1415f22d44d90b63f4b` the same on both
- [x] `deploy/release.sh gym@94.184.45.96` → `Released 20261027-0220-387aa1d (previous:
      20261025-0344-23fdcdd)`, both migrations applied
- [x] After: the panel's `/health` and the public site 200, the live `site.js` carries `townMap`;
      no error in the API's last 15 minutes of logs; the same row counts
- [x] Checked on the live panel by the developer: the absence SMS's box and its names (10.19), 31
      sessions refused with the friendly message, the sale not finished (6.5.38), the «خوش برگشتی»
      card with its six texts (10.20)

---

## Phase 11 — Audit UI and Hardening

### 11.1 Audit UI
Decided with the developer on 1405/07/18 (2026-10-10), BUSINESS_RULES.md §11 *The audit screen*:
sign-in rows hidden behind a switch; the changes in Persian, formatted; the member's name wherever the
record belongs to one; the page is «گزارش تغییرات».
- [x] Rules first: BUSINESS_RULES.md §11 *The audit screen*
- [x] `GET /api/audit-logs` (Owner): the latest first, paged; filtered by Tehran day, user or «سیستم»,
      kind of record, one record, action and member; `RefreshToken` and `TrustedDevice` left out unless
      `IncludeSignIns` or chosen as the kind (`ListAuditLogsHandler`); the member found through the
      record, a payment's subscription, charge or order, an order line's order (`AuditMembers`);
      `Audit.InvalidDateRange`, `Audit.ConflictingUserFilter`
- [x] `GET /api/audit-logs/users` (Owner): every account, the Owner and deactivated staff included
      (`IUserNames.ListAllAsync`)
- [x] `IAppDbContext.AuditLogs` as an untracked `IQueryable`, so Application can only read the log
- [x] The page «گزارش تغییرات» (Owner menu, `/audit-logs`): opens on today; a member or a record shows
      their whole history; each row opens to its fields in Persian (`auditFields.json`, `labels.ts`)
- [x] Tests: each filter, Tehran day edges, paging, the member through every link, insert and delete
      values, reading changes nothing, Staff refused, 400 codes; every audited entity and field has a
      label in `auditFields.json` (integration); the value formats, the hidden fields, the page, its
      filters, the record and member links, the menu (frontend)

Closed 2026-10-10: 2160 backend tests (704 domain, 1456 integration; 21 new) and 1013 frontend tests
(23 new) green, zero warnings, lint, `format:check` and production build pass. No migration: the
indexes on `audit_logs` were built in task 1.6. A release still starts with `./backup.sh run`.

### 11.2 Security review
Decided with the developer on 1405/07/18 (2026-10-10), BUSINESS_RULES.md §1: refresh and logout
limited to 60 a minute per IP, together; user-name discovery through `Auth.LockedOut` accepted.
The review itself, item by item with its evidence: `docs/security-review.md`.
- [x] CORS (Development only), cookie settings, JWT validation, the 500 response, forwarded
      headers, Postgres and the API's user checked: nothing to change
- [x] Security headers: a Content-Security-Policy on the panel (`script-src 'self'`, no inline
      script; the theme snippet moved to `web/public/theme-init.js`), Permissions-Policy and
      Cross-Origin-Opener-Policy; a CSP, `X-Frame-Options` and Permissions-Policy on the public page
- [x] Rate limits: refresh and logout get the `session` policy (`RateLimiting:Session`, 60 a minute
      per IP, one bucket), separate from login's
- [x] Secrets: no key, password or `.env` anywhere in the git history; `npm audit fix` for the
      dev-only `source-map-js`; NuGet has no known vulnerabilities
- [x] The Hangfire dashboard stays Development-only (the stale comment in `Program.cs` corrected)
- [x] OWASP Top 10 (2025) checklist with the accepted risks: `docs/security-review.md`
- [x] Tests: the refresh limit, logout sharing its bucket, login keeping its own
      (`SessionRateLimitTests`); the policy's `script-src` and `frame-ancestors`, no inline script
      or handler in `index.html` (`ContentSecurityPolicyTests`); in Chrome against the production
      images on `https://localhost`, the app works with no violation and an injected script is refused
- [x] Released in `20261010-2323-3428fa7` (1405/07/18); the headers checked on the live panel

Closed 2026-10-10: 2167 backend tests (704 domain, 1463 integration; 7 new) and 1013 frontend tests
green, zero warnings, lint, `format:check` and production build pass. No migration. Fixed on the way:
`AuditLogsEndpointTests.List_Paging_…` from 11.1 assumed rows written in one millisecond get
increasing ids, which a version 7 Guid does not promise; it now checks the total and that no row is
on two pages. A release still starts with `./backup.sh run`.

### 11.3 Performance and logging review
Measured with three years of a busy gym's data in a throwaway database, not with production's few
hundred rows. The review, its method and its numbers: `docs/performance-review.md`.
- [x] N+1 queries: none. `QueryCountTests` calls 44 read endpoints with 1 row and with 25 behind
      them and fails if one sends more SQL commands for 25 (`QueryCounter`, test project only)
- [x] Missing indexes: `payments.cafe_order_id` (migration `AddPaymentsCafeOrderForeignKey`). Every
      member's debt read the whole payments table once per cafe order: 18 to 23 s for «بدهکاران»,
      the receivables report, the dashboard and a year's financial report; now 0.06 to 0.15 s
- [x] Postgres JIT off on the app's connections (`-c jit=off`): 410 ms of compiling for a debt
      query that runs in 60 ms (`DatabaseTuningTests`)
- [x] Foreign key on `payments.cafe_order_id`, in the same migration (production had no payment
      without its order; `PaymentConstraintTests`)
- [x] Review real production logs and error handling: four days read over ssh with read-only
      commands (2 errors, 12 warnings). A cancelled request was logged as a 500 error (the
      request log moved outside the exception handler); .NET 10 had stopped logging a handled
      500's exception (`SuppressDiagnosticsCallback`); four totals queries warned about
      `FirstOrDefault` (now `SingleOrDefault`); the Kerberos message at every start (GSS off);
      the board's successful refreshes, 79% of the log, now at Debug
- [x] Alerting on errors (open since 11.2): decided with the developer to leave it for later
- [x] Playwright end-to-end test for the front desk flow: `npm run e2e` runs the production images on
      `https://localhost` (compose project `gym-e2e`, empty database, the release's migration
      bundle); a staff member signs in, checks a member in from a free locker, sees the locker
      taken on the board and checks them out, then a reload on `/members` loads a lazy page.
      Local, before a release; not in CI
- [x] The 942 kB bundle note (6.4): every page but the login and the locker board loads when first
      opened (main file 1,015 kB → 436 kB, first download 280 → 252 kB gzipped); a tab opened
      before a release reloads once when a page file is gone (`staleChunkReload.ts`); a page
      still downloading shows «در حال بارگذاری…»; the jsdom tests render the pages already
      loaded (`renderApp`)
- [x] `artifacts/` (a 147 MB migration bundle) left out of the Docker build context
- [x] Released in `20261010-2323-3428fa7` (1405/07/18), after checking that no payment pointed to
      a missing cafe order

Closed 2026-10-10: 2184 backend tests (704 domain, 1480 integration; 17 new) and 1017 frontend
tests (4 new) green, zero warnings, lint, `format:check` and production build pass; `npm run e2e`
passes. One migration, `AddPaymentsCafeOrderForeignKey` (an index and a foreign key; production
had no payment without its order, so it refuses no row). A release still starts with
`./backup.sh run`.

### 11.4 Opening hours: no check-in while the gym is closed (DROPPED)
**Dropped by the developer** on 1405/07/18 (2026-10-10), when asked how night-time development would
stay unblocked: nobody checks a member in while the gym is shut, so the rule is not needed.
BUSINESS_RULES.md §0 and §7 *Opening hours* now say the system has no opening hours. Check-out is
unchanged: by hand, or by *Auto-checkout* at `Gym:ClosingTime` (an unpaid guest and a cardio-only
visit with no amount stay open, as before). The items below were never built and stay as the record.
- `Gym:OpeningTime` = 06:00 setting next to `Gym:ClosingTime`
- Check-in between `Gym:ClosingTime` and `Gym:OpeningTime` refused with a stable error code, mapped
  to a Persian message
- Domain tests for the window edges and an integration test for the refusal

Nothing to release: no code changed, only the two documents.

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

Done 1405/07/19 (2026-10-11), all four in one session at the developer's request. The developer
chose the seeding approach (a console command through the real handlers, not seeding through the
API) and screenshots on desktop in both themes; phone screenshots wait for Phase 13's mobile work.
**New screenshots with every release** (asked by the developer): step 3 of the release checklist in
docs/DEPLOYMENT.md.

- [x] **Demo seed data.** `admin seed-demo` (`src/Gym.Api/Admin/Demo/`, ADR 0009): ninety days of a
      made-up gym written through the real handlers and validators, on a `DemoClock` the seeder moves
      day by day, as a `DemoActor` (the Owner for office work, two staff accounts at the desk, nobody
      for the nightly jobs), with the fake SMS sender whatever the configuration says. About sixty
      members, members inside right now, guests, cafe orders, هوازی, debts mostly paid on the next
      visit, a plan sold by mistake then refunded and cancelled, a freeze, a deactivation, a price
      rise, expenses, cheques and instalments, SMS from the day it was turned on. Seeded randomness.
      Refuses a database holding anything but the Owner and what the migrations add
      (`Demo.DatabaseNotEmpty`), a missing Owner (`Demo.NoOwner`), and on Production anything without
      `Demo:Allowed=true` (`Demo.NotAllowedHere`), which only the screenshot run passes, to that one
      command. No `server.sh` command for it
  - [x] Since 2026-10-15 `DemoSeederTests.SeedDemo_EmptyDatabase_FillsThreeMonthsUpToNow` fails:
        no frozen subscription. `FreezeSomeoneAsync` (12 days before today) is a `TryAsync` and
        finds no one with today's date, silently. Date-dependent; it passed on the day it was
        written. Find which condition empties the list and make the freeze certain. Never runs on
        the server; the screenshots are still made (they show no freeze)
        - Fixed on 1405/07/26 (2026-10-18). The freeze did succeed: a day's visits are planned in
          the morning and the freeze comes at 13:00, so on some days the frozen member still had a
          visit planned for that evening, and checking in unfroze the plan (§4 *Freeze*). Run as if
          on eleven days from 2026-10-14 to 10-25: 10-14, 15, 19 and 21 failed. `CheckInAsync` now
          skips a frozen member's planned visit; all eleven pass. A new test pins 2026-10-15
          (`SeedDemo_FrozenMemberHadAVisitPlannedThatEvening_PlanStaysFrozen`): red without the fix,
          green with it
- [x] **README with architecture diagram and screenshots.** The diagram was there. Added what the
      system does, what is worth a look in the code, and the screenshots; the long deployment and
      backup sections moved to docs/DEPLOYMENT.md. `npm run screenshots` (in web/) builds the
      production images, runs them empty, runs `seed-demo`, and saves nine screens in the light and
      the dark theme to `docs/screenshots/`
- [x] **Complete ADRs.** 0005 Result values, 0006 Gregorian storage and Jalali display, 0007
      invariants in the database, 0008 money never deleted and debt calculated, 0009 the demo. 0005 to
      0008 record earlier decisions, with reasons taken from BUSINESS_RULES.md and LEARNING.md
- [x] **Deployment guide.** docs/DEPLOYMENT.md: the release checklist as releases have actually been
      done (tests, `npm run e2e`, screenshots, late at night, look at the server, read-only checks for
      new migrations, backup with matching `sha256sum`, release, checks after, record it), going back
      and when a rollback is not safe, domains, server accounts, backup and restore, the go-live wipe
      record. SERVER-SETUP.md stays the guide for a new server
- [x] Tests: `DemoSeederTests` (a full ninety-day run and what it made, the empty-database, Owner and
      missing-services guards, `IsAllowedHere` per environment)

---

## Phase 13 — Look, feel and mobile

Deliberately last, and deliberately in one pass. Phases 7 to 9 add whole screens; restyling
before they exist means restyling them twice, while a shared set of components built here would
be built against screens that do not exist yet. The exception is 6.5.2, which builds the bar,
the badge and the primary button early because the board needs them anyway.

**13.1 and 13.2 were built on the branch `task/13.2-visual-language`** (24 commits) and merged into
`main` on 1405/08/01 (2026-10-23), after the SMS releases (see *Release order* at the top).

#### Merged with the SMS work (1405/08/01, asked by the developer)
`main` had moved on by 41 commits (10.7 to 10.18) since the branch last took it in. Merged into the
branch first, then the branch into `main`. Every SMS rule stays as it is; only how it looks changed.
- [x] Conflicts: the header, three visit tables, the two locker boxes, the SMS history and settings
      pages kept the new look and took the SMS work: «تولد» on the board, both histories and the
      visit box (a solid pink `Badge` variant `birthday`, beside `cardio` and `guest`), the resend of
      what Kavenegar cancelled and the automatic resends on «پیامک‌ها», the birthday notice in the
      check-in box
- [x] «تنظیمات پیامک» (built in 10.15 after the look was finished): `PageHeader`, the page's states,
      every box and the «همهٔ پیامک‌ها» box with the page's strip, `SaveRow`, `StaleAlert` and
      `useUnchanged` in its sticky save bar, Persian digits in every number box as typed and as
      loaded (the three rows of «جلسات رو به اتمام», the debt reminder's days, the absence days)
- [x] The SMS credit in the header (10.14) edged like the cheque alert beside it (a faint frame, a
      thick start edge in its colour) instead of a round pill; on a phone only its coloured icon
      (BUSINESS_RULES.md §10 *The credit in the header*), so the cheque alert keeps its words
- [x] On a phone only: the rows of «جلسات رو به اتمام» narrower so each stays on one line; no
      sideways scroll on «تنظیمات پیامک» at 390px
- [x] Tests: the SMS settings' boxes expect Persian digits, one new test types English digits in
      four of them; the credit's colour tests read its edge. README screenshots retaken

Closed 2026-10-23: 2705 backend tests (965 domain, 1740 integration) and 1193 frontend tests green,
zero warnings, lint, `format:check` and production build pass; `npm run e2e` (4, the phone run
included) and `npm run screenshots` pass. No package added, no API change, no migration.

#### Release `20261025-0043-0d08196` (1405/08/03, 2026-10-25, 00:43)
13.1, 13.2 and 14.5, previous `20261023-2305-6bcb031`. Frontend and Caddy files only: nothing in
`src/` changed and no migration, so a rollback to `6bcb031` is safe.
- [x] Before it: every test green (2712 backend, 1193 frontend), `npm run e2e` (4) and
      `npm run screenshots` on 2026-10-24. On the server: `TAG` was `6bcb031`, all three containers
      up, API and Postgres healthy, disk 13% used (50 GB free), 2.9 GB memory available
- [x] `./backup.sh run` → `gym-20261025-004013.dump` (1,088,258 bytes, after 771 KB on 2026-10-20),
      copied to `D:\gymbackups`, both `sha256sum` equal (`f972029d…b38335e`)
- [x] `deploy/release.sh gym@94.184.45.96` → `Released 20261025-0043-0d08196`
- [x] After it, from outside: the panel and `/health` 200, the public page 200, www and the .com
      301. The developer checked the panel on a phone: works. The API log after the release was not
      read that night (the developer's call; no backend change)


### 13.1 Mobile
Not cosmetic. The reception PC is a Windows desktop, but the gym loses power: during an outage
the desk has to keep working from a phone, and the Owner checks the gym from home on one.

Split into three sessions with the developer on 1405/07/20 (2026-10-12), on the same branch as 13.2
(`task/13.2-visual-language`; nothing is released before SMS is live): (a) the frame and the desk,
(b) tables as cards, (c) forms, dialogs and touch. One rule over all of it, set by the developer:
**every change is for the phone only, and the computer's screen is not touched at all.** "A phone"
is below Tailwind's `lg` (1024px), where the side menu becomes a drawer: `max-lg:` in the classes,
`useIsPhone()` in the code. The desk's PC and the backup tablet are wider.

- [x] **13.1a The frame and the desk on a phone** (1405/07/20). Check-in, check-out and the
      board fully usable on a phone, the flows that cannot wait for the power to come back. The
      side menu is a drawer from the right, opened by «منو» at the header's start and closed by
      choosing a page, its ✕, Escape or a tap beside it (Radix's dialog, no new package); the
      menu's rows are finger-sized in it. The header drops the user's name and the word «خروج»; the
      page has a 16px gutter and `h-dvh`, so the browser's address bar does not hide its bottom.
      «ورود با کمد» (BUSINESS_RULES.md §6, decided by the developer while looking at it): a smaller
      title with the name search beside it; under them on one line only free, occupied, guest and
      cardio-only, smaller (no out-of-service count, no legend for «بدهکار» or the long-stay bar);
      no «نقشهٔ استفاده» and no hourly chart (not even asked for); the cabinets wrap three to a
      row, each keeping its six doors, the doors sized to fill the row; every locker first, then
      the desk panel at full width and height, then the reserve places. Measured at 1920px before
      and after: the computer's screen is the same to the pixel. Left for 13.1b: «داخل باشگاه»'s
      table still scrolls sideways (check-out on a phone goes through the map). Tests: the drawer
      (opens, closes on a page and on ✕, absent while the password must change, never opens on a
      computer), the panel's place on each screen, the chart neither drawn nor fetched on a phone,
      and an end-to-end run at 390×844 with touch: check-in and check-out on the map, no sideways
      scroll, the drawer. No package added, no API change
- [x] **13.1b Tables become cards** below the breakpoint instead of scrolling sideways
      (1405/07/20). One rule in the shared `Table` for all eighteen lists, as `max-lg:` classes:
      the headings hide and every row is a card framed like the desk panel (`tone-edge`), its
      thick edge at the start in the page's tone. A cell's `label` (its heading) is drawn before
      its value by CSS, so the computer never shows it; a cell without one (the row's title, its
      buttons) takes the whole line, and an empty cell is left out. Rows that belong together stay
      one card through `joins`: a payment's items under its heading, a cafe order's lines under a
      debt, and every edit panel under its row. The members page, decided by the developer while
      looking at it (BUSINESS_RULES.md §2): the search beside the title with «عضو جدید» under them,
      and the six filters on one line. Cards have 12px side padding on a phone instead of 24px.
      Found on the way: the dashboard's charts widened the whole page on a phone (a grid column
      with no minimum), now held to the screen; the attendance heatmap still scrolls sideways in
      its own box, on purpose. Measured at 1920px before and after on thirteen pages: the same to
      the pixel, except what moves by itself (the long-stay bars and an animated door on the map).
      Tests: a label stays out of the cell's text, and everything a label
      or a join adds is `max-lg:` only; an end-to-end run at 390×844 checks out from a card on
      «داخل باشگاه», with no sideways scroll there or on the member list, and the filters on one
      line. The phone e2e now measures the frame's scrolling column too (the window alone never
      showed sideways scroll), and its «بستن» taps name the text button, since the dialog's ✕ has
      the same name. No package added, no API change
- [x] **13.1c Forms and dialogs** open full-screen on a phone, including the service-charge panel
      from 6.5.2; touch targets, and the Jalali date picker and MoneyField on a real phone keyboard
      (1405/07/21). Decided by the developer: only a box that holds a form fills the screen
      (`fullScreenOnPhone` on `DialogContent`: check-in and check-out, the visit, the cafe, هوازی
      and the sales); a short confirmation and a sale's "saved" message stay a card. In a full
      box the row of buttons (`dialogActionsClassName`, also on `SaveRow` and the desk's
      `ConfirmButtons`/`CloseButton`) stays at the screen's bottom while the form scrolls, and the
      ✕ stays in the corner. The row's classes act only inside a box marked `data-phone-full`
      (Tailwind's `in-*` variant), so a form shared with a page carries them harmlessly. Touch, on
      a phone only: buttons 44px (small ones 40px), inputs, selects and the date's three boxes
      44px, the date list's rows taller, the ✕ and the date's clear button 40–44px targets. The
      eight problems found on the locker screen after 13.1a, all chosen by the developer: the
      visit's buttons and the cafe's total and «ثبت» stay at the bottom (1, 3); «تسویه یکجا»
      scrolls its form into view (2); doors two cabinets to a row, about 77px, with the holder's
      name (4, chosen over numbers only); the name search's result under the box (5); the move
      map fills the screen (6; moot once moving left the phone, below); the reserve places three to a row (7); amounts, the check-in time
      and a found member's mobile number never break in the middle (8). Then, looking at it, the
      developer took «جابه‌جایی کمد» and «تاریخچه امروز این کمد» off the phone on every page
      (`max-lg:hidden`), which leaves the visit's bar one row: «ثبت خروج» and «لغو ورود»; and
      on a phone «ورود مهمان» and «ثبت ورود مهمان» in the guest's blue, «خارج از سرویس کردن این کمد» red-edged, the visit's name
      larger and bold. The cafe's two columns
      start at `lg` instead of `md` (the same on every computer). Measured at 1920px before and
      after (`git stash`) on eighteen pages and ten dialog steps: the same to the pixel, besides
      the pulsing dot and two new wrappers that sit exactly where their content was. Tests: the
      full box's classes are `max-lg:` only and the button row's act only inside it; «تسویه
      یکجا» scrolls into view on a phone and not on a computer; an end-to-end run at 390×844 with
      touch: the check-in, visit, هوازی boxes fill the screen with no sideways scroll, the move and
      history buttons are absent, «ثبت
      خروج» and the check-out's confirm are on screen unscrolled, an amount typed in Persian
      digits is grouped and spelled out, a birth date is chosen by touch. The real keyboard on a
      real phone is left to the developer (`npm run dev -- --host`). No package added, no API
      change

#### 13.1 follow-up: three small things on the computer (asked by the developer, 1405/07/21)
Found on the computer while 13.1 was measured, and left alone then because 13.1 changed the phone
only. The developer asked for all three, as suggested:
- [x] A date and time read «۱۴۰۵/۰۷/۲۰، ۲۰:۵۱» with the Persian comma. ICU joins them with a Latin
      one, so `formatDateTime` puts the two together from their parts, like `formatLongDate`; every
      table and page that shows a moment changes at once
- [x] The «تسویه یکجا» receipt's button says «باشه». In the visit box the box's own ✕ is «بستن»
      too, and this button closes only the receipt
- [x] Plain number boxes show Persian digits as they are typed, as `MoneyField` does:
      `withPersianDigits` in `lib/forms.ts` wraps a `register`, rewrites English and Arabic digits
      and puts the caret back. On the custom plan's «تعداد جلسات» (the one found) and, the same gap,
      the instalment's two numbers, the sale's quantity and the SMS settings' threshold. What is
      sent does not change: the parsers already accepted both
- [x] Tests: `withPersianDigits` (English and Arabic digits, the caret in the middle, other
      characters left alone), the comma in `formatDateTime`, the receipt's «باشه». No package, no
      API change

#### UX review fixes (asked by the developer, 1405/07/21)
A review of the running app (the check-in timed step by step, a contrast and font-size scan of
seven pages in both themes) found twelve things; the developer asked for all of them. Decided with
the developer: the card after check-in shows both plans, labelled; a session count means
*remaining* everywhere; the renew date stays the server's, only its text is tidied; «سود خالص»
first on the dashboard.
- [x] After a check-in that took the plan's last session while a plan is queued, the box said
      «۶ از ۶» for the queued plan. It now says «این ورود آخرین جلسهٔ اشتراک … بود؛ ۰ جلسه مانده» and
      labels the plan below «اشتراک بعدی (در صف)», in the check-in, the visit and the check-out
      boxes (`useMemberSubscription` finds the visit's plan in the current card's lookback)
- [x] The locker box's matches say what the check-in will meet: «بدهی …», «پلن تمام‌شده»,
      «جلسات تمام», from what the member list already returns
- [x] The second click of a double click (`detail` above 1) is dropped on the desk's confirm
      buttons and the single-visit sale (`singleClick`): each step put its main button under the
      last one, so a double click on a member could confirm, and a third could sell
- [x] «X از Y» is «remaining of total, مانده» everywhere: `SessionsBar` (its bar now empties), the
      subscription history, the visit box (its extra «… جلسه مانده» line went)
- [x] ↓ ↑ and Enter in the locker box's search choose a member (Enter alone with one match).
      Enter still never confirms, and the result box still closes only by hand (both decided
      earlier, left as they were)
- [x] The birthday pink as text: `--party-pink-ink`, 5.75:1 on white (was 2.58). Menu group
      headings and the gym's name 11.2 → 12px, all at full muted contrast (5.24:1); the name on a
      locker door 11 → 12px
- [x] Dashboard: «سود خالص» is the first card
- [x] Member debt card: «جزء به جزء» right beside the amount
- [x] Renew: plan, amount (bold) and start on three short lines instead of one paragraph
- [x] «خروج» on a member row is «ثبت خروج», as elsewhere; the header's is «خروج از حساب»
- [x] Tests: the hints, the keys, the double click, both check-in cards; the session-count and
      dashboard-order tests follow the new wording and order. No package, no API change
- [x] Follow-up (asked by the developer, 1405/07/22): the session count is back to *used*, as
      before the review, and only «۴ از ۱۲» everywhere (first written «۴/۱۲», changed the same day) (`formatSessionsUsed`): `SessionsBar` (its bar
      fills again), the visit and check-in box, the subscription history. Orange still marks 3 or
      fewer left (§7), now also in the visit box, which was red. Tests follow the wording
- [x] Follow-up, same day: in the locker box's matches the phone never breaks onto two lines and a
      long name ends in «…» (the whole name on hover), on the computer too
- [x] Follow-up, same day: every tag is solid again, as before 13.2i (one «پرداخت‌شده» was edged,
      another filled). Same colours as the edged ones (green, red, grey, amber for running out),
      plus the `Badge` variants `warning`, `cardio` («فقط هوازی») and `guest` («مهمان»); «لغو شده»
      and «ابطال شده» red everywhere. `stateEdge` stays for the rows' buttons. The dashboard's
      change tags («… بیشتر از بازهٔ قبل») stay edged

### 13.2 One visual language
Split into four sessions with the developer on 1405/07/19 (2026-10-11), in this order: (a) the
foundation and the frame, every page on the shared components; (b) the coloured dashboard; (c) the
edit forms' save behaviour; (d) the font and figures, a last look at every screen in both themes,
new README screenshots. The brand colour, chosen by the developer the same day: a deeper blue, kept
apart from the green (free, paid, profit) and the red (held, debt, loss) that mean something on
every screen, rather than the gym's green or a neutral black.

- [x] **13.2a Foundation and frame** (1405/07/19). Tokens in `index.css` for both themes: a cool
      off-white page with white cards, greys leaning to the brand blue, the deep blue brand
      (`--primary`), `--sidebar`, three shadows (`shadow-card`, `shadow-raised`, `shadow-overlay`),
      a larger radius, a dialog that fades in (not for anyone who asked for less motion). The frame:
      a sticky header with the gym's logo on the brand blue, «مدیریت باشگاه» and «باشگاه پاسارگاد»,
      the user's initial; the side menu in five titled groups (پذیرش، فروش بوفه، مالی، مدیریت، حساب
      و سیستم) in the order it always had, sticky beside the page; compact enough (rows 32px, the
      clock on one row) that the whole menu fits a 900px-tall screen with no scrollbar (asked by
      the developer). A window under 820px packs it tighter (the `short:` variant, rows 28px), so
      a small laptop or a zoomed page still fits it; one shorter still shows a thin scrollbar
      beside the menu (hiding the bar was tried and dropped: nothing showed there was more below). Shared components: `PageHeader`
      on every page (title, a line on what the page is for, its buttons), `Table` for all eighteen
      lists, `LoadingState`, `EmptyState`, and `Alert` with an icon per kind. The locker map's look
      (§6) is untouched. No package added, no API change
- [x] Colour, typography and spacing tokens; every screen built from the same components (13.2a)
- [x] **13.2b The coloured dashboard** (1405/07/19). Brought back from the tag
      `dashboard-colour-v1` as it is, not redesigned (`docs/design/dashboard-colour.md`): the green
      banner with the logo and the range pills (kept green, not the brand blue: chosen by the
      developer the same day), a tone and an icon per card, list and chart, the receivables in one
      card with a bar by age, the skeleton cards while loading. The content made since the tag
      stays: «دریافتی متفرقه» gets a new `--tone-rose` and the Package icon, and the staff table
      keeps the shared `Table` of 13.2a. The cards use 13.2a's `shadow-card` and `shadow-raised`
      instead of their own shadows. No package added, no API change
- [x] Decide whether the whole app gets a dark theme: yes, as a switch in the header, light by
      default (6.5.22)
- [x] The Persian font renders numbers and text consistently across screens (13.2d)
- [x] Empty states, loading states and error states that look deliberate (13.2a, 13.2d)
- [x] **13.2d Font, figures and a last look** (1405/07/19). Every page read in the browser for digits
      that reach the screen unformatted: two did, the sessions column of a member's plans («1 / 21»,
      now through `formatNumber`) and the four threshold boxes of «تنظیمات پیامک» (now filled with
      `toPersianDigits`, like the instalment boxes). Every thousands separator is the Persian «٬»,
      no Arabic-Indic digit anywhere, and every element is set in Vazirmatn, charts included. The
      four pages that still said «در حال بارگذاری…» or their error as grey page text (ویرایش عضو,
      پروفایل عضو, تنظیمات قیمت, تنظیمات پیامک) use `LoadingState` and a red `Alert` like every
      other page; the app-level guards keep `PageMessage`. The light screenshots showed one fault in
      the restored dashboard: on a 1440px screen «بدون تغییر نسبت به بازهٔ قبل» ran into the
      breakdown's «انتقال»; the change line now keeps clear of the breakdown column like the figure
      and the hint do. New README screenshots in both themes
- [x] Every edit form saves the way «تنظیمات پیامک» does since 1405/07/16 (asked by the developer):
      «ذخیره» always enabled; pressed with nothing changed it sends nothing and says «تغییری
      نداده‌اید»; every outcome (saved, nothing changed, someone else saved, a server error) is shown
      beside the button, not at the top of a page the user has scrolled away from; an edit clears it.
      Forms that add something keep their own messages
- [x] **13.2c Edit forms' save behaviour** (1405/07/19). The SMS page's pattern made shared:
      `SaveRow` (the buttons and the last outcome beside them, one at a time: someone else saved, a
      server error, nothing changed, saved), `StaleAlert`, and `useUnchanged` (the request the
      values would make against the one they opened with, so «۷۵٬۰۰۰» over «75000», or a value
      changed and put back, is no change). On: تنظیمات پیامک, تنظیمات قیمت, ویرایش عضو, ویرایش
      هزینه, ویرایش چک و قسط, ویرایش محصول بوفه, ویرایش مبلغ خدمت, and the two category renames
      (whose one-line «تغییری نداده‌اید» sits right under the box and its buttons). Forms that close
      on a save keep closing, with the message they showed before (the developer's choice, the same
      day); «تغییرها ثبت شد» beside the button is for the two settings pages, which stay open. A
      form shared by adding and editing takes `editing`; adding is unchanged. No package added, no
      API change
- [x] **13.2e Colour on every page** (asked by the developer, 1405/07/19: only the dashboard is in
      colour). Its own session, on `task/13.2-visual-language`, before 13.2 is released, so the panel
      changes once. Confirmed by the developer the same day: level 1 (level 2 asked after seeing
      it), the tones of the table below, the brand blue for the active item, and the strip on
      **every** card (not only the main ones). Built: the menu in `app/navigation.ts` with a tone
      per group and `sectionFor`; `AppShell` sets `--tone` on `<main>` and hands the menu item's
      icon to `PageHeader` (`PageSectionContext`); the menu's icons in their group's tone with a dot
      by each group's title; `PageHeader`'s icon in a tinted square (grey in «حساب و سیستم»); every
      `Card` with a 4px strip in the page's tone (`tone-strip`, a background, so no dropdown inside
      a card is cut off), the dashboard's two own-coloured cards opted out (`accent={false}`). Asked
      by the developer while looking: the dashboard's banner and range pills from green to violet,
      the colour of «مالی» beside it. Tones moved to `lib/tone.ts`. No package added, no API change
- [x] **13.2f Level 2 and a modern side menu** (asked by the developer, 1405/07/19, with a picture
      of a glowing menu to take ideas from, not to copy). Level 2 of the 13.2e plan: `EmptyState`'s
      icon circle in the page's colour (`tone-soft` and `tone-ink` now fall back to grey without a
      `--tone`), and a shared `StatTile` (`components/StatTile.tsx`) for the totals of «چک و قسط»
      (three tiles over the line that explains them) and of «تاریخچه» (each tab's totals), with
      the same accessible names as before. The side menu became a floating panel: the gym's logo
      and name moved from the header into it, above the clock and the menu; a 1px edge from the
      brand blue into violet, a soft shadow by day and a blue and violet glow at night (tokens
      `--sidebar-edge-*`, `--sidebar-glow`, utility `sidebar-panel`); group titles in their own
      colour; the active item a brand-blue gradient with a faint ring and a bar on its start edge.
      The header covers only the content column. The whole menu still fits a 900px-tall window
      (it needs about 860px, as before). No package added, no API change
- [x] **13.2g The sign-in page and the last loose ends** (asked by the developer, 1405/07/19, after
      a look for what 13.2 had left; mobile and the public site put aside for now). The sign-in page,
      untouched since Phase 1, takes the frame's look: the logo in the menu's blue-to-violet square
      over «باشگاه پاسارگاد», a soft blue and violet glow behind the card, the card raised with a
      blue-to-violet top edge. The app-level guards: «در حال بارگذاری…» with the turning circle
      (`PageLoading`), «اجازهٔ دسترسی به این بخش را ندارید.» under a lock in a circle (`PageMessage`).
      The member's profile, the one working page without `PageHeader`: the name is its title, the
      active/inactive badge (and, when inactive, why it matters) the line under it, «ویرایش» and
      «غیرفعال‌سازی» its buttons, the details in a card «مشخصات»; its three empty lines (no visit,
      no subscription, no debt) are `EmptyState`s. Found while looking: the profile's «اشتراک‌ها»
      table, the one list whose rows were still bare `<td>`s, ran its start date, end date and
      sessions together with no space between them; its rows now use `TableRow` and `TableCell`
      like every other list. The friendly message during an API-only restart
      (Phase 5's note): a 502, 503 or 504 now reads «سرور در حال راه‌اندازی دوباره است؛ چند لحظه
      دیگر دوباره امتحان کنید.» on every form and page, the sign-in page included, instead of the
      general error. Unchanged: a session whose refresh hits the restart still goes to the sign-in
      page. No package added, no API change
- [x] **13.2h Page by page, from the developer's notes** (asked by the developer, 1405/07/20: the
      panel did not read as one design). Three mock-ups of the locker page were drawn first and all
      turned down; the developer then marked up screenshots on a canvas instead, and each note is
      applied in the code and shown back as a new screenshot. One or two pages done together this
      way, then the rest follow the same rules. Done so far, on «ورود با کمد» and the frame:
      a banner like the dashboard's (`PageHero`) in the page's menu-group colour, darkened only in
      lightness (`hero-tone`), slim, the gym's logo at its far end, the lockers' counts on a card
      inside it with the name search and «نقشهٔ استفاده» at that card's end; under each menu group's
      title a line in its colour that drops from the dot and runs straight to the far edge; a 48px
      header and less room above the page; the desk panel's top line in the banner's gradient.
      Then, the same day (the menu "did not belong to the page beside it"): the side menu lost its
      floating panel, edge and glow and sits on the page's own colour with no line beside it, its
      logo row as tall as the header so the header's line runs under it; group titles and icons
      grey, the colour only on each group's dot and line; the menu's marks («به‌روز شد»,
      «آزمایشی», «جدید») and the user's initial removed. One edge for everything, like a locker
      door's (`tone-edge`): a faint frame and a thick start edge in the page's tone, on the active
      menu item, the banner (now one slim card-coloured row, no fill or glow, the logo masked in the
      tone; `hero-tone` removed), the name search, «نقشهٔ استفاده», «ورود بدون کمد» and the hourly
      chart's legend (now beside its title). Frameless on the page's colour with only that edge: the
      doors (BUSINESS_RULES.md §6: no frame or face of their own), the desk panel, the open «ورود
      بدون کمد» box, the hourly chart, and a `Card` with `edge`. A free door tinted faintly green
      from its corner and its number a soft grey; the «بدهکار» tag red letters on a faint red (§6).
      The header: the cheque alert at the end, edged orange (red past its date), then the user, the
      theme and «خروج» in one box. «داخل باشگاه», «تاریخچه» and «اعضا» take the banner (the
      members' search and «عضو جدید» on it) and `Card edge`; `StatTile` is edged too (so «چک و
      قسط»'s totals as well). Found and fixed: the hourly chart's screen-reader table (`sr-only`
      on a `<table>`, which does not collapse) made the page about 685px taller than the layout, so
      the sticky menu was pushed up at the bottom of the locker page; the table is now inside a
      hidden `<div>`. The average's label («میانگین ۱ دوشنبه باز در ۴ هفتهٔ گذشته») stays as it is,
      once explained (the developer's choice). Left: the rest of the pages. No package added, no API
      change
- [x] **13.2i Pages in the panel's look: «داخل باشگاه», «تاریخچه», «اعضا», then «فروش بوفه»** (asked by the
      developer, 1405/07/20–21, from marked-up screenshots). The rules, now in `web/src/lib/edges.ts`
      so every page draws them the same way: a table row is edged at its start in the page's tone
      (`tone-row-edge`, a curved bar on the first cell; a history settlement spans its items with
      one); a row's buttons and tags have no fill and a thick edge at their end in a state's colour
      (`stateEdge`: green for «ثبت خروج», «داخل باشگاه», «پرداخت»/«پرداخت‌شده»; red for «لغو ورود»,
      «پرداخت‌نشده», «استرداد», a member's debt and «تک‌جلسه»; amber for «پلن تمام‌شده»; grey for
      «خارج شده» and «پرداخت جزئی»); filter boxes (dates, member, the dropdowns, the members'
      search) sit on the page's colour, edged in the tone with the thick edge at their end
      (`filterEdge`, `tone-edge-end`), each as wide as what it holds; the chosen tab looks like the
      active menu item (start edge) and the chosen choice of a filter group like a filter box (end
      edge, `choiceClass`). The history's payment-status tag is edged there only (`edged`), the
      cafe, profile and locker box keep the filled one for now. «عضو جدید» is edged in the tone on
      the banner. Shared changes: the date calendar floats over the page instead of pushing the
      table down, on the page's colour with the tone's edge; a `<select>`'s open list is styled
      where Chrome allows it (customizable select, 135 and later: the page's colour, the tone's end
      edge, the chosen row in the tone; older browsers keep their own list); the banner's logo
      sits at its far end on every page; the page column scrolls instead of the window, so the
      scrollbar is on the left, away from the menu (the column is `relative`, so the hourly chart's
      hidden text cannot make the window taller). Kept as asked: «لغو ورود» stays on every row
      (the 30-minute window is checked by the API only). Left: the member profile and the other
      pages, then the light theme, which this work was not checked in: the tone's edge, mixed with
      the near-black text colour, turns almost black there, and the page and the cards are nearly
      the same white. Then «فروش بوفه»'s three pages the same way (asked by the developer,
      1405/07/21: "exactly like the reception pages"): the banner on each, the other cafe pages'
      links on it edged in the tone like «عضو جدید» (`toneAction`, the members' page moved onto it);
      `Card edge`; the product and member searches, the date boxes and «نمایش دسته‌بندی» as filter
      boxes; «فقط پرداخت‌نشده — مهمان» a filter choice instead of a checkbox; each product on the
      till a locker door (start edge in the tone, its count in the cart an edged tag); the orders'
      and the menu's rows edged, their tags and buttons in `stateEdge` («پرداخت‌شده», «موجود»,
      «ثبت پرداخت» green; «پرداخت‌نشده», «لغو شده», «لغو», «حذف» red; «ناموجود» and «مهمان» grey); a
      category carries only «ناموجود», no «موجود» on every shelf (asked by the developer).
      Through the shared parts, the member profile's orders and the locker box's product buttons
      take the same look. A form's main button («ثبت سفارش», «افزودن», «افزودن محصول», «ذخیره») edged in the tone too
      (`toneButton`, asked by the developer: the blue was the one colour out of place), and the new
      product's box edged like the cards. A banner's tools sit at its far end, beside the logo, on
      every page (asked by the developer for «بازگشت به بوفه»). The menu's
      group line and dot are now drawn in the edges' colour (`tone-ink`) instead of the raw tone,
      so «فروش بوفه»'s line matches its pages (asked by the developer). The tone and the text colour
      are now mixed in `oklab` instead of `oklch`, which had pulled the orange towards pink; every
      group's edges keep their own hue. The header's cheque alert, while a payment is only close, is
      a new `--tone-yellow` instead of the amber `--warning`, so it is not read as the cafe's orange
      (both asked by the developer; `--warning` itself is unchanged). Then «مالی»'s three pages
      the same way (asked by the developer, 1405/07/21: "like «پذیرش» and «فروش بوفه»"). «هزینه‌ها»
      and «چک و قسط»: the banner, «ثبت هزینه» and «ثبت چک یا قسط» on it as `toneAction`, `Card
      edge` for the lists, the new boxes and «دسته‌بندی‌ها»; the expenses' dates and category as
      filter boxes and their total an edged `StatTile`; «چک و قسط»'s status as tabs (start edge)
      and its kind as a filter's choices (end edge); rows edged, tags and buttons in `stateEdge`
      («پاس شد»/«پرداخت شد» and paying green; «در انتظار» amber; «سررسید گذشته», «باطل شده»,
      «ابطال» and «برگشت به در انتظار», which undoes a payment, red; «از چک و قسط» grey); the
      forms' main buttons `toneButton`, the red confirmations («تأیید ابطال») kept as in the cafe.
      The dashboard "exactly like the others" (the developer's choice): the violet banner
      (`DashboardHero`, `--hero-*`) gave way to `PageHero`, the range beside the title; the
      presets and «دلخواه» are a filter's choices under it and the dates filter boxes; the stat
      cards, «کل مطالبات» and its buckets edged like `StatTile`; «خلاصهٔ بازه»'s cards all in the
      page's violet (asked by the developer; `StatCard`'s tone is optional now, and without one it
      takes the page's), except «هزینه‌ها» orange and «سود خالص» green (asked by the developer,
      after trying the sale tiles' colours on the cards of the same name and turning them down);
      «امروز»'s each in its own tone for now; the change since the range before
      an edged tag; the charts,
      «نیاز به اقدام» and its lists `Card edge` / edged, a past-due cheque a red start edge
      instead of a fill; the staff table's rows edged. The coloured version stays in the tag
      `dashboard-colour-v1` (docs/design/dashboard-colour.md). No package added, no API change
- [x] **13.2j The light theme and the member profile** (asked by the developer, 1405/07/21: "the
      dark theme is done, do the light one"). The light theme, without moving the dark one: edges
      and lines (`tone-edge`, `tone-line`) keep the tone's full colour on white and stay mixed
      towards the light ink on the dark page (`--tone-edge-share`); green and amber text uses new
      `--success-ink` and `--warning-ink`, mixed towards the ink on white, where the plain colours
      were too faint to read (amber from about 2.8:1 to 4.8:1); text in a tone stays `tone-ink`.
      The menu's group line leaves the end of its title, bends once and runs to the far edge (a
      second sketch from the developer). Then the member profile, «عضو جدید» and «ویرایش عضو»
      like «اعضا» (asked by the developer, from a marked-up screenshot): full width, the banner
      with the member's name and «فعال», «ویرایش» on it as `toneAction` and «غیرفعال‌سازی» red
      with no fill (green «فعال‌سازی»); «مشخصات» and «بدهی» at the start facing «اشتراک فعلی»
      on a wide screen; every card `Card edge`, the tabs `choiceClass`, every list's rows edged;
      the visit's and subscription's tags in `stateEdge` («باز», «فعال» and «پرداخت» green;
      «جلسات تمام‌شده» amber like «پلن تمام‌شده»; «لغوشده» and «استرداد» red; «منقضی», «فریز»,
      «در انتظار شروع» and «بسته» grey) and the payment status edged; «ثبت پرداخت» and
      «تأیید پرداخت» green, «لغو اشتراک» and «استرداد» red with no fill; «فروش اشتراک», «تأیید
      تمدید» and the member form's button `toneButton`. Kept as they were: «تأیید فروش» and
      «تسویه یکجا», which also open in the locker box, a dialog with no page tone, where a
      `toneButton` would turn grey; the red confirmations. The other pages («مدیریت», «حساب و
      سیستم») keep their look: the developer chose not to change them. No package added, no API
      change

#### 13.2e plan

**The level.** Three were discussed: (1) a colour per menu group, carried into each page's header
and main cards, tables and forms left calm; (2) also the summary boxes as dashboard-style stat
cards and tinted empty states; (3) a banner on every page like the dashboard. Recommended: **1,
with the two parts of 2 as optional steps at the end**; 3 is too much for working screens (the
desk must stay fast, a 160px banner only takes room). *Confirm the level.*

**The rule that bounds it.** Green and red mean something on every screen (free / paid / profit,
held / debt / loss), amber means attention (`--warning`), and the brand is the deep blue. A page's
colour is decoration only: never green, red or amber, always beside words, `aria-hidden` on every
tinted icon. The locker map's look (BUSINESS_RULES.md §6) is untouched; the dashboard keeps its
own tone per card.

**Tones per menu group**, from the dashboard's `--tone-*` (each already has a dark value).
*Confirm:*

| Group | Tone | Why |
| --- | --- | --- |
| پذیرش | sky | apart from the brand blue, calm for the screen used all day |
| فروش بوفه | orange | the same hue as the «بوفه» tile in the locker box |
| مالی | violet | the dashboard's charts section is violet already |
| مدیریت | teal | |
| حساب و سیستم | none (muted grey) | two utility pages |

**Steps**

1. *Shared tones.* Move `features/dashboard/tone.ts` to `src/lib/tone.ts` (`Tone`, `toneStyle`,
   `toneColorStyle`) and update the dashboard's imports; move the tone tokens' comment in
   `index.css` from "the dashboard's accents" to "the panel's accents". No new token.
2. *One source for the menu.* Move `navigation` (groups, items, icons, roles, badges) out of
   `AppShell.tsx` into `src/app/navigation.ts`, add `tone?: Tone` to `NavigationGroup`, and a
   `sectionFor(pathname)` that returns the group and item a path belongs to by its longest
   matching prefix (`/members/new` and `/members/:id/edit` → «اعضا»; `/` matches only itself).
   Unit tests for `sectionFor`.
3. *The page's tone from the frame.* `AppShell` sets `--tone` on `<main>` with `toneStyle` from
   `sectionFor(location.pathname)`, so every page inherits its group's colour without each page
   saying it. A page outside the menu, or in «حساب و سیستم», gets no tone, and everything below
   falls back to the muted look.
4. *Menu.* Each group's icons in its tone (`tone-ink`), its title with a small tinted dot. The
   active item keeps the brand highlight it has now, so "where am I" stays one colour.
   *Confirm: brand highlight for the active item, or the group's tone.*
5. *PageHeader.* An icon in a tinted square (`tone-soft tone-ink`, `aria-hidden`) before the
   title, the same shape as the dashboard's `SectionHeading`. The icon comes from the menu item
   by default (`sectionFor`), so the menu and the page cannot disagree; a page can pass its own.
   The locker page gets it too, above the map, which is not touched. *Confirm.*
6. *Cards.* An `accent` prop on `Card` draws the 4px strip on top in `var(--tone)`, as the
   dashboard's stat cards do. Given to each page's main card(s) only (the list or the form the
   page is for), not to cards inside dialogs or the member profile's smaller cards.
   *Confirm: main cards only, or every card on the page.*
7. *Optional (level 2).* `EmptyState`'s icon circle in `tone-soft tone-ink`; the summary boxes
   (چک و قسط's totals, the history's totals) as dashboard-style stat cards. Each its own step,
   done only if the developer wants it after seeing 1 to 6.
8. *Look and record.* Every page in both themes in the browser (the developer's Chrome forces
   its own dark mode on light pages; check light through `npm run screenshots`), a phone width
   glance, `npm run screenshots` for the README, ROADMAP, ARCHITECTURE's one-visual-language
   rule (the page's tone comes from the frame), LEARNING 13.2e.

**Tests.** `sectionFor` (each route, the nested member routes, `/` not matching everything); the
menu still lists the same items per role (the existing AppShell tests); PageHeader's icon is
hidden from screen readers; no test may look for a class name. Frontend suite, `format:check`,
lint. No package, no API change, no backend file.

**Known limit.** The backup tablet's Chrome 109 cannot show `oklch` or `color-mix` (already true
of the whole app; the fix agreed is a newer browser, not code); the tints simply do not show there.

### 13.4 The panel on the phone's home screen (PWA)
Asked by the developer on 1405/08/03 (2026-10-25), opening the closed panel for it: «نصب» in Chrome
made only a shortcut (Chrome's badge on the icon, a browser tab with an address bar), because the
panel had no manifest. For the Owner's phone and the Staff's, Android and iPhone both. Branch
`task/13.4-panel-pwa`.

Decided by the developer: the **dark** icon (of a dark and a light one rendered for them), the name
**«Management»** under the icon (in full «Pasargad Gym Management»; the members' app of Phase 15
will be «Pasargad Gym»), and Claude's proposals for the rest:
- [x] Icons from the brand logo, in the favicon's bronze on the dark theme's background: 192 and
      512 (`any`), 512 `maskable` (the logo inside Android's safe circle), 180 for the iPhone (no
      transparency there). Rendered once in the installed Chrome with Playwright; no package
- [x] `web/public/manifest.webmanifest`: standalone from `/`, `dir: auto` (English names, Persian
      shortcuts), dark opening screen; three long-press shortcuts (Android only) to pages Staff
      can open too: «ورود با کمد», «داخل باشگاه», «اعضا»
- [x] `index.html`: the manifest, the iPhone's icon and title, `theme-color`; the status bar follows
      the theme (`theme-init.js` before the first paint, `applyTheme` on every switch)
- [x] **No offline mode and no service worker** on purpose: the panel shows money and who is
      inside, which must never be an old copy, and a cache could keep an old version after a
      release. Chrome no longer needs a service worker to install
- [x] Caddy: the manifest as `application/manifest+json` (Caddy has no type for `.webmanifest`),
      `no-cache` like every file outside `/assets`. The CSP (`default-src 'self'`) already allows it
- [x] Tests (integration, reading the files): the manifest's display, names, every icon's real
      size, Android's three icons, the shortcuts, `index.html`'s tags and their order, the
      iPhone's icon, both colours written alike, Caddy's type. Frontend: the status bar follows
      `applyTheme`; every shortcut has an icon (Android may drop one without); the four copies of the
      two colours agree. End-to-end (`install.spec.ts`): **desktop** Chrome's own installability
      check (`Page.getInstallabilityErrors`) finds nothing missing on the production stack, with no
      service worker, the only error being `in-incognito` (Playwright's contexts are off the record,
      where Chrome installs nothing); the manifest and the icons are served with their types.
      `npm run e2e`: the four front-desk tests and the three new ones pass. Android's install, its
      shortcuts and the iPhone's status bar are checked on the phones after the release
- [x] **Released 1405/08/03 (2026-10-25), 03:44, as `20261025-0344-23fdcdd`** (previous
      `20261025-0043-0d08196`), `./backup.sh run` first and the dump copied to `D:\gymbackups`.
      The first `release.sh` stopped at "Building the migration bundle … Build failed": the
      developer's personal site's API was running in another `dotnet` process; with it stopped and
      `dotnet build-server shutdown`, the second run went through (the server is untouched when the
      bundle fails, since nothing is shipped before it). After it, from outside: the manifest 200 as
      `application/manifest+json` with `no-cache`, the icons 200 `image/png`, the panel's page carries
      the manifest and `theme-color`, the panel and `/health` 200, the public page 200, www and the
      .com 301
- [x] The developer's Android phone (Samsung, 1405/08/03): the first install gave the dark icon
      and «Management» but with Chrome's badge: Google's WebAPK server did not answer from Iran and
      Chrome fell back to a shortcut. Installed again with a VPN on: the app, without the badge. The
      steps are in docs/DEPLOYMENT.md *The panel on a phone*
- [x] The Staff's phones, by those steps (Android with a VPN for the install; an iPhone in Safari,
      signed in once inside the app); the three shortcuts on Android and the iPhone's status bar
      looked at: everything works (the developer, 1405/08/03). 13.4 is done

Not changed: every screen. Only `<head>` tags and files nobody sees in a page, so the computer's
screens stay as they are; a desk computer's Chrome may now offer to install the panel too.

Closed 2026-10-25: 2723 backend tests (965 domain, 1758 integration; 11 new) and 1194 frontend tests (1 new)
green, zero warnings, lint, `format:check` and production build pass; `npm run e2e` (7, 3 new)
passes; `code-reviewer` found nothing to fix and four improvements, all applied (shortcut icons,
one test for every copy of the colours, the iPhone's status bar and the desktop-only e2e said as
they are). No package, no API change, no migration; nothing in `src/`, so a rollback to
`20261025-0043-0d08196` is safe.

Queued by the developer the same day, not started:
- **The locker map on a phone:** the developer asked whether smaller doors would fit the map on one
  screen. The doors are about 77px with the holder's name under the number (13.1c, the developer's
  choice over three cabinets to a row with numbers only); smaller doors would lose the names. To be
  looked at again from a screenshot with occupied lockers before anything changes

### 13.5 Today's summary on the Owner's phone (خلاصهٔ امروز)
Asked by the developer on 1405/08/03 (2026-10-25), right after 13.4: a home-screen widget is
something a web app cannot have, so a page instead, one press from the icon. Branch
`task/13.5-live-summary`. Rules: BUSINESS_RULES.md §12 *Today's summary*.

Decided by the developer, from a mock-up at a phone's width (light, dark and Android's long-press
menu): the figures are «داخل باشگاه» now, «ورود امروز», «دریافتی امروز» and the plans and single
visits sold; **Owner only**; a page of its own in the menu plus a long-press shortcut, the first
screen staying the locker map. Asked when the page opens, when the phone comes back to it and on
«به‌روزرسانی», **never on a timer** (first proposed every 15 seconds; the developer asked why it could
not simply ask on opening, and chose that with a button). Claude's proposals, confirmed: guests
count as inside, said under the figure; a Staff member who opens the shortcut lands on the locker
map, not on a refusal.
- [x] `GET /api/reports/today` (Owner only, in the reports group): the money and the visits come
      from the dashboard's own handlers for a range of one day, as the daily report SMS does, so the
      three never disagree; "inside" counts open visits in one query, guests apart
- [x] «خلاصهٔ امروز» at `/today`: the menu's «مالی» group for the Owner; one wide card for who is
      inside, one for the money, three small ones side by side; the time of the figures at the top;
      a failed refresh keeps the figures and says how old they are. `refetchOnWindowFocus` for this
      query only (the app turns it off for the desk)
- [x] `RequireRole`'s `otherwise`: Staff are sent to the locker map from `/today`
- [x] The manifest's fourth shortcut, «خلاصهٔ امروز», first in the list
- [x] Tests: integration (a busy day: one inside, one left, one cancelled, a guest, matched against
      the dashboard's reports; nothing yet gives zeros; Staff 403; no token 401; the manifest's
      shortcuts); frontend (the figures in Persian digits, no guest line without guests, «به‌روزرسانی»
      and the page becoming visible each ask again, a failed refresh keeps the figures, Staff sent
      to the map without a request, the menu item for the Owner only)
- [x] The page shown to the developer at a phone's width with real-looking figures, before the
      release (asked by the developer). Light, dark and the menu, from the screenshots' demo stack;
      the developer renamed the button «تازه کن» to «به‌روزرسانی»
- [x] `code-reviewer`: nothing to fix; applied: the shortcut sends to the locker map only when the
      role is known (a failed `/me` keeps the Owner on the page), the date stays on screen when a
      refresh fails (figures left from yesterday cannot pass for today's), and a test that a visit
      still open from yesterday is inside but no check-in today
- [ ] Released, late at night with `./backup.sh run` first; then the Owner's phone. The page is
      there at once (menu); the new long-press shortcut needs Android to rebuild the installed app,
      which Chrome does through the same Google server as the install (13.4), so from Iran it may
      take opening the app with the VPN on once, or installing again with it (docs/DEPLOYMENT.md
      *The panel on a phone*). An iPhone has no long-press shortcuts

Closed 2026-10-25: 2728 backend tests (965 domain, 1763 integration; 5 new) and 1202 frontend tests
(8 new) green, zero warnings, lint, `format:check` and production build pass. No package, no
migration; one new endpoint, read only, so a rollback to `20261025-0344-23fdcdd` is safe.
`npm run e2e` and `npm run screenshots` are left for the release night.

---

## Phase 14 — The public site (سایت اصلی)
Moved out of Phase 13 (it was 13.3) on 1405/07/26 (2026-10-18), asked by the developer: the public
site at `pasargadgymplus.ir` is its own work and has **no link to the panel** for now; the developer
says when something does. Static, no API. Its work goes on its own branch and is released on its
own, never inside the SMS release (*Release order* at the top). 14.0 is what was built and released;
the open work is 14.1 onwards.

### 14.0 Built and released (was 13.3)
- [x] Replace the placeholder on the apex with a real page for the gym: hours, address, contact,
      services. Static, no API. Live since the release of 2026-10-15
      - 1405/07/22 (2026-10-14), branch `task/13.3-public-site`: first page built (direction "Stone and
        steel"), buttons still do nothing; placeholder Unsplash photos in `web/public-site/img/`, to be
        replaced by the gym's own; address, telephone and Instagram still «به‌زودی». The public CSP
        now allows the site's own script (`script-src 'self'`). Not released.
      - Same day, second pass: a hero slider of athletes (4 s, arrows, swipe) between two layers of
        drawn bronze smoke, the background lit by the pointer; the real logo (the panel's); photos for
        every service (massage and analysis still «عکس به‌زودی»); a wide photo; a schematic plan of the
        1000 m² hall with each zone's floor; the phone's bottom bar. Services and floors from the gym.
      - 2026-10-15, third pass: the plan is now the gym's own drawing (lobby, rooms, walls, two doors)
        and a walk on scroll: the stage stays on screen, a bronze line goes from the door through 13
        stops (bodybuilding four, CrossFit two), the lit room's photo fills the screen and its name
        rises in; a click on the plan goes to the nearest stop. One hard rubber floor everywhere. The
        address and telephone are in (tel: links); Neshan/Balad links and Instagram still to come.
        Every service has a photo (massage and analysis added); `tour-*.webp` are 1920×1080 crops for
        the stage.
      - Same day, by the developer: «امکانات» and the tour showed the gym twice, so they are one
        section now, «امکانات و فضای باشگاه»: each stop's line is its service's description, the
        list of services and its photos are gone. Floors: club rubber everywhere. The opening
        line mentions the air («هوای خنک و تازه»), nothing more about it on purpose.
      - Same day, from the gym's next drawing: «استیج فیگور», a narrow room on the right wall above
        CrossFit (which got smaller), is stop ۱۲ of the walk; the walk now has 14 stops. The opening
        line is the developer's own, with its four points in a row of signs that draw themselves
        and move (a hall, a lamp, a door at ground level, a fan); the numbers sit on barbell plates.
      - Same day: a «ثبت‌نام» button in the top bar (phone and computer). It does nothing yet; what
        it opens is the member accounts phase below.
- [x] Released in `20261015-1423-4f9347d` (previous `20261010-2339-7ebfb0d`) on 2026-10-15 at
      14:23, by day at the developer's choice: the panel's look did not change (nothing of 13.2's
      branch is in `main`); it also carries 6.5.37 and the SMS settings wording. Before it: e2e
      green, frontend 1018/1018, lint, format and build clean, backend Release build with no warning,
      2190 of 2191 tests (the one red is the demo seeder's, below in Phase 12; it never runs on the
      server), screenshots retaken (`4f9347d`). `./backup.sh run` first →
      `gym-20261015-141449.dump`, copied to `D:\gymbackups`, both `sha256sum` equal. No migration.
      After it, from outside: `pasargadgymplus.ir` 200 with the new page, `Cache-Control: no-cache`
      on the page and `site.js`, `www.` and `.com` 301 to it, the panel and `/health` 200, no error
      in the API log. A rollback to `7ebfb0d` is safe: the schema did not change. The first
      `release.sh` run stopped before reaching the server: a local `dotnet run` of the API locked
      `bin/Debug` and the migration bundle could not build
- [x] Released in `20261016-1717-7ed7006` (previous `20261015-1423-4f9347d`) on 2026-10-16 at
      17:17, by day at the developer's choice: the public site's phone fixes, the six faceless
      slides, «پاسارگاد پلاس» without «جیم», calisthenics; the panel only gained its tab icon. No
      migration, so a rollback to `4f9347d` is safe. Before it: frontend 1018/1018, lint, format
      and build clean, e2e green, screenshots retaken (`7ed7006`); no backend test, the backend
      unchanged since the last release. Server first: API and Postgres healthy, 50 GB disk and
      2.8 GB memory free. `./backup.sh run` → `gym-20261016-171258.dump` (593 KB), copied to
      `D:\gymbackups`, both `sha256sum` equal. After it, from outside: the public page 200 with the
      new content, `site.css`/`site.js` `no-cache`, the panel, its icon and `/health` 200, `www.`
      and `.com` 301.
      - Found after it: a phone that had seen the site kept showing the old slides. The photos have
        no `Cache-Control`, so a browser guesses how long its copy stays fresh (hours), and the new
        slides kept the old file names (my miss: only the page, site.css and site.js were checked).
        Built the same day, for the next release: `no-cache` on every file of the public site in
        `deploy/Caddyfile`, and `web/Dockerfile` stamps each address in the page with its file's
        fingerprint (`?v=`), so even a phone holding an old copy fetches a changed file. Checked
        in a local build: 53 addresses stamped, none left plain, `caddy validate` passes, all 35
        photos load on a phone and a computer
- [x] Released in `20261016-1747-c6ff9ec` (previous `20261016-1717-7ed7006`) on 2026-10-16 at
      17:47, half an hour after the one above, to fix the stale photos it showed: `no-cache` on
      every file of the public site and a fingerprint (`?v=`) in each address of the page. Only
      `deploy/Caddyfile` and `web/Dockerfile` changed; no migration, a rollback to `7ed7006` is
      safe. `./backup.sh run` first → `gym-20261016-174554.dump` (595 KB), copied to
      `D:\gymbackups`, both `sha256sum` equal. After it, from outside: the page's addresses all
      stamped (`slide-1.webp?v=b45d33dca8`, none plain), the page, site.css, the photos and the font
      `no-cache`, a photo asked again with its ETag answers 304; the panel and `/health` 200,
      `www.` 301. The developer's phone, which kept the old slides, shows the new ones
- The server's restart, noted here on 2026-10-16, is now item 4 of *Release order* at the top
- Queued by the developer on 2026-10-15 (what is still open is 14.1 and 14.2):
  - Trainers (مربیان), open in 14.1: the gym has two or three; one card each (photo, name, specialty,
        experience). The gym sends the names and photos later. Later still, a member picks a trainer
        (that needs the member accounts phase). The developer: "we have to work on it a lot"
        - 2026-10-15: the stage is built (section ۰۵): three bronze cards under one light, the one in
          front tilts with the pointer and turns over on a tap (certificates, honours, hours at the
          gym, «انتخاب این مربی، به‌زودی»); arrows, keys and a swipe go round. Every detail says
          «به‌زودی»; the placeholder photos are athletes seen from behind, so no stranger's face
          stands under a trainer's name. Waiting for the gym's names, photos (portrait 4:5, one
          plain dark wall for all) and details
  - [x] Shop (فروشگاه): a showcase of the categories (protein, creatine, supplements, shakers and
        bands, sportswear, perfume), with photos and no prices
        - 2026-10-15: built (section ۰۶): a phone swipes one row of tall shelves, a computer sees a
          grid with protein big; on hover the photo comes closer and a band of light crosses it.
          Placeholder Unsplash photos without a brand on them. Same day, by the developer: no «استعلام
          قیمت» on the shelves; the wide photo «۱۰۰۰ متر. بدون ستون.» and «بدون ستون» in the plan
          removed
  - Neshan and Balad links for «مسیریابی», open in 14.2
- [x] Before the next release, check these first (found by the developer on 2026-10-15; the release of
      that day is live with them):
  - [x] The photos are the computer's photos, and a phone crops them to its tall screen, so they show
        far too zoomed in (the tour's `tour-*.webp` are 1920×1080: a phone sees a narrow strip of
        each). Likely a tall crop per photo for the phone (`<picture>` with a `max-width` source)
        - 2026-10-16: each stop has a 900×1600 `*-tall.webp` cut from the same Unsplash original
          (three with a focus point chosen by eye: the door, bodybuilding 1, analysis), picked by
          `<picture>` below 900 px. The developer chose this over a framed wide photo: the stage
          stays full-screen, and the gym's own photos will need both cuts. The hero's two wide
          slides gained nothing from a tall cut and stay as they are
  - [x] The tour's plan is so small on a phone that its rooms cannot be told apart
        - 2026-10-16: first a window on half the plan that followed the walker; the developer found
          it far too big and wanted the whole plan on screen, so the camera went. Now the whole
          plan nearly edge to edge, every name drawn as large as its room allows (the developer
          wanted the small rooms named too); the figure stage says «فیگور» on a phone, its full
          name does not fit
  - [x] The trainers' cards are too small: each card should be big, close to the whole screen
        - 2026-10-16: on a phone the card is the screen's width less 3rem (at most 26rem)
  - [x] The top bar's links are far too small on a phone. They go: the three-line button opens and
        closes a menu that holds them
        - 2026-10-16: a drawer under the bar from the button's side (the left), over a shade, not
          the whole screen (the developer's correction of a first full-screen menu; then narrowed
          twice, as too wide, to under a third of the screen, `min(30vw, 7.5rem)`); the button
          becomes a cross; a link, the button, a tap on the shade or Escape closes it. On a phone
          the bar's blur became a darker glass, so the fixed drawer is laid out on the screen. The
          links now go to their sections on the computer too (asked by the developer); the hero's
          two buttons still do nothing
  - [x] The panel has no logo in its browser tab: the public site shows the gym's logo there (and in
        Google), but `web/index.html` has no `<link rel="icon">`
        - 2026-10-16: `web/public/favicon.png`, the logo in bronze, 192×192: the brand file is white
          and would not show on a light tab. The public site uses the same file since the same day
          (`img/favicon.png`)
  - [x] 2026-10-16, asked by the developer: six hero slides instead of four. The old ones showed
        one model's face, which could be taken for someone local; the new ones are free Unsplash
        photos where no face shows clearly (one series from one gym, then three dark ones), found
        from the developer's own examples (two of them are among the six). 1100×1650, the
        originals' 2:3, one file for phone and computer. Two new lines, picked by the developer:
        «نور عالی، تهویه مناسب، طبقه همکف.» and «استیج فیگور، برای تمرین ژست‌ها.»
  - [x] 2026-10-16, asked by the developer: calisthenics (کلیستنیکس), for calisthenics people to
        know they can train here. The CrossFit room is split along the hall's length: calisthenics
        above, CrossFit below, and of CrossFit's two stops the first is calisthenics now (a photo
        of rings, which the gym has; the kettlebells photo went). Named in the band of services,
        the hero's line (کراس‌فیت، کلیستنیکس، ...), the description for Google, and the figure
        stage's line (it is beside calisthenics now); «۹ بخش» became «۱۰». Two hero slides
        (a tattooed back, a dumbbell curl) gave way to the gym's own kit: the rings, and the
        pull-up and dip station
  - [x] 2026-10-16, asked by the developer: the name is «پاسارگاد پلاس», no Persian «جیم» anywhere:
        the hero's «جیم پلاس» line is gone and the carved word has a bronze plus; the top bar says
        «پاسارگاد+»; the title, description and footer say «پاسارگاد پلاس»
  - [x] 2026-10-16, asked by the developer: the ticks under the plan fill from the left (phone and
        computer), with the walk that starts at the door on the plan's left; they filled from the
        right, the page's reading direction
  - Checked on 2026-10-16 (before the ticks, the one change meant for both): the computer's page is unchanged at 1440, 1024 and 900 px (full-page
    pixel comparison with the last commit; the few differing pixels are the clock's, the same
    between two shots of the old page)

### 14.1 Trainers (مربیان)
The stage is built (14.0, section ۰۵) with placeholder photos and «به‌زودی» everywhere.
- [ ] Waiting for the gym: two or three trainers' names, photos (portrait 4:5, one plain dark wall
      for all) and details (specialty, experience, certificates, honours, hours at the gym)
- [ ] Put them in; «انتخاب این مربی» stays «به‌زودی» (choosing a trainer needs 14.6)
- [ ] Asked by the developer (1405/08/07, at 15.4's start), for when the photos come: the app's
      trainers' list shows each trainer's photo too, and pressing one opens that trainer's own page
      with everything about them (specialty, experience, certificates); the details then live with
      the trainer (the Owner enters them in «مربیان»), not only on the public page

### 14.2 Finding the gym (مسیریابی و اینستاگرام)
- [ ] Waiting for the gym: its location link for Neshan and Balad
- [ ] «مسیریابی» opens them; Instagram in the footer and contact section instead of «به‌زودی»
      - Instagram known since 2026-10-25: `pasargad_plus` (https://instagram.com/pasargad_plus),
        written on the sign over the gym's door; the developer: the sign makes it the official page
- [x] The streets around the gym in 3D beside the address (section ۰۷), asked by the developer on
      1405/08/03 (2026-10-25), branch `task/14.2-finding-the-gym`; released 1405/08/05 in
      `20261027-0220-387aa1d` (with 6.5.38, 10.19 and 10.20)
      - Traced from a Neshan screenshot the developer sent: the gym is a shed at the south edge of
        the sports complex between شهید کاظمی and ۱۸ متری فرهنگیان, its door on فرهنگیان, عصر انقلاب
        to the east. Drawn by `site.js` (`townMap`) as isometric SVG in the site's colours: no map
        service, no package. The two streets to the door are lit, with light running along them;
        the shed rises and the bubble (logo, «پاسارگاد پلاس») drops in as the section comes on
        screen. Houses and the streets past the traced ones are filler, faded at the edges
      - A tap on the map does nothing yet: it opens Neshan once the gym's link comes (above)
- Ideas from the photos of the gym's front (`D:\GymPicture\IMG_4245–4247.HEIC`, 2026-10-25),
  parked by the developer ("maybe in the future"; no more work on the end of the page for now):
  - [ ] The shed on the map as it really is: walls of brown tiles, a roof of dark grey corrugated
        sheet with a deep eave, the gable towards فرهنگیان (already so). Before it, the yard wall
        along the pavement: white with the black «FITNESS» bodybuilder mural to the west, then a
        low black wall («آکادمی بدنسازی پاسارگاد+») under a black iron railing, and the gate at the
        east end between two black pillars painted with Persepolis guards. The door behind the gate
        rather than in the middle, a black sign with yellow letters, the floodlight on the roof
  - [ ] «این‌جوری پیدامون می‌کنید»: a photo of the front beside the map, IMG_4245 (the whole front)
        on a computer and IMG_4247 (the gate and the guards) on a phone; a tap on the shed opens it.
        IMG_4247 needs straightening; both a little lift in the shadows and a crop that leaves out
        the rubble on the pavement. IMG_4246 repeats 4245 and is not needed. Windows' own HEIF
        decoder (WPF `BitmapDecoder`) opens the HEIC files; nothing to install
  - [ ] Maybe later: the Persepolis guards as a motif of the site (e.g. either side of «ثبت‌نام»)

### 14.3 The gym's own photos
- [ ] Waiting for the gym: photos of its own rooms, kit and slides, to replace the Unsplash
      placeholders in `web/public-site/img/`; each tour stop needs a wide (1920×1080) and a tall
      (900×1600) cut (14.0)

### 14.4 The buttons that do nothing yet
- [x] The hero's two buttons and «ثبت‌نام» in the top bar do nothing. Open: what each does until
      14.6 exists (e.g. call the gym, or hidden). Closed by 15.3b (1405/08/07): «ورود / ثبت‌نام» opens
      the app, the hero's buttons lead to their sections

### 14.5 The panel out of search results
Done 1405/08/02 (2026-10-24), asked by the developer. Checked on the live server first, read-only:
the panel already sent `X-Robots-Tag: noindex, nofollow` on every response (since 6.5.0), but its
`/robots.txt` was the panel's `index.html` (the SPA fallback), and the public page had no
`robots.txt`, no sitemap and no canonical address.
- [x] Confirm `panel.pasargadgymplus.ir` stays out of Google (`noindex` and `robots.txt` on the
      panel's domain) while the public site is indexed
- [x] Panel: a real `web/public/robots.txt` that **allows** crawling, on purpose: a crawler kept
      out by `Disallow: /` never reads the `noindex`, and a page linked from elsewhere can then be
      listed by its address alone. `<meta name="robots" content="noindex, nofollow">` in
      `web/index.html` too, in case the panel is ever served through another proxy (a shared edge
      Caddy in front of the gym, discussed on 2026-10-23, not started)
- [x] Public page: `robots.txt` open to all with the sitemap's address, `sitemap.xml` (one page),
      and `<link rel="canonical" href="https://pasargadgymplus.ir/">`, so www and the .com count
      as the same page
- [x] Tests: `SearchIndexingTests` (the panel's header and meta, no `Disallow` on either host, no
      `noindex` on the public page or its Caddy block, its canonical and sitemap)
- [x] Released with 13.1 and 13.2 in `20261025-0043-0d08196` (1405/08/03). Checked from outside
      after it: both `robots.txt` and `/sitemap.xml` answer 200 with the new files, the panel sends
      the `noindex` header and meta, the public page its canonical and no `noindex`. Search Console
      (adding the site and sending the sitemap) is the Owner's or the developer's, with the
      domain's DNS: not code

Closed 2026-10-24: 2712 backend tests (965 domain, 1747 integration; 7 new) and 1193 frontend tests
green, zero warnings, lint, `format:check` and production build pass; `npm run e2e` (4) and
`npm run screenshots` pass, and the built `gym-web` image holds both `robots.txt`, the sitemap, the
canonical link and the panel's meta. No package, no API change, no migration: nothing in `src/`
has changed since the last release (`6bcb031`), so a rollback to `20261023-2305-6bcb031` is safe.

### 14.6 Member accounts (later)
The «Future — Member accounts on the public site» section below, kept there until the developer
and the Owner decide; it is the one part of the site that would touch the panel (members, SMS
codes, a public API), so it needs an ADR first.
- 1405/08/03 (2026-10-25): decided by the developer and moved to **Phase 15**, together with the
  trainers' work. «انتخاب این مربی» on the trainers' cards (14.1) becomes 15.4.

---

## Phase 15 — Members and trainers on the site (اعضا و مربیان در سایت)
Asked by the developer on 1405/08/03 (2026-10-25), right after the panel was closed: the site's
main job is the work between the gym's trainers and their students, in this order of importance:
the daily timetable and attendance, the monthly assessment, nutrition. Both sign in on the site
with their phone and a one-time code. The decision is ADR 0010 (accepted 1405/08/03) and the rules
are BUSINESS_RULES.md §15, where each line says whether it is decided, proposed or open. Every
*Open* line there is asked at the start of its task, never assumed, and every *Proposed* line is
confirmed with that task's plan.

How it is built:
- On its own branch per task, merged into `main` when the task is green, as Phase 13 was. One task
  per session; a large one (15.1, 15.3) may be split into a and b when its plan shows it.
- **Released in five parts**, each one useful to someone on the day it goes out, each late at night
  with `./backup.sh run` first (docs/DEPLOYMENT.md). The production server holds real data: every
  migration only adds, and no existing member, plan or visit changes.
- Every task ends with green tests, a `code-reviewer` pass and a short LEARNING.md entry, as before.

### 15.0 The decision (docs only)
- [x] ADR 0010, BUSINESS_RULES.md §15 and this phase written, branch
      `task/15.0-site-accounts-and-trainers` (1405/08/03)
- [x] The developer read them and confirmed (1405/08/03): ADR 0010 accepted; the address
      `app.pasargadgymplus.ir`; the program offline; a deactivated member sees only a notice;
      photos under 18 like anyone's; students keep their history when a trainer is removed
- [x] CLAUDE.md's "No public access, no self-registration, no member logins" and §1's "Members
      never log in" changed: the panel is staff-only, members and trainers sign in on the site (§15)
- [x] The QR code on the Anea sheet: a barcode reader cannot open it (the developer, 1405/08/03)
- [x] The developer, outside the code: Kavenegar and Anea answered (1405/08/03). The one-time code
      goes as the verification template `gymExpiring` (same account and API key, sent from the gym's
      dedicated line `9981600378`, Kavenegar said 1405/08/07); Anea gives the PDF only, its Excel export (8 million, once) is
      not bought for now, and the trainer types the numbers (§15)

### Release 1 — Signing in, and «my plan» (15.1 to 15.5)
A member can sign up, sign in and see their own plan; trainers exist and take students. Nothing
of the trainers' tools yet, but nobody signs in to an empty page.

#### 15.1 Site accounts and the one-time code (backend)
- [x] Asked first (1405/08/03), written in §15: 5 digits valid 60 minutes, 5 wrong tries; no new
      SMS while a code is valid, the same code sent again after 2 minutes unless Kavenegar reported
      it delivered; 5 an hour per phone, 20 per 10 minutes per IP, 300 a day for the gym; code SMS
      kept out of the SMS history (count and cost only); 60 days signed in; development sends real
      codes to `Sms:AllowedReceptors`. Branch `task/15.1-site-sign-in`, split in two:
- **15.1a, the one-time code**
- [x] Request and verify a code: expiry, wrong tries, the send again, every limit checked
      before Kavenegar is called; `verify/lookup` with `gymExpiring`, delivery asked through
      `sms/status` (1405/08/04, not released)
      - `POST /api/site/auth/code` (anonymous, 20 per 10 minutes per IP); the check is
        `VerifyCodeHandler`, with no endpoint of its own until 15.1b signs the phone in
      - Tables `sign_in_codes` (one row per phone: a random seed, never the code) and
        `sign_in_code_sends` (each SMS that may have cost money: the limits and, later, the
        panel's count and cost); a new migration that only adds
      - The code is HMAC-SHA256 of the phone and the seed under `SiteAuth:CodeSecret`: locally in
        user-secrets (set on 1405/08/04), on the server **`SITE_CODE_SECRET` in `.env`, to be
        added before Release 1** (compose refuses to start without it; `openssl rand -base64 48`)
      - Each phone is locked (a Postgres advisory lock) for its request, Kavenegar's call
        included, so two presses at once send one SMS; the gym's sends too, so 300 a day is never
        passed; once Kavenegar is called the request cannot be cancelled (found by the review)
      - Code rows hidden on the audit screen with the other sign-in rows; the seed never audited
- **15.1b, accounts and the site's sign-in**
- [x] Asked first (1405/08/04), written in §15: 15 wrong tries in a gym day close the phone until
      the next day (first "three dead codes", changed after the review); no SMS to the Owner when the 300 is reached (the panel shows it,
      15.2); a member's site actions audited as «سیستم»; a linked member is not asked again; 15
      minutes after the code to sign up
- [x] Site accounts linked to a member; a new phone creates the member (name and birth date, §2's
      rules), an existing one links to it and is not asked again (1405/08/04, not released)
      - **The account has no phone of its own**: a sign-in finds the member by the verified phone
        and the account by the member, so a number the desk changes never opens the account for
        whoever has the old number (decided during the task, against "keyed by the phone" above)
      - `POST /api/site/auth/sign-in` (the code; a member signs in, a new phone gets a 15-minute
        sign-up ticket), `sign-up`, `refresh`, `logout`, and `GET /api/site/me`
- [x] The site's own authentication scheme (`SiteAuth:Issuer`, `Audience`, `SigningKey`), refresh
      tokens (`site_refresh_tokens`, the staff's rules through a shared `RotatingRefreshToken`) and
      cookie (`gym_site_refresh`, path `/api/site/auth`); every staff policy and the fallback name
      the staff scheme, the site's `SiteAccount` policy the site's; the API refuses to start with a
      site key, issuer or audience equal to the staff's
      - A deactivated member signs in and `/me` says `isActive: false`, read from the database on
        every call. The "nothing else" half is for the first site endpoint that shows anything
        (15.4, 15.5): it needs an active-member check, not written yet because nothing uses it
      - On the server **`SITE_JWT_SIGNING_KEY` in `.env` before Release 1**, with `SITE_CODE_SECRET`
- [x] The desk changing a member's phone revokes the member's site sessions (`PhoneChanged`), in
      `UpdateMemberHandler`: the panel's one change in 15.1 (§15, decided 1405/08/05)
- [x] Tests (integration): every endpoint the app maps is called with the other kind of token
      (`SchemeSeparationTests`); each limit; linking never duplicates a member; sign-up makes no
      plan and no SMS; a deactivated member's sign-in; refresh, reuse and logout

#### 15.2 Trainers in the panel (reopens the panel)
Split into a (backend) and b (the panel's screens) when its plan was approved (1405/08/05), on
branch `task/15.2-trainers`, made from `task/15.1-site-sign-in` after `main` was merged into it.
- [x] Asked first (1405/08/05), written in §15: a trainer is not a member but may also be one,
      with the same phone; a stopped trainer can be made one again, with a new period; site
      members are marked on the profile only, no tag or filter in the list; the code SMS shown in
      «پیامک‌ها», and the day's 300 in «نیاز به اقدام»

##### 15.2a The backend
- [x] `Trainer` with its `TrainerPeriod`s: make, correct, stop, make again; phones unique among
      trainers; one open period per trainer and no period ending before it starts, in the database
- [x] `/api/trainers` (Owner only): list, create, update (with `version`), stop, restart
- [x] Site accounts are a member's, a trainer's or both (`SiteAccountLinks`); the access token
      carries `site_role` (`member`, `trainer`), and the policies `SiteMember` and `SiteTrainer`
      wait for 15.4; refresh and «me» read the roles again, so a stopped trainer loses the
      trainer's area within 15 minutes; the Owner changing a trainer's phone ends the old phone's
      trainer-only sessions
- [x] The member's profile carries their site account; `/api/sms/sign-in-codes` (Owner only):
      today's code SMS of 300, this Jalali month's count and cost; «نیاز به اقدام» says when
      today's 300 is used up
- [x] Tests: domain (`TrainerTests`), integration (`TrainerEndpointTests`, `TrainerSignInTests`,
      `SiteInThePanelTests`); the 15.1 tests read «me»'s new shape
- [x] Persian messages for the `Trainers.*` codes in `errors.ts` (the error catalog test asks for them)
- [x] After the code review: a phone change at the desk or by the Owner takes the old phone's lock,
      so a sign-in finishing at that moment cannot outlive it (15.1's member edit too); the
      `member` role only for an active member; the scheme test counts every site policy; three
      answers of the developer written in §15 (a phone change with a member on it, a stopped
      trainer signing in again, a stopped trainer still signed in)
- [x] Stopping a trainer ends the links to their students: with the links, in 15.4a (1405/08/07)

##### 15.2b The panel's screens
- [x] «مربیان» (Owner only) in the menu under «مدیریت», after «کارمندان»: the trainers as small
      boxes (phone, since when, last sign-in on the site, past periods), add and edit in a dialog
      (a changed phone says the trainer will be signed out), «پایان مربی‌گری» with a
      confirmation, «شروع دوباره» in one click
- [x] The member's profile: «حساب سایت» in «مشخصات», with when it was made and the last sign-in
- [x] «پیامک‌ها»: a card «کد ورود سایت» (today of 300, this month's count and cost); «نیاز به
      اقدام»: a red line when the day's 300 is used up, with a link to «پیامک‌ها»
- [x] `/api/sms/sign-in-codes` answers the cost in Toman (`thisMonthCostToman`), like every other
      SMS amount; 15.2a had sent Kavenegar's Rial
- [x] Tests: `TrainersPage.test.tsx`, and the profile, «پیامک‌ها», dashboard and menu tests; the
      computer's screens elsewhere unchanged

#### 15.3 The site app: address, sign-in screens, PWA
Split into a (the app's screens, run locally) and b (on the server, installable, the QR) when its
plan was approved (1405/08/06), on branch `task/15.3-site-app`, made from `task/15.2-trainers`
after 15.2b was committed.

##### 15.3a The app's screens
- [x] A second entry in `web/` sharing `lib/` and `components/` and none of the panel's pages:
      page `web/site/index.html`, code `web/src/site/`, `vite.site.config.ts` (`npm run dev:site`
      on :5174; `npm run build` builds the panel, then the site into `dist-site/`);
      `boundary.test.ts` fails if a site file imports the panel's pages or sign-in, or the panel
      a site file (1405/08/06, not released)
      - The site's own session store, fetch and client (`/api/site/auth/refresh`, Web Lock
        `gym-site-refresh`), never the panel's; what both need moved to `lib/`
        (`emptyFailureAsProblem`, `readTokenClaim`, `memberFields`, `staleChunkReload`)
      - The first download kept under Vite's 500 kB: the sign-in screens use the plain field
        (`components/InputField.tsx`, re-exported by `FormField.tsx`) and no Zod; the sign-up
        page is downloaded when opened
      - Light or dark as the phone is (`site/public/theme-init.js`, `site/theme.ts`); Tailwind
        reads classes from `web/` (`source("..")` in `index.css`), the panel's CSS unchanged
- [x] Sign-in and sign-up in Persian, phone first: the phone, then the code (sent at its fifth
      digit), the four answers of «دریافت کد» (§15), «ارسال دوباره» counting down to the API's
      time, «تغییر شماره»; a new phone gets «ثبت‌نام» (name and birth date, §2's rules shared
      with the desk's form); its ticket kept in memory, so a reload asks for a new code
- [x] The code fills itself in: `autocomplete="one-time-code"` (iPhone) and WebOTP (Android
      Chrome), which works once the template line of §15 is added; typed by hand otherwise
- [x] The first screen: a member's area and a trainer's (their contents come in 15.5 and from
      15.6), both for one person; a member deactivated at the desk sees only the notice; a «me»
      with neither area shows «مربی‌گری شما تمام شده است» on the sign-in screen and signs out
      (§15 *Trainers*, 15.2); «خروج»
- [x] Tests: `SignInPage`, `SignUpPage`, `HomePage`, `siteAuthFetch` (the site's token only, its
      own refresh, a refused refresh leaves the panel's session alone) and the boundary
- [x] After the code review: the code is sent once even when the box is filled twice at once,
      and never with fewer than 5 digits (each would cost a try of §15's 15 a day); the WebOTP
      wait stops once the box is full; a sign-up ticket is dropped when anyone signs in or out or
      asks for a new code; every new token's «me» replaces the shown one, and «me» is read again
      when the app comes back to the front, so a stopped trainer is told without a reload; no
      phone form flashes while a reload restores the session; the boundary test covers the shared
      `lib/` and `components/` too. Accepted as it is: the countdown uses the phone's clock (a
      clock ahead only gets «کد قبلاً برای شما پیامک شده است» from the API)
- [x] Two answers of the developer (1405/08/06), written in §15: the site signs out only when the
      API says the session ended (a 401); no internet, a 5xx or a 429 keeps the person signed in
      behind «دوباره امتحان کنید» (`unreachable` in the site's store); and a trainer whose
      membership the desk deactivated gets neither area (`SitePerson.IsTrainer`, tested in
      `TrainerSignInTests`). The WebOTP line for `gymExpiring` given to the developer; Kavenegar
      approved the edited template (1405/08/07)

##### 15.3b On the server, installable, the QR
Built 1405/08/07 on `task/15.3-site-app`, not merged or released (Release 1 is at 15.5).
- [x] `app.pasargadgymplus.ir`: DNS record (the developer, checked 1405/08/07), Caddy site
      `{$SITE_DOMAIN}` serving `dist-site/` (`/srv-site`) and only `/api/site/*` of the API (any
      other `/api` there is a 404, and the panel's host answers 404 for `/api/site/*`); the panel's
      security headers and `noindex`; `sw.js` and `index.html` with `no-cache`. The API answers to
      both hosts (`AllowedHosts: DOMAIN;SITE_DOMAIN`), and `SITE_DOMAIN` is required in `.env`.
      The rate limits are the API's own (15.1), Caddy has none built in
- [x] Installable on a phone: manifest «My Gym» (named «Pasargad Gym» until the developer renamed it, 1405/08/08), icons (the gym's logo in dark stone on
      bronze, the panel's reversed: the developer's choice, 1405/08/07), a hand-written service
      worker (`site/sw.js`, filled with the build's files by `vite.site.config.ts`) that keeps the
      app's files, never `/api`, and updates itself; the card «نصب برنامه روی گوشی» on the sign-in
      and home screens: «نصب برنامه» on Android, Safari's steps on an iPhone, «بعداً» for 30 days
- [x] `https://pasargadgymplus.ir/qr`, a temporary (302) redirect to the app, so a printed poster
      never has to be printed again; the desk's QR in `deploy/qr/` (`qr.svg`, `qr.png`, level H,
      the app's icon in the middle, read back with a decoder) and the A4 poster (`poster.html` →
      `poster.pdf`), made with the npm package `qrcode` run once outside the repository (the
      developer's choice of tool, 1405/08/07)
- [x] The public page's top bar: one «ورود / ثبت‌نام» that opens the app (the developer's choice);
      the hero's two buttons lead to «امکانات» and «ساعت کاری» on the page. Closes 14.4
- [x] `www.pasargadgymplus.com` redirects to the public page like the other names:
      `www.{$REDIRECT_DOMAIN}` joined the redirect block (its DNS record already pointed here)
- [x] Tests: `npm run e2e` signs a new phone up on `https://app.localhost` (the code computed from
      its seed, as the API does) and reloads; the app installable in Chrome; manifest and `sw.js`
      served fresh; each host refuses the other's API; `/qr` is a 302 to the app.
      `siteAppOffline.spec.ts`: the worker keeps `/` and `/assets`, nothing under `/api`, and the app
      opens offline on «دوباره امتحان کنید». `InstallHint.test.tsx`; `ContentSecurityPolicyTests`
      covers the app's policy and page
- [x] After the code review: `deploy/release.sh` checks the new compose file against the server's
      `.env` before copying it (a missing `SITE_DOMAIN` would otherwise stop the nightly backup and
      the rollback while the old containers keep running); a missing `/assets/*` file is a 404 on
      both hosts, no longer `index.html` cached for a year (the panel had it since 6.5.0); the
      worker's plugin runs last, hashes `index.html` too and fails the build if a placeholder is
      left. Accepted as it is: a page load waits for the browser's own timeout before the offline copy (15.11 may add one)
- [x] After the developer tried it locally (1405/08/07): a sign-in that fails after the right code
      no longer uses the code up (checking it and finding or making the account are one transaction;
      a missing local migration had burnt a code and cost a second SMS); the code screen says the wait
      in words under its buttons («اگر کد نرسید، تا ۱:۰۳ دیگر …»), and when Kavenegar has reported the code
      delivered it says «این کد تا … دیگر معتبر است؛ بعد از آن می‌توانید کد تازه بگیرید» with «کد تازه»
      on the button, never «اگر کد نرسید» under «کد به گوشی شما رسیده است»; the install card shows on phones only
      (Chrome on Windows offered it too); the public page's button links
      `/app`, which Caddy redirects to the app like `/qr`, and `npm run dev:site` also serves the public
      page at `http://localhost:5174/public-site/`, so the whole way from the gym's page to signing in
      runs on one address locally

#### 15.4 A member picks a trainer
Plan approved 1405/08/07 (2026-10-29), on branch `task/15.4-pick-a-trainer`, made from
`task/15.3-site-app`; split in three: a (backend), b (the app's screens), c (the trainer's SMS).
- [x] Asked first, written in §15 *A member and their trainer*: one request waiting at a time, a
      new one replaces it; either side ends the link; the trainer sees name and phone; the trainer
      hears in the app and by SMS (one a day per member and trainer, 5 requests a day per member)

##### 15.4a The backend
Built 1405/08/07, not merged or released.
- [x] `Domain/Coaching`: `TrainerRequest` (waiting, accepted, declined, cancelled, replaced,
      trainer stopped; at most 5 a gym day) and `TrainerLink` (start, end, why it ended); one
      waiting request and one open link per member as partial unique indexes, ends not before
      starts and a reason exactly when ended as check constraints, `xmin` on both
- [x] One gym-wide advisory lock for every change to them (`LockCoachingAsync`): the member's
      request, cancel and end, the trainer's accept, decline and end, and the Owner's «توقف», so
      two presses at once, or an accept and a cancel at once, make one outcome
- [x] `/api/site/coaching`: the member's (`SiteMember`) `trainers`, `my-trainer`,
      `my-trainer/request`, `my-trainer/cancel-request`, `my-trainer/end`; the trainer's
      (`SiteTrainer`) `students`, `requests/{id}/accept`, `requests/{id}/decline`,
      `students/{memberId}/end`. Every handler reads the member or trainer from the database, so a
      deactivation at the desk or a stop by the Owner is refused at once, not 15 minutes later
- [x] The Owner stopping a trainer ends their links and waiting requests that day
- [x] Persian names for the two tables in the audit screen; Persian messages for `Coaching.*`
- [x] Tests: domain (`TrainerRequestTests`, `TrainerLinkTests`), integration (`CoachingTests`):
      one trainer at a time, the former trainer loses the member that day, accept and cancel at
      once, the two areas apart, the database refusing a second waiting request or open link

##### 15.4b The app's screens
Built 1405/08/07, not merged or released.
- [x] The member: «مربی من» on the home screen (no trainer, the trainer since a date, the waiting
      request, or both while changing), «انتخاب مربی» / «تغییر مربی» to `/trainers`, the
      trainers' list with «انتخاب این مربی» (the trainer now marked «مربی شما», the one asked «در
      انتظار پاسخ»), «لغو», «پایان مربی‌گری» (asks first)
- [x] The trainer: «درخواست‌ها» (only when there are any) with «پذیرفتن» and «رد کردن» (asks
      nothing: the member may ask again), «شاگردان من» with name, phone (a tap calls) and
      «غیرفعال», «پایان» (asks first). Both lists are read again when the app comes back to the
      front
- [x] The questions, chosen with the plan (the developer, 1405/08/07, «for now»): «مربی‌گری {مربی}
      برای شما از امروز تمام می‌شود. ادامه می‌دهید؟» and «مربی‌گری شما برای {شاگرد} از امروز تمام
      می‌شود. ادامه می‌دهید؟»; picking a trainer asks nothing
- [x] A change refused because the other side was first shows its message and reads the lists
      again
- [x] Tests: Vitest (`MyTrainerCard`, `TrainersPage`, `TrainerArea`), e2e
      `siteCoaching.spec.ts` (a member signs up and picks, the trainer on a second phone accepts)

##### 15.4c The trainer's SMS
Built 1405/08/07, not merged or released.
- [x] Its own kind, «انتخاب مربی», in «تنظیمات پیامک» under «پیامک به مربیان» (the panel's one change
      in 15.4, asked by the developer), off at first; sent at once, after 22:00 at 10:00; one a day
      per member and trainer, held by a unique index (`notifications.trainer_id`, the request's day
      in `report_date`); the text in `SmsText` and `docs/sms-texts.md`, shown on its card (§15).
      «لغو11» stays (Kavenegar adds it anyway)
- [x] Asked for after the request's commit (`TrainerRequestSms`), sent by a Hangfire job
      (`TrainerRequestSmsJob`, one at a time) that checks everything again: only a request still
      waiting, a trainer now and an active member get it (proposed by Claude, written in §15)
- [x] «پیامک‌ها» lists it under the trainer's name, with no link to the member
- [x] Tests: domain (`SmsTextTests`, `NotificationTests`, `SmsSettingsTests`), integration
      (`TrainerRequestSmsTests`, `SmsSettingsEndpointTests`), Vitest (the card, the switch saved)

#### 15.5 The member's home, first part
Decided 1405/08/08 (§15 *The member's own records*): the member sees everything the panel's profile
holds about them except the SMS; nothing is edited from the app (the birth date least of all); the
desk's «یادداشت» and the staff's names and reasons on cancelled rows are left out. Split in two:
- [x] **15.5a, the backend**: read-only `/api/site/me/...` behind `SiteMember` (details with the
      day they joined and the day they signed up on the site, the plan now and the one bought for
      later, the debt item by item, check-ins with lockers, plans, payments, cafe purchases), each
      calling the panel's own handler with the member's own id and mapped to site records without
      the staff's fields; a deactivated member gets nothing; integration tests (a staff token
      refused, another member's data unreachable). Built 1405/08/08 on `task/15.4-pick-a-trainer`,
      not merged or released: `Application/SiteRecords/` and `SiteRecordsEndpoints.cs`, paths
      `details`, `plan`, `debt`, `visits`, `plans`, `payments`, `cafe-orders`; no path names a
      member. `plan` is `SubscriptionSchedule.CurrentPlan` (the member list's rule) and the soonest
      queued one after it. Left out: `notes`, `cancellationReason`, `cancelReason`, the refund's
      `reason`, `receivedByUserId`, `placedByUserId`, `recordedByUserId`, `voidReason` (voided
      charges are not listed at all, as in the panel). A deactivated member or a trainer who is not
      a member gets 403 `Coaching.MemberInactive`. Tests: `SiteRecordsTests` (30)
- [x] **15.5b, the screens**: compact cards for the plan, the debt and the trainer, the history
      in tabs; read-only. Vitest, an e2e, and shown in the developer's Chrome. Built 1405/08/09 on
      `task/15.4-pick-a-trainer`, not merged or released: `web/src/site/records/`. The active
      member's home is «اشتراک من» (sessions left, the last day and what is paid as three small
      boxes; a frozen plan says since when; the plan bought for later under it), «بدهی من» (the
      total and each item, or «بدهی ندارید.»), «مربی من», «سوابق من» (tabs «مراجعه‌ها»,
      «اشتراک‌ها», «پرداخت‌ها», «بوفه», ten rows a page, each read only when opened; a cancelled row
      marked, never explained) and «مشخصات من» (read-only, «برای اصلاح مشخصات به پذیرش باشگاه
      مراجعه کنید.»). The site keeps its own copies of the panel's Persian labels
      (`site/records/labels.ts`) instead of sharing them: the developer wants the panel kept apart
      (1405/08/09). Tests: Vitest `MyRecords.test.tsx` (12) and `HomePage.test.tsx`, e2e
      `siteRecords.spec.ts` (a desk member with a plan, a payment and a visit)
- [x] «خروج» asks first (asked by the developer, 1405/08/07): «با خروج، برای ورود دوباره یک کد
      پیامکی تازه لازم است. خارج می‌شوید؟» with «خروج» and «انصراف», since signing in again costs an
      SMS; built with 15.5b (the site's `ConfirmDialog`, full width)
- [ ] **Release 1**, late at night, backup first; `SITE_CODE_SECRET`, `SITE_JWT_SIGNING_KEY` (15.1a, 15.1b)
      and `SITE_DOMAIN=app.pasargadgymplus.ir` (15.3b) added to the server's `.env` first: compose refuses
      to start without any of the three. The poster (`deploy/qr/poster.pdf`) printed after it

### Release 2 — The timetable and attendance (priority 1)

#### 15.6 The trainer's timetable (سانس‌بندی)
- [ ] Asked first: §15 *Timetable and attendance*'s open questions (group or private, capacity,
      who puts a student in a session, changes to a single day, cancelling and its SMS)
- [ ] The weekly pattern, the sessions it makes for each day, a single day's change or
      cancellation, the students of each session; the member sees their next sessions

#### 15.7 Attendance for the trainer (پیگیری حضور)
- [ ] «امروز» for the trainer: today's sessions and who has checked in at the desk, live
- [ ] Attendance per student and per month; the absence alert to the trainer
- [ ] **Release 2**

### Release 3 — The monthly assessment (priority 2)

#### 15.8 File storage
- [ ] Photos and PDFs on a Docker volume, metadata in Postgres, downloads only through a checked
      endpoint; photos re-encoded and stripped of EXIF (an image library, asked for first)
- [ ] `backup.sh`, `pull-backup.ps1` and the restore take the files; the restore drill covers them;
      disk space on the server watched

#### 15.9 Assessment and the analysis (ارزیابی و آنالیز)
- [ ] Asked first: the list of numbers (§15), the other parts (circumferences, tests, goals);
      the numbers are typed (Anea's Excel export not bought, 1405/08/03)
- [ ] The analysis PDF and its numbers, the comparison with the month before, charts, the reminder
      when a month has passed; the member sees theirs

#### 15.10 Progress photos (عکس‌های پیشرفت)
- [ ] Front, side, back taken with the phone's camera; side-by-side comparison by date
- [ ] §15's rules: only the member and the current trainer, a later trainer with permission,
      deletion on the member's request (really removed)
- [ ] **Release 3**

### Release 4 — Programs, offline

#### 15.11 Programs (برنامه تمرینی و غذایی)
- [ ] Asked first: what a workout program holds (days, exercises, sets, reps, rest, notes, a
      picture or video per exercise?) and what a diet program holds; what "marking done" means
- [ ] The trainer's exercises and templates; a program per student, made from a template and
      changed; earlier programs kept; the member sees theirs and today's part on the home page
- [ ] Offline: the program last opened stays usable without internet, and marks made offline are
      sent with the time they were made once the phone is online
- [ ] **Release 4**

### Release 5 — Nutrition (priority 3)

#### 15.12 The daily check-in (گزارش غذایی)
- [ ] Asked first: §15's open fields, and a field for medical conditions on the member's file
- [ ] The member's short daily check-in and the trainer's weekly summary; reminders, if any, by SMS
- [ ] **Release 5**

Later, not in this phase: buying a plan on the site (online payment); push notifications (on
Android they pass through Google's servers, unreliable from Iran); help from an AI model to draft
programs (trainer-feature discussion, 1405/07/17).

---

## Future — QR / Scanner (not in MVP)
**Parked by the developer** on 1405/07/21 (2026-10-13): not wanted for a long while. Not a next task.
- [ ] QR token design
- [ ] Generate and revoke QR credential
- [ ] QR identification endpoint
- [ ] Scanner integration
- [ ] Fast check-in and check-out
- [ ] Security and rate limiting
- [ ] Keep the phone and name fallback

---

## Future — Group visits (not in MVP)
**Dropped by the developer** on 1405/07/21 (2026-10-13). Kept for the history of the idea; the guest
visit (6.5.11) is what the desk uses for a visitor without a plan.
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


---

## Future — Absence SMS (پیامک غیبت)
**Done in task 10.12** (1405/07/27): every question below is answered there and in BUSINESS_RULES.md
§10 *The absence SMS*. Kept for the history of the idea.
Planned by the developer on 1405/07/15 (2026-10-07): a member who has not come for N days gets a
friendly SMS («جای شما در باشگاه خالی است»), with no offer in it. A fifth SMS kind on Phase 10's
design: the settings page row (on/off, days, send time), a unique index so it is sent once,
and the daily run; the last visit is already in `attendances`. The migration only adds new columns,
off and empty, so it is safe on the server's real data. Nothing is decided yet; BUSINESS_RULES.md §10
gets the rule once these are answered:

- [x] Who gets it: only a member with an active plan who stops coming, or also one whose plan has
      ended (to bring them back)? Frozen plans and deactivated members presumably not (§10)
- [x] How often: once per absence, so nothing more until they come again?
- [x] A member who bought a plan and never came at all: counted from the plan's start?
- [x] The wording, added to `docs/sms-texts.md` and `SmsText`

## Future — Free entry on the birthday (ورود رایگان روز تولد)
**Done in task 10.8** (1405/07/27): every question below is answered there and in BUSINESS_RULES.md
§7 *Birthday visit*. Kept for the history of the idea.
Planned by the developer on 1405/07/15 (2026-10-07). Two separate parts. The gym's rule: a member
gets a free visit on their birthday; the desk already knows whose birthday it is (BUSINESS_RULES.md
§6 *The desk panel*), so the desk can offer it at check-in, and no discount code is needed. And the
SMS telling them, which may count as advertising.

- [x] Every SMS now goes out as free text from the gym's line (`sms/send`, 1405/07/16). Ask
      Kavenegar's support whether «امروز ورودت رایگان است» counts as advertising there, and whether
      members who blocked advertising SMS then do not get it
- [x] Who: only a member with an active plan, or any member?
- [x] Does the visit use a session of their plan, or none?
- [x] How it shows in the money: a single visit at zero with the reason «تولد», never a record
      deleted, so the financial report can count them
- [x] Only on the day itself, or within some days of it?

## Future — Discount codes (کد تخفیف)
Planned by the developer on 1405/07/15 (2026-10-07), for later. Its own phase, because it touches
every sale and the financial report. Sending a code by SMS is advertising, from the gym's line like
every SMS since 1405/07/16 (see the birthday entry above).

- [ ] What it applies to: plans, single visits, the cafe?
- [ ] A percentage or a fixed amount?
- [ ] Single use or many; one member's or anyone's; an expiry date
- [ ] Who makes them (presumably the Owner only), and the audit log
- [ ] How a sale with a discount is recorded: the full price and a discount line, or the final price
      only; the financial report's figures follow from that
- [ ] A code used twice refused by the database too (a unique index), not only by the code

## Future — Member accounts on the public site (حساب اعضا)
Phase 14's task 14.6 (1405/07/26). **Decided on 1405/08/03 and moved to Phase 15**; kept here for
the history of the idea, except buying a plan online, which is still for later.
Asked by the developer on 2026-10-15, for later, to be reviewed in detail first. Members sign up
and sign in on the public site; they never enter the admin panel. This changes a founding rule of
the project ("no self-registration, no member logins"), so it needs an ADR and the Owner's decision
before any code, and its rules written in docs/BUSINESS_RULES.md.

- [ ] Sign-up and sign-in with the phone number and a one-time code by SMS (OTP); after the first
      sign-in the member gives first name, last name and birth date, which the panel can use
- [ ] A phone already in the panel links to that member, never a duplicate; whether a sign-up is a
      request the desk confirms, and whether a member's own entry may change what the desk entered
- [ ] Abuse and SMS cost: a limit per phone and per IP, codes that expire, a few wrong tries only;
      every code is a paid SMS
- [ ] What a signed-in member sees (subscription, sessions left, trainer?); a sign-in with nothing
      behind it is not worth building
- [ ] Perhaps selling plans online: payment gateway, refunds, and how it meets the desk's sales
- [ ] Picking a trainer (see 14.1, trainers)
- [ ] The public site is static and calls no API today; these pages would, so the API gets public
      endpoints with their own authentication, separate from the staff's
