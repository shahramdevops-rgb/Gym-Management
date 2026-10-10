# 0007. Enforce invariants in the database as well as in the code

- Status: Accepted
- Date: 2026-09-17 (recorded 2026-10-11)

## Context

The rules that protect money and history are checked in the domain entities and the handlers:
a member never holds two plans covering the same day, a phone number belongs to one member, a
locker has one open visit at a time, a refund never exceeds what was paid. Code checks have two
weaknesses. Two requests can both read "the locker is free" and both write, because the check and
the write are not one step. And anything that reaches the database another way, such as a
migration, a hand-written fix on the server, or a future feature that forgot a check, meets no
check at all.

The database is shared by every feature (ADR 0001), so it is the one place every write passes.

## Decision

Every invariant that the database can state is stated there too, and tests prove the database
refuses a bad row on its own (raw SQL inserts in the integration tests):

- **Unique and partial unique indexes**: one member per normalized phone number, one open visit
  per member, per locker and per reserve place, one SMS per event.
- **Check constraints**: amounts above zero, a refund always has a reason, a single-session plan
  is one day and one session, a guest visit has a name and no member, a kind that is on has its
  settings filled.
- **An exclusion constraint** (`btree_gist`, hand-written in the migration) for "two memberships
  never cover the same date", which no unique index can express.
- **`xmin` concurrency tokens** on rows two people may edit at once, so the second save is refused
  instead of silently overwriting the first.

The code still checks first, so the desk gets a clear error before anything is written. Where a
race can reach the constraint, the handler catches the violation and returns the same `Error`
(ADR 0005).

A rule the database cannot hold forever stays in code only: "not after today" moves every
midnight, and a check constraint must stay true for a row for ever.

## Alternatives considered

- **Code checks only.** Fewer migrations, and every rule lives in one place. But a race between
  two desks, or one manual `UPDATE` on the server, could store exactly the state the rule forbids,
  and nothing would notice until a report was wrong.
- **Database constraints only.** One source of truth. But the error would arrive as a Postgres
  SQLSTATE after the work was done, the desk would hear a generic conflict, and the rules could not
  be unit-tested in Gym.Domain.Tests without a database.

## Consequences

- Easier: a whole class of race conditions cannot corrupt data; the production data stays valid
  even after a manual fix; tests against a real Postgres (Testcontainers, never the InMemory
  provider) prove the rules hold where they matter.
- Harder: a rule lives in two places and a change touches both, plus a migration. Changing an
  invariant has a visible price, which is sometimes the reason a cheaper change wins.
- Ongoing: since the server holds real data (2026-10-02), a new constraint must accept every
  existing row; a migration carries old rows forward instead of refusing them.
