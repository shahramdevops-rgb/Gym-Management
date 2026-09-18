# Business Rules

This file is the source of truth for how the gym works.
If code and this file disagree, this file wins. If this file is silent, ask before implementing.

---

## 0. Settings still to decide

These values live in configuration (the `Gym` and `Sms` sections). Decide each one before the phase listed.

| Setting | Decide before | Notes |
|---|---|---|
| `Gym:TimeZone` | Phase 4 | Asia/Tehran. Defines "today" for every business date. |
| `Gym:Currency` | Phase 4 | Toman: choose one storage unit and never mix. Amounts are always `decimal`. |
| `Gym:PhoneDefaultRegion` | Phase 2 | Two-letter region code used to normalize local phone formats is 09 |
| Payment methods | Phase 4 | For example Cash, Card, BankTransfer. |
| `Gym:MaxFreezeDaysPerSubscription` | Phase 4 | Total frozen days allowed per subscription. |
| `Gym:ClosingTime` | Phase 5 | Local time for the nightly auto-checkout. |
| Check-in with an unpaid or partially paid subscription | Phase 5 | Block it, or allow it with a warning? |
| Cafe orders paid in full at creation (no tabs) | Phase 8 | Suggested: yes. |
| `Sms:ExpiringDaysBefore`, `Sms:LowSessionsThreshold`, `Sms:MaxAttempts`, quiet hours | Phase 11 | |
| SMS provider | Phase 11 | |

Decided values:
- `Gym:CancelCheckInWindowMinutes` = 30

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
- The Owner creates Staff accounts with a temporary password. Staff have `MustChangePassword = true`.
- A user with `MustChangePassword = true` may only call change-password and logout.
- Deactivated users cannot log in, and all their refresh tokens are revoked immediately.
- Access token: JWT, 15 minutes, kept in memory by the frontend (never localStorage).
- Refresh token: random value, stored only as a hash, rotated on every use, sent as an HttpOnly, Secure, SameSite=Strict cookie. Reusing an already-rotated token revokes the whole token family.
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
| Plans, lockers setup, staff accounts | ✅ | ❌ |
| Freeze, unfreeze, cancel subscriptions | ✅ | ❌ |
| Refunds, voids, cafe order cancellation, stock adjustments | ✅ | ❌ |
| Products and categories | ✅ | ❌ |
| Expenses, dashboard, reports, audit log, SMS resend | ✅ | ❌ |

---

## 2. Members

- Fields: `FullName`, `PhoneNumber`, `Notes` (optional), `IsActive`.
- Phone numbers are normalized to E.164 before saving and before searching.
- Phone numbers are unique across all members, including inactive ones.
- Members are deactivated, never deleted. Inactive members cannot check in or receive new subscriptions.
- Phone input accepts Persian (۰-۹), Arabic (٠-٩), and English digits.
- Names are stored as entered and also in a normalized search column (see section 13).
- Name search is partial, case-insensitive, and uses the normalized form. Phone search normalizes the input first.

---

## 3. Plans

- Fields: `Name`, `DurationDays` (> 0), `SessionCount` (null means unlimited, otherwise > 0), `Price` (>= 0), `IsActive`.
- Inactive plans cannot be sold. Existing subscriptions are not affected.
- Editing a plan never changes existing subscriptions.

---

## 4. Subscriptions

- At sale, the subscription stores a snapshot: `PlanName`, `Price`, `DurationDays`, `TotalSessions`.
- `StartDate` and `EndDate` are `DateOnly` in the gym's time zone. `EndDate = StartDate + DurationDays - 1` (the end date is inclusive).
- A member never has two subscriptions covering the same date.
  - No current or queued subscription: the new one starts today.
  - Otherwise: the new one is queued and starts the day after the latest existing `EndDate`.
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

### Freeze
- Only `Active` subscriptions can be frozen. A frozen subscription cannot be used for check-in.
- Unfreezing extends `EndDate` by the number of frozen days, and shifts that member's queued subscriptions by the same number of days.
- Total frozen days cannot exceed `Gym:MaxFreezeDaysPerSubscription`.

### Cancel
- Requires a reason. Payments are not deleted; money is returned only through refunds.

### Payment status (calculated)
- Net paid = payments − refunds.
- `Paid` when net paid >= Price, `Partial` when 0 < net paid < Price, `Unpaid` when net paid = 0.

---

## 5. Payments

- A payment belongs to exactly one of: a Subscription or a CafeOrder (enforced by a check constraint).
- Fields: `Kind` (Payment or Refund), `Amount` (> 0), `Method`, `ReferenceNumber` (optional), `PaidAt` (UTC), `ReceivedByUserId`, `Reason` (required for refunds).
- A subscription cannot be overpaid.
- Payments are never edited or deleted. A mistaken entry is fixed with a full refund whose reason explains the mistake (a "void").
- A refund cannot exceed the current net paid amount.
- Revenue for a period = payments − refunds, by `PaidAt` in the gym's time zone. There is no separate Income table.

---

## 6. Lockers

- Fields: `Number` (unique), `IsOutOfService`.
- A locker is occupied when an open attendance references it. Occupancy is derived, never stored.
- A locker cannot be marked out of service while occupied.

---

## 7. Attendance

### Check-in (one database transaction)
Preconditions: the member is active, has an `Active` subscription, and has no open attendance.
1. Load the member's current subscription.
2. `subscription.ConsumeSession(today)`.
3. Choose the lowest-numbered locker that is in service and not occupied.
4. Insert the Attendance row with `CheckedInAt` and the locker.
5. Save and commit.

- If no locker is available, check-in still succeeds with no locker, and the response includes a warning.
- Database: partial unique index on `member_id` where `checked_out_at IS NULL`, and on `locker_id` where `checked_out_at IS NULL`. Cancelled attendances count as closed.
- Unique-violation or concurrency errors are returned as a clear 409 conflict, never a 500.

### Check-out
- Only an open attendance can be checked out. Sets `CheckedOutAt`, which frees the locker.

### Cancel check-in
- Allowed only for an open attendance within `Gym:CancelCheckInWindowMinutes` of check-in.
- Restores the session, frees the locker, and marks the attendance cancelled (who and when). The row is kept.
- Cancelled attendances are excluded from attendance reports.

### Auto-checkout
- A nightly job at `Gym:ClosingTime` closes all open attendances and marks them `AutoClosed`. The session stays consumed.

---

## 8. Cafe

- Categories and Products (`Name`, `CategoryId`, `Price`, `StockQuantity`, `IsActive`).
- Stock changes only through the StockMovement ledger (`Purchase`, `Sale`, `Adjustment`, `Cancellation`). `Product.StockQuantity` is updated in the same transaction and can never go negative (check constraint).
- An order has items with snapshots of `ProductName` and `UnitPrice`, and `Quantity` > 0.
- `MemberId` on an order is optional (walk-in customers are allowed).
- Creating an order validates stock, writes Sale movements, and records the payment in one transaction.
- Cancelling an order requires a reason, restores stock with Cancellation movements, and creates a refund.
- Stock adjustments require a reason.

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

---

## 11. Audit

- A `SaveChangesInterceptor` writes an AuditLog row for every insert, update, and delete.
- Fields: `UserId`, `Action`, `EntityType`, `EntityId`, `OccurredAt` (UTC), `OldValues` and `NewValues` (jsonb, changed properties only), `IpAddress` when available.
- Never audit password hashes, security stamps, or token hashes.
- AuditLog is append-only.

---

## 12. Reports

- Date ranges are inclusive and interpreted in the gym's time zone.
- Revenue by source (subscriptions, cafe) and by payment method.
- Expenses by category (voided excluded). Net profit = revenue − expenses.
- Attendance per day and by hour (cancelled excluded).
- Active subscriptions, expiring soon, low sessions. Top cafe products.

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
- Dates are shown in the Jalali calendar. They are stored and exchanged as Gregorian `DateOnly` and UTC timestamps.
- Reports offer Jalali periods (this Jalali month, this Jalali year) that the frontend converts to Gregorian date ranges.
- SMS messages are Persian. Unicode SMS parts hold fewer characters than Latin ones, so templates are kept short and the part count is calculated before sending.
