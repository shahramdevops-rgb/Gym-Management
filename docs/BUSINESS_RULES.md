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
| Who receives the cheque and instalment reminder SMS, and where that number is kept | Phase 10 | The Owner's number is stored nowhere yet. Until then the reminder is on the dashboard only (§9 *Cheques and instalments*). |

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
| Guest visit (ورود مهمان): check in, check out, cancel, record its هوازی, sales and cafe, settle what it owes (§7 *Guest visit*) | ✅ | ✅ |
| Cardio-only visit (ورود فقط هوازی): check in without consuming a session (§7 *Cardio-only visit*) | ✅ | ✅ |
| Assign or renew subscriptions | ✅ | ✅ |
| Register payments, create cafe orders | ✅ | ✅ |
| See the two prices (§3) | ✅ | ✅ |
| Change the two prices (§3), staff accounts | ✅ | ❌ |
| Lockers: see the map, see who had a locker today, take one out of service, bring it back in, move a visit to another locker (§6, §7) | ✅ | ✅ |
| Lockers screen: the desk panel, today by hour, the locker usage map (§6; counts only, no money) | ✅ | ✅ |
| Freeze, unfreeze, cancel subscriptions (a check-in by either role still ends a freeze, §4 *Freeze*) | ✅ | ❌ |
| Refunds, voids (outside the cafe) | ✅ | ❌ |
| Gym service charges: record, change the amount, void (§7 *Gym services*) | ✅ | ✅ |
| Cafe: products, categories, orders, and cancelling an order (§8) | ✅ | ✅ |
| The gym's history (تاریخچه): check-ins and هوازی of any day (§12 *History*) | ✅ | ✅ |
| The gym's history: payments of today and the 3 days before it (§12 *History*) | ✅ | ✅ |
| The gym's history: payments of any earlier day | ✅ | ❌ |
| The gym's history: sales (فروش‌ها) of any day, paid and unpaid (§12 *Sales*) | ✅ | ❌ |
| The gym's history: the totals of the sales and payments sections (§12 *Totals*) | ✅ | ❌ |
| Expenses, dashboard, reports, audit log, SMS resend | ✅ | ❌ |
| Cheques and instalments (چک و قسط): see, register, edit, mark paid, send back to pending, cancel (§9 *Cheques and instalments*) | ✅ | ❌ |

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
- Birth date (decided with the developer, 1405/06/31 — roadmap 2.4; made required 1405/07/10 — roadmap 6.5.24).
  - **Required** (changed by the developer, 1405/07/10, from optional): every member has one, entered when they are registered (the members page and the check-in dialog alike) and corrected, never removed, on edit (`Members.BirthDateRequired`). The gym greets members on their birthday, at the desk now and by SMS once that is turned on, which only works when nobody is missing. No member was registered yet anywhere (the server was still a trial), so no old member is left without one; the database column is `NOT NULL`.
  - A business date (`DateOnly`) like every other date here: stored Gregorian, chosen and shown Jalali (three dropdowns rather than the calendar, §13).
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
  - A second, separate filter shows only members who owe something (§5 *Member debt*: debt above zero, counted the same way as the amount on their row). It combines with the status filter and the search, so "inactive and still owing" is one list. Asked by the developer on 1405/07/07, roadmap 6.5.20.
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

- The desk types one number when it sells a subscription: the **sessions**. The **days** follow
  from the sessions; the desk never chooses them (decided by the Owner on 1405/07/07 (2026-09-29),
  roadmap 6.5.18, replacing "any days from 1 to 365, unrelated to the sessions" of 6.5.6):

  | Sessions | Days |
  |---|---|
  | 5 to 10 | 30 |
  | 11 to 20 | 45 |
  | 21 to 140 | 70 |

  - Fewer than 5 sessions is refused (`Subscriptions.SessionCountTooLow`); more than 140 is refused
    (`Subscriptions.SessionCountTooHigh`). Nobody trains more than once a day, so 140 is the
    ceiling of a 70-day plan with room to spare.
  - The server works the days out. The sale form shows them, filled in by themselves as the
    sessions are typed, and does not let the desk change them.
  - There is no "unlimited sessions" plan. Every subscription has a session count.
- **Price = sessions × the session price** (*Prices* below). The days do not change the price. The
  server works it out; the desk never types a price, and the sale form shows it before the desk
  confirms. A price that does not fit the money column is refused (`Subscriptions.PriceTooLarge`).
- A plan has no name. It reads as its numbers: «۱۲ جلسه - ۳۰ روزه».
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
  - **Assign** sells the sessions the desk types, for the days they give (§3). **Renew** sells the same sessions as the member's latest subscription (by `EndDate`, cancelled ones included), for the days today's table gives them, at **today's** session price, not the old one (the same "current values, not the old snapshot" rule renew always had; rewritten in 6.5.6 and 6.5.18). A member with no subscription has nothing to renew (`Subscriptions.NothingToRenew`).
  - Both follow the same start-date rule, and both are refused for an inactive member (`Members.Inactive`).
  - A cancelled subscription covers no dates: it neither delays a new sale nor counts as an overlap.
  - If the latest subscription is `Exhausted` (all sessions used before its `EndDate`), the new one does not wait: the exhausted one ends yesterday and the new one starts today. If the exhausted one started today, it ends today and the new one starts tomorrow, so two subscriptions never cover the same date.
  - The same applies when the sale came first and the sessions ran out afterwards: if the current subscription becomes `Exhausted` while a queued one is waiting, the next check-in closes the exhausted one early and moves the queued one forward to start today, keeping its full duration. The same edge case holds — an exhausted subscription that only started today still covers today, so the queued one starts tomorrow and the member cannot check in until then.
  - Two sales for the same member at the same moment are handled one after the other: the second waits for the first and is queued after it. Both succeed.
  - A queued subscription's stored dates are where it stands today, not a promise: they move earlier if the plan before it runs out of sessions, and later if that plan is frozen. Only its length in days is fixed. So the member's subscription history shows its start as «بعد از پلن قبلی» with the stored date beside it as «فعلاً …», and its end as «N روز از شروع» (decided with the developer, 1405/07/10, roadmap 6.5.23). An upcoming subscription with no live membership right before it (the one it waited for was cancelled) does not move, and shows its plain dates.
- The history also shows when each subscription was sold («تاریخ فروش», the moment it was created), whether or not anything was paid; each payment keeps its own time (§5). Who sold it is not recorded (developer, 1405/07/10: not needed for now).
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
- Database: `total_sessions` is required; check constraints `used_sessions <= total_sessions`, and, unless single-session, 5 to 140 sessions with the days the §3 table gives them; `xmin` concurrency token.

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
- **One row per item, shown together.** Each item still gets its own ordinary `Payment` row, and
  every row of one settlement carries the same `SettlementId`. Both payment histories (the gym's
  and the member's own) show those rows under one heading: the moment, whose money, «تسویه یکجا»
  with how many items it paid, the one amount the desk took, the method and who took it. Each item
  sits one step in under it, with only what it was for and its amount. *Asked by the developer,
  1405/07/11 (2026-10-03), replacing Claude's earlier "no record ties them together" (task 7.5).*
  - The guest's «تسویه یکجا» (§7 *Guest visit*) is a settlement too, and is shown the same way.
  - Only payments carry a `SettlementId`. A refund never does: correcting a settlement refunds
    each row on its own (below). The database refuses a refund with one.
  - The heading's amount and count are the whole handover's, even when a filter (method, source)
    lets only some of its rows through. When only one row of it is shown (a filter, or a page
    boundary), that row reads as a plain row.
  - Settlements made before the column existed were given one when it was added: payments with
    exactly the same moment, method and staff member, two or more of them. Only «تسویه یکجا»
    writes payments like that.
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
- **A locker held by a guest** (§7 *Guest visit*) is occupied like any other and shows the guest's
  name, but in **a colour of its own**, distinct from free, occupied and out of service, with
  «مهمان» in its label and its own line in the map's legend. The desk sees at a glance that nobody
  paid for that key.
- **A locker held on a cardio-only visit** (§7 *Cardio-only visit*) is drawn in yellow, its own
  colour, with «هوازی» in its label and its own line in the legend: the desk sees that no session
  was taken and that the treadmill must be charged before the key comes back.
- **A holder who owes money is marked on the map** (asked by the developer, 1405/07/06): a small
  red «بدهکار» tag hanging from the door's top edge at its top-right corner (changed by the
  developer, 1405/07/07, from a band across the top-left corner), whenever the member holding the locker has any
  debt at all (§5 *Member debt*: a subscription, a service such as هوازی, or a cafe order). It is
  information only, like the debt shown at check-in; the amount and what it is for are in the
  locker's box. A holder who owes nothing, and a free locker, carry no label. A guest holder carries
  the same label while anything bought on their visit is unpaid, a cafe order, a هوازی or a sale
  (§7 *Guest visit*); for a guest it is
  not only information, because the guest cannot check out until it is paid.
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
  member is standing there. It is offered only before a member is chosen: once a member is chosen,
  the box is about confirming them (the developer's answer, 1405/07/06, replacing the earlier button
  next to the confirmation). The box an occupied locker opens offers the same list under the visit's
  buttons (asked by the developer, 1405/07/07): while the locker is in use there is no other way to
  it. Each name links to that member's profile, with when they came in and left.
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

### The desk panel: birthdays and renewal opportunities (تولد، فرصت تمدید)

Decided by the developer, 1405/07/07 (2026-09-29). Roadmap 6.5.12. It fills the empty space beside
the wall outside the changing room (the top zone is five cabinets wide; the zone below it is seven).
- It lists **only members who are inside now** (an open visit, on a locker or a reserve place):
  the desk can act on it only while the member is in front of them. It has up to two lists, and a
  list with nobody in it is not shown. With nobody in either, the panel is not shown at all.
- **Birthday (تولدت مبارک):** a member whose birthday is today, by the **Jalali** month and day
  (the day Iranians celebrate; the Gregorian anniversary can fall a day off). Someone born on
  30 Esfand is listed on 29 Esfand in a year that has no 30 Esfand. A member with no birth date
  is never listed. **Their door celebrates** (changed by the developer, 1405/07/07, from "nothing
  is drawn on the door"): a ring of party colours turns around its edge, confetti falls inside it
  behind the number and the name, and it glows softly. It takes no corner (those are kept for the
  «بدهکار» tag) and no line of text; the door's label for a screen reader says «امروز تولدش است».
  A reserve place in use celebrates the same way. The birthday list in the panel is plain, like the
  renewal list: the celebration is on the door only (changed by the developer, 1405/07/07). For
  anyone who has asked for less motion the ring stands still and no confetti falls.
- **Renewal opportunity (فرصت تمدید):** a member whose visit's subscription is running out by the
  board's own thresholds (§7 *The "currently inside" board*: 3 or fewer sessions left, or ending
  within 5 days), so the desk tells them to renew before it runs out. A single-session visit is
  never listed, for the same reason the board leaves it unmarked. **A member who has already bought
  the next subscription** (a queued one, §4 `Upcoming`, not cancelled) is not listed: they have
  renewed, and listing them would only invite selling it to them again. The board itself is not
  changed by this and still marks them.
- Each entry shows the name, the locker number (or «رزرو» for a reserve place), and, for a renewal,
  what is running out: «۲ جلسه مانده», «۳ روز مانده», or «امروز تمام می‌شود». Entries are in the
  order the members came in, like the board.
- Pointing at an entry makes that member's locker blink on the map, so the desk finds them at a
  glance. Clicking it opens the locker's box, the same box a click on the locker opens. A reserve
  place has no door to blink; its entry still opens its box.
- A guest (§7 *Guest visit*) is never listed: a guest has no birth date and no subscription.

### Long stay (خیلی وقته داخله)

Decided by the developer, 1405/07/07 (2026-09-29). Roadmap 6.5.13.
- An occupied door carries a thin bar along its bottom edge that fills as the visit goes on,
  reaching full at **3 hours** after check-in. Its colour moves with it, from green at check-in
  through yellow to red at 3 hours (changed by the developer, 1405/07/07, from a red bar that
  turned wholly the warning colour). From 3 hours the full red bar blinks, calmly but clearly: it
  glows, then nearly fades out, once every two seconds (made stronger by the developer,
  1405/07/07, after a faint blink went unnoticed); it stays still for anyone who has asked their
  system for less motion. At that point the member has probably left without checking out, or has
  not given the key back. The desk looks into it the same day, before auto-checkout closes the
  visit at night (§7 *Auto-checkout*).
- It is only information: nothing is blocked or closed by it. It takes no corner of the door and
  no line of text; the door's label for a screen reader says it («بیش از ۳ ساعت»).
- A reserve place in use shows the same bar.

### Finding a member on the map (جستجو با نام)

Decided by the developer, 1405/07/07 (2026-09-29). Roadmap 6.5.16.
- A search field above the map, at the end of the legend's row, takes a **name only**. There are
  72 lockers and the drawing already shows where each one is, so a locker number is not searched
  for.
- It looks only at who is **inside now**: the holders of occupied lockers and the members on
  reserve places. It needs no request; the screen already has both lists.
- The match is partial and uses the same normalization as member search (§13), so «علي» finds
  «علی» and a half-space matches a space. Nothing is searched until the field holds at least
  2 characters after normalization.
- Matching doors are lifted and outlined; every other door fades. A match on a reserve place opens
  the reserve places and marks that place. Clearing the field (or Escape) brings the map back.
  It is a way to find someone, not a filter: every door can still be clicked.

### The desk screen's look (ظاهر صفحه ورود با کمد)

Decided by the developer, 1405/07/07 (2026-09-29), from their own sketch. Roadmap 6.5.16.
- The screen has no theme of its own: it is light or dark with the rest of the app (§14). (It was
  always dark from 1405/07/07 until the developer turned it light on 1405/07/08, roadmap 6.5.21.)
- A free door is a plain tile with a green number and a green edge down its right side. An occupied
  door is tinted red from its top corner, with a red edge, the number in the text colour and the
  holder's name under it. An out-of-service door is hatched, with a grey number and a lock under it.
- A door rises a little under the mouse, so the map feels alive. A door whose state changed since
  the last refresh pulses once in its new colour. Neither moves for anyone who has asked their
  system for less motion.
- Above the map: the counts of free, occupied and out-of-service lockers, and what the «بدهکار»
  tag and the long-stay bar mean. (A strip of 72 marks showing how full the lockers are, and a
  "refreshed N seconds ago" mark, were tried and removed by the developer, 1405/07/07: the desk
  does not need them.)
- The reserve places (*Reserve places* above) are drawn like doors: a used one like an occupied
  door with the member's name, an empty one like a free door with «خالی», faded while a locker is
  still free.
- The time and today's date, written out («سه‌شنبه ۷ مهر ۱۴۰۵»), are at the top of the side
  menu, so they are on every screen, not only this one.

### Today by hour (ورود امروز ساعت به ساعت)

Decided by the developer, 1405/07/07 (2026-09-29). Roadmap 6.5.14.
- Under the map, on the same screen, a small chart: for each hour of today, how many visits were
  checked in during that hour (by `CheckedInAt` in the gym's time zone, §0). Cancelled check-ins
  are not counted (§12). Nor are guests (§7 *Guest visit*): only members' visits are attendance.
- Beside each hour, the **average for the same hour on the same weekday over the previous 4 weeks**,
  so the desk and the Owner see whether today is busier or quieter than usual.
- The average is taken over **only those of the 4 days that had at least one counted check-in**
  (decided by the developer, 1405/07/07): a day the gym was closed (a holiday such as Nowruz) or a
  day before the system was in use is left out rather than counted as zero, so the average shows an
  ordinary open day. With none of the 4 days open, the average is zero. The chart says how many
  days the average covers when it is fewer than 4.
- Only the hours from the first to the last with anything in them (today or on average) are drawn,
  so the night the gym is shut takes no room. The hour it is now is marked. Hours run from right to
  left, as time runs along the long-stay bar.
- It shows counts only, never money, so it is on the desk's screen for both roles (§1 *Permissions*).
  Full attendance reports stay Owner-only (§12).

### Locker usage map (نقشهٔ استفادهٔ کمدها)

Decided by the developer, 1405/07/07; the details below confirmed by the developer, 1405/07/07.
Roadmap 6.5.15.
- A switch on the map shows, instead of who holds each locker, **how often each locker was used**
  over a period the viewer picks: the last **7, 30 or 90 days**, counted in the gym's days (§0) up
  to and including today, so "7 days" is today and the 6 days before it. **30 days** is picked
  when the switch is turned on.
- The doors are coloured from least to most used, **relative to the most used locker in the
  period**, in five steps of one colour that is neither green nor red (those already mean free and
  occupied on this screen). Each door shows its count. A locker never used in the period has a
  colour of its own: it is either in a bad spot or has a problem nobody has reported. An
  out-of-service locker keeps its small lock, so a count of zero on it explains itself.
- A use is a visit that held the locker. Cancelled check-ins are not counted. A visit moved to
  another locker counts for the locker it holds at the end, the same way as *Who had a locker
  today*: the old locker is recorded only in the audit log. Reserve places have no number, so they
  are not part of this view.
- It is a view only. No door can be clicked in it, and what belongs to who is inside now (the
  holder's name, the «بدهکار» tag, the long-stay bar, the birthday, the desk panel and the name
  search) is not shown. Switching back shows the map as usual.
- Counts only, so both roles see it (§1 *Permissions*).

---

## 7. Attendance

### Check-in (one database transaction)
Preconditions: the member is active, has an `Active` subscription, and has no open attendance.
The one visit without a member or a subscription is a guest's, under its own rules (*Guest visit*).
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

- One row per open visit: the member, the locker (or "رزرو" for a reserve place, §6), the time they came in, and how much of their
  subscription is left.
- **Nothing is bought from this board, and nothing from the member's profile** (decided by the
  developer, 1405/07/06). A visit's هوازی and cafe are rung up from the member's locker (*Confirming
  at the front desk*), and the cafe also from its till (§8). One place to look at what a visit
  bought, instead of three that could disagree.
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
- Only an open attendance can be checked out. Sets `CheckedOutAt`, which frees the locker or reserve place. A cardio-only visit also needs its هوازی amount first (*Cardio-only visit*).

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
  confirms, and the visit is recorded with that locker. The member list («اعضا») and the member's
  profile have no check-in button. For a member who is inside they show the locker and offer
  check-out (the profile also offers cancel check-in). The member search screen is gone: its box,
  and registering a person nobody matches, moved onto the member list (asked by the developer,
  1405/07/07).
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
  - The plan form (the sessions, the days they give and the price, shown before confirming, §3) opens under the
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
  their profile), when they came in, the sessions beside them (used of total, and how many are left),
  the plan and its period (first and last day, and the days left), هوازی and cafe for the visit (§7
  *Gym services*, §8), the visit's cafe purchases, and check-out, cancel check-in, move to another
  locker and, for a locker, who had it today (§6 *Who had a locker today*). The layout is the
  developer's, 1405/07/07.
  - The visit's cafe purchases are listed one line per product, the quantities of the same product
    added across orders: two espressos, one from the locker and one from the till, are "× 2".
  - Below them, the member's debt stops at the total and its split by source (plan, هوازی, cafe);
    the items one by one are not repeated under a list that already shows them (decided by the
    developer, 1405/07/06). The same holds before a check-out. At check-in there is no such list,
    so the debt is shown item by item there. Clicking an out-of-service locker offers to bring it back into service; clicking a free
  one also offers to take it out of service.
- Everywhere the desk can check a member in or out (the lockers screen, the member list, the member's profile and the "currently inside" board), check-in and check-out each ask the desk to confirm before anything is sent, in the same box. A mistaken press costs a session or closes someone else's visit, and undoing either is a separate action with its own rules (*Cancel check-in*).
- A member who is inside is offered check-out, not check-in. Check-in would only be refused (*Check-in*: no open attendance).
- After a check-in, the same box shows the locker (or that a reserve place was used), the plan and the sessions left, and the member's debt item by item: unpaid subscriptions, services such as هوازی, and cafe orders (§5 *Member debt*). It stays until the desk closes it.
- Before a check-out, the box shows the locker to take back, the same plan and sessions, the visit's cafe purchases and the debt by source, so the desk can collect what is owed while the member is still there. Debt is shown, never enforced: check-out is not refused for money owed, the same way check-in is not.
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
  (decided by the Owner, 1405/07/06, roadmap 6.5.8). The box lists the visit's هوازی, each of
  its sales (فروشگاه and آنالیز, *Sale at the desk*, tasks 6.5.28 and 6.5.29) and each of its cafe orders with its
  amount, each with its own tick, all unticked. A purchase left unticked
  stays on the member's account and is paid like any other: the member may have used the treadmill
  or taken a drink and still had to leave. The ticked ones are voided (هوازی, §7 *Gym services*) or
  cancelled (cafe, §8) with the reason that the check-in was cancelled, and what was paid on them
  goes back the way it came, in the same transaction as the cancellation.
  - When anything is ticked, the box asks a second time before sending, naming what will be
    cancelled and reminding the desk to hand back what was collected for it; nothing collected
    means nothing to hand back. With nothing ticked there is no second question.
  - The request names the choice outright — void the هوازی or not, which sales, and
    which cafe orders — and the server does not guess: a request without it is refused. An order named that is not a
    standing order of this visit refuses the whole cancellation (`Attendance.CafeOrderNotOnVisit`),
    so nothing is half done. A purchase added after the desk opened the box is simply not named
    and stays.

### Auto-checkout
- A nightly job at `Gym:ClosingTime` closes all open attendances and marks them `AutoClosed`. The session stays consumed. Two kinds of visit stay open for the desk the next day: a cardio-only visit with no هوازی amount (*Cardio-only visit*), and a guest's visit with anything still unpaid (*Guest visit*).

### Guest visit (ورود مهمان)
Decided by the developer, 1405/07/07 (2026-09-29). Implemented in roadmap 6.5.11. This replaces
marking a locker «خارج از سرویس» for someone who takes a key without paying: that recorded no name,
looked like a broken locker to the next shift, was never freed at midnight, and left no trace in the
locker's history.
- **Who it is for:** people the gym lets in for free, such as the first-degree relatives of the
  gym's people. They take a key and hold a locker, but they have no plan, pay nothing for the gym and
  are not registered.
- **A guest visit is an attendance with no member and no subscription**, only the guest's **full
  name**. The name is required, with the same length limit and normalization as a member's name
  (§2, §13). No phone number is asked, no member record is created, no session is consumed and
  no plan is sold. A guest is not a member: they never appear in the member search, and the same
  person coming again is a new guest visit with their name typed again.
  - The database holds the rule too: an attendance has either a member or a guest name, never
    both and never neither, and it has a subscription exactly when it has a member (check
    constraints).
- **Where:** from the box a free locker opens, as a third choice beside finding a member and
  registering one («ورود مهمان»), or from a reserve place under §6's rules: only when no locker is
  both in service and free, and at most 15 at once, counted together with members.
- **Who:** both roles (§1). It is front-desk work like the rest of the map; the audit log records
  who let each guest in, and every guest inside is on the map and the board for anyone to see.
- **On screen:** the guest's locker has its own colour and shows their name (§6). The locker's
  today history lists them, marked «مهمان», with no profile link (§6 *Who had a locker today*). The
  "currently inside" board shows them with «مهمان» where the sessions go, never marked as needing
  attention.
- **Everything else is an ordinary visit:** move to another locker (§7 *Moving to another locker*),
  check-out with «key received», cancel check-in within the same window (nothing to give back:
  there is no session), and auto-checkout at midnight (unless something is unpaid, below).
- **A guest may use every service the gym sells** (decided by the developer, 1405/07/12, roadmap
  6.5.31; replaces "هوازی is not recorded on a guest visit, a guest uses the treadmill for free"
  and "sales at the desk are for members only"). Their locker box has the same four tiles as a
  member's: هوازی, بوفه, فروشگاه and آنالیز. Each follows its own rules (*Gym services*, *Sale at the
  desk*, §8): one standing هوازی per visit, any number of sales and orders, the price typed by the
  desk and never checked against a rate. Whether a guest is charged for the treadmill, and how
  much, is the desk's call on the Owner's instructions, exactly as for a member: the system never
  prices it, and a هوازی left unrecorded costs nothing.
- **Purchases:** a guest buys on their visit, under their name, from their locker's box, and in the
  cafe also from the till (§8). While they are inside, what they bought may stay unpaid, and their
  locker carries «بدهکار» (§6). **A guest has no account and leaves no debt behind:**
  - **Check-out is refused while anything bought on the visit is unpaid**, a cafe order, a هوازی, a
    فروشگاه item or an آنالیز (`Attendance.GuestHasUnpaidPurchases`). The guest's box settles all of
    them in one step («تسویه یکجا»), then checks out. This is the one place money blocks a
    check-out: a member's debt stays on their account and is shown, never enforced (§5), but a
    guest has no account to leave it on.
  - **Cancel check-in** asks about each purchase, as for a member (*Cancel check-in*). The ticked
    ones are cancelled or voided; an unticked one that is not fully paid refuses the cancellation
    with the same error, so it is paid first or ticked.
  - **Auto-checkout leaves a guest visit open while anything bought on it is unpaid** (decided by
    the developer, 1405/07/12; replaces "auto-checkout closes it anyway and the debt waits in
    «بدهی مهمان‌ها»"). The locker stays held and «بدهکار» the next morning, and the desk settles it
    from the guest's box and checks the guest out, exactly as it would have the night before. A
    guest visit with nothing unpaid is auto-closed like any other. In practice a guest pays before
    leaving, so this is the rare night the desk missed it.
  - There is no guest account (decided by the developer, 1405/07/12): a guest's debt is never
    carried to their next visit, and the same person coming again starts with nothing owed. If the
    gym later wants guests to leave owing money, that is a new rule.
- **No separate guest debt list** (decided by the developer, 1405/07/12, which removed the
  «بدهی مهمان‌ها» page built in 6.5.31): what a guest owes is always on a visit that is still open,
  so the locker map shows it («بدهکار» and the guest's box). The cafe's «پرداخت‌نشده — مهمان» filter
  stays.
- **Not counted as attendance** in any attendance report (Phase 9). A guest visit used no
  session and sold no plan; counting it would make the gym look busier than its members make it.
  - This includes the chart under the map (§6 *Today by hour*): it counts members' check-ins only.
    The locker usage view (§6 *Locker usage map*) does count a guest's visit, because it is about
    how much a locker is used, not about attendance, and the guest held the key like anyone else
    (both confirmed by the developer, 1405/07/10).
- **«تسویه یکجا» pays everything or nothing.** The desk is shown the total the visit's purchases
  still owe and pays exactly that, one ordinary payment per item: the cafe orders first, then the
  هوازی and the sales, each oldest first. If anything was added, paid, voided or cancelled since
  the box opened, nothing is paid and the desk is asked to look again (`Settlements.DebtChanged`).
  A single item can still be paid on its own from its tile. *Claude's default, 1405/07/10, widened
  to every purchase 1405/07/12; pending review.*
- **Where the guest can be chosen at the till:** the guests inside are listed under the till's
  member search, narrowed by the name typed. *Claude's default, 1405/07/10; pending review.*
- **The database cannot tell a guest's order from a member's.** A check constraint sees only the
  order's own row, so "an order on a member's visit names that member" is kept by the application
  alone since 6.5.11 (the old `ck_cafe_orders_visit_has_member` refused every guest order). The
  attendance side is enforced by check constraints, as above (confirmed by the developer,
  1405/07/10). A service charge is the same since 6.5.31: its member is copied from its visit, and
  a guest's charge has none, which no check constraint on the charge's own row can compare.

### Cardio-only visit (ورود فقط هوازی)
Decided by the developer, 1405/07/11 (2026-10-03). Roadmap 6.5.27.
- **Who it is for:** a member with a plan who comes in on a day they only want the treadmill. They
  pay for the هوازی, and **no session is consumed**. Once inside they may use everything, like any
  visit; what makes it different is only that the plan is not charged a session.
- **Where:** from the box a free locker (or a reserve place, under §6's rules) opens, after
  choosing the member: «ورود فقط هوازی» beside the ordinary check-in. Both roles (§1); the audit
  log records who let the member in this way. Nothing can be sold with it (*Confirming at the front
  desk*): a member who needs a sale comes in the ordinary way.
- **The member must hold a plan:** a membership (not a single visit) that is `Active` today, or
  one that is `Frozen`. Anything else is refused with the reason ordinary check-in gives
  (`Attendance.NoSubscription`, `Subscriptions.Expired`, `Subscriptions.NoSessionsLeft`,
  `Subscriptions.NotStarted`, ...). Coming in this way **touches the plan in no way**: no session is
  consumed, a frozen plan stays frozen, and a queued plan is not moved forward over an exhausted
  one. The same preconditions as any check-in hold too: the member is active, and has no open visit.
- The visit names the plan it was let in on (`Attendance.SubscriptionId`), so the board and the
  locker's box show the plan and its sessions as for any visit, marked «فقط هوازی».
- **Check-out is refused until a هوازی amount is recorded** on the visit
  (`Attendance.CardioChargeMissing`), and the box's check-out button stays disabled until then. The
  amount must be recorded, not paid: what is not paid stays on the member's account as debt like any
  هوازی (§5 *Member debt*, §7 *Gym services*). A charge that is voided leaves the visit without one
  again. The minutes are not recorded: the desk works the price out, as for every هوازی.
- **Auto-checkout leaves such a visit open** while it has no هوازی amount: its locker is still held
  the next morning, and the member cannot check in again (one open visit per member) until the desk
  records the amount and checks them out. A visit that has its amount is auto-closed like any other.
- **Cancel check-in** within the usual window needs no amount: the member did not stay. There is
  no session to give back. Its هوازی, if any, is ticked or not like any visit's (*Cancel check-in*).
- **On screen:** the locker has **a colour of its own, yellow**, distinct from free, occupied,
  out of service and a guest's blue, with «هوازی» in its label and its own line in the map's legend
  (§6). The board, the locker's today history, the member's attendance history and the gym's
  history (§12) mark the visit «فقط هوازی».
- **Counted as attendance** like any member's visit, the chart under the map included (§6 *Today
  by hour*): the member came in and used the gym; only the plan was not charged.
- Moving to another locker, cafe and the «بدهکار» tag are as for any member's visit.

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
- A cardio-only visit with no هوازی amount, and a guest visit with something unpaid, are left open by
  *Auto-checkout* (*Cardio-only visit*, *Guest visit*), so they can still be inside after midnight.
  Task 11.4 has to decide what happens to them.
- Until task 11.4 is done, **the code must not enforce this rule**. The developer checks in test members
  at night, and enforcing it early would block that work.

### Gym services (هوازی and anything else sold during a visit)

Decided with the developer, 1405/06/31. Implemented in task 5.7.

- A **service charge** is money owed for something the member used during a visit. There are three kinds: `Cardio` (هوازی, the treadmill) and two things sold at the desk, `Miscellaneous` (فروشگاه, task 6.5.28) and `Analysis` (آنالیز, task 6.5.29) (*Sale at the desk* below); sauna or massage would be new kinds of the same thing, not new tables.
- **The price is not calculated by the system, on purpose.** The gym's rate (for example 10,000 Toman per 3 minutes) changes without notice and staff already work it out at the desk. The system takes the number they type and never checks it against a rate. There is no rate setting to keep in sync with reality.
- The amount is per visit, not per member: the same member may use the treadmill today and not tomorrow, so there is no cardio price on the member record.
- Recorded against an **open** visit (`CheckedOutAt IS NULL`, not cancelled), for whoever that visit belongs to. Front desk work, so both roles.
- **A guest visit too** (decided by the developer, 1405/07/12, roadmap 6.5.31; replaces "never on a guest visit, a guest uses the treadmill for free"): the charge goes under the guest's name on their visit, never on any member's account, and must be paid before the guest checks out (§7 *Guest visit*). Whether the treadmill is charged to a guest at all is the desk's call, as it is for a member.
- One non-voided هوازی per visit (a visit may have any number of sales, *Sale at the desk*). While the visit is open and nothing has been paid against it, staff can change the amount or remove it — nothing has been settled yet. After check-out, or after the first payment, it is a financial record: it is corrected with a **void plus a reason**, and a fresh charge if one is due (§5: financial records are never edited or deleted).
- Cancelling a check-in voids the visit's هوازی only when the desk ticks it (§7 *Cancel check-in*), with the reason that the check-in was cancelled. Left unticked, the charge stays owed on a visit that is now closed, and is corrected like any closed visit's charge: void plus a reason. *Replaces "cancelling always voids it", decided by Claude in task 5.7; decided by the Owner, 1405/07/06, roadmap 6.5.8.*
- **Voiding a charge that has been paid gives the money back**, as refunds written in the same transaction, one per payment method that is in credit — cash taken at the desk comes back as cash, a card payment is reversed on the card. §5 says there is no wallet, so the money cannot simply sit against the member's name, and neither the void screen nor cancel check-in has to ask which method to use (decided with the developer, 1405/07/01).
- **Recording, changing and voiding a charge are all Staff or Owner** (decided with the developer, 1405/07/01). This is a deliberate exception to §1's permissions table, which puts "refunds, voids" with the Owner: the amount is typed at the desk and the desk has to be able to take back its own mistake while the member is still standing there. The controls are that the reason is required and the audit log records who did it.
- There is no separate refund endpoint for a service charge. A charge that needs correcting is voided with a reason and re-entered at the right amount; a partial refund of a treadmill amount is that, not a refund.

- Auto-checkout changes nothing about a charge.
- A service charge is paid like anything else: it is one of the three things a payment can belong to (§5), it counts toward the member's debt (§5 *Member debt*), and it can be settled later or in instalments.
- The amount follows the same money rules as every other amount: greater than zero, at most 2 decimal places, `numeric(18,2)`.

### Sale at the desk (فروشگاه and آنالیز)

Asked by the developer, 1405/07/11 (2026-10-03). Roadmap 6.5.28 (فروشگاه, then called متفرقه) and
6.5.29 (آنالیز, and the changes of 1405/07/12). Something is sold at the desk that is neither on the
cafe's price list nor a service the system knows, and the desk still has to record the money. The
system does not know what it is, so the desk says.

- **Two kinds, one set of rules** (decided with the developer, 1405/07/11, task 6.5.29):
  «فروشگاه» (kind `Miscellaneous`, renamed from «متفرقه» everywhere on screen) and «آنالیز» (kind
  `Analysis`). Everything below holds for both unless it says otherwise; the kind says which source
  the sale is filed under.
- **فروشگاه: one or more items, each with three things typed** (decided with the developer,
  1405/07/12): the name of what was sold (required, at most 100 characters), how many (1 to 999,
  typed or stepped with ▲/▼, starting at one) and the price of one. «افزودن کالای دیگر» adds a
  line when two things were sold together; up to 50 lines are saved in one go, all or none. Each
  line becomes its own row, so each is paid or voided on its own. A line's total is the price of
  one times how many, stored with it.
- **آنالیز: only a price** (decided with the developer, 1405/07/12): one amount, typed in the same
  field as the هوازی amount. It has no name, quantity or unit price.
- The system never checks the name or a price against anything, the same as the هوازی amount
  above.
- **No money is taken when it is recorded** (decided with the developer, 1405/07/12; replaces "card,
  transfer, cash or «به حساب عضو», chosen in the form", 1405/07/11). The sale goes on the member's
  account like a cafe purchase from the locker, and nothing is asked. It counts toward the member's
  debt and is paid afterwards like any other debt: from «ثبت پرداخت» in the tile's list, which asks
  "آیا پول دریافت شد؟" as every payment does (§5 *Confirming money at the desk*), or in «تسویه یکجا».
- **From the locker box, against the open visit**, a member's or a guest's (decided by the developer,
  1405/07/12, roadmap 6.5.31; replaces "members only", 1405/07/11). On a guest's visit it goes
  under their name and is paid before they leave (§7 *Guest visit*), not on an account.
- **Any number per visit** (decided with the developer, 1405/07/11), each its own row.
- **Each kind is its own source** (decided with the developer, 1405/07/11): in the debt by source at
  check-out, the payment histories and the gym's history a sale reads «فروشگاه: …» or «آنالیز»,
  next to plan, هوازی and cafe, never folded into the cafe or into each other. The history's «بابت»
  filter offers هوازی, فروشگاه and آنالیز apart, and its «هوازی، فروشگاه و آنالیز» section lists all
  three, each row saying which. Cafe gross profit (§8) is not touched by either.
- **In the locker box each has its own tile** (task 6.5.29): هوازی, بوفه, فروشگاه and آنالیز sit
  side by side as coloured tiles, each with its icon and name, with no heading above them. Once
  something is recorded, its total and payment status show on the tile under the name.
- It is a service charge, so everything above about service charges holds — open visit, Staff or
  Owner, voiding gives the money back the way it came, auto-checkout changes nothing — with these
  differences. *Decided by Claude during task 6.5.28; pending review:*
  - **It is never edited.** A mistake is voided with a reason and the sale entered again, the rule a
    cafe order follows (§8). `ServiceCharges.SaleNotEditable` refuses a change of amount.
  - **In «تسویه یکجا» it is paid with the هوازی charges:** after the cafe, before the subscription,
    the oldest first (§5 *Settling several items at once*).
  - **Cancelling a check-in lists each sale with its own tick,** فروشگاه and آنالیز alike, unticked,
    exactly like the cafe orders (*Cancel check-in*). A sale named that is not a standing sale of
    this visit refuses the whole cancellation (`Attendance.SaleNotOnVisit`).
  - It does not count as the هوازی amount of a cardio-only visit (*Cardio-only visit*).
- The database enforces the shape: a فروشگاه item has its name, quantity and unit price, all three,
  and its amount is exactly what they make; هوازی and آنالیز have none of them
  (`ck_service_charges_miscellaneous`).

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
- A walk-in order (no member and no visit) is paid in full at creation: there is no account to put it on.
- **An order on a guest visit** (§7 *Guest visit*) names the visit and no member. It is under the
  guest's name, may stay unpaid while the guest is inside, and must be paid before the guest checks
  out. It is rung up from the guest's locker or from the till, where the guests inside can be chosen
  as the buyer. It never counts toward any member's debt. Paid and cancelled like any other order
  (reason, refund the way the money came).
- **A purchase made while the member is inside is tied to that visit** (decided with the developer,
  1405/07/04), exactly as a هوازی charge is (§7 *Gym services*). It is rung up from the member's
  locker, goes on the member's account with nothing paid, and can be added only to an open visit
  that is that member's own (`CafeOrders.VisitNotOpen`, `CafeOrders.VisitOfAnotherMember`).
  At check-out the box lists what the visit bought and the member's debt added up by source —
  plan, هوازی, cafe — before the item-by-item list.
  - **An order rung up at the till for a member who is inside joins their open visit by itself**
    (decided by the developer, 1405/07/06; replaces "an order from the till names no visit"). The
    till may be on a computer of its own, and the member is buying during that visit wherever the
    order is entered, so it shows on their locker as one of the visit's purchases, and in the
    debt and «تسویه یکجا» of the box, without the desk reloading anything. Whatever was
    paid at the till stays paid. A member who is not inside, and a walk-in, get an order with no
    visit, as before.
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
  - An expense recorded by paying a cheque or an instalment is not edited or voided on its own
    (`Expenses.LinkedToPayable`): see *Cheques and instalments* below.
  - Expenses and their categories are Owner only, reading included (§1).

### Cheques and instalments (چک و قسط)

Decided with the developer, 1405/07/12–13 (2026-10-04/05), roadmap 9.4; instalments, and the
expense written on payment, decided with the developer on 1405/07/13 (2026-10-05; replaces "a cheque
is not an expense, the Owner records it by hand, nothing links the two"). The gym pays for
equipment with dated cheques and with instalments; the system reminds the Owner of them and, once
one is paid, records the expense itself.

- **A cheque or an instalment is an expense once it is paid.** Marking a cheque «پاس شد» or an
  instalment «پرداخت شد» records an expense (above) in the same transaction: its amount, its
  expense category, its description, dated **the day it was marked** (the day the money left), no
  reference number. Before that it is not money in any report: what is still pending is only the
  register's total. **Members never pay by cheque**: the payment methods (§0) do not change.
- Two kinds, chosen on the form: **چک** (cheque) and **قسط** (instalment). One register holds both.
- Fields: `Kind`, `Amount`, `DueDate` (DateOnly, the date written on the cheque or the instalment's
  due day), `Payee` (در وجه for a cheque, «پرداخت به» — a bank or a seller — for an instalment),
  `Description`, `CategoryId` (the expense category the payment is recorded under),
  `RegisteredByUserId`. All typed fields are required. `Amount` follows the money rules of every
  other amount (greater than zero, at most 2 decimals, refused rather than rounded). `Payee` is at
  most 200 characters, `Description` at most 500. There is no cheque number and no bank field
  (decided with the developer). The category must exist (`Payables.CategoryNotFound`).
- **An instalment also says which one it is: «قسط n از N»** (`InstallmentNumber`,
  `InstallmentCount`), both required, `1 ≤ n ≤ N ≤ 360` (360: thirty years of monthly instalments).
  A cheque has neither (`Payables.InstallmentNumbersOnlyForInstallments`). Each instalment is
  entered on its own, like a cheque: there is no schedule that writes the rest.
- `DueDate` has no limit: one written months ago can still be entered late, and one dated a year
  ahead is normal.
- **Status**, from the record's own fields, never typed:
  - «در انتظار» (pending): registered, neither paid nor cancelled.
  - «پاس شد» / «پرداخت شد» (paid): the Owner marks it once the money has left the account. The day
    and the user are kept. **A cheque only on or after its date** (`Payables.ChequeNotDueYet`):
    under the Sayad system a bank does not pay a cheque early. **An instalment at any time**: paying
    one early is normal.
  - «باطل شده» (cancelled): entered by mistake or taken back from the payee. A reason is required,
    at most 500 characters.
  - One past its date is **not** paid on its own: it stays pending until the Owner marks it, so the
    reminder does not disappear by itself (decided with the developer).
- **Editable while pending**, every edit audited, the kind included. Cancelled is **final**: no
  edit, no mark, no way back (`Payables.AlreadyCancelled`); a mistake after that is a fresh one.
  Paid is not edited either (`Payables.AlreadyPaid`).
- **A payment marked by mistake goes back to pending, with a reason** (decided with the developer,
  1405/07/13): «برگشت به در انتظار», reason required, at most 500 characters. Its expense is
  **voided** with the same reason, in the same transaction, never deleted; the record is pending
  again and can be edited, cancelled or paid again (a new expense then). Only a paid one goes back
  (`Payables.NotPaid`). The audit log and the voided expense keep the trail.
- **The expense of a payment belongs to it.** On the expenses page it is marked «از چک و قسط» and
  cannot be edited or voided there (`Expenses.LinkedToPayable`): the only way to change it is to
  send the payment back to pending, so the register and the expenses never disagree. At most one
  standing (not voided) expense per cheque or instalment.
- Records are **never deleted**.
- **The register** lists both kinds together, filtered by status and by kind: pending ones the
  earliest date first, with the total of everything pending (what the gym still has to pay), and
  that total split into cheques and instalments; paid, cancelled and all, the latest date first.
- **The reminder** is on the dashboard (§12 *Needs attention*, *Cheques and instalments coming
  due*): 7 days before the date, one fixed number for both kinds (decided with the developer). An
  SMS waits for Phase 10; who receives it, and where that number is kept, is decided there.
- **The header alert** (decided with the developer, 1405/07/14): in the header, on every page, for
  the Owner only. Every pending cheque and instalment dated **within the next 5 days**, today
  included, and every pending one past its date (red, until the Owner marks it). It names the
  nearest one and how many days are left («امروز», «فردا», «۳ روز دیگر», «۲ روز گذشته»), with the
  count of the others; opening it lists them all. Nothing is shown when there are none. The
  dashboard's 7-day list stays as it is.
- Owner only, reading included (§1).

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
- Expenses by category (voided excluded). Net profit = revenue without فروشگاه and آنالیز − expenses (*Financial report* below).
- Attendance per day and by hour (cancelled excluded).
- Active subscriptions, expiring soon, low sessions. Top cafe products.
- Single-session (تک‌جلسه‌ای) revenue is reported separately from membership sales: they are the same
  kind of record (§4) but not the same business (*Financial report* below, since 9.1).

### Financial report (گزارش مالی)

Decided with the developer, 1405/07/12 (2026-10-04). Roadmap 9.1. The figures behind the Owner's
dashboard (9.3).

- **Owner only**, reading included. Staff are refused (403). Staff keep the desk panel and the
  history; a dashboard of their own is not planned.
- **A range is required**, `from` and `to`, both inclusive, in the gym's time zone, at most **366
  days** long (a Jalali leap year), so one request never reads more than a year.
- **Revenue is money received**: payments − refunds, each by its `PaidAt` in the gym's time zone
  (§5). Every payment and refund counts, those on a cancelled or voided item included, as in the
  history's «پرداخت‌ها» totals: the money did move, in and back out.
  - **By source**, six of them: membership plans, single-session visits (تک‌جلسه‌ای, apart from
    membership as above), هوازی, فروشگاه, آنالیز, cafe. Each with received, refunded and net,
    and **how many were sold** in the range: counted by the day each sale belongs to, cancelled
    and voided ones left out, the rule «فروش» below follows (decided with the developer,
    1405/07/13). The count does not follow the money: a plan sold before the range and paid in it
    is money of the range, not a sale of it. A cafe order is one sale, however many items it holds.
  - **By method**: card, bank transfer, cash. The cash net is what the drawer should hold for the
    range (صندوق).
  - **By staff member**: who took the payment or gave the refund (`ReceivedByUserId`), with
    received, refunded and net. The Owner sees who handled how much money, which the developer wants
    as the gym takes on more staff.
- **Guests' money is revenue.** A guest pays for nothing at the door, but what a guest buys (§7
  *Guest visit*) is an ordinary payment and counts in its source like a member's. Guests stay out of
  every attendance figure (§7), as before.
- **Sales of the range** («فروش») sit beside revenue: what was sold in the range, by the day each
  sale belongs to (§12 *Sales in the history*), cancelled and voided sales left out. The two figures
  answer different questions and neither replaces the other.
  - **فروشگاه and آنالیز are left out of it** (decided with the developer, 1405/07/14): «فروش» is
    plans, single visits, هوازی and the cafe, and the dashboard says so in its name, «فروش (به غیر
    از آنالیز و فروشگاه)». They are still sales of their own source: each is counted in its
    «sold» above, and the history's sales totals keep them.
  - **Paid on them, by method** (decided with the developer, 1405/07/14): card, bank transfer and
    cash, what has been paid so far on those same sales, refunds taken off, whenever it was paid.
    An unpaid sale has no method, so the three add up to less than «فروش» by what is still owed.
    The dashboard writes them «کارت», «انتقال», «نقد» inside the «فروش» card, beside the figure.
- **Expenses** by `ExpenseDate`, voided ones left out (§9), in total and by category.
- **Net profit («سود خالص») = net revenue without فروشگاه and آنالیز − every expense** of the same
  range (decided with the developer, 1405/07/14, like «فروش» above). Every expense counts,
  whatever its category. Revenue by when money arrived, expenses by the date the Owner gave them.
  The revenue itself still counts every payment, فروشگاه and آنالیز included; it is drawn in the
  revenue chart and the revenue breakdowns, but has no card of its own: «درآمد ناخالص» and
  «صندوق نقدی» were taken off the summary, and «فروش» took the first place (asked by the
  developer, 1405/07/14).
- **Cafe gross profit («سود بوفه» on the dashboard, 1405/07/14) = the cafe's net revenue − expenses in the «خرید بوفه» category** of the same
  range (the seeded category, by its fixed id, so a rename does not break it). The cafe counts no
  stock (§8), so this is the closest the gym gets to a margin, and only over a range long enough for
  purchases and sales to even out.
- **Compared with the range before it**: the same figures for the range of the same length ending
  the day before `from` (1405/07/01–07/12 is compared with 1405/06/19–06/30).
- **Day by day**: for every day of the range, net revenue and expenses, for the chart. A day with
  nothing is a zero, not a gap.

### Receivables (مطالبات)

Decided with the developer, 1405/07/12 (2026-10-04). Roadmap 9.1.

- **Owner only.** What everyone owes the gym right now, whatever the dashboard's range: every
  non-cancelled, non-voided sale's `amount − net paid`, never below zero per sale (§5 *Member debt*,
  the history's «مانده»). It includes what a guest still inside owes on their visit.
- **By age**, by the gym's day the sale was recorded on: 0–7 days, 8–30 days, more than 30 days
  old (today 1405/07/12 → 0–7 is 07/05 to 07/12). An old debt is the one that needs a phone call.

### Operational reports (گزارش‌های عملیاتی)

Decided with the developer, 1405/07/12 (2026-10-04). Roadmap 9.2. The counts behind the Owner's
dashboard (9.3), beside the money above.

- **Owner only**, like the financial report. A range is the financial report's: required, inclusive,
  in the gym's time zone, at most 366 days.
- **"Plan" means a membership plan.** A single visit (تک‌جلسه‌ای, §4) is one day for one visit: it
  is never counted as an active plan, never runs out, is never renewed and makes nobody a member.
- **Attendance** (per range): members' check-ins by the moment they began, in the gym's time zone.
  Cancelled check-ins and guests are left out (§7 *Guest visit*); a cardio-only visit counts (§7
  *Cardio-only visit*). The total, the total of the range before it (the same length, ending the day
  before `from`), how many different members came, every day of the range (a day with nothing is a
  zero), and a table of weekday × hour, Saturday first.
- **Plans today** (no range): plans active today (in their dates, not frozen, a session left),
  plans frozen now, and of the active ones those running out by the board's thresholds (§7 *The
  "currently inside" board*): ending within **5 days**, and **3 sessions** left or fewer. A plan
  running out counts here even when the member has already bought the next one; that is the
  «نیاز به اقدام» lists' question.
- **Top cafe products** (per range): the **10** products sold most by quantity, with what they were
  sold for, by the order's `OrderedOn`. Cancelled orders are left out; walk-ins' and guests' orders
  count, since the question is what sells. A product is shown by its name today. *The 10, and quantity
  before amount, are Claude's defaults, 1405/07/12; pending review.*
- **Renewal rate** (per range, by the plan's end date):
  - A plan **ended** when its last day is in the range and before today (a plan still covers its last
    day). A frozen plan has not ended, whatever its end date says.
  - It was **renewed** when the member has a later plan, not cancelled, sold no more than **30 days**
    after its end: before the end (a queued renewal) or after it (the member coming back). A member
    who comes back after 31 days is a returning member, not a renewal (decided with the developer,
    1405/07/12).
  - A plan not renewed yet whose 30 days are not over is **waiting**, and is left out of the rate:
    rate = renewed ÷ (ended − waiting). Otherwise the last month would always look worse than it is.
    *Claude's default, 1405/07/12; pending review.*
- **Single visit to plan conversion rate** («نرخ تبدیل تک‌جلسه‌ای به پلن», per range; decided with
  the developer, 1405/07/14): of the new people who came for a single visit, how many bought a plan.
  - **Who counts:** a person sold a single visit in the range, not cancelled, with **no membership
    plan sold to them before it** (cancelled ones made nobody a member). A former member back for
    one day is not trying the gym out, and is left out.
  - **Each person once**, by their first single visit of the range: three single visits and then a
    plan is one person, converted.
  - **Converted** when a membership plan, not cancelled, was sold to them from that single visit on
    and no more than **30 days** after its day, the same day included.
  - Not converted yet and the 30 days not over is **waiting**, left out like a plan waiting to be
    renewed: rate = converted ÷ (people − waiting).
- **New members** (per range): a member counts as new on the day their **first membership plan**
  was sold («تاریخ فروش», §4), cancelled plans left out (decided with the developer, 1405/07/12).
  Registering a member, or selling them single visits, does not make them a member. While the paper
  members are being entered, an old member's first plan in the system also counts as new.
- Renewal and new members come day by day; the dashboard adds the days up into Jalali months. The
  server keeps to Gregorian dates (§13).

### Needs attention (نیاز به اقدام)

Decided with the developer, 1405/07/12 (2026-10-04). Roadmap 9.2. Who the Owner should call today;
no range. One member can be on more than one list.

- **The plan lists leave out deactivated members** (the gym has already let them go) and single
  visits. The debt list does not leave anyone out: money owed is owed. *Including deactivated
  members in the debt list is Claude's default, 1405/07/12; pending review.*
- **Running out, not renewed:** a plan in its dates today and not frozen, with **3 sessions left or
  fewer** (none left included: the sessions are used up before the end date) **or ending within 5
  days**, and no plan bought after it (§4 `Upcoming`, not cancelled). The board's thresholds (decided
  with the developer, 1405/07/12, instead of "ending this week"), for every member rather than only
  those inside (§6 *The desk panel*). The soonest end first.
- **Left in the last 30 days:** a member whose latest plan ended between 30 days ago and yesterday,
  with nothing after it. A member with a frozen plan is away on purpose and is not listed. The
  latest first.
- **Stopped coming:** a member with a plan usable today (§4 `Active`) and **no visit for 10 days or
  more** (decided with the developer, 1405/07/12). The days count from the last visit, or from the
  plan's start when the member has not come since it started: a plan bought three days ago is not a
  member who stopped coming. A cancelled check-in is not a visit; a cardio-only one is. A frozen plan
  is not listed. The longest away first.
- **Old debts:** each member's debt on sales recorded **more than 30 days ago** (the receivables'
  oldest age, above), with the day of the oldest such sale; the largest first. What walk-ins and
  guests owe on such sales is one figure beside the list, since there is nobody to call. The list
  and that figure add up to the receivables' «more than 30 days».
- **Cheques and instalments coming due** (چک و قسط نزدیک سررسید, roadmap 9.4): every pending cheque
  and instalment (§9 *Cheques and instalments*) dated **within the next 7 days**, today included,
  and every pending one **past its date**, marked «سررسید گذشته», until the Owner marks it paid or
  cancels it. The earliest date first. Not a member list: each row is a kind, a payee, an amount
  and a date, with «قسط n از N» for an instalment.

### Dashboard (داشبورد)

Decided with the developer, 1405/07/12 (2026-10-04). Roadmap 9.3. The figures of the reports above,
on one page.

- **Owner only**, like the reports. Staff have no menu item for it, and the page asks nothing for
  them. The first screen stays the locker map for everyone (§7).
- **One range for the whole page**, chosen in one press: «امروز», «این هفته», «این ماه», «ماه
  قبل», «امسال», or «دلخواه» for two Jalali date boxes. It opens on **this Jalali month**.
  - "This week", "this month" and "this year" run from their first day **to today**, not to their
    last day, so the comparison with the range before covers the same number of days (07/01–07/12
    is compared with 06/19–06/30). The Iranian week starts on **Saturday**; the year on 1 Farvardin.
  - «ماه قبل» is the whole Jalali month before this one, 29, 30 or 31 days.
  - A range missing a date, running backwards or longer than 366 days is refused on the page with
    the API's own messages, before anything is asked.
- **Compared with the range before**: a card shows the change as a percent with an arrow and a word,
  green when it is good news and red when it is bad (expenses going up is bad news). When the
  range before had nothing or a loss, a percent says nothing true, and the card shows the figure
  before instead. New members and the renewal rate have no comparison: their reports send none.
- **«خرید پلن» and «تک‌جلسه‌ای»** (asked by the developer, 1405/07/14) are two cards of the
  range's summary: the money received for membership plans and for single visits, by the day it
  was paid like the revenue (the same figures as their bars in «درآمد به تفکیک منبع»), compared
  with the range before, with how many were sold in the range underneath. Beside the renewal rate
  sits the single visit to plan conversion rate (*Operational reports* above), without a
  comparison, like the renewal rate.
- **A profit is green, a loss is red** (decided with the developer, 1405/07/14): «سود خالص» and
  «سود ناخالص بوفه» show their figure in green above zero and in red below it, with its minus
  sign («−۳۰٬۵۰۰٬۰۰۰ تومان»). A month without revenue can end in a loss, and the Owner must see
  it as one, not as a missing figure. Zero is neither colour.
- **What does not depend on the range** (plans today, receivables, needs attention) stays the same
  when the range changes.
- **Charts read right to left**, like the page: the oldest day or month is on the right. A range
  longer than 62 days shows revenue and expenses by Jalali month instead of by day. New members and
  the renewal rate are by Jalali month. The weekday × hour table shows from the earliest hour
  anyone came to the latest.
- **Revenue by source** lists پلن, تک‌جلسه‌ای, هوازی, بوفه, فروشگاه, آنالیز: the cafe above
  فروشگاه. The plan and single-visit bars also say how many were sold in the range («۱۲ پلن فروخته
  شد»): plans sold matter more to the Owner than new members (asked by the developer, 1405/07/13).
  The bar's length stays the money received.
- **New members** are explained on the page as "members whose first membership plan was sold in
  this range" (this month, on the monthly chart), the rule of *Operational reports* above.

### History (تاریخچه)

Decided with the developer, 1405/07/10 (2026-10-02). Roadmap 6.5.25. Each member's own history is
on their profile and the cafe has its own order history (§8); this is the history of the whole gym.
It is a list of rows, not totals or charts: those are the reports above.

- **One «تاریخچه» page, three sections:** ورود و خروج (check-ins), پرداخت‌ها (payments and refunds)
  and «هوازی، فروشگاه و آنالیز» (renamed from هوازی when the sales joined it, tasks 6.5.28 and 6.5.29). The
  cafe keeps its own page and is linked from here.
- **Who recorded it is on every row:** who took the payment or gave the refund
  (`Payment.ReceivedByUserId`), who recorded the هوازی (`ServiceCharge.RecordedByUserId`) and who
  voided it, and who checked the member in (the row's `CreatedBy`). A visit the nightly job closed
  shows its check-out as «خودکار»: no user did it.
- **Staff and payments:** Staff see payments of today and the 3 days before it, in the gym's time
  zone (today 1405/07/10 → 07/07 to 07/10). The API enforces it: a payments request from Staff whose
  range starts earlier, or has no start at all, is refused (`Payments.HistoryTooFarBack`). The Owner
  has no limit.
- **Check-ins and هوازی:** no date limit for either role.
- **Filters:** a Jalali date range and one member, in every section. Payments also filter by method
  and by source (subscription, هوازی, فروشگاه, آنالیز, cafe).
- **Which day a row belongs to:** a check-in by the moment it began, a payment by the moment it was
  taken (`PaidAt`), a هوازی by its business date (`ChargedOn`), all in the gym's time zone. Each
  section lists newest first.
- **Cancelled, voided, refunds:** listed and marked, never hidden, as the cafe's order history does.
  A refund is its own row, marked «استرداد» with its reason. A payment whose item was later
  cancelled or voided keeps its row and is marked, so the refund that followed reads beside it.
- **Guests** (ورود مهمان, §7) are listed in the check-ins, marked «مهمان», with no profile link.
  They still count nowhere in the reports above.
- Details. *Decided by Claude during task 6.5.25; pending review.*
  - Every section opens on today (from and to both today). Widening the range, or clearing it for
    no bound (Owner, or Staff outside payments), is one change of a date box.
  - The member filter is a member chosen by name or mobile, the way the cafe's till chooses one,
    not free text matched against every row.
  - A page holds 20 rows.
  - A payment for a guest's cafe order shows the guest's name, marked «مهمان», not «مشتری آزاد»: the
    order is under that name (§8). So does a guest's هوازی or sale, in every section that lists it
    (since 6.5.31).

### Sales in the history (فروش‌ها)

Asked by the developer, 1405/07/12 (2026-10-04). Roadmap 6.5.30. The Owner wants to see everything
the gym sold in one place, and each kind on its own, with what is still owed. Each section ends
with its totals (roadmap 6.5.32, *Totals in the history* below); reports and charts are Phase 9.

- **Owner only.** Staff keep the three sections above, unchanged. The API refuses Staff (403).
- **For the Owner, the history's sections are:** ورود و خروج، پرداخت‌ها، همهٔ فروش‌ها، فروش پلن،
  هوازی، فروشگاه، آنالیز، بوفه. The separate هوازی, فروشگاه and آنالیز sections replace the combined
  «هوازی، فروشگاه و آنالیز» one, which only Staff still see.
- **What a sale is:** a subscription (single-session and membership together under «فروش پلن», each
  row saying which), a هوازی charge, a فروشگاه item, an آنالیز, a cafe order. «همهٔ فروش‌ها» lists
  all five kinds together, cafe orders of walk-ins and guests included. The cafe's own order page
  stays as it is.
- **Which day a sale belongs to:** a subscription by when it was sold (`CreatedAt`, «تاریخ فروش»,
  in the gym's time zone), a هوازی or sale at the desk by its `ChargedOn`, a cafe order by its
  `OrderedOn`. Newest first, by the moment it was recorded.
- **Paid or not** (a three-way choice on every sales section: همه / پرداخت شده / پرداخت نشده):
  - «پرداخت شده»: net paid (payments − refunds) covers the amount. A free plan (`Price = 0`) is paid.
  - «پرداخت نشده»: anything still owed, a partial payment included.
  - A cancelled subscription or cafe order and a voided charge are in neither: they owe nothing and
    were never fully a sale. They are listed under «همه», marked with their reason, never hidden.
- **Who recorded it:** who placed the cafe order (`PlacedByUserId`) and who recorded the charge
  (`RecordedByUserId`). A subscription shows none: who sold it is not shown (§4).
- The filters are the history's own: the date range and one member. A page holds 20 rows.

### Totals in the history (جمع)

Asked by the developer, 1405/07/12 (2026-10-04). Roadmap 6.5.32. Each section where money changes
hands ends with a totals row, so the Owner reads what a day, a member or a kind of sale came to
without adding up the rows.

- **Owner only**, like the sales sections. Staff see the same rows as before and no totals; the API
  refuses Staff (403).
- **Over everything the filters let through**, every page, not only the 20 rows on screen. The
  totals follow every filter of their section: the date range, the member, the sales section's kind
  and «وضعیت پرداخت», the payments' «روش پرداخت» and «بابت».
- **Every sales section** (همهٔ فروش‌ها، فروش پلن، هوازی، فروشگاه، آنالیز، بوفه) shows three figures:
  - «مبلغ»: what the sales were sold for.
  - «دریافتی»: net paid on them (payments − refunds).
  - «مانده»: what is still owed, each sale's `amount − net paid` never below zero (as §5's debt).
  - A cancelled subscription or cafe order and a voided charge count toward none of the three. Their
    row stays in the list under «همه», marked; their money already came back as a refund (§5).
- **«پرداخت‌ها»** shows «دریافتی» (the payments), «بازگشت» (the refunds) and «خالص» (payments −
  refunds). Every payment and refund counts, a cancelled or voided item's included: the money did
  move, in and back out.

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
- Dates are shown in the Jalali calendar. They are stored and exchanged as Gregorian `DateOnly` and UTC timestamps. **Every date is chosen in three dropdowns** — day, month by name, year — the way mobile banking apps ask for it, never typed as a Gregorian date, and converted to ISO before it is sent (asked by the developer for a birth date on 1405/07/12, and for a cheque's or instalment's date and an expense's date on 1405/07/13, replacing the calendar picker). The days follow the month (Esfand follows the year), and until all three are chosen there is no date. Each list opens short (about six rows) and scrolls, the same size for all three, and typing digits jumps to a year (۱۴۰۵ or 1405). The years offered depend on the date (chosen by the developer): **a birth date** runs from ۱۴۰۰ back to ۱۳۲۰ (1405/07/12; the API's 120-year and not-in-the-future rules of §2 still apply); **a cheque's, instalment's or expense's date** runs from three years after the current Jalali year back to ۱۴۰۴ (1405/07/13), so the list moves on by itself each Nowruz and a cheque due years ahead can still be entered. A stored date outside its range still shows, with its year added to the list. Every date other than a birth date has a «پاک کردن» button. **A filter's date is the exception** (asked by the developer, 1405/07/13, after trying the dropdowns there: a filter is changed again and again, and three lists made that slow): «از تاریخ» and «تا تاریخ» on the history, expenses and cafe orders pages and in the dashboard's custom range are a box that takes both picking a day in a Jalali calendar and typing the date by hand — digits alone get their slashes as they are typed (۱۴۰۵۰۶۱۰ → ۱۴۰۵/۰۶/۱۰), and `/`, `-`, `.`, a space, a comma, `÷`, `٫` or `،` between the parts are all accepted — with a clear button, because an empty date is how a filter says "no limit".
- **Every amount of money on screen is protected against miscounted zeros** (decided with the developer, 1405/06/31 — roadmap 4.8). Toman amounts are large enough that `500000` and `5000000` look alike at a glance, and a wrong figure here is a wrong figure in the gym's books.
  - Every amount that is **typed** goes through the shared money field: Persian digits, grouped in threes as it is typed (۵۰۰٬۰۰۰), and the same amount written out in words underneath it — «پانصد هزار تومان». The words are the check: nobody miscounts a word.
  - Every amount that is **displayed** goes through one formatter, which groups in threes and appends "تومان". A report's figure below zero (a loss, §12 *Dashboard*) keeps its minus sign, on the number's left as Persian number formatting writes it.
  - No screen formats an amount by itself, and no money value is ever held as a JavaScript number: rounding a price is never acceptable.
- Reports offer Jalali periods (today, this week, this Jalali month, last month, this Jalali year; §12 *Dashboard*) that the frontend converts to Gregorian date ranges.
- SMS messages are Persian. Unicode SMS parts hold fewer characters than Latin ones, so templates are kept short and the part count is calculated before sending.

## 14. Theme (تم روشن و تیره)

Decided by the developer, 1405/07/09 (2026-10-01). Roadmap 6.5.22.
- The app has two themes, light and dark, and every screen follows the same one: the frame, every
  page, the dialogs and the date picker's calendar. No screen picks a theme of its own.
- It opens light. A button in the header (a moon, «تم تیره») switches to dark; pressed again (a sun)
  it switches back.
- The choice is remembered **on that device, in that browser**, not for the user: the desk PC can
  stay dark while the Owner's phone stays light, and signing in as someone else on the desk PC keeps
  the desk's theme. A browser that cannot store it (a private window) opens light every time.
- A device that saved dark opens dark straight away, with no white flash while the page loads.
