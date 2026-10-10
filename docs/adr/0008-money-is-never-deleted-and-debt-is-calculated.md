# 0008. Never edit or delete money; calculate debt from named items, with no wallet

- Status: Accepted
- Date: 2026-09-23 (recorded 2026-10-11)

## Context

Every figure the Owner trusts (revenue, what a member owes, what each member of staff took at the
desk) is built from payments. A payment that can be edited or deleted makes every past report
arguable, and a mistake at the desk is not rare: the wrong amount, the wrong member, a plan sold
twice.

A member also often owes for several things at once: a plan paid in instalments, a هوازی charge, a
drink left on their account. On 1405/07/01 (2026-09-23) the developer proposed a wallet, so money
taken at the desk would not have to be tied to a named item, and then decided against it. The
reasons are in BUSINESS_RULES.md §5 *Member debt*; this record keeps the decision with the others.

## Decision

- **Payments are never edited or deleted.** A mistake is corrected with a refund whose reason
  explains it; a full refund of a mistaken payment is a "void". A service charge or an expense is
  voided with a reason, and the row stays. Subscriptions and cafe orders are cancelled, not
  removed.
- **Every payment belongs to exactly one item**: a subscription, a service charge or a cafe order
  (a check constraint). A lump sum for several items is written as one payment per item, all in
  one transaction («تسویه یکجا»), and the rows share a settlement id so the history can show them
  together.
- **Debt is calculated, never stored**: per item, its price minus net paid, never below zero,
  added up over the member's live items. There is no balance column and no credit balance.

## Alternatives considered

- **Editable payments with an audit trail.** The audit log would show what changed, but every
  report would read the current values, so last month's revenue could change after it was
  reported.
- **A wallet**: deposits on the member's account, spent on items later. Rejected by the developer:
  it splits "money arrived" from "money earned", so revenue would have to choose between the
  deposit date and the allocation date; debt would become a net figure instead of a list the
  breakdown can point at; and refunds would gain a destination. If the gym wants one later, its
  balance must be calculated from a deposit and allocation ledger, never kept in a column.
- **A stored balance per member**, updated with each payment. Faster to read, but it has to be
  kept right by every code path forever, and one missed update makes it disagree with the items
  it is supposed to sum.

## Consequences

- Easier: revenue for a period is payments minus refunds by `PaidAt`, and it never changes after
  the fact; "جزء به جزء" behind every debt is one query; the database can refuse an orphan payment.
- Harder: correcting a mistake takes two steps (a refund, then a cancel or void), and the reports
  must show refunds as their own lines. A member who pays ahead for a plan they have not chosen yet
  cannot be recorded.
- Ongoing: any new thing the gym sells needs to be a payment target with its own price, or it
  cannot be paid for.
