# Architecture Decision Records

One file per significant decision: what was decided, in what situation, and what it costs.
Records are never rewritten after they are accepted. A changed decision gets a new record
that supersedes the old one, so the history of *why* stays readable.

| # | Decision | Status |
|---|---|---|
| [0001](0001-modular-monolith-clean-architecture.md) | Modular monolith with Clean Architecture | Accepted |
| [0002](0002-identity-user-in-infrastructure.md) | Keep the Identity User in Infrastructure, not Domain | Accepted |
| [0003](0003-deployment-topology.md) | Deploy as one Docker Compose stack on a single Iranian VPS | Accepted |
| [0004](0004-password-policy-and-lockout.md) | Check passwords by length and blocklist, and lock out unknown devices only | Accepted |
| [0005](0005-business-failures-as-result-values.md) | Return business failures as Result values; throw only for the unexpected | Accepted |
| [0006](0006-gregorian-storage-jalali-display.md) | Store Gregorian dates and UTC moments; show Jalali only in the browser | Accepted |
| [0007](0007-invariants-enforced-by-the-database.md) | Enforce invariants in the database as well as in the code | Accepted |
| [0008](0008-money-is-never-deleted-and-debt-is-calculated.md) | Never edit or delete money; calculate debt from named items, with no wallet | Accepted |
| [0009](0009-demo-data-through-the-real-handlers.md) | Make the demo data through the real handlers on a moving clock | Accepted |

0005 to 0008 were written on 2026-10-11 (roadmap Phase 12) for decisions made earlier; each gives
the date the decision was made and the date it was recorded. Their reasons come from
BUSINESS_RULES.md and docs/LEARNING.md, where they were first written down.

## Template

```markdown
# NNNN. Title in the imperative

- Status: Proposed | Accepted | Superseded by NNNN
- Date: YYYY-MM-DD

## Context
The situation and the forces at play. What makes this a decision rather than a default?

## Decision
What we will do, stated plainly.

## Alternatives considered
Each option and why it lost.

## Consequences
What becomes easier, what becomes harder, and what we now have to keep doing.
```
