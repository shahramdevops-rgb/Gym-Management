# Business Rules

This file is the source of truth for how the gym works.
If code and this file disagree, this file wins. If this file is silent, ask before implementing.

---

## 0. Settings still to decide

These values live in configuration (the `Gym` and `Sms` sections). Decide each one before the phase listed.

| Setting | Decide before | Notes |
|---|---|---|
| `Sms:ExpiringDaysBefore`, `Sms:LowSessionsThreshold`, `Sms:MaxAttempts`, quiet hours | Phase 10 | |
| SMS provider | Phase 10 | An Iranian panel, probably Kavenegar. Ask whether it allows free text or only approved templates — see §10. |

Decided values:
- Currency = Toman. There is no `Gym:Currency` setting and no currency column anywhere: the gym
  has one currency, amounts are `decimal`/`numeric(18,2)` everywhere, and the frontend appends
  "تومان" when it formats money (`web/src/lib/format.ts`).
- Payment methods = Cash, Card, BankTransfer (decided with the developer in task 4.4).
- `Gym:CancelCheckInWindowMinutes` = 30
- `Gym:PhoneDefaultRegion` = `IR` (Iran: a local `09…` number becomes `+989…`)
- `Gym:TimeZone` = `Asia/Tehran`. Defines "today" for every business date (decided in task 4.1).
- `Gym:MaxFreezeDaysPerSubscription` = 30 (decided in task 4.1).
- `Gym:ClosingTime` = 23:00 (Asia/Tehran). Local time the nightly auto-checkout job runs at (decided in task 5.5).
- `Gym:OpeningTime` = 06:00 (Asia/Tehran). The gym is open 06:00–23:00, and with `Gym:ClosingTime`
  this bounds the hours a member can check in (§7 *Opening hours*). **Pending:** decided with the
  developer on 1405/07/04 (2026-09-26), but deliberately not enforced until roadmap task 11.4,
  because the developer builds and tests the system at night and must not be blocked by it.
- **The gym has no closed days.** It is open every day of the year, holidays included, with the same
  hours. There is no holiday calendar and no per-weekday schedule (decided with the developer,
  1405/07/04).
- **The gym runs open accounts** (حساب باز): a member may owe money and settle later, in as many
  instalments as it takes, both for a subscription and for cafe orders. This replaces the two
  settings that used to sit in the table above — "block check-in when unpaid" and "cafe orders paid
  in full at creation" — and both are answered "no" (decided with the developer, 1405/06/31).
  There is no configuration switch: owing money is how the gym works, not an option. What it means
  in each part of the system is §5 *Member debt*, §7 *Check-in* and §8.
- Money owed never blocks anything. Check-in, selling a subscription and buying from the cafe all
  succeed with a balance outstanding; the front desk is shown the amount instead of being stopped
  (decided with the developer, 1405/06/31). There is no debt ceiling. *If the gym later wants one,
  it becomes a `Gym:MaxMemberDebt` setting and a refusal, not a change to any of the rules below.*
- The single-session (تک‌جلسه‌ای) rate is **not** a setting. There is no `Gym:DropInPrice`: the one
  single-session plan (§3) carries the price and snapshots it onto every sale (§4), so the Owner
  editing that plan is the whole mechanism for changing the rate. A setting would be a second place
  to keep true (decided with the developer, 1405/07/03).

---

## 1. Users and access

- Closed system. There is no registration endpoint. Members never log in.
- Roles: `Owner` and `Staff`.
- The Owner account is seeded at startup from configuration (`Seed:OwnerUserName`, `Seed:OwnerPassword`, optional `Seed:OwnerFullName`). Seeding is idempotent: running it twice creates nothing new.
  - "The Owner already exists" means any user already holds the Owner role, even if `Seed:OwnerUserName` has since changed. Seeding never modifies that existing Owner, including their password.
  - If `Seed:OwnerUserName` or `Seed:OwnerPassword` is missing, seeding logs a warning and does nothing — it never queries the database. Startup does not fail.
  - `Seed:OwnerFullName` defaults to "مدیر" when not configured. The Owner can rename themselves later.
  - The seeded Owner has `MustChangePassword = true`.
- Password policy: at least 8 characters, containing at least one letter and one digit. No case (upper/lower) or symbol is required — passwords are typed on a Persian keyboard at the front desk.
  - Persian and Arabic digits in a password are converted to English digits before it is sent, on every password field (login, change, create, reset) and again by the API, including the seeded Owner password, so "رمز۱۲۳۴" and "رمز1234" are the same password whatever client sends it. Letters are kept exactly as typed. *Decided by Claude during task 1.7 while the developer was away; pending review.*
- The Owner creates Staff accounts with a temporary password. Staff have `MustChangePassword = true`.
- Staff account management (Owner only). *Decided by Claude during task 1.5 while the developer was away; pending review.*
  - User names: 3 to 50 characters, Latin letters, digits and `- . _ @ +` (Identity's default set), unique ignoring case. Full names: required, at most 200 characters, trimmed with repeated spaces collapsed.
  - These endpoints manage Staff accounts only. An Owner account id is answered like an unknown id (`Staff.NotFound`), so the Owner cannot deactivate or reset themselves by mistake.
  - Deactivating revokes all of the user's refresh tokens in the same transaction. Their current access token keeps working until it expires (at most 15 minutes). Deactivating an inactive account, or reactivating an active one, succeeds and changes nothing.
  - Reactivating does not reset the password; the user logs in with the password they had.
  - Resetting a password: the Owner types a new temporary password (same policy as any password). It sets `MustChangePassword = true`, clears any lockout, and revokes all of the user's refresh tokens.
  - After a reset, the staff member's current access token (at most 15 minutes old) still says they need no password change, so the gate does not stop them until it expires; their next refresh fails because every refresh token was revoked. The same 15-minute window as deactivation.
- A user with `MustChangePassword = true` may only call change-password and logout.
- Changing a password:
  - Requires the current password. A wrong current password returns `Auth.CurrentPasswordIncorrect`, counts toward lockout like a failed login, and the endpoint is rate-limited like login.
  - The new password must follow the password policy and must differ from the current one (`Auth.PasswordUnchanged`).
  - Success clears `MustChangePassword`, revokes all of the user's refresh tokens (every other browser is logged out), and starts a fresh session for the browser that made the change.
- Deactivated users cannot log in, and all their refresh tokens are revoked immediately.
- Access token: JWT, 15 minutes, kept in memory by the frontend (never localStorage).
- Refresh token: random value, stored only as a hash, rotated on every use, sent as an HttpOnly, Secure, SameSite=Strict cookie. Reusing an already-rotated token revokes the whole token family.
  - A refresh token lives 7 days. Every refresh issues a new one with a fresh 7 days, so a user who opens the app at least weekly stays logged in. There is no fixed maximum per login.
  - Rotation is strict, with no grace period: presenting a token that was already rotated, including two tabs refreshing at the same moment, counts as reuse and revokes the family. The frontend makes sure only one refresh runs at a time.
  - Refreshing as an inactive user is rejected (`Auth.UserInactive`) and revokes that token family.
  - Refresh ignores account lockout: lockout stops password guessing, and a refresh involves no password.
  - Logout revokes the presented refresh token and always succeeds, with or without a valid token.
- Login has account lockout after repeated failures and per-IP rate limiting.
  - Lockout: 5 consecutive wrong passwords lock the account for 15 minutes. A successful login resets the count. The Owner can be locked out too.
  - Rate limit: 10 login attempts per minute per IP address. Front-desk staff share one IP, so the limit allows several people to log in at once.
  - Failure responses: an unknown user name and a wrong password return the same error (`Auth.InvalidCredentials`), so user names cannot be discovered. A locked account returns `Auth.LockedOut`. An inactive account returns `Auth.UserInactive`, but only when the password was correct; otherwise `Auth.InvalidCredentials`.

### Permissions

| Action | Owner | Staff |
|---|---|---|
| Members: create, update, deactivate, search | ✅ | ✅ |
| Check-in, check-out, cancel check-in | ✅ | ✅ |
| Assign or renew subscriptions | ✅ | ✅ |
| Register payments, create cafe orders | ✅ | ✅ |
| Plans, staff accounts, creating a locker | ✅ | ❌ |
| Lockers: see the list, take one out of service, bring it back in | ✅ | ✅ |
| Freeze, unfreeze, cancel subscriptions | ✅ | ❌ |
| Refunds, voids (outside the cafe) | ✅ | ❌ |
| Gym service charges: record, change the amount, void (§7 *Gym services*) | ✅ | ✅ |
| Cafe: products, categories, orders, and cancelling an order (§8) | ✅ | ✅ |
| Expenses, dashboard, reports, audit log, SMS resend | ✅ | ❌ |

---

## 2. Members

- Fields: `FullName`, `PhoneNumber`, `BirthDate` (optional), `Notes` (optional), `IsActive`.
- Phone numbers are normalized to E.164 before saving and before searching.
- Phone numbers are unique across all members, including inactive ones.
- Members are deactivated, never deleted. Inactive members cannot check in or receive new subscriptions.
- Phone input accepts Persian (۰-۹), Arabic (٠-٩), and English digits.
- Only Iranian mobile numbers are accepted. Landlines are rejected (`Members.PhoneNotMobile`): the number receives SMS reminders. Foreign numbers are rejected (`Members.PhoneNotIranian`): the gym has no foreign members, and a visitor can use the gym without being registered as a member.
- Every member has a phone number (required).
- Limits: full name at most 200 characters, notes at most 1000.
- Birth date (decided with the developer, 1405/06/31 — roadmap 2.4).
  - Optional and expected to stay empty for most members: the gym has no birth date for anyone who joined before this field existed, and staff must never be forced to invent one.
  - A business date (`DateOnly`) like every other date here: stored Gregorian, typed and shown Jalali.
  - It cannot be in the future (`Members.BirthDateInFuture`) and cannot be more than 120 years ago (`Members.BirthDateTooOld`). Both are judged against the gym's today, so the rule lives in the entity and the handler passes the date in.
  - It is not searchable and does not appear in the members list; it is shown on the member's profile.
- An inactive member's details can still be edited, for example to correct a phone number before reactivating.
- Names are stored as entered and also in a normalized search column (see section 13).
- Name search is partial, case-insensitive, and uses the normalized form. Phone search normalizes the input first.
- Search and list (decided with the developer in task 2.2):
  - One search box takes a name or a phone number. Input made only of digits, `+`, spaces, dashes and brackets is a phone search; anything else is a name search.
  - A whole phone number, in any format, finds the member with exactly that number. At least 4 digits that are not a whole number match anywhere in the stored number (leading zeros are dropped first, so `0912 123` works). Fewer digits, or a number that matches nobody, returns an empty list, not an error.
  - A search needs at least 2 characters after normalization (`Members.SearchTooShort`). A blank search lists everyone.
  - The list and search include inactive members by default, so staff can find someone to reactivate or correct. An optional filter shows only active or only inactive members.
  - Results are sorted by name.
- Deactivating an inactive member, or reactivating an active one, succeeds and changes nothing.
- Member screens (decided with the developer in task 2.3):
  - The home page is the member search. The server status page moved to its own menu item.
  - The search runs by itself about 300 ms after typing stops; Enter searches at once.
  - Deactivating asks for no confirmation: nothing is deleted, and reactivating undoes it in one click.
  - After a new member is saved, their profile opens.
  - Phone numbers are shown in the local format with Persian digits (`۰۹۱۲ ۱۲۳ ۴۵۶۷`); the API stores and returns E.164.

---

## 3. Plans

- Fields: `Name`, `DurationDays` (> 0), `SessionCount` (null means unlimited, otherwise > 0), `Price` (>= 0), `IsActive`.
- Inactive plans cannot be sold. Existing subscriptions are not affected.
- Editing a plan never changes existing subscriptions.
- Details (decided with the developer in task 3.1):
  - Limits: name at most 100 characters, `DurationDays` 1 to 365, `SessionCount` 1 to 365 (or null for unlimited).
  - `Price` has at most 2 decimal places. More is refused, never rounded (`Plans.PriceTooManyDecimals`).
  - Names are unique across all plans, inactive ones included, compared in normalized form (§13), so the same name typed with the Arabic ي is a duplicate (`Plans.NameAlreadyExists`).
  - Owner and Staff can list and view plans (staff sell subscriptions); only the Owner creates, edits, activates or deactivates them.
  - The plan list is paged like every list, active plans first, then by name, with an optional active/inactive filter.
  - Activating an active plan, or deactivating an inactive one, succeeds and changes nothing.

### The single-session plan (تک‌جلسه‌ای)

Decided with the developer, 1405/07/03. Roadmap 6.5.3.

- A plan has a `Kind`: `Membership` or `SingleSession`. Every plan that existed before this is a
  `Membership`.
- There is exactly **one** `SingleSession` plan, enforced by a partial unique index on `plans` where
  the kind is single-session. One rate for everyone: the gym charges a walk-in visitor a single
  figure, and changing that figure is editing this plan's price, which never changes what a past sale
  was worth (§3 above). So there is no second single-session plan and no versioning of one.
- Its shape is not typed by hand: a `SingleSession` plan always has `DurationDays = 1` and
  `SessionCount = 1`. Anything else is refused (`Plans.SingleSessionShape`), and a check constraint
  refuses it in the database too.
- The Owner creates it once from the plans screen, like any other plan. It is **not** seeded at
  startup: its price is the gym's own number and nothing may invent one. *Decided by Claude during
  task 6.5.3; pending review.*
- It can be deactivated like any plan, which stops single-session entry. The entry screen says that
  is why, rather than offering an action that fails with nothing to explain it.

---

## 4. Subscriptions

- At sale, the subscription stores a snapshot of the plan's **numbers**: `Price`, `DurationDays`, `TotalSessions`. Editing a plan afterwards never changes what a past sale was worth.
- The plan's **name** is not snapshotted. It is read live through `PlanId`, so renaming a plan corrects the label on every subscription and receipt it has ever appeared on. The name is how a plan reads; the numbers are what was sold. Renaming a plan into a different product therefore mislabels history — change the numbers and it is a new plan, not a rename.
- `StartDate` and `EndDate` are `DateOnly` in the gym's time zone. `EndDate = StartDate + DurationDays - 1` (the end date is inclusive).
- A member never has two subscriptions covering the same date.
  - No current or queued subscription: the new one starts today.
  - Otherwise: the new one is queued and starts the day after the latest existing `EndDate`.
- Selling (decided with the developer in task 4.2):
  - **Assign** sells a chosen plan. **Renew** sells the same plan as the member's latest subscription (by `EndDate`, cancelled ones included), at that plan's current name, price and limits, not the old snapshot. A member with no subscription has nothing to renew (`Subscriptions.NothingToRenew`); an inactive plan cannot be renewed (`Plans.Inactive`).
  - Both follow the same start-date rule, and both are refused for an inactive member (`Members.Inactive`).
  - A cancelled subscription covers no dates: it neither delays a new sale nor counts as an overlap.
  - If the latest subscription is `Exhausted` (all sessions used before its `EndDate`), the new one does not wait: the exhausted one ends yesterday and the new one starts today. If the exhausted one started today, it ends today and the new one starts tomorrow, so two subscriptions never cover the same date.
  - The same applies when the sale came first and the sessions ran out afterwards: if the current subscription becomes `Exhausted` while a queued one is waiting, the next check-in closes the exhausted one early and moves the queued one forward to start today, keeping its full duration. The same edge case holds — an exhausted subscription that only started today still covers today, so the queued one starts tomorrow and the member cannot check in until then.
  - Two sales for the same member at the same moment are handled one after the other: the second waits for the first and is queued after it. Both succeed.
  - The no-overlap rule is enforced by the database too: a Postgres exclusion constraint on (member, date range) for non-cancelled subscriptions. If a write ever gets past the application's ordering, it is refused with `Subscriptions.ChangedConcurrently` rather than stored.
- Status is calculated, never stored. First match wins:
  1. `Cancelled`
  2. `Frozen`
  3. `Upcoming` (today < StartDate)
  4. `Expired` (today > EndDate)
  5. `Exhausted` (limited plan and UsedSessions = TotalSessions)
  6. `Active`
- `ConsumeSession(today)` fails unless the status is `Active`. It increments `UsedSessions`.
- `RestoreSession()` decrements `UsedSessions` (used only by cancel check-in) and never goes below 0.
- Database: check constraint `used_sessions <= total_sessions` (when total is not null); `xmin` concurrency token.

### Single-session subscriptions (تک‌جلسه‌ای)

Decided with the developer, 1405/07/03. Roadmap 6.5.3.

A single-session sale is an **ordinary subscription** sold from the one single-session plan (§3), not
a separate kind of record. It has a price, a payment, a place in the member's debt, an attendance row
and a locker like anything else, and the member can buy هوازی during the visit (§7). What makes it
different is that it is invisible to every rule that orders a member's calendar: it is one day for one
visit, and it must never move, delay or shorten what the member already bought.

- The flag is **snapshotted onto the subscription** at sale, not read live through `PlanId` the way
  the plan's name is. Two reasons: the exclusion constraint below lives on `subscriptions` and cannot
  join to `plans`, and the shape of a past sale must not change if the plan ever does.
- **Start date is always today**, and `EndDate` is today. It never reads the member's calendar and it
  is never queued, whatever else the member holds.
- **Overlap:** the no-overlap rule and its exclusion constraint apply to **membership** subscriptions
  only. A single-session subscription may cover a date a membership also covers, and two of them may
  cover the same date. The constraint's condition becomes "not cancelled **and not single-session**".
  What stays guarded is the rule that matters: two memberships never cover the same date.
- **When both are usable today, the single visit is consumed first.** Because a single visit may now
  overlap a membership, a member can hold two `Active` subscriptions on one day, and check-in has to
  spend one of them. It spends the visit: it is worth nothing tomorrow, while the membership's
  sessions keep. Older first if there are two visits. *Decided by Claude during task 6.5.3; pending
  review — it decides whose money is spent, so it is a rule and not an implementation detail.*
- **It never moves anything.** Selling one does not close an `Exhausted` membership early and does not
  pull a queued subscription forward; and a used single-session subscription is itself never the
  "current exhausted subscription" those rules act on. Without this, selling a single visit to a member
  whose monthly pack has run out of sessions would rewrite that pack's `EndDate` to yesterday — the
  desk would be changing the member's own subscription by letting them in for one day.
- **Renew is refused.** Renewal needs a plan with more than one session, so renew reads the member's
  latest **membership** subscription and ignores single-session rows entirely. A member whose only
  history is single-session visits has `Subscriptions.NothingToRenew`.
- **Freeze is refused** (`Subscriptions.SingleSessionNotFreezable`): a one-day subscription has nothing
  to suspend. Unfreezing a membership shifts that member's queued subscriptions (§4 *Freeze*) but never
  a single-session row — those are dated today or earlier, and shifting them would rewrite history.
- **A member whose membership is frozen and who comes in today is sold a single visit, and their freeze
  is not touched.** Freeze and unfreeze stay Owner-only (§1): the desk needs neither of them to let the
  person in, so this feature changes no permissions.
- **Two visits in one day** are two single-session sales. The one-open-visit-per-member index (§7) means
  the member checks out before coming back.
- **Cancel and refund are unchanged** (§4 *Cancel*, §5): cancellable and refundable before the visit,
  neither afterwards, and the 30-minute cancel-check-in window restores the session, which brings
  `UsedSessions` back to 0 and makes it refundable again.
- **It is sold to the person who uses it.** One member, one visit. Buying visits for other people is not
  possible: a subscription belongs to one member and a member holds one open visit at a time, so ten
  friends would need ten member records — and every member needs a unique Iranian mobile (§2). The group
  case is recorded under "Future — Group visits" in the roadmap, with why it needs more than a
  subscription.

### Freeze
- Only `Active` subscriptions can be frozen. A frozen subscription cannot be used for check-in.
- Unfreezing extends `EndDate` by the number of frozen days, and shifts that member's queued subscriptions by the same number of days.
- Total frozen days cannot exceed `Gym:MaxFreezeDaysPerSubscription`.
- Details (decided with the developer in task 4.1):
  - Frozen days = unfreeze date − freeze date. Freezing on the 10th and unfreezing on the 12th is 2 days; freezing and unfreezing on the same day is 0.
  - A subscription can be frozen several times; the days add up.
  - Freezing is refused when no freeze days are left (`Subscriptions.FreezeLimitReached`).
  - A freeze that runs past the remaining allowance still ends normally, but `EndDate` moves only by the days that were left. The Owner is never blocked from unfreezing.

### Cancel
- Requires a reason. Payments are not deleted; money is returned only through refunds.
- Details (decided with the developer in task 4.1):
  - Only a subscription nobody has used yet can be cancelled: `UsedSessions = 0` **and** the status is `Upcoming`, `Active` or `Frozen` (decided with the developer, 1405/06/31 — it replaces the earlier task 4.1 rule that any status could be cancelled). A service that has been consumed is not un-sold, and an `Expired` or `Exhausted` subscription is history, not something still to decide about.
    - `Subscriptions.AlreadyUsed` when a session has been consumed, `Subscriptions.Expired` when it has ended, `Subscriptions.Cancelled` when it was cancelled before.
    - A visit recorded by mistake is undone with cancel check-in, which restores the session (§7). While the member is still at the desk that brings `UsedSessions` back to 0 and the subscription becomes cancellable again — that 30-minute window is the intended escape hatch, not an exception to this rule.
  - The reason is required and at most 500 characters.
  - Cancelling does not move queued subscriptions earlier.
  - A cancelled subscription cannot be unfrozen, frozen or used.

### Payment status (calculated)
- Net paid = payments − refunds.
- `Paid` when net paid >= Price, `Partial` when 0 < net paid < Price, `Unpaid` when net paid = 0.
  - A free (`Price = 0`) subscription is `Paid` from the start, never `Unpaid`, even though its net
    paid is also zero: it owes nothing. *Decided by Claude during task 4.4; pending review.*

---

## 5. Payments

- A payment belongs to exactly one of: a Subscription, a CafeOrder or a ServiceCharge (§7 *Gym services*), enforced by a check constraint. The service charge target arrived in task 5.7 and the cafe order target in task 7.2.
- Fields: `Kind` (Payment or Refund), `Amount` (> 0), `Method`, `ReferenceNumber` (optional), `PaidAt` (UTC), `ReceivedByUserId`, `Reason` (required for refunds).
- A subscription cannot be overpaid.
- Payments are never edited or deleted. A mistaken entry is fixed with a full refund whose reason explains the mistake (a "void").
- A refund cannot exceed the current net paid amount.
- A subscription can only be refunded while nobody has used it (`UsedSessions = 0`), whatever its status (decided with the developer, 1405/06/31). Sessions already taken are not bought back. `Payments.RefundAfterUse` otherwise.
  - This makes the refund unavailable for correcting a payment typed wrong on a subscription the member has already used. That is deliberate: the overpayment guard above refuses more than the price as it is typed, so a wrong figure is caught at the desk, and a visit entered by mistake can be undone within the cancel window (§7).
- Details. *Decided by Claude during task 4.4; pending review.*
  - `Amount` follows the same money rule as `Plan.Price`: at most 2 decimal places, refused rather
    than rounded, capped at the same column limit (`numeric(18,2)`).
  - `ReferenceNumber` is at most 100 characters; `Reason` is at most 500 (the same limit as a
    subscription's cancellation reason). Blank input is stored as `null`.
  - Registering a payment sets `PaidAt` to the current moment; there is no way to record a
    backdated payment.
  - The `CafeOrderId` column and the one-target check constraint exist from this task on;
    cafe orders started setting `CafeOrderId` in task 7.2.
- Revenue for a period = payments − refunds, by `PaidAt` in the gym's time zone. There is no separate Income table.

### Member debt (open account, حساب باز)

Decided with the developer, 1405/06/31. Implemented for subscriptions in task 4.7 and for service
charges in 5.7; cafe orders join the same total in Phase 7.

- Debt is **calculated, never stored**, the same way subscription status and payment status are: there is no balance column to keep correct, and no nightly job to recompute one.
- A member's debt = what is still owed on their non-cancelled subscriptions, plus what is still owed on their non-voided service charges (§7 *Gym services*), plus (from Phase 7) what is still owed on their non-cancelled cafe orders. Per item that is `Price − net paid`, never below zero.
- Cancelled items owe nothing. A subscription can only be cancelled while nobody has used it (§4), and money already paid on one comes back as a refund, not as a debt that quietly disappears. A voided service charge owes nothing for the same reason.
- **There is no wallet and no credit balance.** Every payment still belongs to exactly one subscription, service charge or cafe order (§5 above), so the total is always the sum of named items and never a figure nobody can account for. A member who hands over a lump sum for several things has it entered against each of those items.
  - Reviewed with the developer on 1405/07/01, who proposed a wallet so that money taken at the desk would not have to be tied to a named item, and then decided against it. The reasons, recorded so the question does not have to be reopened from scratch: a wallet splits "money arrived" from "money earned", so the revenue figure in §12 would have to choose between the deposit date and the allocation date; debt would become a net figure rather than the sum of items the breakdown can point at; and refunds would gain a destination. *If the gym later wants one, the balance must be calculated from a deposit/allocation ledger, never stored in a column, like every other figure here.*
- The total is never shown on its own: opening it shows the breakdown item by item — what it is (subscription, service charge or cafe order), its date, its price, what has been paid and what is left. The developer's requirement is that "جزء به جزء" is always one click from the number.
- **Debt outlives the thing that created it.** An `Exhausted`, `Expired` or otherwise finished subscription keeps its outstanding balance and keeps accepting payments: the front desk can register a payment whenever anything is left to pay, whatever the subscription's status. Sessions running out settles nothing.
- Paying in instalments is ordinary, not a special case: several payments against the same item, each its own row with its own moment, method, reference number and the staff member who took it. The existing payment rules already allow this; nothing new is needed for it.
- A free item (`Price = 0`) owes nothing and never appears in the breakdown.

---

## 6. Lockers

- Fields: `Number` (unique), `IsOutOfService`.
- A locker is occupied when an open attendance references it. Occupancy is derived, never stored,
  and so is the member holding it: the locker list names whoever the open attendance belongs to,
  so the desk can answer "whose is locker 1?" without opening attendance.
- A locker cannot be marked out of service while occupied.
- Staff see the list and change a locker's service state; only the Owner adds a locker. The
  person who finds a locker broken is the one at the desk, and the same person sees it repaired.

---

## 7. Attendance

### Check-in (one database transaction)
Preconditions: the member is active, has an `Active` subscription, and has no open attendance.
1. Load the subscription that is in effect today — an `Active` one always wins over a queued renewal that ends later. If none is active, apply the exhausted-with-a-queue rule above.
2. `subscription.ConsumeSession(today)`.
3. Choose a locker at random from those that are in service and not occupied. Random, not lowest-numbered: any free locker is equally valid, and always taking the lowest one wore out the first few lockers while the high numbers were never used.
4. Insert the Attendance row with `CheckedInAt` and the locker.
5. Save and commit.

- If no locker is available, check-in still succeeds with no locker, and the response includes a warning.
### The "currently inside" board

- One row per open visit: the member, the locker, the time they came in, how much of their
  subscription is left, and the visit's هوازی charge.
- Sessions are shown as used of total. An unlimited subscription has no total to count against,
  so it reads "نامحدود" rather than a bar with no denominator.
- A single-session visit has no session count to show: the row reads "تک‌جلسه‌ای" where used-of-total
  goes, and it is excluded from **both** "needs attention" thresholds below. It is always 1 of 1 used
  and always expires today, so the mark would be on for every such row — and a mark that is always on
  says nothing, the same reasoning that removed the status badge from this board.
- A row is marked as needing attention when the subscription behind that visit has **3 or fewer
  sessions left**, or **expires within 5 days**. Both are shown to the front desk while the member
  is standing there, which is the only moment renewing costs nobody a phone call.
  These are the desk's thresholds. Phase 10's SMS reminders (§10) have their own configured ones;
  they should be set to the same numbers, so what the desk sees and what the member is texted
  about do not disagree.
- No status badge here. Check-in refuses a subscription that is not usable today (§7 *Check-in*),
  so every row on this board would read "فعال" — a badge that is always the same tells nobody
  anything. Status belongs where expired and unsubscribed members appear together.

- Money owed never blocks a check-in. The visit is recorded and the front desk is shown the member's outstanding total, the same way a missing locker is a warning rather than an error (§0, §5 *Member debt*).
- When nothing is usable because the member used every session on the subscription's first day and renewed the same day, the refusal is `Subscriptions.NextStartsTomorrow` ("today is over for them, come back tomorrow"), not the queued subscription's own `Subscriptions.NotStarted`, which sounds like the sale went wrong.
- Database: partial unique index on `member_id` where `checked_out_at IS NULL`, and on `locker_id` where `checked_out_at IS NULL`. Cancelled attendances count as closed.
- Unique-violation or concurrency errors are returned as a clear 409 conflict, never a 500.

### Check-out
- Only an open attendance can be checked out. Sets `CheckedOutAt`, which frees the locker.

### Confirming at the front desk
Decided with the developer, 1405/07/04.
- Everywhere the desk can check a member in or out (the entry screen, the member's profile and the "currently inside" board), check-in and check-out each ask the desk to confirm before anything is sent, in the same box. A mistaken press costs a session or closes someone else's visit, and undoing either is a separate action with its own rules (*Cancel check-in*).
- A member who is inside is offered check-out, not check-in. Check-in would only be refused (*Check-in*: no open attendance).
- After a check-in, the same box shows the locker (or that none was free), the plan and the sessions left, and the member's debt item by item: unpaid subscriptions, services such as هوازی, and cafe orders (§5 *Member debt*). It stays until the desk closes it.
- Before a check-out, the box shows the locker to take back and the same plan, sessions and itemized debt, so the desk can collect what is owed while the member is still there. Debt is shown, never enforced: check-out is not refused for money owed, the same way check-in is not.
- When the visit has a locker, the desk must tick "key received" before the check-out can be confirmed: closing the visit hands the locker to the next person in. After check-out, the box shows that the locker is free and repeats the itemized debt.
- Selling a single visit from that box needs no second confirmation: pressing the priced button is already the decision.
- Cancelling a check-in (*Cancel check-in*) asks in the same box, with nothing else shown: it gives the session back and frees the locker, so a stray press is worth one more click.

### Cancel check-in
- Allowed only for an open attendance within `Gym:CancelCheckInWindowMinutes` of check-in.
- Restores the session, frees the locker, and marks the attendance cancelled (who and when). The row is kept.
- Cancelled attendances are excluded from attendance reports.

### Auto-checkout
- A nightly job at `Gym:ClosingTime` closes all open attendances and marks them `AutoClosed`. The session stays consumed.

### Opening hours (PENDING — not enforced yet, roadmap 11.4)
Decided with the developer, 1405/07/04.
- The gym is open from `Gym:OpeningTime` (06:00) to `Gym:ClosingTime` (23:00), in the gym's time
  zone, every day (§0: no closed days). Outside those hours there is no check-in and no check-out.
  Check-in is refused with a clear error, not recorded. Check-out has nothing to close, because
  *Auto-checkout* has already closed every visit at 23:00.
- Together with *Auto-checkout*, this means that from 23:00 on, nobody is recorded as inside the gym.
  Today a check-in after 23:00 still succeeds and stays open until the next night's job.
- There is no Owner override. If the gym ever needs to open at night, the hours change as a new
  rule, agreed with the developer and written here first. It is not an exception granted at the desk.
- Until task 11.4 is done, **the code must not enforce this rule**. The developer checks in test members
  at night, and enforcing it early would block that work.

### Gym services (هوازی and anything else sold during a visit)

Decided with the developer, 1405/06/31. Implemented in task 5.7.

- A **service charge** is money owed for something the member used during a visit. Today there is exactly one kind, `Cardio` (هوازی, the treadmill); sauna or massage would be new kinds of the same thing, not new tables.
- **The price is not calculated by the system, on purpose.** The gym's rate (for example 10,000 Toman per 3 minutes) changes without notice and staff already work it out at the desk. The system takes the number they type and never checks it against a rate. There is no rate setting to keep in sync with reality.
- The amount is per visit, not per member: the same member may use the treadmill today and not tomorrow, so there is no cardio price on the member record.
- Recorded against an **open** visit (`CheckedOutAt IS NULL`, not cancelled) and only for the member of that visit. Front desk work, so both roles.
- One non-voided charge per visit per kind. While the visit is open and nothing has been paid against it, staff can change the amount or remove it — nothing has been settled yet. After check-out, or after the first payment, it is a financial record: it is corrected with a **void plus a reason**, and a fresh charge if one is due (§5: financial records are never edited or deleted).
- Cancelling a check-in voids that visit's service charges too, with the reason that the check-in was cancelled: money for a visit that never happened is not owed. *Decided by Claude during task 5.7; pending review.*
- **Voiding a charge that has been paid gives the money back**, as refunds written in the same transaction, one per payment method that is in credit — cash taken at the desk comes back as cash, a card payment is reversed on the card. §5 says there is no wallet, so the money cannot simply sit against the member's name, and neither the void screen nor cancel check-in has to ask which method to use (decided with the developer, 1405/07/01).
- **Recording, changing and voiding a charge are all Staff or Owner** (decided with the developer, 1405/07/01). This is a deliberate exception to §1's permissions table, which puts "refunds, voids" with the Owner: the amount is typed at the desk and the desk has to be able to take back its own mistake while the member is still standing there. The controls are that the reason is required and the audit log records who did it.
- There is no separate refund endpoint for a service charge. A charge that needs correcting is voided with a reason and re-entered at the right amount; a partial refund of a treadmill amount is that, not a refund.

- Auto-checkout changes nothing about a charge.
- A service charge is paid like anything else: it is one of the three things a payment can belong to (§5), it counts toward the member's debt (§5 *Member debt*), and it can be settled later or in instalments.
- The amount follows the same money rules as every other amount: greater than zero, at most 2 decimal places, `numeric(18,2)`.

---

## 8. Cafe

**The gym does not count stock** (decided by the Owner, 1405/07/03, when Phase 7 started). There is
no `StockQuantity`, no StockMovement ledger and no "not enough left" refusal anywhere: the Owner
does not want to know how many bottles are in the fridge, and a number nobody maintains is worse
than no number. A product is a line on the price list, not an item in a warehouse. *This replaces
the stock rules that stood here before; roadmap 7.1 was rewritten with them.*

- Categories (`Name`, `IsActive`) and Products (`Name`, `CategoryId`, `Price`, `IsActive`).
  Nothing else is tracked about a product.
- Products are switched off, never deleted: an order already sold points at the product it sold.
  A category is a heading with no financial history, so it can be renamed, switched off, and
  deleted while no product uses it. *Decided by Claude during task 7.1; pending review.*
- Product names are unique across the whole cafe, not merely within a category, compared in
  normalized form (§13) — two products called "آب معدنی" in different categories would be a
  coin flip at the till. *Decided by Claude during task 7.1; pending review.*
- The price follows the same money rules as `Plan.Price`: at most 2 decimal places, refused
  rather than rounded, `numeric(18,2)`, and never negative.
- **The whole cafe is front-desk work, both roles** (decided by the Owner, 1405/07/03 and
  widened the same day): adding, editing, removing and switching off both products *and*
  categories. The person who sees a new box of bars arrive, with its price on it, is the one at
  the desk, and making them wait for the Owner means the item is sold off the books. Same
  reasoning as the service-charge amounts in §7. Staff have no restriction in the cafe at all;
  the controls are the audit log and the fact that an order snapshots what it sold.
- **A product or a category is switched on and off rather than deleted, and the switch means
  "in stock" to the people using it** — `موجود` / `ناموجود` on screen. The gym counts no
  quantities, so this flag is the only thing that says whether something can be bought today.
- **Something is sellable only when it is switched on *and* its category is switched on.**
  Switching a category off takes its whole shelf out of the till in one action — that is the
  point of having the switch on a category at all. The screens where a purchase is rung up (the
  till, and the product search on an order) show only sellable items; the management screens show
  everything with its state. *The rule that a category's state reaches its products is Claude's;
  pending review.*
- A category can still be deleted outright while no product uses it. Switching it off is for
  "not today"; deleting is for "this was a mistake".
- An order has items with snapshots of `ProductName` and `UnitPrice`, and `Quantity` > 0. The
  snapshot is what makes editing a price safe: yesterday's order keeps yesterday's figure.
- `MemberId` on an order is optional (walk-in customers are allowed), but an order **on account**
  must name the member: putting it on an account is exactly the act of entering the purchase under
  that person's name so the money can be collected later (decided with the developer, 1405/06/31).
- Creating an order writes the order and its items in one transaction, and is never refused for
  stock. The payment belongs to the same transaction only when the order is paid there and then;
  an order on a member's account is created unpaid and settled later with ordinary `Payment` rows
  — same partial/paid status, same instalments, and it counts toward that member's debt
  (§5 *Member debt*).
- A walk-in order (no member) is paid in full at creation: there is no account to put it on.
- **A purchase made while the member is inside is tied to that visit** (decided with the developer,
  1405/07/04), exactly as a هوازی charge is (§7 *Gym services*). It is rung up from the "currently
  inside" board, goes on the member's account with nothing paid, and can be added only to an open
  visit that is that member's own (`CafeOrders.VisitNotOpen`, `CafeOrders.VisitOfAnotherMember`).
  At check-out the box lists what the visit bought and the member's debt added up by source —
  plan, هوازی, cafe — before the item-by-item list. An order from the till names no visit.
  - **Cancelling a check-in leaves the visit's cafe orders standing** (decided by the developer,
    1405/07/04): the goods were handed over whether or not the visit counted — the member may
    have bought something and had to leave — so the sale stays on record and on their account.
    An order is only ever undone by its own cancellation with a reason. This is the opposite of a
    هوازی charge, which is voided with the check-in because it was for using the gym during
    that visit.
- **An order is never edited. It is cancelled with a reason and rung up again** (decided by the
  Owner, 1405/07/03). Two of something that should have been one is a cancellation and a fresh
  order, not a quantity corrected in place — §5's rule that financial records are never edited,
  applied to the cafe. An order that is one minute old and unpaid is no exception: the exception
  is what would make the audit trail arguable.
- Cancelling an order requires a reason and creates a refund for whatever was actually paid.
  Nothing is put back anywhere, because nothing was counted. An unpaid order on account leaves
  nothing to refund; cancelling it simply removes that item from the member's debt.
  - **The money goes back the way it came**: one refund per payment method still in credit, in
    the same transaction as the cancellation, carrying the cancellation's reason — the same rule
    §7 uses for a voided service charge, so the cancel screen never asks which method to use
    (decided with the developer, 1405/07/04).
  - A cancelled order takes no more payments (`CafeOrders.AlreadyCancelled`), and cannot be
    cancelled twice.
- **Order history** lists orders newest first, for the whole counter or one member, optionally
  within an inclusive range of the order's business date (`OrderedOn`). Cancelled orders are
  included and marked, the same choice the attendance history makes: it is the record of what
  happened at the till, not a revenue figure. *Decided by Claude during task 7.3; pending review.*
- **Cancelling is Staff or Owner** (decided by the Owner, 1405/07/03), the same exception §7 makes
  for service charges and for the same reason: the customer is still standing at the desk, and the
  person who rang it up has to be able to take it back. The controls are the required reason and
  the audit log. This is deliberately not the general rule for refunds, which stay with the Owner.
- **A paid-now order can be part-paid.** Whatever the customer hands over is registered against the
  order and the rest stays on their account — the ordinary instalment rule of §5, so nothing new is
  needed for it. "Paid in full at creation" applies only to a walk-in order with no member, because
  there is no account to leave a balance on.
- **What the gym buys for the cafe is money, not goods** (decided by the Owner, 1405/07/03). The
  cost of restocking is entered in Phase 8 as an expense under "Cafe Purchasing" (§9), and gross
  profit is worked out in the reports (§12) as cafe revenue minus those expenses. No purchase
  price is recorded against a product or an order, so the same money is never counted twice.

---

## 9. Expenses

- ExpenseCategory is a table, seeded with: Rent, Salary, Electricity, Water, Equipment, Maintenance, Cafe Purchasing, Other. The Owner can add more.
- Fields: `Amount` (> 0), `CategoryId`, `ExpenseDate` (DateOnly), `Description`, `ReferenceNumber` (optional), `RecordedByUserId`.
- Expenses can be edited (every edit is audited). They are voided with a reason, never deleted. Voided expenses are excluded from reports.

---

## 10. Notifications (SMS)

- Phone number is the only contact channel. Inactive members receive no SMS.
- Types: `SubscriptionExpiring`, `LowSessions`.
- A daily job creates notifications for `Active` subscriptions where:
  - days until `EndDate` <= `Sms:ExpiringDaysBefore`, or
  - the plan is limited and remaining sessions <= `Sms:LowSessionsThreshold`.
- Unique index on (`subscription_id`, `type`): the same reminder is never created twice.
- Status: `Pending`, `Sent`, `Failed`. Sending retries with backoff and becomes `Failed` after `Sms:MaxAttempts`.
- Nothing is sent during quiet hours; sending is deferred.
- The Owner can manually resend a failed notification.
- Development and tests use `FakeSmsSender`, which only logs.
- The provider will be an Iranian panel. Those generally require a **pre-approved template** for service
  messages rather than free text: the Persian wording is registered in the panel and the caller sends a
  template id plus named parameters. So `ISmsSender` is defined template-first from task 10.1, even while
  only `FakeSmsSender` exists. If the panel chosen at purchase does allow free text, a template-first
  interface still works; the reverse does not. Confirm this when the panel is bought.

---

## 11. Audit

- A `SaveChangesInterceptor` writes an AuditLog row for every insert, update, and delete.
- Fields: `UserId`, `Action`, `EntityType`, `EntityId`, `OccurredAt` (UTC), `OldValues` and `NewValues` (jsonb, changed properties only), `IpAddress` when available.
- Never audit password hashes, security stamps, or token hashes.
- AuditLog is append-only.
- Details. *Decided by Claude during task 1.6 while the developer was away; pending review.*
  - Audit rows are written in the same database transaction as the change, so a rolled-back change leaves no audit row.
  - Also never recorded: concurrency stamps and row versions (they change on every save), and the `CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` fields (the audit row already records who and when). An update whose only changes are such fields writes no row.
  - Everything else is audited, including refresh tokens: every login and every refresh adds rows. That volume is the price of "every insert, update and delete"; the audit screen (task 11.1) can filter it out.
  - Append-only is enforced by a database trigger that rejects UPDATE and DELETE.

---

## 12. Reports

- Date ranges are inclusive and interpreted in the gym's time zone.
- Revenue by source (subscriptions, gym services, cafe) and by payment method.
- Expenses by category (voided excluded). Net profit = revenue − expenses.
- Attendance per day and by hour (cancelled excluded).
- Active subscriptions, expiring soon, low sessions. Top cafe products.
- Single-session (تک‌جلسه‌ای) revenue is reported separately from membership sales: they are the same
  kind of record (§4) but not the same business. Until Phase 9 implements the split, the
  subscription-sales figure includes single-session visits.

---

## 13. Persian language

- The UI is Persian and right-to-left.
- Text normalization (applied to names before saving the search column, and to every search input):
  - Arabic ي (U+064A) and ى (U+0649) → Persian ی (U+06CC)
  - Arabic ك (U+0643) → Persian ک (U+06A9)
  - Persian and Arabic digits → English digits
  - Remove harakat (Arabic vowel marks, U+064B to U+0652) and tatweel (U+0640): they are visual only
  - Zero-width non-joiner (U+200C) → a normal space, because users type half-space and space interchangeably
  - Remove other zero-width and direction marks (U+200B, U+200D, U+200E, U+200F, U+FEFF)
  - Trim and collapse repeated spaces
- Dates are shown in the Jalali calendar. They are stored and exchanged as Gregorian `DateOnly` and UTC timestamps. A date is typed into a Jalali calendar picker, never as a Gregorian date, and converted to ISO before it is sent.
- **Every amount of money on screen is protected against miscounted zeros** (decided with the developer, 1405/06/31 — roadmap 4.8). Toman amounts are large enough that `500000` and `5000000` look alike at a glance, and a wrong figure here is a wrong figure in the gym's books.
  - Every amount that is **typed** goes through the shared money field: Persian digits, grouped in threes as it is typed (۵۰۰٬۰۰۰), and the same amount written out in words underneath it — «پانصد هزار تومان». The words are the check: nobody miscounts a word.
  - Every amount that is **displayed** goes through one formatter, which groups in threes and appends "تومان".
  - No screen formats an amount by itself, and no money value is ever held as a JavaScript number: rounding a price is never acceptable.
- Reports offer Jalali periods (this Jalali month, this Jalali year) that the frontend converts to Gregorian date ranges.
- SMS messages are Persian. Unicode SMS parts hold fewer characters than Latin ones, so templates are kept short and the part count is calculated before sending.
