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
- `Gym:ClosingTime` = 00:00, midnight (Asia/Tehran). Local time the nightly auto-checkout job runs at
  (decided as 23:00 in task 5.5; moved to midnight by the developer on 1405/07/04, 2026-09-26).
- `Gym:OpeningTime` = 06:00 (Asia/Tehran). The gym is open 06:00–00:00 (midnight), and with `Gym:ClosingTime`
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
- **The gym has two prices, and the Owner sets both in the app** (§3 *Prices*): the price of one
  session of a plan, and the price of a single-session (تک‌جلسه‌ای) visit. They are stored in the
  database, not in configuration, because the Owner changes them with inflation and must not need a
  deployment to do it (decided with the developer, 1405/07/05, replacing the 1405/07/03 rule that the
  single-session rate was a plan's price and not a setting — there are no plans any more).

---

## 1. Users and access

- Closed system. There is no registration endpoint. Members never log in.
- Roles: `Owner` and `Staff`.
- The Owner account is seeded at startup from configuration (`Seed:OwnerUserName`, `Seed:OwnerPassword`, optional `Seed:OwnerFullName`). Seeding is idempotent: running it twice creates nothing new.
  - "The Owner already exists" means any user already holds the Owner role, even if `Seed:OwnerUserName` has since changed. Seeding never modifies that existing Owner, including their password.
  - If `Seed:OwnerUserName` or `Seed:OwnerPassword` is missing, seeding logs a warning and does nothing — it never queries the database. Startup does not fail.
  - `Seed:OwnerFullName` defaults to "مدیر" when not configured. The Owner can rename themselves later.
  - The seeded Owner has `MustChangePassword = true`.
- Password policy, following NIST SP 800-63B: length and a blocklist, not composition rules (decided with the developer, 1405/07/04, task 11.5; replaces "8 characters with a letter and a digit"). A password is refused, with the first rule it breaks, when it:
  - is shorter than **12** or longer than 128 characters (`Auth.PasswordTooShort`, `Auth.PasswordTooLong`). 12 rather than NIST's 15 for a password-only login, chosen by the developer for the front desk;
  - has anything but English letters, digits, symbols and the space, i.e. printable ASCII (`Auth.PasswordNotEnglish`). Persian letters look the same but have different code points on different keyboards (ی/ي, ک/ك), so a Persian password set on one device could fail on another;
  - contains the account's user name, ignoring case (`Auth.PasswordContainsUserName`);
  - is a repetition or a run: fewer than 5 different characters, or any stretch of the alphabet, the digits (wrapping 9→0), a keyboard row or `!@#$%^&*()_+`, forwards or backwards (`Auth.PasswordTooSimple`);
  - contains the gym's own name (`Auth.PasswordContainsGymName`): **pasargad**, **bashgah**, **varzesh** or **badansazi**, anywhere in the password, ignoring case, spaces, digits and symbols, and the usual look-alike swaps (`@`/`4` for a, `0` for o, `1`/`!` for i, `3` for e, `$`/`5` for s), so `pasargadplas`, `Pasargad@1405` and `P@sarg4d-is-mine` are refused. Everyone who knows the gym tries its name first, so a typo in it protects nothing (decided with the developer, 1405/07/06, after `pasargadplas` was accepted);
  - is a common password (`Auth.PasswordTooCommon`): lowercased, it equals an entry of the 100,000 most common leaked passwords or one of the ordinary gym words (gym, gymplus, fitness, bodybuilding), either whole or after trimming digits, symbols and spaces from both ends ("Football2024!" is "football"). Equality, not "contains": a passphrase with a common word in it passes ("I train at the gym every morning"). The list ships with the app; no password is sent anywhere to be checked.
  - No upper case, digit or symbol is required.
  - The web app shows the rules as a checklist under every new-password field, each line turning green as it is met. The blocklist lives only on the server, so its error appears under the field after submitting. Every password field has a show/hide button and warns while the text contains a Persian letter ("the keyboard is on Persian").
  - Passwords set before this policy keep working, but a login whose password fails the current policy sets `MustChangePassword = true`, so the user must choose a new one before anything else. Login is the only moment the server sees a password rather than its hash, so it is the only place this can be checked.
  - Persian and Arabic digits in a password are converted to English digits before it is sent, on every password field (login, change, create, reset) and again by the API, including the seeded Owner password, so "رمز۱۲۳۴" and "رمز1234" are the same password whatever client sends it. Letters are kept exactly as typed. *Decided by Claude during task 1.7 while the developer was away; pending review.*
- The Owner creates Staff accounts with a temporary password. Staff have `MustChangePassword = true`.
- Staff account management (Owner only). *Decided by Claude during task 1.5 while the developer was away; pending review.*
  - User names: 3 to 50 characters, Latin letters, digits and `- . _ @ +` (Identity's default set), unique ignoring case. Full names: required, at most 200 characters, trimmed with repeated spaces collapsed.
  - These endpoints manage Staff accounts only. An Owner account id is answered like an unknown id (`Staff.NotFound`), so the Owner cannot deactivate or reset themselves by mistake.
  - Deactivating revokes all of the user's refresh tokens in the same transaction. Their current access token keeps working until it expires (at most 15 minutes). Deactivating an inactive account, or reactivating an active one, succeeds and changes nothing.
  - Reactivating does not reset the password; the user logs in with the password they had.
  - Resetting a password: the Owner types a new temporary password (same policy as any password). It sets `MustChangePassword = true`, clears any lockout, revokes all of the user's refresh tokens and forgets all of their trusted devices (*Lockout* below).
  - Unlocking: «باز کردن قفل» clears a lockout at both doors without touching the password or sessions (*Lockout* below).
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
  - Lockout: 5 consecutive wrong passwords lock for 15 minutes, counted separately for each trusted device and for all untrusted devices together (*Lockout* below). A successful login resets the count. The Owner can be locked out too.
  - Rate limit: 10 login attempts per minute per IP address. Front-desk staff share one IP, so the limit allows several people to log in at once.
  - Failure responses: an unknown user name and a wrong password return the same error (`Auth.InvalidCredentials`), so user names cannot be discovered. A locked account returns `Auth.LockedOut`. An inactive account returns `Auth.UserInactive`, but only when the password was correct; otherwise `Auth.InvalidCredentials`.

### Lockout

Decided with the developer, 1405/07/04, task 11.6 (ADR 0004). A plain per-account lockout lets anyone who knows a user name keep that person out forever, with five wrong passwords every 15 minutes. So lockout is split by device:

- **Trusted devices.** After a successful login the browser gets a `gym_device` cookie (a random secret; HttpOnly, Secure, SameSite=Strict, path `/api/auth`), and that browser becomes trusted for that user. The server stores only the secret's hash, one row per user per browser, so a shared front-desk PC has one cookie and a row for everyone who logs in on it.
- **Two doors.** A login (or a change-password check) from a device trusted for that user counts its wrong passwords on that device alone: 5 in a row lock that device, for that user, for 15 minutes. Any other login (no cookie, an expired one, one trusted only for someone else, or one the app never issued) counts on the user's own counter, and when that locks, only untrusted devices are refused. An attacker on the internet therefore locks only the untrusted door; the front-desk PC and the Owner's own devices keep working.
- A correct password resets the count at the door it came through, and the untrusted door's count too. A lockout already in force stays until it ends or is cleared.
- A failed login never clears the device cookie. A cookie value the app never issued is replaced at the next successful login, never trusted.
- **The secret changes on every use.** Each successful login, and each session refresh from a trusted device, gives the browser a new secret and moves every row that shared the old one onto it (a shared PC keeps one cookie). A copy of the cookie stops counting as that device the next time the real browser is used.
- **Trust lasts 90 days** from the last time the device was used: a successful login, or a session refresh from that trusted device (opening the app is using it). After that the device is untrusted until the next successful login. A refresh never trusts a device that was not trusted already, because it proves a session, not a password.
- **Trust ends** for every other device of the user when they change their password (the device making the change stays trusted), and for all of their devices when the Owner resets it or it is set from the server. A device that logged in with the old password, perhaps an attacker's, must not keep its own door.
- **Unlocking.** Locked means locked at either door, and the Owner's staff list shows «قفل‌شده» for either. Three ways clear both doors:
  - «باز کردن قفل» on the Owner's staff page, for staff accounts. The password is unchanged and sessions stay: the usual reason is someone else's guesses.
  - Resetting the password, as before.
  - The server console, for any account, the Owner's included: `./server.sh unlock <user>`.
- **The server console** (`deploy/server.sh`, run over SSH on the server; there is deliberately no web endpoint for it):
  - `./server.sh unlock <user>`: clears both doors.
  - `./server.sh set-password <user>`: asks for the new password twice without showing it and passes it on standard input, never on the command line. It must follow the password policy. The person needs no change at the next login (they typed it themselves); every session ends and every trusted device is forgotten, as for a reset.
  - `./server.sh rename <user> <new-user>`: a new user name, following the user-name rules below. Sessions stay; the next login uses the new name.
- **Guessable user names are refused** for new staff accounts (`Staff.UserNameGuessable`) and for renames: admin, administrator, owner, manager, modir, root, test, user, staff, gym, support, superuser, system, guest, operator, reception, paziresh, karmand, pasargad and bashgah, compared on the name's English letters alone, so `Owner2` and `admin_1` are refused and `owner.reza` is not. The Owner's seeded account keeps whatever name it was given; `./server.sh rename` changes it.

### Permissions

| Action | Owner | Staff |
|---|---|---|
| Members: create, update, deactivate, search | ✅ | ✅ |
| Check-in (from the lockers screen only, §7 *Confirming at the front desk*), check-out, cancel check-in | ✅ | ✅ |
| Assign or renew subscriptions | ✅ | ✅ |
| Register payments, create cafe orders | ✅ | ✅ |
| See the two prices (§3) | ✅ | ✅ |
| Change the two prices (§3), staff accounts | ✅ | ❌ |
| Lockers: see the map, see who had a locker today, take one out of service, bring it back in, move a visit to another locker (§6, §7) | ✅ | ✅ |
| Freeze, unfreeze, cancel subscriptions (a check-in by either role still ends a freeze, §4 *Freeze*) | ✅ | ❌ |
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

## 3. Plans and prices

Rule change, decided by the Owner on 1405/07/05 (2026-09-27), roadmap 6.5.6. It replaces the list of
plans (task 3.1) and the one single-session plan (task 6.5.3): **the gym has no plans to choose
from.** Each member's plan is built for them at the desk, and every session costs the same.

### A member's plan

- The desk types two numbers when it sells a subscription: the **days** and the **sessions**.
  - Days: 1 to 365 (`Subscriptions.DurationInvalid`).
  - Sessions: at least **5** (`Subscriptions.SessionCountTooLow`), with no upper limit.
  - The two are unrelated: 10 days with 12 sessions is allowed.
  - There is no "unlimited sessions" plan. Every subscription has a session count.
- **Price = sessions × the session price** (*Prices* below). The days do not change the price. The
  server works it out; the desk never types a price, and the sale form shows it before the desk
  confirms. A price that does not fit the money column is refused (`Subscriptions.PriceTooLarge`).
- A plan has no name. It reads as its numbers: «۳۰ روز · ۱۲ جلسه».
- *If the gym later prices by tiers (a cheaper session for a bigger pack), that becomes a change to
  how the price is worked out here, not to anything in §4.*

### Prices

- Two prices, set by the Owner on the settings screen (تنظیمات):
  - `SessionPrice`, the price of one session of a plan (قیمت هر جلسه);
  - `SingleVisitPrice`, the price of a single-session visit (قیمت تک‌جلسهٔ آزاد, §4
    *Single-session subscriptions*).
- They follow the money rules of every price: at least 0, at most 2 decimal places (more is
  refused, never rounded), and within `numeric(18,2)` (`Pricing.PriceNegative`,
  `Pricing.PriceTooManyDecimals`, `Pricing.PriceTooLarge`).
- **Both start unset.** They are not seeded: they are the gym's own numbers and nothing may invent
  them. Selling a plan while `SessionPrice` is unset is refused (`Pricing.SessionPriceNotSet`), and a
  single visit while `SingleVisitPrice` is unset (`Pricing.SingleVisitPriceNotSet`); the desk is
  told the Owner has to set it. Once saved, the Owner saves both together and neither can be
  cleared. *Proposed by Claude in the 6.5.6 planning session, approved by the developer.*
- **A price change never reaches a past sale**: each subscription stores its own price (§4).
- Owner and Staff see the prices (the desk sells at them); only the Owner changes them. Each change
  is in the audit log, like any other edit. Two Owners saving at once: the second is refused
  (`Pricing.ChangedConcurrently`) rather than silently overwriting the first.
- There is exactly one row of prices, enforced by the database.

---

## 4. Subscriptions

- At sale, the subscription stores the numbers that were sold: `Price`, `DurationDays`, `TotalSessions`. Changing the prices afterwards never changes what a past sale was worth.
- `StartDate` and `EndDate` are `DateOnly` in the gym's time zone. `EndDate = StartDate + DurationDays - 1` (the end date is inclusive).
- A member never has two subscriptions covering the same date.
  - No current or queued subscription: the new one starts today.
  - Otherwise: the new one is queued and starts the day after the latest existing `EndDate`.
- Selling (decided with the developer in task 4.2):
  - **Assign** sells the days and sessions the desk types (§3). **Renew** sells the same days and sessions as the member's latest subscription (by `EndDate`, cancelled ones included), at **today's** session price, not the old one (the same "current values, not the old snapshot" rule renew always had; rewritten in 6.5.6). A member with no subscription has nothing to renew (`Subscriptions.NothingToRenew`).
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
  5. `Exhausted` (UsedSessions = TotalSessions)
  6. `Active`
- `ConsumeSession(today)` fails unless the status is `Active`. It increments `UsedSessions`.
- `RestoreSession()` decrements `UsedSessions` (used only by cancel check-in) and never goes below 0.
- Database: `total_sessions` is required; check constraints `used_sessions <= total_sessions`, `duration_days` 1 to 365, and at least 5 sessions unless single-session (§3); `xmin` concurrency token.

### Single-session subscriptions (تک‌جلسه‌ای)

Decided with the developer, 1405/07/03. Roadmap 6.5.3. Since 6.5.6 it is sold at `SingleVisitPrice`
(§3 *Prices*) instead of from a single-session plan, and the desk types nothing: one click sells it.

A single-session sale is an **ordinary subscription** of 1 day and 1 session at `SingleVisitPrice`, not
a separate kind of record. Guests and walk-in visitors are sold it too. It has a price, a payment, a place in the member's debt, an attendance row
and a locker like anything else, and the member can buy هوازی during the visit (§7). What makes it
different is that it is invisible to every rule that orders a member's calendar: it is one day for one
visit, and it must never move, delay or shorten what the member already bought.

- The subscription carries an `IsSingleSession` flag, set at sale, and a check constraint ties it to
  1 day and 1 session. The exclusion constraint below reads it.
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
- **Renew is refused.** Renewal sells a plan (at least 5 sessions), so renew reads the member's
  latest **membership** subscription and ignores single-session rows entirely. A member whose only
  history is single-session visits has `Subscriptions.NothingToRenew`.
- **Freeze is refused** (`Subscriptions.SingleSessionNotFreezable`): a one-day subscription has nothing
  to suspend. Unfreezing a membership shifts that member's queued subscriptions (§4 *Freeze*) but never
  a single-session row — those are dated today or earlier, and shifting them would rewrite history.
- **A single visit the member already holds for today is used before a frozen membership, and the
  freeze is left alone** (§4 *Freeze*). This replaces "a frozen member is sold a single visit and the
  freeze is not touched" (roadmap 6.5.9): a frozen member who comes in without one is no longer sold
  one, their plan is unfrozen instead.
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
- Only `Active` subscriptions can be frozen. A frozen subscription is not used while it is frozen.
- **A frozen member who comes in is unfrozen** (asked by the Owner, 1405/07/06, roadmap 6.5.9). When
  nothing else is usable today, check-in ends the freeze by the rules below — frozen days added to
  `EndDate` within the allowance, queued subscriptions shifted — and then uses a session, all in the
  check-in's transaction (§7). Either role may do it: coming in is what ends the freeze, and the
  manual freeze and unfreeze stay Owner-only (§1).
  - Anything already usable today is used first and the freeze is left alone: a single visit held for
    today (§4 *Single-session subscriptions*), or another membership that is active today.
  - If the plan turns out expired once unfrozen (a freeze that ran past the allowance), check-in is
    refused with `Subscriptions.Expired` and nothing changes: the plan stays frozen for the Owner.
  - A check-in that carries a sale (§7 *Confirming at the front desk*) never unfreezes: it uses what it
    sold or is refused, so a plan sold at the locker is never queued behind one unfrozen a moment later.
  - The check-in box warns before confirming that the plan is frozen and will be unfrozen, and says
    afterwards that it was, with the days added to its end.
  - Cancelling that check-in (§7) gives the session back but does not put the freeze back. The Owner
    freezes it again from that day; freeze days add up, so none are lost.
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
  - `Amount` follows the same money rule as the prices (§3): at most 2 decimal places, refused rather
    than rounded, capped at the same column limit (`numeric(18,2)`).
  - `ReferenceNumber` is at most 100 characters; `Reason` is at most 500 (the same limit as a
    subscription's cancellation reason). Blank input is stored as `null`.
  - Registering a payment sets `PaidAt` to the current moment; there is no way to record a
    backdated payment.
  - The `CafeOrderId` column and the one-target check constraint exist from this task on;
    cafe orders started setting `CafeOrderId` in task 7.2.
- Revenue for a period = payments − refunds, by `PaidAt` in the gym's time zone. There is no separate Income table.

### Confirming money at the desk

Decided with the developer, 1405/07/04. A payment is never deleted, so a stray press on "تأیید"
should not be enough to write one. This rule covers every screen that records money.

- **No payment method is chosen in advance.** The "روش پرداخت" list starts empty ("انتخاب کنید…"),
  and a payment or refund with no method picked is refused before anything is sent. The desk has
  to look at the list and choose.
- **The list is always in the same order: کارت (Card), انتقال بانکی (BankTransfer), نقدی (Cash).**
- **Before any money is written, the desk answers a second question**, which names the amount
  and the method: "آیا پول دریافت شد؟" for a payment, and "آیا پول به عضو برگردانده شد؟" for a
  refund. Nothing is sent until the desk answers yes. "No" closes the question and leaves the form
  as it was.
- This applies to a payment against a subscription, a هوازی charge or a cafe order, to «تسویه
  یکجا», to the cafe till, and to a refund.
- A cafe order left wholly on a member's account takes no money, so it asks neither for a method
  nor for the confirmation (§8).
- This is a desk-side guard only. The API already requires a valid method on every payment, so
  nothing changes on the server.

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

### Settling several items at once (تسویه یکجا)

Decided with the developer, 1405/07/04. Roadmap 7.5. A walk-in who takes a single visit, uses the
treadmill and buys a drink owes three items, and paying each one through its own form is three
entries for one handover of money.

- **One amount, one method, several items.** The desk picks the items (all of the member's debt is
  ticked to begin with, and any item can be unticked or ticked again), types one amount and one
  payment method, and the system writes one ordinary `Payment` per item, all in one transaction.
  This is the "lump sum entered against each of those items" above, done by the system instead of
  by hand: there is still no wallet, and every payment still belongs to exactly one item.
- **The payment methods are the usual ones** (Cash, Card, BankTransfer), and a settlement uses one
  of them. The gym does not split one handover between cash and card.
- **Less than the ticked total is allowed, and it is spent in a fixed order: cafe first, then
  هوازی (service charges), then the subscription.** Each item is paid in full before the next one
  gets anything; the last one reached may be part-paid, and the rest stays owed as usual.
  - Within the same kind, the oldest item first. *Decided by Claude during task 7.5; pending
    review.*
- **More than the ticked total is refused** (`Payments.Overpayment`), the same as for a single item.
- **What the desk saw is what gets paid.** The request names each ticked item and what the desk
  was shown as owed on it. If any of them has changed in between — paid elsewhere, voided,
  cancelled, or another purchase changed the figure — nothing is written and the desk is shown the
  new debt (`Settlements.DebtChanged`). Money is never spread over a list the desk did not see.
- Front-desk work, both roles, the same as registering a payment (§1).
- **The history shows one row per item**, all with the same moment, method and staff member. No
  settlement or receipt record ties them together. *Decided by Claude during task 7.5; pending
  review: a `SettlementId` on `payments` can be added later if the gym wants one printed receipt.*
- Correcting a settlement is the same as correcting any payment: each row is refunded or its item
  voided or cancelled on its own terms (§5, §7, §8).

---

## 6. Lockers

Rewritten as decided by the Owner, 1405/07/05 (2026-09-27). Roadmap 6.5.5. This replaces
"only the Owner adds a locker" and the random pick at check-in (§7).

- Fields: `Number` (unique), `IsOutOfService`.
- **The gym has exactly 72 lockers, numbered 1 to 72, and nobody creates or deletes one.** They
  arrive with the migration, the way the expense categories do (§9), so every database has all
  of them from the start. There is no endpoint and no screen that adds a locker. The number
  changes only when the gym buys or removes a cabinet, and then it is a code change with a
  migration, agreed first, not something done at the desk. The database refuses a number outside
  1–72 with a check constraint.
- **Where they stand.** The lockers screen draws them the way they stand in the gym, so the desk
  sees on screen what is in front of the member:
  - Every cabinet is two columns of three. Numbers run down a column, then on to the next
    column: 1, 2, 3 in the first column, 4, 5, 6 in the second.
  - **Outside the changing room (بیرون رختکن), 1–30:** one wall of five cabinets: 1–6, 7–12,
    13–18, 19–24, 25–30.
  - **Inside the changing room (داخل رختکن), 31–72:** the main wall of six cabinets (31–36,
    37–42, 43–48, 49–54, 55–60, 61–66), and one free-standing cabinet on another wall, 67–72,
    drawn apart from the wall.
  - Left to right, as on the wall, even though the rest of the screen is right-to-left.
  - The screen does not repeat a locker's location in words; the map already shows it.
- A locker is occupied when an open attendance references it. Occupancy is derived, never stored,
  and so is the member holding it: the map names whoever the open attendance belongs to,
  so the desk can answer "whose is locker 1?" without opening attendance.
- A locker cannot be marked out of service while occupied. A locker that breaks while someone
  holds it: move that visit to another locker first (§7 *Moving to another locker*), then take
  the empty one out of service.
- **The map is the desk's screen, the same for Staff and the Owner** (confirmed by the Owner,
  1405/07/05). Everything on it is front-desk work for both roles: check-in, check-out, cancel
  check-in, moving a visit, هوازی, cafe, and taking a locker out of service or back in. Nothing on
  the map is Owner-only, so a staff member never meets a button that would be refused. The person
  who finds a locker broken is the one at the desk, and the same person sees it repaired.

### Who had a locker today (تاریخچه امروز کمد)

Asked by the developer, 1405/07/06 (2026-09-28); where the button goes and the two cases below are
the developer's answers. Roadmap 6.5.10.
- The box a free locker opens can list everyone who had that locker **today**, so the desk can
  answer "who used locker 5 this morning?" (something left behind, something broken) while the next
  member is standing there. It is offered both before a member is chosen and next to the check-in
  confirmation. Each name links to that member's profile, with when they came in and left.
- **Today only**: visits checked in since midnight of the gym's day (`Gym:TimeZone`, §0), oldest
  first. Earlier days are not asked for here; a member's own history is on their profile.
- A visit counts for the locker it holds now. A visit moved to another locker (§7 *Moving to
  another locker*) is listed under the locker it was moved to, not the one it left: moves are
  nearly always the desk correcting a wrong locker, and the old locker is not stored anywhere but
  the audit log.
- A cancelled check-in (§7 *Cancel check-in*) is listed, marked «لغو شده»: the member held the key,
  however briefly. This is an operational view, like a member's history, not a report.
- A reserve place has no such list: it has no number, so "who had it" means nothing (§6 *Reserve
  places*).

### Reserve places (ورود بدون کمد)

- **15 reserve places** for a visit with no locker. They have no number: on screen each one shows
  the name of the member using it where a locker would show its number.
- A reserve place can be used **only when no locker is both in service and free**
  (`Attendance.LockersStillFree`). They are there for the day every locker is full, not as a
  choice beside a free locker.
- **At most 15 at once** (`Attendance.ReserveFull`). The sixteenth person with no locker is
  refused. The database enforces it too: an open attendance holds a reserve place from 1 to 15
  (check constraint), and a partial unique index on that place where `checked_out_at IS NULL`
  lets no two open visits hold the same one. The place's number is internal and never shown.
- Every open visit holds **exactly one** of the two: a locker or a reserve place (check
  constraint). Check-out, cancel check-in and auto-checkout free a reserve place the same way they
  free a locker.
- A reserve place behaves like a locker in every other way. Clicking it opens the same box, with
  هوازی, cafe, check-out, cancel and move (§7).
- They are out of the way on the screen, behind one small control that shows how many are used
  (for example "ورود بدون کمد ۰ از ۱۵"). All lockers being full is rare, and 15 boxes that are
  nearly always empty must not take the desk's room.

---

## 7. Attendance

### Check-in (one database transaction)
Preconditions: the member is active, has an `Active` subscription, and has no open attendance.
The request names the locker the desk chose, or asks for a reserve place (§6). It may also carry a
sale — a single visit or a plan — which is made first, in the same transaction, and is then what
step 1 finds (*Confirming at the front desk*).
1. Load the subscription that is in effect today — an `Active` one always wins over a queued renewal that ends later. If none is active, apply the exhausted-with-a-queue rule above. If still none and a membership is frozen, unfreeze it (§4 *Freeze*) — unless the request carries a sale.
2. `subscription.ConsumeSession(today)`.
3. Take the place the desk chose (decided by the Owner, 1405/07/05; this replaces the random pick):
   - **A locker:** it must exist (`Lockers.NotFound`), be in service (`Lockers.OutOfService`), and
     have no open attendance (`Attendance.LockerTaken`). Two desks choosing the same free locker
     at the same moment meet the partial unique index on `locker_id`: one succeeds, the other
     gets `Attendance.LockerTaken`, the same error as a locker that was already taken.
   - **No locker:** a reserve place, under §6's conditions (`Attendance.LockersStillFree`,
     `Attendance.ReserveFull`).
   - There is no longer a check-in "with no locker and a warning". Every visit has a locker or a
     reserve place.
4. Insert the Attendance row with `CheckedInAt` and the locker or reserve place.
5. Save and commit.
### The "currently inside" board

- One row per open visit: the member, the locker (or "رزرو" for a reserve place, §6), the time they came in, how much of their
  subscription is left, and the visit's هوازی charge.
- Sessions are shown as used of total.
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

- Money owed never blocks a check-in. The visit is recorded and the front desk is shown the member's outstanding total, as information rather than an error (§0, §5 *Member debt*).
- When nothing is usable because the member used every session on the subscription's first day and renewed the same day, the refusal is `Subscriptions.NextStartsTomorrow` ("today is over for them, come back tomorrow"), not the queued subscription's own `Subscriptions.NotStarted`, which sounds like the sale went wrong.
- Database: partial unique index on `member_id` where `checked_out_at IS NULL`, and on `locker_id` where `checked_out_at IS NULL`. Cancelled attendances count as closed.
- Unique-violation or concurrency errors are returned as a clear 409 conflict, never a 500.

### Check-out
- Only an open attendance can be checked out. Sets `CheckedOutAt`, which frees the locker or reserve place.

### Moving to another locker
Decided by the Owner, 1405/07/05. Roadmap 6.5.5.
- An open visit can be moved to another locker: the desk gave the wrong one, or the locker broke
  while in use (§6). Front-desk work, so both roles.
- Only an open attendance (`Attendance.NotOpen`). The target is a locker that is in service and
  free, under the same checks and errors as check-in (`Lockers.NotFound`, `Lockers.OutOfService`,
  `Attendance.LockerTaken`), and not the locker the visit already holds (`Attendance.SameLocker`).
- A visit on a reserve place can move to a locker, which frees the reserve place: the member gets
  a locker as soon as one is free. A move always ends on a locker, never on a reserve place
  (proposed by Claude in the 6.5.5 planning session, confirmed by the Owner, 1405/07/05). The one
  case it leaves out is a locker breaking while in use on a day every other locker is full.
- Moving consumes no session and gives none back; the visit, its هوازی and its cafe orders stay
  exactly as they were. The old locker is free as soon as the move is saved, because occupancy is
  derived. The audit log records the change like any other.

### Confirming at the front desk
Decided with the developer, 1405/07/04. Where check-in happens changed with the Owner, 1405/07/05.
- **Check-in happens only on the lockers screen**, which is named "ورود با کمد" and is the first
  screen of the app (decided by the Owner, 1405/07/05). The desk clicks the free locker it chooses,
  which opens a box with one search field for a name or a mobile number. It then picks the member,
  confirms, and the visit is recorded with that locker. The search screen and the member's profile
  no longer have a check-in button. For a member who is inside they still show the locker and offer
  check-out and cancel check-in.
- In that search, a member who is already inside is marked "داخل باشگاه" with their locker.
  Choosing them shows an error in the box and sends nothing (*Check-in*: no open attendance; the API
  refuses it anyway with `Attendance.AlreadyCheckedIn`).
- Everything the member search screen offered for check-in before moves into that box: registering a person who is not
  found, and selling when the member has nothing usable (§4) — a single visit or a plan.
- **What is sold in that box is sold with the check-in** (decided by the Owner, 1405/07/06, roadmap
  6.5.7). The sale and the check-in are one transaction, with the locker the desk clicked, so the
  locker is chosen once. A subscription sold at the locker always comes with that locker: when the
  member still cannot come in (the locker was taken a moment earlier, or the plan would not start
  today), the check-in is refused and nothing is sold. The one way such a sale ends up without a
  visit is *Cancel check-in*, which keeps the subscription and gives its session back for next time.
  - Payment is not asked for at the sale. What is owed shows on the visit's box, where the desk
    collects it then or later (§5).
  - The plan form (days and sessions, the price shown before confirming, §3) opens under the
    refusal only when a new plan would start today: after "no subscription", "expired", "no
    sessions left" or "cancelled". When the member holds a plan bought for later, a new plan would
    queue behind it (§4) and could not let them in today, so the box offers the single visit and
    leaves selling a plan to the profile. A frozen plan is no longer a refusal: check-in unfreezes
    it (§4 *Freeze*).
  - A person registered in the box has no subscription by definition, so the box goes straight to
    the sale instead of asking to confirm a check-in that could only be refused.
  - Selling from the member's profile is unchanged: the plan is added to the member's
    subscriptions (starting today or queued, §4) and is used on the next visit.
- Clicking an occupied locker (or a used reserve place) opens that visit's box: the member (linked to
  their profile), when they came in, the plan and sessions left, the debt item by item, هوازی and
  cafe for the visit (§7 *Gym services*, §8), and check-out, cancel check-in and move to another
  locker. Clicking an out-of-service locker offers to bring it back into service; clicking a free
  one also offers to take it out of service.
- Everywhere the desk can check a member in or out (the lockers screen, the member search screen, the member's profile and the "currently inside" board), check-in and check-out each ask the desk to confirm before anything is sent, in the same box. A mistaken press costs a session or closes someone else's visit, and undoing either is a separate action with its own rules (*Cancel check-in*).
- A member who is inside is offered check-out, not check-in. Check-in would only be refused (*Check-in*: no open attendance).
- After a check-in, the same box shows the locker (or that a reserve place was used), the plan and the sessions left, and the member's debt item by item: unpaid subscriptions, services such as هوازی, and cafe orders (§5 *Member debt*). It stays until the desk closes it.
- Before a check-out, the box shows the locker to take back and the same plan, sessions and itemized debt, so the desk can collect what is owed while the member is still there. Debt is shown, never enforced: check-out is not refused for money owed, the same way check-in is not.
- When the visit has a locker, the desk must tick "key received" before the check-out can be confirmed: closing the visit hands the locker to the next person in. After check-out, the box shows that the locker is free and repeats the itemized debt.
- Selling a single visit or a plan from that box needs no second confirmation: pressing the priced button, or «فروش و ثبت ورود» under the plan's price, is already the decision.
- Cancelling a check-in (*Cancel check-in*) asks in the same box. When the visit bought nothing, that
  is the only question: it gives the session back and frees the locker, so a stray press is worth
  one more click. When it bought something, the box asks about each purchase (*Cancel check-in*).

### Cancel check-in
- Allowed only for an open attendance within `Gym:CancelCheckInWindowMinutes` of check-in.
- Restores the session, frees the locker, and marks the attendance cancelled (who and when). The row is kept.
- A freeze the check-in ended stays ended (§4 *Freeze*): the Owner freezes the plan again if it was a mistake.
- Cancelled attendances are excluded from attendance reports.
- **What the visit bought is cancelled only when the desk says so, one purchase at a time**
  (decided by the Owner, 1405/07/06, roadmap 6.5.8). The box lists the visit's هوازی and each of
  its cafe orders with its amount, each with its own tick, all unticked. A purchase left unticked
  stays on the member's account and is paid like any other: the member may have used the treadmill
  or taken a drink and still had to leave. The ticked ones are voided (هوازی, §7 *Gym services*) or
  cancelled (cafe, §8) with the reason that the check-in was cancelled, and what was paid on them
  goes back the way it came, in the same transaction as the cancellation.
  - When anything is ticked, the box asks a second time before sending, naming what will be
    cancelled and reminding the desk to hand back what was collected for it; nothing collected
    means nothing to hand back. With nothing ticked there is no second question.
  - The request names the choice outright — void the هوازی or not, and which cafe orders — and
    the server does not guess: a request without it is refused. An order named that is not a
    standing order of this visit refuses the whole cancellation (`Attendance.CafeOrderNotOnVisit`),
    so nothing is half done. A purchase added after the desk opened the box is simply not named
    and stays.

### Auto-checkout
- A nightly job at `Gym:ClosingTime` closes all open attendances and marks them `AutoClosed`. The session stays consumed.

### Opening hours (PENDING — not enforced yet, roadmap 11.4)
Decided with the developer, 1405/07/04.
- The gym is open from `Gym:OpeningTime` (06:00) to `Gym:ClosingTime` (00:00, midnight), in the gym's
  time zone, every day (§0: no closed days). Outside those hours there is no check-in and no check-out.
  Check-in is refused with a clear error, not recorded. Check-out has nothing to close, because
  *Auto-checkout* has already closed every visit at midnight.
- Together with *Auto-checkout*, this means that from midnight to 06:00, nobody is recorded as inside
  the gym. Today a check-in after midnight still succeeds and stays open until the next night's job.
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
- Cancelling a check-in voids the visit's هوازی only when the desk ticks it (§7 *Cancel check-in*), with the reason that the check-in was cancelled. Left unticked, the charge stays owed on a visit that is now closed, and is corrected like any closed visit's charge: void plus a reason. *Replaces "cancelling always voids it", decided by Claude in task 5.7; decided by the Owner, 1405/07/06, roadmap 6.5.8.*
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
- The price follows the same money rules as the prices (§3): at most 2 decimal places, refused
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
  - **Cancelling a check-in cancels only the cafe orders the desk ticks** (decided by the Owner,
    1405/07/06, roadmap 6.5.8; replaces "always leaves them standing", 1405/07/04). Each order of
    the visit has its own tick, unticked by default: goods handed over stay sold unless the desk
    says otherwise — the member may have bought something and had to leave. A ticked order is
    cancelled exactly as from the till (reason, refund the way the money came), the reason being
    that the check-in was cancelled (§7 *Cancel check-in*).
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
- Details (decided with the developer, 1405/07/05, task 8.1):
  - The seeded categories carry Persian names: اجاره (Rent), حقوق (Salary), برق (Electricity), آب (Water), تجهیزات (Equipment), تعمیر و نگهداری (Maintenance), خرید بوفه (Cafe Purchasing), سایر (Other). They arrive with the migration, so every database has them from the start.
  - Category names are unique in normalized form (§13), like the cafe's. The Owner can add and rename a category, the seeded ones included. A category is never deleted or switched off: expenses and reports point at it.
  - `Description` is required, at most 500 characters. `ReferenceNumber` is optional, at most 100. The void reason is required, at most 500.
  - `Amount` follows the money rules of every other amount: greater than zero, at most 2 decimals, refused rather than rounded.
  - `ExpenseDate` cannot be after the gym's today (`Expenses.DateInFuture`). Any past date is accepted, so an old bill can still be entered.
  - A voided expense is final: it cannot be edited or voided again. A correction is a fresh expense.
  - Expenses and their categories are Owner only, reading included (§1).

---

## 10. Notifications (SMS)

- Phone number is the only contact channel. Inactive members receive no SMS.
- Types: `SubscriptionExpiring`, `LowSessions`.
- A daily job creates notifications for `Active` subscriptions where:
  - days until `EndDate` <= `Sms:ExpiringDaysBefore`, or
  - remaining sessions <= `Sms:LowSessionsThreshold`.
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
