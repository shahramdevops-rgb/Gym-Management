# 0009. Make the demo data through the real handlers on a moving clock

- Status: Accepted
- Date: 2026-10-11

## Context

The README needs screenshots, and a developer trying the app needs something to look at. Neither
may come from the gym's server: it has held real members, phone numbers and payments since
2026-10-02. So an empty database has to be filled with a made-up gym that looks like three months
of real use, with full charts, members inside right now, debts, a frozen plan and a refund.

The obstacle is time. Every handler stamps what it writes with the injected `TimeProvider`, and
there is deliberately no way to record a backdated payment (BUSINESS_RULES.md §5). Data created
through the API all lands on today, and the dashboard's monthly charts stay empty.

## Decision

`admin seed-demo` (`src/Gym.Api/Admin/Demo/`), a server console command beside `admin unlock`:

- It calls the **same handlers the endpoints call**, with the same validators, one scope per step
  as one request would.
- For this command only, `Program.cs` swaps three services before the host is built: a
  `DemoClock` the seeder moves forward day by day, starting ninety days back and never passing the
  real time; a `DemoActor` as the current user (the Owner for office work, two staff accounts at
  the desk, nobody for the nightly jobs); and the fake SMS sender, whatever the configuration says.
- The randomness is seeded, so a run on a given day is repeatable.
- **It refuses anything but an empty database** (only the Owner, the lockers and the expense
  categories), and on a Production host it also needs `Demo:Allowed=true`, which only the
  screenshot stack passes, to that one command.

`npm run screenshots` (web/) builds the production images, runs them on an empty database, runs
the command, and saves the screens in both themes to `docs/screenshots/`.

## Alternatives considered

- **Seed through the HTTP API in Playwright**, as the end-to-end tests do. No new backend code,
  but everything is dated today, so the dashboard and the financial report would be nearly empty.
- **Insert rows directly** (SQL or EF Core without the handlers). Any date is possible, but the
  data would follow the rules only as well as the seeder copied them: a plan's days, a queued
  plan moving up, a guest settling before leaving. Every rule change would need the seeder
  changed too, or the demo would show states the app can never reach.
- **A copy of the real database with the names scrambled.** Real visit patterns, but real money
  and real people's habits, and one mistake in the scrambling publishes a member's phone number.

## Consequences

- Easier: the demo obeys every rule automatically, and a rule that changes changes the demo with
  it; screenshots can be taken again before every release with one command; a failing step is
  printed with its error code and moment.
- Harder: a rule change that refuses something the seeder does breaks the command, so its
  integration test (`DemoSeederTests`, a full ninety-day run) has to be kept green. The run takes
  well under a minute.
- Ongoing: the guard must stay. The command must never be given `Demo:Allowed`, or be run at all,
  on the gym's server.
