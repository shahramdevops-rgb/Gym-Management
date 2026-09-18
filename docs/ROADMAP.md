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
- [ ] Vite + React + TypeScript in `web/`, ESLint, Prettier, Vitest
- [ ] `<html lang="fa" dir="rtl">`, self-hosted Vazirmatn font
- [ ] Tailwind CSS and shadcn/ui, Radix `DirectionProvider` set to RTL
- [ ] React Router and TanStack Query
- [ ] Generated API types (openapi-typescript) and client (openapi-fetch); npm script to regenerate
- [ ] Dev proxy `/api` → API
- [ ] `lib/format.ts`: Persian digits, money, Jalali date display; `lib/normalize.ts`: Persian/Arabic character and digit normalization; unit tests for both
- [ ] RTL app shell (header, right-side navigation) with a status page calling `/health`

Done when: `npm run dev` shows a Persian RTL page reporting the server health, and `npm test` passes.

### 0.8 CI and first decision record
- [ ] GitHub Actions: backend restore, build, test; frontend lint, test, build
- [ ] `docs/adr/0001-modular-monolith-clean-architecture.md`
- [ ] README skeleton

Done when: CI passes on GitHub.

---

## Phase 1 — Identity, Access and Audit

### 1.1 Identity and Owner seeding
- [ ] `User : IdentityUser<Guid>` with `IsActive`, `MustChangePassword`, `FullName`
- [ ] Roles Owner and Staff
- [ ] Idempotent Owner seeding from configuration
- [ ] Tests: seeding twice creates one Owner

### 1.2 Login and access tokens
- [ ] Login endpoint with lockout
- [ ] JWT access token (15 minutes)
- [ ] Rate limiting on login
- [ ] Tests: wrong password, lockout, inactive user rejected

### 1.3 Refresh tokens and logout
- [ ] RefreshToken entity (hash, family, expiry, revoked, replaced by)
- [ ] Refresh endpoint with rotation and reuse detection
- [ ] HttpOnly cookie
- [ ] Logout revokes the token
- [ ] Tests: rotation works, reused token revokes the family

### 1.4 Current user, policies, change password
- [ ] `ICurrentUser`
- [ ] Policies `OwnerOnly`, `StaffOrOwner`
- [ ] Change password endpoint
- [ ] Forced password change gate
- [ ] Interceptor fills CreatedBy/UpdatedBy
- [ ] Tests: user who must change password is blocked from other endpoints

### 1.5 Staff management API
- [ ] Owner: create staff, deactivate (revokes tokens), reactivate, reset password
- [ ] Tests: staff cannot create staff; deactivated staff cannot refresh

### 1.6 Audit log
- [ ] AuditLog entity and table
- [ ] Audit `SaveChangesInterceptor` with sensitive-field exclusions
- [ ] Tests: update writes old and new values; password hash never appears

### 1.7 UI: login and staff
- [ ] Login page
- [ ] Access token in memory, silent refresh on 401, logout
- [ ] Forced change-password screen
- [ ] Route guards by role; navigation shows only allowed items
- [ ] Error code → Persian message map (`lib/errors.ts`)
- [ ] Owner: staff list, create, deactivate, reset password

Done when: you can log in as the seeded Owner in the Persian app, change the password, and create a staff account.

---

## Phase 2 — Members

### 2.1 Member creation and update
- [ ] Member entity and configuration, including `NormalizedFullName` for search
- [ ] Phone normalization service (libphonenumber, accepts Persian and Arabic digits)
- [ ] Persian text normalizer (Arabic ي/ك → Persian ی/ک, spaces, zero-width non-joiner)
- [ ] Unique phone index
- [ ] Create and update endpoints with validation
- [ ] Tests: the same phone in different formats (including Persian digits) is rejected as a duplicate

### 2.2 Member queries and lifecycle
- [ ] Deactivate and reactivate
- [ ] Get by id
- [ ] Paged list
- [ ] Search by partial name and by phone
- [ ] Tests: searching "علي" finds "علی"; phone search normalizes input

### 2.3 UI: members
- [ ] Home: search by phone or name
- [ ] Member list with paging
- [ ] Create and edit member form (Zod validation, Persian messages)
- [ ] Member profile page (basic info; later phases add sections)

Done when: staff can find, create, and edit members in the app.

---

## Phase 3 — Plans

### 3.1 Plans API
- [ ] Plan entity (duration, session count or unlimited, price, active)
- [ ] Create, update, activate, deactivate (Owner only), list
- [ ] Validation rules
- [ ] Tests: staff cannot create plans; inactive plans cannot be sold

### 3.2 UI: plans
- [ ] Owner plans screen: list, create, edit, activate/deactivate

---

## Phase 4 — Subscriptions and Payments

### 4.1 Subscription domain model
- [ ] Subscription entity with snapshot fields
- [ ] `ConsumeSession`, `RestoreSession`, `Freeze`, `Unfreeze`, `Cancel`
- [ ] Calculated status and remaining sessions
- [ ] Extensive unit tests with FakeTimeProvider (dates, freeze, unlimited plans, status precedence)

### 4.2 Assign and renew
- [ ] Assign endpoint (starts today or queued)
- [ ] Renew endpoint
- [ ] `xmin` concurrency token and session check constraint
- [ ] Tests: renewal before expiry is queued; inactive plan or member rejected

### 4.3 Freeze, unfreeze, cancel endpoints
- [ ] Owner-only endpoints
- [ ] Unfreeze shifts queued subscriptions
- [ ] Tests: max freeze days enforced; queued subscription shifted

### 4.4 Payments
- [ ] Payment entity with the one-target check constraint
- [ ] Register payment (partial allowed, overpayment rejected)
- [ ] Calculated payment status
- [ ] Tests: Unpaid → Partial → Paid

### 4.5 Refunds and member history
- [ ] Refund and void (Owner only)
- [ ] Member subscription history
- [ ] Member payment history
- [ ] Tests: refund cannot exceed net paid; status recalculates

### 4.6 UI: subscriptions and payments
- [ ] Member profile: current subscription card (status, Jalali dates, sessions left, payment status)
- [ ] Assign and renew subscription dialog
- [ ] Register payment dialog
- [ ] Subscription and payment history tabs
- [ ] Owner: freeze, unfreeze, cancel, refund actions

Done when: staff can sell a subscription and take payment from the member profile.

---

## Phase 5 — Lockers and Attendance

### 5.1 Lockers API
- [ ] Locker entity, Owner setup endpoints
- [ ] Locker list with derived occupancy
- [ ] Tests: cannot put an occupied locker out of service

### 5.2 Check-in
- [ ] Attendance entity with both partial unique indexes
- [ ] Transactional check-in use case
- [ ] No-locker warning
- [ ] Tests: expired, frozen, exhausted, inactive member, already inside

### 5.3 Check-out, cancel, lists
- [ ] Check-out
- [ ] Cancel check-in within the window
- [ ] "Currently inside" list with locker numbers
- [ ] Attendance history by member and by date range
- [ ] Tests: cancel restores the session; cancel after the window fails

### 5.4 Concurrency tests
- [ ] Two parallel check-ins for the same member: one succeeds, one gets 409, one session consumed
- [ ] Race for the last free locker: no locker assigned twice
- [ ] Fix any issues found

### 5.5 Background jobs
- [ ] Hangfire with PostgreSQL storage, dashboard restricted to Owner
- [ ] Nightly auto-checkout job
- [ ] Tests: job closes open attendances and frees lockers

### 5.6 UI: front desk
- [ ] One-click check-in from search results and profile, showing the locker number and warnings
- [ ] Check-out and cancel check-in
- [ ] "Currently inside" board (auto-refresh)
- [ ] Member attendance history tab
- [ ] Owner lockers screen with live occupancy

Done when: the full front desk flow works in the app: search → check-in → locker shown → check-out.

---

## Phase 6 — First Deployment (MVP 1 live)

### 6.1 Production containers
- [ ] Multi-stage API Dockerfile running as non-root
- [ ] Production compose: API, Postgres, Caddy (HTTPS, serves the React build, proxies /api)

### 6.2 Release process
- [ ] EF migration bundle executed during deploy
- [ ] Secrets via environment variables
- [ ] Log retention settings

### 6.3 Backups
- [ ] Daily `pg_dump` with rotation
- [ ] Off-server copy
- [ ] Documented and tested restore

### 6.4 Go live
- [ ] Deploy to the server
- [ ] Smoke test checklist
- [ ] Optional: deploy workflow in GitHub Actions

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
- [ ] `ISmsSender`, `FakeSmsSender`
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
