# Performance and logging review

Task 11.3, 1405/07/18 (2026-10-10). The gym has used the system with real data since 2026-10-02,
so production is still small: a few hundred rows in most tables. A query that is slow with three
years of data is fast today and gives no warning until it is too late to fix calmly. This review
therefore measures with a database the size the gym will reach, not the size it has. It records
what was measured, how, what changed, and what is left to watch. Run it again after a large
feature or when a screen feels slow.

## What changed in this review

- **An index on `payments.cafe_order_id`** (migration `AddPaymentsCafeOrderForeignKey`). Payments
  had indexes for their subscription and their service charge but not for their cafe order. Every
  member's debt sums each cafe order's payments, so Postgres read the whole payments table once
  per order: **18 to 23 seconds** for the «بدهکاران» filter, the receivables report, the
  dashboard and a year's financial report, with three years of data. With the index: under half
  a second. `DatabaseTuningTests` fails if the index disappears.
- **JIT off on the application's connections** (`BuildConnectionString`, `Options=-c jit=off`).
  Postgres compiles a query to machine code when the planner's cost *estimate* is high. The debt
  queries' correlated sub-selects are estimated high, so Postgres spent **410 ms compiling a
  query that then ran in 60 ms**. JIT pays off for analytical queries that run for seconds; this
  app has none. Set on the connection, it holds the same in development, the tests and
  production, and needs no change to the server's Postgres. `DatabaseTuningTests`.
- **A guard against N+1 queries.** `QueryCountTests` calls 44 read endpoints with 1 row and with
  25 rows behind them and fails if any sends more SQL commands for 25. None did.
- **Pages load when first opened** (`web/src/app/router.tsx`). Only the login page and the locker
  board come with the app; every other page is its own file. The main file fell from 1,015 kB to
  436 kB and Vite's 500 kB warning is gone. The first download fell less, 280 kB to 252 kB
  gzipped, because the board itself uses the shared forms and the Jalali date picker.
- **A tab opened before a release recovers by itself** (`web/src/app/staleChunkReload.ts`). A
  release replaces the hashed files, so a desk tab left open since the morning asks for a page
  file that no longer exists. On Vite's `vite:preloadError` the page reloads once and the new
  version opens. This already applied to the dashboard, which was the one lazy page before.
  Opened straight from the address bar, a page that is still downloading shows «در حال بارگذاری…»
  instead of a blank window. The jsdom tests render every page already loaded (`renderApp`); the
  end-to-end test reloads on `/members` to cover the real download.
- **An end-to-end test of the front desk** (`npm run e2e` in `web/`, below).
- **A foreign key on `payments.cafe_order_id`**, in the same migration. Subscriptions and service
  charges had one; cafe orders, added later, did not. `PaymentConstraintTests`.
- **The logs say what happened** (from the production logs, below): a request the browser
  cancelled is no longer a 500 error, a real 500's exception is logged again (.NET 10 had
  silenced it), four totals queries no longer warn, the Kerberos message is gone, and the
  board's own refreshes are at Debug.
- **`artifacts/` left out of the Docker build context** (`.dockerignore`). A 147 MB migration
  bundle from the previous release was being sent into every image build.

## N+1 queries

`QueryCountTests` seeds one member with a busy day (a plan half paid, a visit with a locker, a
cardio charge, an owed cafe order, a birthday SMS, an expense, a cheque, a guest), measures every
read endpoint, adds 24 more such members and measures again. The counter is an EF Core
`DbCommandInterceptor` in the test project only; it counts the commands of requests that carry
its header, so Hangfire's background queries are not counted.

Every endpoint sent the same number of commands for 25 rows as for 1, except the audit list
filtered by member, which sent fewer (11, then 6): it looks up names once per *kind* of record
on the page, and a page of 20 payments has fewer kinds than one row of each. The test runs with
the rest of the suite; its output lists every endpoint's count.

The highest counts are not N+1 but fixed work: the financial report sends 18 commands (one per
total it shows), the member list and the locker map 11.

## Indexes, with three years of data

### How it was measured

1. A throwaway `postgres:18` container on another port, migrated with `dotnet ef database update
   --connection ...`.
2. `docs/performance-volume.sql`: 3,000 members joining over three years, 10,500 plans, 103,500
   visits, 49,700 payments, 24,500 cafe orders with 49,000 lines, 19,600 cardio charges, 9,700
   SMS, 70,000 refresh tokens and 514,600 audit rows.
3. A second copy of the API on a free port against that database, with `Sms__Provider=Fake`.
4. Postgres's `auto_explain` module (`ALTER DATABASE … SET session_preload_libraries =
   'auto_explain'`, `log_min_duration = 0`, `log_analyze = on`) writes the real plan of every
   statement, with its real parameters, to the container log. Every read endpoint was called and
   the log searched for slow statements and `Seq Scan` on large tables.
5. Timings: the second call of each endpoint with `curl`, `auto_explain` off.

### Results

| Endpoint | Before | Index | Index and JIT off |
|---|---|---|---|
| `/api/members?debtorsOnly=true` | 22.8 s | 0.17 s | 0.11 s |
| `/api/reports/financial` (a year) | 22.4 s | 0.23 s | 0.15 s |
| `/api/reports/receivables` | 18.4 s | 0.47 s | 0.06 s |
| `/api/reports/needs-attention` | 18.1 s | 0.47 s | 0.08 s |
| `/api/sales` (a month) | 0.75 s | 0.01 s | 0.01 s |
| `/api/members/{id}/payments` | 0.19 s | 0.13 s | 0.02 s |
| `/api/audit-logs?memberId=` | 0.27 s | 0.21 s | 0.21 s |

The "Before" column was measured with `auto_explain` on, which adds a little to every query; it
explains the small rows, not the 18 seconds.

Every other read endpoint answered in under 30 ms: the board, the locker map, the member search
by name or phone, the member's page, the history pages, the SMS history and the audit list.

### Left to watch

- **Debt is computed, never stored.** Every member's debt reads every plan, charge and order
  with its payments (through indexes now). 0.06 to 0.15 s at three years. If it grows past a
  second, the answer is a stored balance per member, which is a design change, not an index.
- **The audit list filtered by one member** reads the audit table sequentially (0.2 s at 514,000
  rows), because the member's records are matched as many (type, id) pairs. Fine for an Owner's
  screen; look again past two million rows.
- **The production server has 2 cores.** These timings are from a development machine; expect
  the server to be up to twice as slow.

## Production logs and error handling

Read on 2026-10-10 over ssh, with read-only commands only (`docker compose ps` and `logs`,
`grep`, and `SELECT`s in a session with `default_transaction_read_only=on`). The API's logs
covered four days, from release `20261006-0505-73e6c33` (each release starts a new container,
and with it a new log): 23,208 lines, 2 at Error, 12 at Warning. Postgres's covered two weeks.

| Finding | What it was | Done |
|---|---|---|
| 2 × `GET /api/sales` and `/totals` "responded 500" at Error | The browser cancelled both requests 17 ms in (the page was left or reloaded). ASP.NET answers that with 499, but the request log sat inside the exception handler, saw the raw `OperationCanceledException` and wrote 500 | The request log moved outside the exception handler (`Program.cs`), so it records the status the client got (`RequestLogLevelTests`) |
| — | Found on the way: since .NET 10 the exception handler middleware does not log an exception that an `IExceptionHandler` handled. Only the request log's position had kept a real 500's stack trace in the log | `SuppressDiagnosticsCallback = _ => false` (`ExceptionLoggingTests`) |
| 9 × EF "`First`/`FirstOrDefault` without `OrderBy`" | Four totals queries (`receivables`, `subscriptions`, sales and payment totals) group on a constant and took the "first" of one group | `SingleOrDefaultAsync`; results unchanged |
| `Cannot load library libgssapi_krb5.so.2` at every start and migration | Npgsql tries Kerberos encryption first; the images have no Kerberos library and Postgres offers none | `GssEncryptionMode.Disable` (`DatabaseTuningTests`) |
| 79% of all lines | The board's own refreshes: `currently-inside`, `lockers`, `today-by-hour`, `due-soon` | Logged at Debug when they succeed, like `/health`; a failing one is still logged |
| 1 × refresh-token reuse, 01:43 Tehran | One refresh with a token already rotated, 1.5 hours after the last activity, no other refresh near it; the user signed in again 4 seconds later. Most likely a rotation whose answer never reached the browser | Nothing: the design working (BUSINESS_RULES.md §1). Watch for repeats |
| Speed | No request over 500 ms in four days. The board's refreshes average 7 to 12 ms, the slowest report (financial) 59 ms | — |
| Status codes | 22,509 × 200, 136 × 201, 479 × 401 (an access token expiring, then one refresh), 9 × 400, 1 × 409 | — |
| Caddy, Postgres | Caddy: two start-up notes (no HTTP/2 or HTTP/3 on port 80). Postgres: errors from the first start on an empty database, one hand-typed `psql` command, two connections ended by an administrator command | — |

Also checked: the database is 13 MB (70 members, 143 visits), so its scan statistics say nothing
about indexes yet; Postgres reads tables this small whole whatever indexes exist. No payment
pointed at a missing cafe order, so the new foreign key on `payments.cafe_order_id` refused no
row (`PaymentConstraintTests`). JIT is on in the server's Postgres; the app's connections turn
it off for themselves.

Log retention: Docker keeps 3 files of 10 MB per container. At four days' volume that was
about two weeks; with the refreshes at Debug it is about two months, and every release starts
over anyway.

## The front desk, end to end

`npm run e2e` (in `web/`) builds the production images, starts them on `https://localhost`
under their own compose project (`gym-e2e`) with an empty database, migrates it with the same
bundle a release uses, and runs Playwright in the installed Chrome:

1. Through the API: the Owner's first login and password change, the prices, a staff account
   past its own first login, a member with a 10-session plan.
2. In the browser, as the staff member: sign in, open a free locker, find the member, check in,
   see the locker taken by them on the board, check out with the key taken back, see the
   locker free again.
3. A reload straight on `/members`: the session comes back from the refresh cookie and the
   page's own file downloads under the Content-Security-Policy.

It finishes with `docker compose down --volumes`. It runs locally before a release, not in CI:
building the images takes minutes. Ports 80 and 443 must be free.

## Still open

- **Alerting** (from `docs/security-review.md`): decided with the developer to leave it for later.
  Four days held two errors, both false; now that a cancelled request is no longer an error,
  any Error line means something. An alert (an SMS to the Owner, say) is its own task, with
  its channel and threshold decided as a business rule first.
- **Refresh-token reuse**: one in four days. If it repeats, look at whether rotation answers
  are being lost on the network before changing anything in §1.
- **The volume test is manual.** Repeat it after a feature that adds a list or a report:
  `docs/performance-volume.sql`, then the steps under *How it was measured*.
