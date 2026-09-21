# Architecture Decision Records

One file per significant decision: what was decided, in what situation, and what it costs.
Records are never rewritten after they are accepted. A changed decision gets a new record
that supersedes the old one, so the history of *why* stays readable.

| # | Decision | Status |
|---|---|---|
| [0001](0001-modular-monolith-clean-architecture.md) | Modular monolith with Clean Architecture | Accepted |
| [0002](0002-identity-user-in-infrastructure.md) | Keep the Identity User in Infrastructure, not Domain | Accepted |
| [0003](0003-deployment-topology.md) | Deploy as one Docker Compose stack on a single Iranian VPS | Accepted |

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
