# Business Rules

This file is the source of truth for how the gym works.
If code and this file disagree, this file wins. If this file is silent, ask before implementing.

---

## 0. Settings still to decide

These values live in configuration (the `Gym` and `Sms` sections). Decide each one before the phase listed.

Nothing is open at the moment. (The last one, whether Kavenegar accepts the birthday greeting as a
template, was closed on 1405/07/16: Kavenegar refused every template, and all SMS now go out as free
text from the gym's dedicated line, §10 *Sending*.)

The SMS numbers themselves (days, sessions, send times) are not configuration: the Owner sets them
on the SMS settings page (§10).

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
- There is no `Gym:OpeningTime`. The system does not limit check-in to opening hours (§7 *Opening
  hours*; dropped by the developer on 1405/07/18, 2026-10-10).
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
- `Sms:MaxAttempts` = 3, and `Sms:RetryDelays` = 1 minute, then 5 minutes: a message that may
  still pass is tried at most three times, waiting 1 minute before the second try and 5 before
  the third (decided with the developer, 1405/07/14, task 10.3; §10 *The daily runs*). Not on the
  settings page.
- A run the server missed (it was down at the send time) is made when the server is back **the
  same day, before 22:00**; after 22:00 that day's run is skipped, and the next day's run reaches
  whoever is still covered (decided with the developer, 1405/07/14, task 10.3).
  The daily report (§10) is made until midnight instead (decided with the developer, 1405/07/27,
  task 10.7): a missed run of its chosen time is still made the same night.
- A cheque or instalment whose date is changed after its SMS was sent **gets no second SMS**:
  one per cheque or instalment, whatever its date becomes (decided with the developer,
  1405/07/14, task 10.3). The header alert and the dashboard follow the new date.
- **The gym has two prices, and the Owner sets both in the app** (§3 *Prices*): the price of one
  session of a plan, and the price of a single-session (تک‌جلسه‌ای) visit. They are stored in the
  database, not in configuration, because the Owner changes them with inflation and must not need a
  deployment to do it (decided with the developer, 1405/07/05, replacing the 1405/07/03 rule that the
  single-session rate was a plan's price and not a setting — there are no plans any more).

---

## 1. Users and access

- The panel is a closed system: it has no registration endpoint, and members and trainers never
  log in to it. They sign in on the site instead, with a phone code (§15, ADR 0010, decided
  1405/08/03; until then "Members never log in" held everywhere).
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
  - Refresh and logout are rate-limited per IP address: 60 requests per minute, counted together (decided with the developer, 1405/07/18, task 11.2). They need no password, so they do not share login's 10, but they are anonymous, so a flood from one address is turned away before it reaches the database. Each open tab refreshes about every 15 minutes, so a front desk behind one IP never comes near it.
- Login has account lockout after repeated failures and per-IP rate limiting.
  - Lockout: 5 consecutive wrong passwords lock for 15 minutes, counted separately for each trusted device and for all untrusted devices together (*Lockout* below). A successful login resets the count. The Owner can be locked out too.
  - Rate limit: 10 login attempts per minute per IP address. Front-desk staff share one IP, so the limit allows several people to log in at once.
  - Failure responses: an unknown user name and a wrong password return the same error (`Auth.InvalidCredentials`), so user names cannot be discovered. A locked account returns `Auth.LockedOut`. An inactive account returns `Auth.UserInactive`, but only when the password was correct; otherwise `Auth.InvalidCredentials`.
  - Accepted (decided with the developer, 1405/07/18, task 11.2): `Auth.LockedOut` tells whoever reaches it that the user name exists, since an unknown name is never locked. Hiding it would mean a pretend counter for every name typed, or telling a real user "wrong password" while their right one is refused. Guessable names are refused (*Lockout* below), the lock only closes the untrusted door, and the login rate limit makes collecting names slow.

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
| Cardio-only visit (ورود فقط هوازی): check in without consuming a session, with or without a plan (§7 *Cardio-only visit*) | ✅ | ✅ |
| Assign or renew subscriptions | ✅ | ✅ |
| Register payments, create cafe orders | ✅ | ✅ |
| See the two prices (§3) | ✅ | ✅ |
| Change the two prices (§3), staff accounts | ✅ | ❌ |
| Lockers: see the map, see who had a locker today, take one out of service, bring it back in, move a visit to another locker (§6, §7) | ✅ | ✅ |
| Lockers screen: the desk panel, today by hour, the locker usage map (§6; counts only, no money) | ✅ | ✅ |
| Freeze, unfreeze, cancel subscriptions (a check-in by either role still ends a freeze, §4 *Freeze*) | ✅ | ❌ |
| Voids (outside the cafe), and the refunds they write (§5) | ✅ | ❌ |
| Gym service charges: record, change the amount, void (§7 *Gym services*) | ✅ | ✅ |
| Cafe: products, categories, orders, and cancelling an order (§8) | ✅ | ✅ |
| The gym's history (تاریخچه): check-ins and هوازی of any day (§12 *History*) | ✅ | ✅ |
| The gym's history: payments of today and the 3 days before it (§12 *History*) | ✅ | ✅ |
| The gym's history: payments of any earlier day | ✅ | ❌ |
| The gym's history: sales (فروش‌ها) of any day, paid and unpaid (§12 *Sales*) | ✅ | ❌ |
| The gym's history: the totals of the sales and payments sections (§12 *Totals*) | ✅ | ❌ |
| Expenses, dashboard, reports, audit log | ✅ | ❌ |
| SMS: settings, history and cost, resend (§10) | ✅ | ❌ |
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
  - Results are sorted by **the latest visit first** (decided with the developer, 1405/07/15, roadmap 6.5.34, replacing "sorted by name"): the members the desk sees are at the top. A cancelled check-in is not a visit; a cardio-only visit is. Members who never came go last, by name.
  - **No status column** (removed by the developer, 1405/07/15): the «همه / فعال / غیرفعال» filter already splits active from inactive, so «فعال», «غیرفعال» and «فریز» are no longer shown on the rows.
  - **«تک‌جلسه» tag** (red): the member's latest visit (cancelled ones left out) used a single-session subscription (§4), whatever they had before it.
  - **Never both tags** (developer, 1405/07/15): a member whose latest visit was a single visit and whose plan has ended shows only what happened more recently. A plan that still had sessions ended with its last day, so it is the more recent when that day is the single visit's day or later; a plan whose sessions ran out ended at its last session, a visit before the single visit, so the single visit is the more recent. The filters follow the tags.
  - **Each tag is also a filter** («تک‌جلسه», «پلن تمام‌شده»), beside «بدهکار» and working the same way: applied before paging, combined with the status, the search and each other. A filter lists exactly the members its tag is shown on (asked by the developer, 1405/07/15).
  - **«پلن تمام‌شده» tag:** the member's plan is over, by its dates (`Expired`) or its sessions (`Exhausted`), and they have no active, frozen or queued plan after it. Single visits are not plans: one bought after the plan ended does not clear the tag, and a member who only ever bought single visits never gets it. Deactivated members never get it (the gym has let them go, as in §9 *Needs attention*).
  - **«جلسات» column:** the sessions bar of the «داخل باشگاه» board (§7: used of total, marked at 3 or fewer left) for the member's plan: the active one, else the frozen one, else the soonest queued one, else the one that ended last. Nothing for a member with no plan.
- Deactivating an inactive member, or reactivating an active one, succeeds and changes nothing.
- Member screens (decided with the developer in task 2.3):
  - The home page is the member search. The server status page moved to its own menu item.
  - The search runs by itself about 300 ms after typing stops; Enter searches at once.
  - Deactivating asks for no confirmation: nothing is deleted, and reactivating undoes it in one click.
  - After a new member is saved, their profile opens.
  - Phone numbers are shown in the local format with Persian digits (`۰۹۱۲ ۱۲۳ ۴۵۶۷`); the API stores and returns E.164.
  - On a phone (roadmap 13.1b, decided by the developer, 1405/07/20 (2026-10-12)): the search sits beside the title «اعضا», with «عضو جدید» on the line under them, and the six filters («همه / فعال / غیرفعال» and «بدهکار / تک‌جلسه / پلن تمام‌شده») share one line, smaller. Each member is a card (as every list is on a phone, 13.1b): the name and its tags, then a line each for mobile, sessions and debt; no debt line for a member who owes nothing. The computer's screen is not changed by it.

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
  | 21 to 30 | 70 |

  - Fewer than 5 sessions is refused (`Subscriptions.SessionCountTooLow`); more than **30** is
    refused (`Subscriptions.SessionCountTooHigh`), at the desk with «حداکثر ۳۰ جلسه می‌شود فروخت؛
    لطفاً عددی از ۵ تا ۳۰ بنویسید.» (decided by the Owner on 1405/08/03 (2026-10-25), roadmap
    6.5.38, replacing the ceiling of 140 of 6.5.18). The days of the table did not change.
  - A plan sold before then keeps its sessions; a sale is never rewritten. The only one above 30 was
    123 sessions typed by mistake and already cancelled, with nothing paid, so it is kept as a
    cancelled plan. Renewing a plan above 30 is refused like any other sale of that many.
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
- The history also shows when each subscription was sold («تاریخ فروش», the moment it was created), whether or not anything was paid; each payment keeps its own time (§5). Who sold it is the subscription's `CreatedBy`, whoever was logged in; the sales history shows it as «ثبت توسط» (developer, 1405/07/15, replacing "not needed for now" of 1405/07/10).
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
- Database: `total_sessions` is required; check constraints `used_sessions <= total_sessions`, and, unless single-session, 5 to 30 sessions with the days the §3 table gives them (a cancelled plan may have up to the old 140, task 6.5.38); `xmin` concurrency token.

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
- **Cancelling gives back everything paid on it, in the same step** (decided by the developer,
  1405/07/22, roadmap 6.5.37; replaces "payments are untouched, the refund is a separate step",
  after a cancelled plan at the gym kept its money because the second step was forgotten). As a
  cancelled cafe order (§8) and a voided charge (§7): one refund per payment method still in
  credit, so cash comes back as cash and a card payment is reversed on the card, each carrying the
  cancel reason. Nothing paid, nothing refunded: it is simply cancelled.
  - There is no other refund of a subscription. A payment taken wrong is fixed by cancelling the
    whole subscription, which gives all of it back, and selling a new one at the right figures.
  - A cancelled subscription takes no more payments (`Subscriptions.Cancelled`), as a cancelled
    cafe order takes none (§8): with no refund of its own, money paid onto it could never come
    back. *Decided by Claude during task 6.5.37; pending review.*
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
- Payments are never edited or deleted. A mistaken entry is fixed by undoing what it paid for, which refunds it: cancelling the subscription (§4 *Cancel*) or the cafe order (§8), voiding the charge (§7).
- A refund cannot exceed the current net paid amount.
- **No refund is written by hand** (decided by the developer, 1405/07/22, roadmap 6.5.37; replaces the subscription's own refund of task 4.4, partial or a full "void"). Every refund comes from a cancel or a void, and gives back all that is still paid, per method.
  - Sessions already taken are not bought back: only a subscription nobody has used can be cancelled (§4), so only that one is refunded (decided with the developer, 1405/06/31). A payment typed wrong on a subscription the member has already used stays as it is. That is deliberate: the overpayment guard above refuses more than the price as it is typed, so a wrong figure is caught at the desk, and a visit entered by mistake can be undone within the cancel window (§7), after which the subscription can be cancelled.
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
  and a payment with no method picked is refused before anything is sent. The desk has
  to look at the list and choose.
- **The list is always in the same order: کارت (Card), انتقال بانکی (BankTransfer), نقدی (Cash).**
- **Before any money is written, the desk answers a second question**, which names the amount
  and the method: "آیا پول دریافت شد؟". Nothing is sent until the desk answers yes. "No" closes the question and leaves the form
  as it was.
- This applies to a payment against a subscription, a هوازی charge or a cafe order, to «تسویه
  یکجا», and to the cafe till. A refund is never typed (§5, roadmap 6.5.37): the cancel or void
  that writes it asks for its reason and says how much goes back instead.
- A cafe order left wholly on a member's account takes no money, so it asks neither for a method
  nor for the confirmation (§8).
- This is a desk-side guard only. The API already requires a valid method on every payment, so
  nothing changes on the server.

### Member debt (open account, حساب باز)

Decided with the developer, 1405/06/31. Implemented for subscriptions in task 4.7 and for service
charges in 5.7; cafe orders join the same total in Phase 7.

- Debt is **calculated, never stored**, the same way subscription status and payment status are: there is no balance column to keep correct, and no nightly job to recompute one.
- A member's debt = what is still owed on their non-cancelled subscriptions, plus what is still owed on their non-voided service charges (§7 *Gym services*), plus (from Phase 7) what is still owed on their non-cancelled cafe orders. Per item that is `Price − net paid`, never below zero.
- Cancelled items owe nothing. A subscription can only be cancelled while nobody has used it (§4), and money already paid on one comes back as refunds written by the cancellation itself (§4 *Cancel*), not as a debt that quietly disappears. A voided service charge owes nothing for the same reason.
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
- Correcting a settlement is the same as correcting any payment: each row's item is voided or
  cancelled on its own terms, which refunds that row (§4, §5, §7, §8).

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
  «بدهکار» tag in red letters on a faint red ground (the ground was solid red until 1405/07/20,
  softened by the developer) hanging from the door's top edge at its top-right corner (changed by
  the developer, 1405/07/07, from a band across the top-left corner), whenever the member holding
  the locker has any debt at all (§5 *Member debt*: a subscription, a service such as هوازی, or a
  cafe order). It is information only, like the debt shown at check-in; the amount and what it is for are in the
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
- A free door is tinted green from its top corner, like an occupied one in red, with a green edge
  down its right side and the number a soft grey, lighter than an out-of-service door's (changed by
  the developer, 1405/07/20: the face was plain and the number green, then white was too bright).
  An occupied door is tinted red from its top corner, with a red edge, the number in the text
  colour and the holder's name under it. An out-of-service door is hatched, with a grey number and
  a lock under it.
- No door has a frame or a face of its own: it is the page's own colour, and only its edge and its
  tint mark it (changed by the developer, 1405/07/20: it was a tile in a thin frame).
- A door rises a little under the mouse, so the map feels alive. A door whose state changed since
  the last refresh pulses once in its new colour. Neither moves for anyone who has asked their
  system for less motion.
- Above the map: the counts of free, occupied and out-of-service lockers, and what the «بدهکار»
  tag and the long-stay bar mean. (A strip of 72 marks showing how full the lockers are, and a
  "refreshed N seconds ago" mark, were tried and removed by the developer, 1405/07/07: the desk
  does not need them.)
- On a phone (roadmap 13.1a, decided by the developer, 1405/07/20 (2026-10-12)): above the map
  only the counts of free, occupied, guest and cardio-only lockers, on one line, and the name
  search beside the title; no out-of-service count, no meaning of the «بدهکار» tag or the
  long-stay bar, no usage view (*Locker usage map* below) and no hourly chart (*Today by hour*
  below). The cabinets wrap to the next row, each keeping its six doors as they stand: two to a
  row, so a door is wide enough (about 77px on a 390px phone) to show its holder's name under the
  number (chosen by the developer in 13.1c, 1405/07/21 (2026-10-13), over three to a row with
  numbers only; three to a row was 13.1a's first look). What the name search found is written
  under the search box, not beside it. The reserve places stand three to a row, a little lower.
  Every locker comes first, then the desk panel (*The desk panel*), then the reserve places. Every
  change for a phone stays on a phone: the desk's computer screen is not changed by it.
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
- Not on a phone (decided by the developer, 1405/07/20: not needed there); the computer's screen
  only.

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
- Not on a phone (decided by the developer, 1405/07/20: not needed there); the switch is on the
  computer's screen only.

---

## 7. Attendance

### Check-in (one database transaction)
Preconditions: the member is active, has an `Active` subscription, and has no open attendance.
The one visit without a member or a subscription is a guest's, under its own rules (*Guest visit*).
The request names the locker the desk chose, or asks for a reserve place (§6). It may also carry a
sale — a single visit or a plan — which is made first, in the same transaction, and is then what
step 1 finds (*Confirming at the front desk*).
1. Load the subscription that is in effect today — an `Active` one always wins over a queued renewal that ends later. If none is active, apply the exhausted-with-a-queue rule above. If still none and a membership is frozen, unfreeze it (§4 *Freeze*) — unless the request carries a sale.
2. `subscription.ConsumeSession(today)` — except on the member's birthday visit (*Birthday visit*),
   which checks that the plan is usable today and consumes nothing.
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
  These are the desk's thresholds. The SMS reminders (§10) have their own, set by the Owner, and
  they may differ: the desk costs nothing and an SMS does (decided with the developer, 1405/07/14).
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
- On a phone (roadmap 13.1c, decided by the developer, 1405/07/21 (2026-10-13)): a box that holds
  a form (check-in and check-out, the locker's visit, buying from the cafe, هوازی and the other
  sales) fills the whole screen, its buttons stay at the bottom of the screen while it scrolls,
  and its ✕ stays in the corner. A short question (confirming a payment, a freeze, an
  out-of-service locker) and the "saved" message after a sale stay a small card. When
  «تسویه یکجا» is opened, the page scrolls to its form. Not on a phone, on any page (decided by
  the developer the same day: not needed there): «جابه‌جایی کمد» and «تاریخچه امروز این کمد»,
  in the visit's box and in a free locker's box; the computer keeps both. Also on a phone only,
  asked the same day: in a free locker's box «ورود مهمان» and the guest form's «ثبت ورود مهمان» are in the guest's
  blue (as a guest's door is; هوازی keeps its yellow) and «خارج از سرویس کردن این کمد» is
  red-edged (both looked like plain words); in the visit's box the person's name is larger
  and bold. The computer's boxes are not changed.

### Cancel check-in
- Allowed only for an open attendance within `Gym:CancelCheckInWindowMinutes` of check-in.
- Restores the session, frees the locker, and marks the attendance cancelled (who and when). The row is kept. A visit that used no session (a birthday visit, a cardio-only one, a guest's) has none to restore.
- A freeze the check-in ended stays ended (§4 *Freeze*): the Owner freezes the plan again if it was a mistake.
- Cancelled attendances are excluded from attendance reports.
- **What the visit bought is cancelled only when the desk says so, one purchase at a time**
  (decided by the Owner, 1405/07/06, roadmap 6.5.8). The box lists the visit's هوازی, each of
  its sales (فروشگاه, آنالیز and متفرقه, *Sale at the desk*, tasks 6.5.28, 6.5.29 and 6.5.36) and each of its cafe orders with its
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

### Birthday visit (ورود رایگان تولد)
Decided with the developer, 1405/07/27 (2026-10-19), roadmap 10.8. The birthday SMS (§10) tells
the member of it; the visit is free whether or not the SMS went out.
- **On their birthday, a member's first visit uses no session of their plan**, like a guest's
  (§7 *Guest visit*). The birthday is the Jalali month and day, the same day the desk celebrates
  (§6 *The desk panel*; 29 Esfand for someone born on 30 Esfand in a year without it). The day they
  were born is not a birthday.
- **It is automatic**: the desk chooses nothing. Check-in finds the subscription as always (*Check-in*
  step 1); when that is a **membership plan** (§4, not a single visit), the visit is the birthday
  visit: the plan must still be usable today (`Active`, the same refusals as `ConsumeSession`), and
  `UsedSessions` does not change.
  - A frozen plan is unfrozen by the check-in as usual (§4 *Freeze*), and then uses no session.
  - **A plan sold with the check-in counts too** (decided by the developer, 1405/07/27): someone who
    buys a plan at the locker on their birthday comes in on it for free.
  - A **single visit** is never free: when check-in uses one (one held for today, or one sold in the
    box), it is used as usual, the money already paid for it.
  - A cardio-only visit (*Cardio-only visit*) uses no session anyway and is not a birthday visit.
- **Once per birthday**: the second visit on the same day uses a session as usual. The database
  holds it too: a partial unique index allows one birthday visit per member and Jalali year, not
  counting cancelled ones.
- **Cancelling it** (*Cancel check-in*) gives no session back, since none was used; the visit no
  longer counts, so the member's next visit that day is the free one.
- **No money moves**: nothing is sold or recorded at zero, so the financial report is unchanged and
  no record is deleted. The visit is ordinary attendance and is counted in every attendance report
  and in the daily report's «ورود» (§10).
- **On screen**: the check-in box says before confirming that today is the member's birthday and
  the visit on their plan is free, and afterwards that no session was used; the visit is marked
  «تولد» wherever visits are listed (the board, the locker's box, the member's history and the
  history page).

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
  and "sales at the desk are for members only"). Their locker box has the same five tiles as a
  member's: هوازی, بوفه, فروشگاه, آنالیز and متفرقه (since 6.5.36). Each follows its own rules (*Gym services*, *Sale at the
  desk*, §8): one standing هوازی per visit, any number of sales and orders, the price typed by the
  desk and never checked against a rate. Whether a guest is charged for the treadmill, and how
  much, is the desk's call on the Owner's instructions, exactly as for a member: the system never
  prices it, and a هوازی left unrecorded costs nothing.
- **Purchases:** a guest buys on their visit, under their name, from their locker's box, and in the
  cafe also from the till (§8). While they are inside, what they bought may stay unpaid, and their
  locker carries «بدهکار» (§6). **A guest has no account and leaves no debt behind:**
  - **Check-out is refused while anything bought on the visit is unpaid**, a cafe order, a هوازی, a
    فروشگاه item, an آنالیز or a متفرقه (`Attendance.GuestHasUnpaidPurchases`). The guest's box settles all of
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
Decided by the developer, 1405/07/11 (2026-10-03). Roadmap 6.5.27. *No plan needed* decided by the
developer, 1405/07/15 (2026-10-07), roadmap 6.5.35.
- **Who it is for:** a member who comes in on a day they only want the treadmill. They pay for the
  هوازی, and **no session is consumed**. Once inside they may use everything, like any visit; what
  makes it different is only that no plan is charged a session.
- **Where:** from the box a free locker (or a reserve place, under §6's rules) opens, after
  choosing the member: «ورود فقط هوازی» beside the ordinary check-in. It is offered again **under
  the sale** (single visit, plan) that the box shows when an ordinary check-in is refused for want
  of a usable plan, and right after a new member is registered there. Both roles (§1); the audit
  log records who let the member in this way. Nothing can be sold with it (*Confirming at the front
  desk*): a member who needs a sale comes in the ordinary way.
- **No plan is needed** (roadmap 6.5.35; *replaces "the member must hold a membership that is
  `Active` today or `Frozen`, anything else refused", 6.5.27*). A member with no plan at all, only
  single visits, a plan that expired, ran out of sessions or has not started, comes in all the same.
  Coming in this way **touches no plan in any way**: no session is consumed, a single visit stays
  unused, a frozen plan stays frozen, and a queued plan is not moved forward over an exhausted one.
  The preconditions of any check-in still hold: the member is active (`Members.Inactive`), and has
  no open visit (`Attendance.AlreadyCheckedIn`).
- **The plan the visit names** (`Attendance.SubscriptionId`): the membership `Active` today, else a
  `Frozen` one, so the board and the locker's box show the plan and its sessions as for any visit,
  marked «فقط هوازی». With neither, the visit names **no plan**: the board shows «—» where the
  sessions go and no end date, the locker's box says «بدون اشتراک». An expired or exhausted plan is
  not named: it says nothing the desk needs at the door. The database allows a member's visit
  without a plan only when it is cardio-only (`ck_attendances_subscription_with_member`).
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

### Opening hours (none in the system)
Decided with the developer, 1405/07/18 (2026-10-10, roadmap 11.4). This drops the rule decided on
1405/07/04, which would have refused check-in from `Gym:ClosingTime` until a `Gym:OpeningTime` of 06:00.
- **The system has no opening hours.** Check-in, check-out and everything else at the desk work at
  any time of day. Nobody checks a member in while the gym is shut, so a rule against it would only
  add a setting to keep right and block the developer, who tests at night.
- Leaving works as it always has: staff (Owner or Staff) check a visit out by hand, and
  *Auto-checkout* closes the rest at `Gym:ClosingTime`, except a cardio-only visit with no هوازی
  amount and a guest visit with something unpaid, which stay open for the desk (*Cardio-only visit*,
  *Guest visit*).
- If the gym ever wants check-in refused at certain hours, that is a new rule, agreed with the
  developer and written here first.

### Gym services (هوازی and anything else sold during a visit)

Decided with the developer, 1405/06/31. Implemented in task 5.7.

- A **service charge** is money owed for something the member used during a visit. There are four kinds: `Cardio` (هوازی, the treadmill) and three things sold at the desk, `Miscellaneous` (فروشگاه, task 6.5.28), `Analysis` (آنالیز, task 6.5.29) and `Other` (متفرقه, task 6.5.36) (*Sale at the desk* below); sauna or massage would be new kinds of the same thing, not new tables.
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

### Sale at the desk (فروشگاه, آنالیز and متفرقه)

Asked by the developer, 1405/07/11 (2026-10-03). Roadmap 6.5.28 (فروشگاه, then called متفرقه),
6.5.29 (آنالیز, and the changes of 1405/07/12) and 6.5.36 (متفرقه, 1405/07/15). Something is sold at the desk that is neither on the
cafe's price list nor a service the system knows, and the desk still has to record the money. The
system does not know what it is, so the desk says.

- **Three kinds, one set of rules** (decided with the developer, 1405/07/11, task 6.5.29; متفرقه
  1405/07/15, task 6.5.36): «فروشگاه» (kind `Miscellaneous`, renamed from «متفرقه» everywhere on
  screen), «آنالیز» (kind `Analysis`) and «متفرقه» (kind `Other`). Everything below holds for all
  three unless it says otherwise; the kind says which source the sale is filed under.
- **فروشگاه: one or more items, each with three things typed** (decided with the developer,
  1405/07/12): the name of what was sold (required, at most 100 characters), how many (1 to 999,
  typed or stepped with ▲/▼, starting at one) and the price of one. «افزودن کالای دیگر» adds a
  line when two things were sold together; up to 50 lines are saved in one go, all or none. Each
  line becomes its own row, so each is paid or voided on its own. A line's total is the price of
  one times how many, stored with it.
- **آنالیز: only a price** (decided with the developer, 1405/07/12): one amount, typed in the same
  field as the هوازی amount. It has no name, quantity or unit price.
- **متفرقه: exactly the rules of آنالیز** (decided by the developer, 1405/07/15, task 6.5.36): only
  a price, any number per visit, never edited, its own tick in cancel check-in, paid before a guest
  leaves, recorded by both roles. Its money is someone else's, like فروشگاه and آنالیز (§12
  *Financial report*). The new «متفرقه» is not the old one: the rows written as «متفرقه» before
  6.5.29 are فروشگاه items (kind `Miscellaneous`) and stay so; the new kind is stored as `Other`.
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
  check-out, the payment histories and the gym's history a sale reads «فروشگاه: …», «آنالیز» or
  «متفرقه», next to plan, هوازی and cafe, never folded into the cafe or into each other. The
  history's «بابت» filter offers هوازی, فروشگاه, آنالیز and متفرقه apart, and its «هوازی، فروشگاه،
  آنالیز و متفرقه» section lists all four, each row saying which. Cafe gross profit (§8) is not
  touched by any of them.
- **In the locker box each has its own tile** (task 6.5.29): هوازی, بوفه, فروشگاه, آنالیز and
  متفرقه (since 6.5.36) sit side by side as coloured tiles, each with its icon and name, with no heading above them. Once
  something is recorded, its total and payment status show on the tile under the name.
- It is a service charge, so everything above about service charges holds — open visit, Staff or
  Owner, voiding gives the money back the way it came, auto-checkout changes nothing — with these
  differences. *Decided by Claude during task 6.5.28; pending review:*
  - **It is never edited.** A mistake is voided with a reason and the sale entered again, the rule a
    cafe order follows (§8). `ServiceCharges.SaleNotEditable` refuses a change of amount.
  - **In «تسویه یکجا» it is paid with the هوازی charges:** after the cafe, before the subscription,
    the oldest first (§5 *Settling several items at once*).
  - **Cancelling a check-in lists each sale with its own tick,** فروشگاه, آنالیز and متفرقه alike, unticked,
    exactly like the cafe orders (*Cancel check-in*). A sale named that is not a standing sale of
    this visit refuses the whole cancellation (`Attendance.SaleNotOnVisit`).
  - It does not count as the هوازی amount of a cardio-only visit (*Cardio-only visit*).
- The database enforces the shape: a فروشگاه item has its name, quantity and unit price, all three,
  and its amount is exactly what they make; هوازی, آنالیز and متفرقه have none of them
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
  due*): 7 days before the date, one fixed number for both kinds (decided with the developer). The
  SMS goes to the Owner's number, kept on the SMS settings page, as many days ahead as the Owner
  sets there, one for each cheque and instalment (§10).
- **The header alert** (decided with the developer, 1405/07/14): in the header, on every page, for
  the Owner only. Every pending cheque and instalment dated **within the next 5 days**, today
  included, and every pending one past its date (red, until the Owner marks it). It names the
  nearest one and how many days are left («امروز», «فردا», «۳ روز دیگر», «۲ روز گذشته»), with the
  count of the others; opening it lists them all. Nothing is shown when there are none. The
  dashboard's 7-day list stays as it is.
- Owner only, reading included (§1).

---

## 10. Notifications (SMS)

Decided with the developer on 1405/07/14 (2026-10-06). **Every SMS costs money**, so nothing is
sent that the Owner did not choose, and the same message is never paid for twice.

### The four kinds
- **Subscription running out (`SubscriptionExpiring`)**, to the member: an `Active` subscription
  (not frozen, not queued) whose `EndDate` is at most *N days* away, today included.
- **Few sessions left (`LowSessions`)**, to the member: an `Active` subscription with at most
  *N sessions* left, where *N* depends on the plan's size (decided with the developer, 1405/08/01,
  task 10.17): three rows, plans of 5 sessions (the smallest plan, §3) up to a size the Owner
  chooses (8 at first), up to a second size (15 at first), and every bigger plan, each with its own
  *N* from **1 to 4**. The plan's size is the sessions bought on it. The first row ends at 5 to 29
  sessions, the second above it and at most 30 (since 6.5.38; it was 139 and 140) (`Sms.LowSessionsSmallPlanMaxOutOfRange`,
  `Sms.LowSessionsMediumPlanMaxOutOfRange`, `Sms.LowSessionsOutOfRange`); the database holds the
  same. When the rows came in, the one number the Owner had chosen was carried into all three,
  capped at 4.
- For both: a single-session visit never gets one; a deactivated member never gets one (nor any
  member's SMS, the broadcast included, except the debt reminder, *The debt reminder*); **a member
  who has already bought the next subscription** (a queued one, §4 `Upcoming`, not cancelled) never
  gets one, since they have renewed and the message would only cost money. If that queued one is
  cancelled later, the member counts as not renewed again, and the next run may send it.
- **Birthday (`Birthday`)**, to the member: *N days* before the member's birthday by the **Jalali**
  month and day, the same day the desk celebrates (§6 *The desk panel*: someone born on 30 Esfand has
  their birthday on 29 Esfand in a year without it), telling them that **on their birthday the gym
  is free for them** (§7 *Birthday visit*). It works as a window, like the other kinds (decided with
  the developer, 1405/07/14, task 10.3): *N* is 1 to 7, and it goes when the birthday is between
  tomorrow and *N* days ahead, so a missed day or a member registered a few days before still gets
  it. **Never on the day itself** (decided with the developer, 1405/07/27, task 10.8; replaces "0 =
  the day itself"): the member hears of the free visit early enough to plan it.
  **Only an active member who holds a plan gets it** (decided with the developer, 1405/07/27, task
  10.8; replaces "every member with a birth date, deactivated ones included"): on the day the SMS
  is written, a membership plan (§4, not a single visit, not cancelled) that is active, frozen, or
  bought for later. A plan whose sessions are used up or whose days have ended does not count, nor
  does a deactivated member. At most once per member per Jalali year.
- **Cheque or instalment coming due (`PayableDue`)**, to the **Owner**: a pending cheque or
  instalment (§9 *Cheques and instalments*) dated between today and *N days* ahead. **One SMS for
  each cheque and each instalment**, never a daily summary (decided with the developer). A paid or
  cancelled one gets none.
- **Each event is sent once**, enforced by unique indexes in the database: one `SubscriptionExpiring`
  and one `LowSessions` per subscription, one `Birthday` per member and Jalali year, one `PayableDue`
  per cheque or instalment, one `DailyReport` per day (*The daily report* below), one
  `SubscriptionReceipt` per subscription (*The subscription receipt* below), one `Welcome` per
  member (*The welcome* below), one `DebtReminder` per member and debt (*The debt reminder*
  below), one `Absence` per member and absence (*The absence SMS* below), one `WelcomeBack` per member and
  absence (*The welcome back* below), one `Broadcast` per
  member and broadcast (*The broadcast* below). Running the job twice sends nothing twice. A cheque or instalment whose date is changed after its SMS went out
  gets no second one (§0).

### The daily report (گزارش روزانه)
Decided with the developer on 1405/07/27 (2026-10-19), task 10.7. The first of the kinds added after
the four above (roadmap 10.7 to 10.13).
- **One SMS a day to the Owner (`DailyReport`)**, about **that same day**, sent at the time the
  Owner chooses on the settings page, **08:00 to 23:45** on a quarter hour (Asia/Tehran; decided with
  the developer, 1405/08/01, task 10.17, replacing "22:45, fixed in the code"; it was 22:45 until
  the Owner changes it, `Sms.DailyReportSendTimeOutOfRange`). It covers the day up to the moment it
  goes: whatever is recorded after it and before midnight is in no report. The gym is open until
  midnight (`Gym:ClosingTime`), and the developer chose the evening of the day itself over the next
  morning. Sending it after the last check-out of the day was considered and dropped: the system
  cannot know which check-out is the last (the gym can be empty in the afternoon, a forgotten
  check-out keeps it from ever emptying, and the auto-checkout runs at midnight, the next day).
- **Its own hours: 08:00 until midnight**, instead of 08:00–22:00, for this kind only. A retry, a
  run the server missed and a resend by hand may be made until 23:59; the members' messages and the
  cheque reminder keep 08:00–22:00.
- **What it says**, the figures of the dashboard (§12 *Financial report*, *Dashboard*) for that one
  day, so the SMS and the dashboard never disagree:
  - **ورود**: the members' check-ins of the day, by the moment they began, cancelled ones left out,
    cardio-only visits counted, guests left out (§12 *Attendance*, §7).
  - **پلن** and **تک‌جلسه**: the membership plans and the single visits sold that day, by the day of
    the sale, cancelled ones left out, each counted on its own.
  - **دریافتی**: the dashboard's «دریافتی» for the day (§12 *Financial report*): every payment
    taken that day less every refund given, old debts included, **without فروشگاه, آنالیز and
    متفرقه**. Then cash, card and bank transfer, each on its own line; **the cash line is left out on
    a day with no cash**, card and transfer are always there. A method can come out below zero (more
    refunded than taken); it is written with a minus sign.
  - **In thousands of Toman** (the developer, 1405/07/27): every amount is **rounded to the nearest
    thousand Toman**, 500 rounded up, and the total says «هزار تومان». Each figure is rounded on its
    own, so the three lines may add up to one more or one less than the total; the exact figures are
    on the dashboard. Written in full, the text would run to three parts instead of two.
- **A day with nothing is not sent**: no check-in, nothing sold and no money in or out (every
  figure zero). No row is written for it, and nothing is paid.
- **The Owner's number for it is its own**, on its row of the settings page, apart from the cheque
  reminder's (the developer, 1405/07/27): the report can go to someone else.
- **One per day**, enforced by a unique index on the day it is about; the run made twice, or a
  missed run made late, sends nothing twice.

### The subscription receipt (رسید خرید اشتراک)
Decided with the developer on 1405/07/27 (2026-10-19), task 10.9.
- **One SMS to the member (`SubscriptionReceipt`) when a plan becomes fully paid** (§4 *Payment
  status*): sent for the payment that brings its net paid up to the price, so a member who got it
  has surely paid. A plan paid in parts gets it with the last part; one paid at once, with that
  payment. Both ways of paying count: the subscription's own payment form and «تسویه یکجا» (§5).
- **Plans only**: a single visit never gets one (it is sold every day and paid on the spot; the
  SMS would only cost money), nor does a free plan (`Price = 0`): no money was paid.
- **Only while it is on**: a payment gets a receipt only when all SMS and this kind are on at the
  moment the payment is written. Turning it on later sends nothing for payments made before.
- **When**: at once, in the background, so the desk never waits for Kavenegar. A payment made
  outside 08:00–22:00 has its receipt sent **at 10:00 the next morning** (Asia/Tehran; the same
  morning for one made before 08:00), fixed in the code. A receipt whose time comes while the server
  is down is sent once it is back, or at the next 10:00 if that is outside 08:00–22:00.
- **Checked again when it is sent**: nothing goes if all SMS or this kind has been turned off since,
  the subscription has been cancelled, or the member deactivated (as every member's SMS, *The four
  kinds*). *The deactivated member: proposed by Claude and approved with the plan, 1405/07/27.*
- **Cancelled after its receipt went out: nothing more** is sent (decided with the developer).
- **What it says**: the plan's sessions, its dates without the year, and the price paid. A plan
  queued behind another (§4: its dates are only where it stands today) gives its days and «پس از
  اشتراک فعلی» instead of dates, the same test as the member's history («بعد از پلن قبلی»).
- **One per subscription**, enforced by the unique index on the subscription and the kind.
- **Written before it is sent, retries and resend** as every kind (*The daily runs*): saved as
  `Pending` before the request; a failure that may pass is tried again until 22:00; the Owner can
  resend a failed one. A receipt the server stopped between the two saves is found `Pending` when
  its job is made again after the restart, and becomes `Unknown`, never sent again by itself.
  Receipts are sent one at a time.

### The welcome (خوش‌آمد)
Decided with the developer on 1405/07/27 (2026-10-19), task 10.10.
- **One SMS to the member (`Welcome`) when their first membership plan is sold**, paid or not: it is
  about joining, not about money. The **first plan** is the one sold while the member has no other
  membership plan that is not cancelled, the same test as «عضو جدید» on the dashboard (§12 *New
  members*). A single visit never gets one, and single visits bought before do not count as plans.
- **Every way of selling a plan counts**: assign, renew (a member whose only plans were cancelled can
  renew into a first plan), and a plan sold with a check-in (§7 *Confirming at the front desk*).
- **Its price does not matter**: a plan is priced by its sessions (§3), and one that came out at 0
  (the session price set to 0) is still a member's first plan.
- **Members who hold plans already never get it**: their next plan is not their first. A member
  moved over from the paper notebook whose first plan in the system is sold now gets it like anyone
  new (decided with the developer); the text says nothing that would be wrong for them.
- **Only while it is on**: a sale gets a welcome only when all SMS and this kind are on at the moment
  the sale is written. Turning it on later sends nothing for sales made before.
- **When**: as the receipt (*The subscription receipt*): at once, in the background, so the desk
  never waits for Kavenegar; a sale made outside 08:00–22:00 has it sent **at 10:00 the next
  morning**, fixed in the code. A first plan paid at once gets two SMS, the welcome and then its
  receipt: they are separate kinds with separate switches (decided with the developer).
- **Checked again when it is sent**: nothing goes if all SMS or this kind has been turned off since,
  the plan has been cancelled, or the member deactivated. A first plan cancelled **before** its
  welcome went out sends nothing, and the next plan sold to them is then their first (a plan sold
  while that one still stood is not, being sold after it). One cancelled
  **after** sends nothing more, and no later plan gets a welcome.
- **What it says**: the member's name and the gym's welcome (`docs/sms-texts.md`); two SMS parts since the developer's warmer wording of 1405/08/01 (it was one).
- **Once per member**, enforced by a unique index on the member for this kind, and once per plan by
  the index on the subscription and the kind.
- **Written before it is sent, retries and resend** as the receipt: saved as `Pending` before the
  request; a failure that may pass is tried again until 22:00; the Owner can resend a failed one; a
  welcome the server stopped between the two saves becomes `Unknown`, never sent again by itself.
  Welcomes are sent one at a time.

### The debt reminder (یادآوری بدهی)
Decided with the developer on 1405/07/27 (2026-10-19), task 10.11.
- **One SMS to the member (`DebtReminder`) when their debt has been owed for a while**: a daily
  run at the time chosen on the settings page, like the four kinds.
- **The debt is §5's *Member debt***: every sale not cancelled or voided that is not fully paid
  (plans, single visits, هوازی, فروشگاه, آنالیز, متفرقه, cafe orders), counted exactly as
  «مطالبات» and «بدهی‌های قدیمی» count it (§12), so the SMS and the reports never disagree. A
  guest or a walk-in has no account (§7 *Guest visit*) and never gets one.
- **The days count from the oldest unpaid sale**: the gym's day it was recorded on, as the
  receivables' ages (§12). The reminder goes once that sale is **at least *N* days old**: sold on
  or before today − *N* (*N* = 1: yesterday's debt is reminded today). *The boundary proposed by
  Claude and approved with the plan, 1405/07/27.*
- **The minimum amount**: only a member whose whole debt is at least that amount gets it. It is
  1,000 Toman or more (decided with the developer), typed like every amount (§3: at most 2
  decimal places, refused rather than rounded).
- **The amount in the text is the member's whole open account**, newer sales included: the figure
  the desk and the member list show (§5), so the member comes with one number and clears it all.
- **Once per debt.** After a reminder, the member gets **no other until everything owed when it
  was written has been paid** (or cancelled or voided); a debt that builds after that has its own
  reminder once its oldest sale is *N* days old. In practice the next reminder is due only when
  the oldest unpaid sale was recorded after the last reminder was written. A partial payment
  brings no second reminder. *Refined by Claude and approved with the plan, 1405/07/27: "until the
  debt is back to zero" would never remind again a member who always carries some newer debt.*
  A reminder that failed counts as written, as every kind: the Owner can resend it (*Sending*).
- **Deactivated members get it too** (decided with the developer, 1405/07/27): money owed is owed.
  The one member's SMS that does not leave them out (*The four kinds*).
- **Checked when the run writes it**: the debt and its amount are read at that moment; there is
  no later check, since the message is sent at once.
- **One per member and debt**, enforced by a unique index on the member and the day of the oldest
  unpaid sale the reminder was about; a run made twice sends nothing twice.
- **What it says**: the member's name and the whole amount, politely (`docs/sms-texts.md`); two
  SMS parts.

### The absence SMS (پیامک غیبت)
Decided with the developer on 1405/07/27 (2026-10-19), task 10.12.
- **One friendly SMS to the member (`Absence`) who has stopped coming**, with no offer in it: a
  daily run at the time chosen on the settings page, like the four kinds.
- **Only a member with a plan usable today**: an active member whose membership plan is §4
  `Active` (not a single visit, not cancelled, not frozen, started and not ended, sessions left).
  A deactivated member, a frozen plan, a plan bought for later that has not started, and a plan
  used up or ended get none: the same members as «دیگر نمی‌آید» on «نیاز به اقدام» (§12).
- **The days count as on «نیاز به اقدام»**: from the gym's day of the member's last visit, or from
  the plan's start when the member has not come since it started, so a member who bought a plan
  and never came is counted from its start. A cancelled check-in is not a visit; a cardio-only one
  is. It goes once the member has been away **at least *N* days**: the last visit (or the start) on
  or before today − *N*. *N* is **7 to 60** (proposed by Claude and approved with the plan,
  1405/07/27: fewer than 7 is too soon for someone who comes twice a week). The board's own
  threshold stays 10 days (§12): the SMS has its own number, because an SMS costs money.
- **Once per absence**: after it, nothing more until the member comes again; the next absence has
  its own. An absence is named by the day it counts from, so a member who did not come on one plan
  and buys another counts from the new plan's start, and that is a new absence. A check-in
  cancelled after the SMS brings the old absence back, which has had its SMS already. A message
  that failed counts as written, as every kind: the Owner can resend it (*Sending*).
- **Checked when the run writes it**; the message is sent at once, so there is no later check.
- **One per member and absence**, enforced by a unique index on the member and the day the absence
  counts from; a run made twice sends nothing twice.
- **What it says** (`docs/sms-texts.md`; rewritten by the developer on 1405/08/01): «{name} عزیز، جات
  توی باشگاه پاسارگاد خالیه! {days} روزه که باشگاه نیومدی و منتظر حضور گرمت هستیم!», two SMS parts.
  *{days}* is how long the member has really been away, counted as above, not the settings' *N*.
- **Whether it worked** is on the dashboard: how many came back within 5 days of it (§12 *The
  absence SMS's effect*, task 10.19).
- **The member who comes back** gets *The welcome back* below.

### The welcome back (خوش برگشتی)
Decided with the developer on 1405/08/04 (2026-10-26), task 10.20.
- **One warm SMS to the member (`WelcomeBack`) on their first visit after the absence SMS**: the
  member came again, and the gym is glad of it. No offer in it.
- **Which absence SMS**: the member's latest absence SMS (*The absence SMS*), if it **reached the
  phone**: sent (`Sent`) and not given back by the provider (cancelled, or blocked by the receiver,
  §10 *Sending*), the same messages as §12 *The absence SMS's effect*. One that failed, whose fate is
  not known, or that is still waiting never earns a welcome back. One whose delivery is not reported
  yet does.
- **The first visit after it, however late** (the developer, 1405/08/04): the first check-in after
  the moment the absence SMS was sent (a resend's moment, for one the Owner sent again), with no
  window of days. A cancelled check-in is not a visit, a cardio-only one is, as for the absence SMS.
  A member who has come once since the message gets nothing on the next visit, even if that first
  one was before this kind existed or while it was off (the developer, 1405/08/04: a member who came
  back before the release never gets it after it).
- **Half an hour after the check-in** (the developer, 1405/08/04), in the background, so the desk
  never waits and the message reaches the member while they are training. **Its own hours: 08:00
  until midnight**, like the daily report (the developer, 1405/08/04: the gym is open until midnight,
  and a member who came at 22:30 is still inside). A check-in whose half hour falls before 08:00 has
  it at 08:00 the same day. One made after 23:30, whose half hour falls after midnight, never gets
  it, nor one whose message could not go by midnight (the server was down): the next day it would
  no longer be about coming back today. A retry, an automatic resend
  and the Owner's resend may be made until 23:59, as for the daily report.
- **Only while it is on**: a check-in asks for it only when all SMS and this kind are on at that
  moment. Turning it on later sends nothing for visits made before.
- **Checked again when it is sent**: nothing goes if all SMS or this kind has been turned off since,
  the check-in was cancelled, another visit is now the first after the absence SMS (a check-in
  cancelled and then made again: the new one asks for its own), or the member was deactivated.
- **Once per absence**, enforced by a unique index on the member and the day the absence counted
  from (the same day the absence SMS carries); a job made twice sends nothing twice.
- **What it says: one of six texts, in turn** (the developer, 1405/08/04: the same text every time
  would feel automatic; `docs/sms-texts.md`). A member's first welcome back is the first text, the
  second is the second, and so on; after the sixth it starts again. The turn is the number of
  welcome backs the member already has, whatever became of them, so a member never gets the same
  text twice in a row. Each text names the member and takes two SMS parts.
- **Written before it is sent, retries and resend** as the welcome: saved as `Pending` before the
  request; one the server stopped between the two saves becomes `Unknown`, never sent again by
  itself. Welcome backs are sent one at a time.

### The broadcast (پیامک گروهی)
Decided with the developer on 1405/07/27 (2026-10-19), task 10.13. The one kind whose wording is
not fixed in the code: the Owner writes it each time (the gym closed for a few hours, a piece of
news, an offer).
- **Owner only**, on its own page «پیامک گروهی». Every broadcast is in the audit log (§11).
- **Who gets it: the Owner chooses each time**, one of two groups:
  - **«همه اعضای فعال»**: every active member.
  - **«اعضای دارای پلن»**: every active member with a **plan usable today**, the members the absence
    SMS looks at (*The absence SMS*): a membership plan that is §4 `Active` (not a single visit, not
    cancelled, not frozen, started and not ended, sessions left).
  - A deactivated member never gets it, nor a guest or a walk-in (they are not members, §7).
- **The text**: the Owner writes the middle; the system puts **«{name} عزیز، »** before it (the
  member's first name, *Sending*) and the opt-out line after it, as every kind. The Owner's text is
  trimmed at both ends, keeps its line breaks, and cannot be empty (`Sms.BroadcastTextRequired`).
- **At most 3 SMS parts** for every recipient (decided with the developer): measured with the
  **longest first name among the chosen recipients**, so no one's message can come out longer. A
  text that would make anyone's message longer is refused (`Sms.BroadcastTooLong`). The page counts
  the characters and parts as the Owner types.
- **Before sending, a confirmation** shows the number of recipients, the total number of SMS parts
  (exact: each recipient's own message counted), and the cost **estimated** from the latest message
  that was sent and charged (its cost divided by its parts, times the total parts), with the
  account's remaining credit. With no such message yet, only the parts are shown. Kavenegar tells no
  price before sending, so the estimate is marked «حدود».
- **A group with nobody in it is refused** (`Sms.BroadcastNoRecipients`): nothing would be sent.
- **Sent at once**, in the background, only within **08:00–22:00**: outside the hours it is refused
  (`Notifications.OutsideSendingHours`). There is no chosen time (decided with the developer).
- **A switch of its own** on the settings page, off at the start, like every kind; the all-SMS
  switch must be on too. With either off, a broadcast is refused (`Sms.BroadcastOff`).
- **One broadcast at a time**: while one is sending, another is refused (`Sms.BroadcastInProgress`),
  enforced by the database too. *Proposed by Claude and approved with the plan, 1405/07/27.*
- **The recipients are read when the sending starts**, and each message is written and sent as in
  a run (*The daily runs*): saved `Pending` before its request; a failure that may pass is tried
  again until 22:00; a message that failed can be resent by hand (*Sending*).
- **Once per member and broadcast**, enforced by a unique index on the broadcast and the member.
- **The switches and the hours are read again before every message**, so 22:00, or a switch the
  Owner turns off, stops a broadcast in the middle; turning the broadcast's switch off is the
  Owner's brake. The text is measured again when the sending starts: if a member with a longer first
  name has joined the group since, so that their message would take more than 3 parts, nothing is
  written and it stops. *Found in review and approved by the developer, 1405/07/27.*
- **A broadcast that stops before everyone is written** (the credit used up, the hours ended, all
  SMS or the broadcast turned off, the text grown too long for a new member's name, an unexpected
  error) is **«متوقف شد»** with its reason. The
  Owner can **continue it** («ادامه ارسال») under the same rules as a new one (the switches, the
  hours, the 3 parts): it sends the same text to the members of the same group as it stands then
  who have no message of this broadcast yet. *Proposed by Claude and approved with the plan,
  1405/07/27.* A server restart in the middle continues it by itself (at the start, a broadcast
  still sending is handed to its job again); the message that was in flight becomes `Unknown`
  (*The daily runs*). A job that fails before it could stop the broadcast stops it with an error,
  so a broadcast is never left «در حال ارسال» blocking the next one. *Approved by the developer,
  1405/07/27.*
- **How it shows**: on «پیامک‌ها» each recipient has a row of their own, of the kind «پیامک
  گروهی», like every message. The broadcast page lists the broadcasts, the latest first, each with
  its date, text, group, state, how many were sent, failed or unknown, and its cost.

### The daily runs
Decided with the developer on 1405/07/14 (2026-10-06), task 10.3.
- **One run per kind, at its own send time**, every day, in Asia/Tehran. Only a kind that is on
  (with the all-SMS switch on) has a run. Saving the settings page moves the runs: a change of time
  applies from the next run, and a kind turned off has none.
- A run reads the settings when it starts, and sends nothing when all SMS or its kind is off, or
  when it is outside 08:00–22:00. A run the server missed is made when the server is back the same
  day, before 22:00 (§0). The daily report's hours run until midnight instead (*The daily report*).
- **Each message is written before it is sent**, one at a time: the row is saved as `Pending`,
  then the request is made, then its outcome is saved. If the run stops between the two (the
  server restarted), nobody can tell whether the request left, so the next run of that kind marks
  that row `Unknown`, and it is never sent again by itself (the Owner can resend it, task 10.5).
- **A failure that may pass** is tried again after the others: at most `Sms:MaxAttempts` tries in
  all, waiting `Sms:RetryDelays` between them (1 minute, then 5; §0). After the last try, or when the
  next try would fall after 22:00 (after midnight for the daily report), it is `Failed`.
- **Credit used up** stops the run at once: that message is `Failed`, and those not yet written are
  not written; whoever is still covered the next day is reached by the next day's run. Messages
  waiting for a retry are `Failed` too, since they would meet the same empty account.
- The runs of different kinds never overlap: one waits until the other ends.

### SMS settings (تنظیمات پیامک)
- One page, **Owner only**. It is **separate from the desk**: the desk keeps its own fixed
  thresholds (§7 *The "currently inside" board*), and the SMS numbers can differ from them, because
  the desk costs nothing and an SMS does.
- **It starts empty and everything starts off.** There are no default values: the Owner fills in a
  kind's fields and then turns it on. A kind cannot be turned on while one of its fields is empty
  (`Sms.SettingsIncomplete`), and the database enforces it too (a check constraint: on means filled).
  Nothing is sent before the Owner decides. A kind that is off may have any of its fields empty,
  and the Owner can empty them again; a filled field must still be within its range (decided with
  the developer, 1405/07/14, task 10.2).
- The settings:

  | Kind | Fields | Allowed |
  |---|---|---|
  | All SMS | on/off | |
  | Subscription running out | on/off, days before `EndDate`, send time | 1–30 days |
  | Few sessions left | on/off, two plan sizes that divide three rows, sessions left for each row, send time | 1–4 sessions; first row ends at 5–29, second above it, at most 30 |
  | Birthday | on/off, days before the birthday, send time | 1–7 days |
  | Cheques and instalments | on/off, days before the date, send time, the Owner's mobile number | 0–30 days |
  | Daily report | on/off, the mobile number it goes to, send time | 08:00–23:45 |
  | Subscription receipt | on/off | sent at once; after 22:00 at 10:00, fixed |
  | Welcome | on/off | sent at once; after 22:00 at 10:00, fixed |
  | Debt reminder | on/off, days since the oldest unpaid sale, the minimum amount, send time | 1–90 days; 1,000 Toman or more |
  | Absence | on/off, days away, send time | 7–60 days |
  | Welcome back | on/off | half an hour after the check-in, 08:00 until midnight |
  | Broadcast | on/off | sent at once from its own page, 08:00–22:00 |

  Every send time is between **08:00 and 22:00** (Asia/Tehran), both included, on a quarter hour
  (:00, :15, :30, :45), chosen as an hour and a minute (decided with the developer, 1405/07/14,
  task 10.2; `Sms.SendTimeOutOfRange`). The Owner's number follows the
  members' phone rules (§2: Iranian mobile only, any digits). So does the daily report's number. There is no template name any more
  (1405/07/16, *Sending*): the wording is fixed in the code.
- A change applies **from the next run**, never to the past: whoever already got a message does not
  get it again, and raising a number brings in the people it now covers.
- Every change is in the audit log (§11).
- **Each kind shows the message it sends** (asked by the developer, 1405/07/28, task 10.15): under
  its settings, the text exactly as the code writes it, with a sample member («سید علی رضایی», named
  «علی») and sample figures, and how many SMS parts it takes. It is built by the same code as the real
  messages, so the page cannot show a wording the members do not get. The kinds are grouped by who
  receives them: the members, the Owner, the broadcast, and the trainers («پیامک به مربیان», with
  the one kind of §15, 15.4c).
- **The broadcast is one card on this page** (*The broadcast*): its switch, and writing and sending
  one, with the message drawn as it is typed. It has no page, tab or menu item of its own, and no
  list of the broadcasts sent: their messages are on «پیامک‌ها». Only the latest, while it is
  sending or stopped, shows on the card, with «ادامه ارسال» (asked by the developer, 1405/07/28).
- Not on the page: the Kavenegar API key (a secret: an environment variable on the server, user
  secrets on the developer's machine), the dedicated line's number (`Sms:Kavenegar:Sender`, an
  environment variable on the server: it belongs to the account, like the key) and how many times a
  failed send is retried.

### Sending
- **Provider: Kavenegar**, through its free-text method (`sms/send`), **from the gym's dedicated line**
  (خط اختصاصی). The template method (`verify/lookup`) was the first plan, but Kavenegar refused every
  template because the gym's site was not up yet when it checked them, so the gym bought a
  dedicated line (1405/07/18; Kavenegar's registration of it, with identity verification and a
  business licence, may take about a week); a line needs no template, and Kavenegar's support said
  to send with `sms/send` (decided with the developer, 1405/07/16). The system writes the whole
  text and sends it.
  **Members who blocked advertising SMS still get it** (settled by the developer, 1405/07/18): the
  gym's messages go to its own members through `sms/send` on the dedicated line, with no limit and
  no block from the receiver's advertising setting. Should one still come back blocked, its
  delivery shows it as before (status 14, the cost given back).
  **Offers and discounts may be sent too** (settled by the developer with Kavenegar on 1405/07/26):
  the members signed up in the gym's own customer club and were registered at the desk, so any
  message to them may go from the line.
- **The wording is fixed in the code** (decided with the developer, 1405/07/16), as agreed on
  1405/07/14 and kept in `docs/sms-texts.md`. Changing it is a release, not a setting. The one
  exception is the broadcast, whose middle the Owner writes each time (*The broadcast*). The values in
  it are written the way the app shows them: Persian digits, a Jalali date as `۱۴۰۵/۰۷/۲۰`, an amount
  with thousands separators.
- **Every message ends with Kavenegar's opt-out line, `لغو11`**, on a line of its own, English
  digits, no space (decided with the developer, 1405/07/26, after the first messages from the
  dedicated line arrived with it). Kavenegar adds it to every message from the line anyway; written
  by the system, the history keeps the text exactly as it reached the phone, and Kavenegar does not
  add a second one. All four kinds carry it, the Owner's reminder too. Messages written before keep
  their text.
- **A member is named by the first name only** (decided with the developer, 1405/07/27, task
  10.13; replaces "the first 6 words of the name"): the system keeps one full name (§2), so the
  first name is its **first word**, and when that word is «سید», the word after it («سید محمد
  احمدی» → «محمد»). A name of one word is used whole; a first name is cut at 100 characters. Every
  member's message uses it; messages written before keep their text.
- **A long payee is cut short**, as it was in the templates' blanks (decided with the developer,
  1405/07/14): its first 9 words, then the first 100 characters.
- **The wording** (decided with the developer, 1405/07/14, in `docs/sms-texts.md`): the gym is
  named «باشگاه پاسارگاد»; the subscription message gives the end date, not the days left; the
  birthday has one text, always sent 1 to 7 days early, giving the birthday's month and day
  (without the year) and the free visit and ending «پیشاپیش تولدت مبارک رفیق!!» (decided with the developer, 1405/07/27, task 10.8, in spoken Persian since 1405/08/01; replaces two
  texts, one of them for the day itself); the Owner's message gives the kind, the amount, the date
  and the payee.
- **Length:** a Persian SMS holds 70 characters in one part and 67 in each part of a longer one, and
  each part is paid for. The four texts run to about two parts each; the developer decided that
  is fine.
- **Status:** `Pending`, `Sent`, `Failed`, `Unknown`.
  - A failure that may pass (Kavenegar busy, `409`; too many requests from the server's IP for a
    while, `451`, decided with the developer on 1405/07/17; the server or the network down before
    the request left) is retried with backoff, up to `Sms:MaxAttempts`, then `Failed`. Kavenegar
    refused the request, so a second try cannot be paid twice.
  - A failure that will not pass is `Failed` at once, with its code. The codes named here, from
    Kavenegar's documentation (read on 1405/07/17), each with its meaning in Persian on the history:
    the account disabled `401`, the API key not valid `403`, the server's IP not set in Kavenegar's
    security settings `407`, an invalid number `411`, the line not valid for the account `412`, the
    text empty or too long `413`, the server's IP not the one the key is restricted to `416`, links
    not allowed in the text `420`, a bad character `422`, the line needing an access level the
    account does not have `427`, the server's IP blocked `429`, and test messages allowed only to the
    account owner's number `501`.
  - **Credit used up (`418`)**: that message is `Failed`, the rest of the run is not sent, and the
    Owner is warned on the SMS pages.
  - **No answer after the request left** (the connection broke while waiting): the message may have
    gone. It becomes `Unknown` and is **never retried automatically**, because the request carries no
    duplicate guard for Kavenegar to check and a second try could be paid twice.
  - Nothing is sent outside 08:00–22:00 (the daily report and the welcome back: until midnight). A retry that would fall after 22:00 is not made: the message
    is `Failed`.
- **Delivery:** after sending, the system asks Kavenegar (which keeps it for 48 hours) whether the
  message reached the phone: delivered (`10`), not delivered (`11` the phone off or out of reach,
  `6` the carrier's error), blocked by the receiver (`14`), or cancelled by Kavenegar (`13`, a
  sending error on its side). It asks **once a day, at 23:30** (Asia/Tehran), about every message
  sent in the last 48 hours that is not yet delivered, blocked or cancelled, so each one is asked about at least once before Kavenegar forgets it, and a
  "not delivered" (the phone was off) can still become "delivered" the next night. The same question
  is also asked **every 15 minutes** about the messages sent **today**, to find what Kavenegar
  cancelled while it can still be sent again that day (*Automatic resend*, task 10.16). A message still on
  its way gets nothing yet. If Kavenegar does not answer, it is only logged: the messages keep no
  delivery, and nothing else depends on it (decided with the developer, 1405/07/15, task 10.4: these
  messages are information, not something the gym must be sure arrived).
- **Cost:** Kavenegar returns each message's cost in **Rial**. It is kept with the message, and shown
  in Toman like every amount here (÷ 10). **Kavenegar gives back the cost of a blocked or cancelled
  message** (its documentation), so once the delivery check hears either, that message's cost becomes
  0 and its delivery no longer changes, and the history says the cost was given back: the month's
  total is what the gym really paid (decided with the developer, 1405/07/17).
  The SMS history shows each month's total. The account's remaining credit is shown on the settings
  page and on the history.
- **The SMS history (پیامک‌ها)**, Owner only (task 10.5): every message, the latest first, with its
  kind, recipient, status, delivery, cost and the provider's code for a failure; filtered by date,
  kind and status. A message's date is the day it was written (Asia/Tehran). The page opens on the
  current Jalali month, and the total is the cost of the messages the filter shows.
- **The credit warning** (decided with the developer, 1405/07/16, task 10.5): shown on both SMS pages
  while the latest message that failed for credit used up (`418`) is newer than the latest one sent.
  It goes away by itself once a message is sent again, by a run or a resend.
- **The credit in the header** (decided with the developer, 1405/07/27, task 10.14): the Owner sees
  the account's remaining credit on every page, in the header beside the cheques and instalments
  alert, and it opens «تنظیمات پیامک». Its colour says how much is left:
  - below **1,000 Toman**: the whole box flashes solid red, fast (asked by the developer the same
    day: almost nothing can be sent any more);
  - below **100,000 Toman**: red, blinking like a cheque past its date;
  - from 100,000 to 200,000 Toman, both included: orange;
  - above 200,000 Toman: green.

  It is asked from Kavenegar when the panel opens and then **every 10 minutes**, and again when an
  SMS page opens or after a resend. Kavenegar's answer to a send gives only that message's cost, and the
  account is charged outside the panel, so the panel never works the figure out by itself. When
  Kavenegar does not answer, the **last figure it gave** is kept, in grey; with none yet, the header
  says the credit is unknown. In test mode it is not shown. Nothing is sent by SMS about the credit:
  the header is the warning (an SMS to two numbers was considered and dropped by the developer).

  On a phone (below 1024px, Phase 13.1) the box shows only its coloured icon, the same colours and
  flash, so the cheques alert beside it keeps its words; the amount is read out to a screen reader
  and is on «تنظیمات پیامک», which the box opens (1405/08/01, when the new look met the SMS
  release; the developer may ask for the amount back).
- **Resend:** the Owner can resend a `Failed` or `Unknown` message by hand, and a sent one that
  **Kavenegar cancelled** (`13`: it never reached the phone and its cost was given back; decided
  with the developer, 1405/08/01, task 10.16). A delivered, not delivered or blocked one cannot be
  resent: the first two were paid for, and a blocked one would be blocked again. For `Unknown` the page
  says it may already have arrived. Decided with the developer on 1405/07/16 (task 10.5):
  - It sends **exactly the same message**: the same number and text that were written, even if the
    member's number or name has changed since.
  - **One request, no retries:** a failure that may pass makes it `Failed` at once with its code, and
    the Owner can resend again. Credit used up is `Failed` too, and `Unknown` as in a run.
  - It is allowed **while all SMS or that kind is off**: the switches stop the daily runs, and a resend
    is the Owner's own choice. It is not allowed outside 08:00–22:00 (`Notifications.OutsideSendingHours`), a daily report or a welcome back outside 08:00 to midnight.
  - Only a `Failed`, `Unknown` or cancelled message can be resent (`Notifications.NotResendable`); two resends of
    one message at once send it once. Like a run, the message is saved as `Pending` before the request,
    so a resend the server stopped in the middle becomes `Unknown` at that kind's next run.
  - A cancelled message is sent again **as the same row**: what its last request left (Kavenegar's
    id, the cost it gave back, the moment it was sent, «کاوه‌نگار لغو کرد») is replaced by the new
    request's. Its cost was 0, so the month's total does not change.
- **Automatic resend of what Kavenegar cancelled** (decided with the developer, 1405/08/01, task
  10.16). On 1405/07/29 and 07/30 Kavenegar accepted a whole evening's messages (about 18:00 to
  20:10) and then cancelled them, while the ones before and after reached the phone. So the system
  sends a cancelled message again by itself:
  - Every **15 minutes** it asks Kavenegar about today's messages (*Delivery*), and sends again each
    one Kavenegar cancelled, **at least 30 minutes after its last request**, so the tries are not all
    spent inside the same bad stretch.
  - **At most 5 times** per message (`auto_resends`, held by a check constraint). After the fifth,
    only the Owner's resend is left. The Owner's resends are not counted.
  - **The same day only**, within the kind's sending hours (08:00–22:00; the daily report and the welcome back until
    midnight). What is still cancelled when they end stays so, with the Owner's resend.
  - **Every kind**, the Owner's own messages included (the daily report, cheques and instalments).
  - **The switches hold**: with all SMS or the message's kind off, nothing is resent automatically, as
    the settings page says nothing is sent. The Owner's resend is still allowed then.
  - Each one is sent like the Owner's resend: one request, saved `Pending` before it. A failure that
    may pass, or credit used up, makes it `Failed` with its code, for the Owner to resend; credit
    used up also leaves the other cancelled ones for later. Only a cancelled message is resent by
    itself: a failed or unknown one may have cost money or arrived, so it stays the Owner's choice.
  - The history shows how many times a message was resent automatically («N بار خودکار دوباره
    فرستاده شد»).
- **Development and tests:** tests always use `FakeSmsSender`, which only logs, and never reach
  Kavenegar. On the developer's machine Kavenegar can be turned on with the developer's own account,
  and `Sms:AllowedReceptors` then limits real sending to the listed numbers (anything else is only
  logged). The server has no such list. The limit holds in **every environment but Production**, and
  an empty or missing list means nothing is really sent, so forgetting the list can never reach a
  member (decided with the developer, 1405/07/15, task 10.4: the local database holds made-up
  numbers that may belong to real people). Asking about delivery or the credit sends nothing, so the
  list does not limit those.
- **Kavenegar answers it does not describe:** an answer that is not Kavenegar's own (a proxy's error
  page, a broken reply) to a send that may have left is `Unknown`, never retried by itself, because a
  second try could be paid twice. A Kavenegar code not named above is `Failed`, with its code.
- **Accounts:** development and testing use the developer's Kavenegar account. At release the API key
  of the account that holds the dedicated line, and the line's number, go into the server's `.env`
  (`SMS_PROVIDER`, `SMS_KAVENEGAR_API_KEY`, `SMS_KAVENEGAR_SENDER`; left out, the server stays on the
  fake provider), and the Owner fills in the settings page. Nothing waits for an approval, and no code changes.

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

### The audit screen (گزارش تغییرات)
Decided with the developer on 1405/07/18 (2026-10-10), task 11.1.
- **Owner only** (§1, permissions). It is a page in the Owner's menu, «گزارش تغییرات», separate
  from «تاریخچه» (§12), which is the desk's view of payments, visits and sales.
- **The latest first**, paged. It opens on **today**; a date range reaches further back. Dates are
  the day the change happened in the gym's time zone, both ends included. Choosing a member or one
  record drops the "today" default, so their whole history shows.
- **Filters:** dates; who did it (one user, or «سیستم» for rows with no user: logins, seeding,
  background jobs); the kind of record; insert, update or delete; one member; one record.
- **Sign-in rows hidden by default.** Refresh tokens and trusted devices (`RefreshToken`,
  `TrustedDevice`) are about half the log and say only that someone logged in or stayed logged in.
  They are left out unless the Owner turns on «نمایش ورودها و دستگاه‌ها», or chooses one of those
  kinds of record as the filter.
- **The member's name beside the row** wherever the record belongs to a member: the member itself,
  a subscription, a visit, a service charge, a cafe order and its lines, an SMS, and a payment
  through the subscription, charge or order it pays for. The member filter finds the same rows.
- **The changes in Persian:** each field under a Persian label, money with `formatMoney`, dates in
  Jalali, yes/no and the kinds (payment method, charge kind, …) in Persian. A field with no label
  yet shows its English name, so a new field never breaks the page. Not shown, because they say
  nothing to the Owner: the record's own id, the `Normalized…` copies, and internal links (token
  family, settlement). Another record's id shows as a short code; a user's id shows as the name.
- **Read-only.** The screen changes nothing; the log stays append-only.

---

## 12. Reports

- Date ranges are inclusive and interpreted in the gym's time zone.
- Revenue by source (subscriptions, gym services, cafe) and by payment method.
- Expenses by category (voided excluded). Net profit = revenue without فروشگاه, آنالیز and متفرقه − expenses (*Financial report* below).
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
  - **By source**, seven of them: membership plans, single-session visits (تک‌جلسه‌ای, apart from
    membership as above), هوازی, فروشگاه, آنالیز, متفرقه (since 6.5.36), cafe. Each with received, refunded and net,
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
  - **فروشگاه, آنالیز and متفرقه are left out of it** (decided with the developer, 1405/07/14;
    متفرقه 1405/07/15): «فروش» is plans, single visits, هوازی and the cafe, and the dashboard says
    so in its name, «فروش (به غیر از آنالیز، فروشگاه و متفرقه)». They are still sales of their own source: each is counted in its
    «sold» above, and the history's sales totals keep them.
  - **Paid and still owed** (decided with the developer, 1405/07/14): what has been paid so far
    on those same sales, refunds taken off, whenever it was paid («پرداخت‌شده»), and the rest of
    «فروش», still owed («مانده», first written «نسیه», renamed by the developer, 1405/07/14).
    The dashboard writes both inside the «فروش» card, beside the
    figure. A sale on credit is in «فروش» but not in the drawer, and the Owner checking the drawer
    at night must not be sent looking for it.
- **Received («دریافتی»)** (decided with the developer, 1405/07/14): the revenue by method, card,
  bank transfer and cash, **without فروشگاه, آنالیز and متفرقه**, refunds taken off, by `PaidAt` like all
  revenue: old debts paid in the range count, a sale on credit does not. It is what the gym's
  drawer, card reader and account should show for the range, so the nightly check compares with
  it. **فروشگاه, آنالیز and متفرقه are someone else's business**, run at the gym's desk: the money
  of فروشگاه and آنالیز is its own card, «دریافتی آنالیز و فروشگاه», by method too, and متفرقه's
  another, «دریافتی متفرقه», by method as well (decided by the developer, 1405/07/15: a card of
  its own rather than joining theirs). None of it is in «دریافتی» or the net profit.
- **Expenses** by `ExpenseDate`, voided ones left out (§9), in total and by category.
- **Net profit («سود خالص») = net revenue without فروشگاه, آنالیز and متفرقه − every expense** of
  the same range (decided with the developer, 1405/07/14, like «فروش» above; متفرقه 1405/07/15). Every expense counts,
  whatever its category. Revenue by when money arrived, expenses by the date the Owner gave them.
  The revenue itself still counts every payment, فروشگاه, آنالیز and متفرقه included; it is drawn in the
  revenue chart and the revenue breakdowns, but has no card of its own: «درآمد ناخالص» and
  «صندوق نقدی» were taken off the summary, and «فروش» took the first place, with «دریافتی»
  beside it in their stead (asked by the developer, 1405/07/14).
- **No cafe profit** (removed by the developer, 1405/07/14). «سود بوفه», the cafe's net revenue
  minus the «خرید بوفه» expenses, was taken off the dashboard and out of the report. The cafe's
  money is shown as «دریافتی بوفه» (*Dashboard* below), and «خرید بوفه» stays an ordinary
  expense category, counted in «هزینه‌ها» and «سود خالص».
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

### The absence SMS's effect (اثر پیامک غیبت)

Decided with the developer on 1405/08/04 (2026-10-26), roadmap 10.19. Of the members the absence
SMS (§10 *The absence SMS*) went to, how many came again soon after it.

- **Owner only**, like every report. **No range**: every message since the first (*Where it is*
  below).
- **Which messages:** the absence SMS that were sent (`Sent`), by the gym's day of the moment they
  were sent (a resend's moment, for one the Owner sent again). One the provider gave
  back, cancelled or blocked by the receiver (§10 *Sending*), never reached the phone and is left
  out, as is one that failed, whose fate is not known, or that is still waiting to go. One whose
  delivery the provider has not reported yet counts. Each message once: a member who had two
  absences is two messages. A member deactivated since stays in: the message was
  sent and paid for.
- **The window is 5 days** (first 14, then 7; made 5 by the developer on 1405/08/04: the closer to
  the message, the more a visit is its answer). The day of sending is day 0; the window ends on
  day 5.
- **Only a message that could be answered is measured** (decided with the developer, 1405/08/04,
  so the figures are not pulled down by members who had no chance to come):
  - **Not measurable** («قابل سنجش نیست») when the plan the member held on the day of sending (a
    membership plan, not cancelled, in its dates that day) ends before the window does, or no such
    plan is found any more (cancelled since). This is known from the day of sending, whatever the
    member did, so it leans the figure neither way.
  - **Not measurable** too when the plan was frozen inside the window before the member came: the
    member is away on purpose. Only a freeze still running is known (a freeze that has ended leaves
    only its longer end date), so an ended one is not seen.
  - Not measurable messages are counted apart and left out of the rate.
- **Came back** («برگشتند»): a measurable message whose member checked in after the moment it was
  sent and on or before the window's last day. A cancelled check-in is not a visit; a cardio-only
  one is, as for the absence SMS itself. The days from the day of sending to that first visit are
  its «روز تا برگشت».
- **Did not come back** («برنگشتند»): a measurable message whose window is over with no such visit.
- **Waiting** («در انتظار»): measurable, not come back yet, and the window not over (today on or
  before the day of sending + 5). Left out of the rate, like a plan waiting to be renewed.
- **Rate** = came back ÷ (came back + did not come back).
- **After it, not because of it:** some members would have come anyway, and there is no group left
  without the SMS to compare with (declined by the developer, 1405/08/04). The figure says who came
  after the message, in those words. **How soon** they came is shown beside it: the members who
  came back, by «روز تا برگشت», day 0 to day 5. Most of them in the first day or two points to the
  message; spread evenly, to habit.
- **What it shows:** the rate, the counts (sent, came back, did not, waiting, not measurable), the
  average «روز تا برگشت», the members who came back by that day, and what every counted message
  cost, in Toman (the provider's cost, given back cost being 0, §10 *Sending*), with that cost
  divided by those who came back («هزینه برای هر برگشت»).
- **Where it is:** «اثر پیامک غیبت» in «نیاز به اقدام», in the place «بدهی قدیمی» had; «بدهی قدیمی»
  moved to the end of the panel (asked by the developer, 1405/08/04). One place only: first also
  drawn as a card of «خلاصهٔ بازه» over the dashboard's range, it was taken off the same day, since
  a new month left it nearly empty and two places showed the same thing.
  - **Every message since the first**, whatever the dashboard's range, so a new month never
    empties it; and the same figures month by Jalali month (by the day of sending), the latest
    first.
  - **«فهرست نام‌ها»** opens the messages of the **last 30 days**, today included, so the list stays
    short: each with the member's name (a link to their page), the day it was sent, and «بعد از n
    روز آمد» («همان روز آمد» on day 0), «n روز مانده» («امروز روز آخر است» on the last day),
    «نیامد» or «قابل سنجش نیست». Those who came back first, then those waiting, those who did not,
    and those not measurable; the latest sent first in each.

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
  - The pressed button is the preset the range is. When several are the same days (a month's 1st:
    «امروز» and «این ماه»; 1 Farvardin: «امسال» too), «این ماه» lights, as the page opens on it;
    otherwise the first one listed (a Saturday: «امروز», not «این هفته»). Decided with the
    developer, 1405/08/01.
  - A range missing a date, running backwards or longer than 366 days is refused on the page with
    the API's own messages, before anything is asked.
- **Compared with the range before**: a card shows the change as a percent with an arrow and a word,
  green when it is good news and red when it is bad (expenses going up is bad news). When the
  range before had nothing or a loss, a percent says nothing true, and the card shows the figure
  before instead. New members and the renewal rate have no comparison: their reports send none.
- **«دریافتی پلن» and «دریافتی تک‌جلسه‌ای»** (asked by the developer, 1405/07/14; first named
  «خرید پلن» and «تک‌جلسه‌ای», renamed the same day, the figure unchanged) are two cards of the
  range's summary: what the membership plans and the single visits sold in the range were sold
  for, paid or not, **by the day of the sale**, compared with the range before, with how many
  were sold underneath, and in the card's corner, like «فروش», «پرداخت‌شده» (paid so far on those
  sales, refunds taken off) and «مانده» (still owed). First drawn as the money received for them by the day it was paid, they
  showed a single visit sold days earlier and paid today as today's, with nothing sold today;
  today's money is «دریافتی».
- **The renewal rate in plain words** (asked by the developer, 1405/07/14): the card says what it
  measures, "of the members whose plan ended in this range, what percent bought a plan again
  within 30 days", and lists in its corner «تمدید کردند», «تمدید نکردند» and «در انتظار»; the
  rate is the first over the first two. "Waiting" is explained on the card when there are any.
- **«دریافتی بوفه»** (asked by the developer, 1405/07/14): the cafe's money that came in during
  the range, old tabs included, refunds taken off, compared with the range before. One figure,
  without card, transfer and cash. It is part of «دریافتی», and the card says so.
  **«دریافتی هوازی»** beside it (asked by the developer, 1405/07/14) is the same for هوازی.
- **The summary's order** (1405/07/14): «فروش», «دریافتی», «دریافتی آنالیز و فروشگاه»,
  «دریافتی متفرقه» (since 6.5.36), «دریافتی بوفه», «دریافتی هوازی», «دریافتی پلن», «دریافتی تک‌جلسه‌ای», then expenses and net
  profit, then the attendance and members' figures. Beside the renewal rate
  sits the single visit to plan conversion rate (*Operational reports* above), without a
  comparison, like the renewal rate. «اثر پیامک غیبت» is not a card of the summary: it is in
  «نیاز به اقدام», which does not follow the range (*The absence SMS's effect* above, 1405/08/04).
- **A profit is green, a loss is red** (decided with the developer, 1405/07/14): «سود خالص»
  shows its figure in green above zero and in red below it, with its minus
  sign («−۳۰٬۵۰۰٬۰۰۰ تومان»). A month without revenue can end in a loss, and the Owner must see
  it as one, not as a missing figure. Zero is neither colour.
- **What does not depend on the range** (plans today, receivables, needs attention) stays the same
  when the range changes.
- **Charts read right to left**, like the page: the oldest day or month is on the right. A range
  longer than 62 days shows revenue and expenses by Jalali month instead of by day. New members and
  the renewal rate are by Jalali month. The weekday × hour table shows from the earliest hour
  anyone came to the latest.
- **Revenue by source** lists پلن, تک‌جلسه‌ای, هوازی, بوفه, فروشگاه, آنالیز, متفرقه: the cafe
  above فروشگاه. The plan and single-visit bars also say how many were sold in the range («۱۲ پلن فروخته
  شد»): plans sold matter more to the Owner than new members (asked by the developer, 1405/07/13).
  The bar's length stays the money received.
- **New members** are explained on the page as "members whose first membership plan was sold in
  this range" (this month, on the monthly chart), the rule of *Operational reports* above.

### History (تاریخچه)

Decided with the developer, 1405/07/10 (2026-10-02). Roadmap 6.5.25. Each member's own history is
on their profile and the cafe has its own order history (§8); this is the history of the whole gym.
It is a list of rows, not totals or charts: those are the reports above.

- **One «تاریخچه» page, three sections:** ورود و خروج (check-ins), پرداخت‌ها (payments and refunds)
  and «هوازی، فروشگاه، آنالیز و متفرقه» (renamed from هوازی when the sales joined it, tasks 6.5.28,
  6.5.29 and 6.5.36). The
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
  and by source (subscription, هوازی, فروشگاه, آنالیز, متفرقه, cafe).
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
  - A page holds 50 rows (20 until roadmap 6.5.34).
  - A payment for a guest's cafe order shows the guest's name, marked «مهمان», not «مشتری آزاد»: the
    order is under that name (§8). So does a guest's هوازی or sale, in every section that lists it
    (since 6.5.31).

### Sales in the history (فروش‌ها)

Asked by the developer, 1405/07/12 (2026-10-04). Roadmap 6.5.30. The Owner wants to see everything
the gym sold in one place, and each kind on its own, with what is still owed. Each section ends
with its totals (roadmap 6.5.32, *Totals in the history* below); reports and charts are Phase 9.

- **Owner only.** Staff keep the three sections above, unchanged. The API refuses Staff (403).
- **For the Owner, the history's sections are:** ورود و خروج، پرداخت‌ها، همهٔ فروش‌ها، فروش پلن،
  هوازی، فروشگاه، آنالیز، متفرقه، بوفه («متفرقه» since 6.5.36). The separate هوازی, فروشگاه, آنالیز
  and متفرقه sections replace the combined «هوازی، فروشگاه، آنالیز و متفرقه» one, which only Staff
  still see.
- **What a sale is:** a subscription (single-session and membership together under «فروش پلن», each
  row saying which), a هوازی charge, a فروشگاه item, an آنالیز, a متفرقه, a cafe order. «همهٔ
  فروش‌ها» lists all six kinds together, cafe orders of walk-ins and guests included. The cafe's own
  order page stays as it is.
- **Which day a sale belongs to:** a subscription by when it was sold (`CreatedAt`, «تاریخ فروش»,
  in the gym's time zone), a هوازی or sale at the desk by its `ChargedOn`, a cafe order by its
  `OrderedOn`. Newest first, by the moment it was recorded.
- **Paid or not** (a three-way choice on every sales section: همه / پرداخت شده / پرداخت نشده):
  - «پرداخت شده»: net paid (payments − refunds) covers the amount. A free plan (`Price = 0`) is paid.
  - «پرداخت نشده»: anything still owed, a partial payment included.
  - A cancelled subscription or cafe order and a voided charge are in neither: they owe nothing and
    were never fully a sale. They are listed under «همه», marked with their reason, never hidden.
- **Who recorded it:** who sold the subscription (its `CreatedBy`: whoever was logged in, §4), who
  placed the cafe order (`PlacedByUserId`) and who recorded the charge (`RecordedByUserId`).
- **No status column** (developer, 1405/07/15): a cancelled subscription or cafe order reads
  «لغو شده», a voided charge «ابطال شده», as a tag beside what was sold, with the reason under it.
- The filters are the history's own: the date range and one member. A page holds 50 rows.

### Totals in the history (جمع)

Asked by the developer, 1405/07/12 (2026-10-04). Roadmap 6.5.32. Each section where money changes
hands ends with a totals row, so the Owner reads what a day, a member or a kind of sale came to
without adding up the rows.

- **Owner only**, like the sales sections. Staff see the same rows as before and no totals; the API
  refuses Staff (403).
- **Over everything the filters let through**, every page, not only the 50 rows on screen. The
  totals follow every filter of their section: the date range, the member, the sales section's kind
  and «وضعیت پرداخت», the payments' «روش پرداخت» and «بابت».
- **Every sales section** (همهٔ فروش‌ها، فروش پلن، هوازی، فروشگاه، آنالیز، متفرقه، بوفه) shows three
  figures:
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
  - **One exception: the installed app's name** (roadmap 13.4, decided by the developer, 1405/08/03):
    «Management» under the icon and «Pasargad Gym Management» in full, in English, which is short
    and never breaks under an icon. The long-press shortcuts keep the menu's Persian names. The
    members' and trainers' app (Phase 15) is «Pasargad Gym» (15.3b), so the two are told apart.
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
- SMS messages are Persian. A Persian part holds 70 characters (67 in a longer message) against 160 for Latin, so texts are kept short and each one's part count is worked out when it is written (`docs/sms-texts.md`, §10).

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
- In the installed app (roadmap 13.4), on Android, the phone's status bar follows the theme: the
  light or the dark background. An iPhone takes its status bar from its own setting, left at the
  system's default; how it looks there is checked on a Staff iPhone after the release, and any
  change to it is the developer's choice. The app's icon and its opening screen are always dark (the
  developer's choice, 1405/08/03), whatever the theme.

## 15. The site: members and trainers (اعضا و مربیان در سایت)

Decided with the developer on 1405/08/03 (2026-10-25), with ADR 0010 (accepted the same day),
roadmap Phase 15. The panel stays for the staff only (§1). Each line says who decided it:
- *Decided by the developer* (1405/08/03): said by the developer, who is the gym's Owner.
- *Proposed by Claude*: suggested on 1405/08/03 and accepted by the developer in general terms
  («بقیه چیزهایی که گفتی درسته»); each is confirmed again when its task's plan is approved.
- *Open*: asked at the start of its task; nothing is built on it before then.

### Who uses the site
- **Two kinds of people sign in on the site: members (شاگرد) and trainers (مربی).** Both sign in
  with their phone number and a one-time code sent by SMS; there are no passwords on the site.
  *Decided by the developer.*
- **The panel is never reachable from the site**, and a site account never opens the panel. Staff
  keep their own sign-in (§1). *Decided by the developer* (2026-10-15, 14.6).
- **The gym is for men only, and about 95% of its members are 18 or older.** *Decided by the
  developer* (a fact of the gym, which shapes the photo rules below).
- **The site's account area is at `app.pasargadgymplus.ir`** and installs on a phone like an app
  (a PWA). *Decided by the developer* (1405/08/03).
  - **Its icon is the panel's reversed**: the gym's logo in dark stone on bronze, so the two icons
    are told apart at a glance on a phone that has both; «Pasargad Gym» under it (§13). *Decided by
    the developer* (1405/08/07, 15.3b).
  - **The app asks to be installed** in a small card on the sign-in screen and the home screen:
    on Android a «نصب برنامه» button (where the browser offers it), on an iPhone the steps of
    Safari's Share menu. Never on a computer (the developer, 1405/08/07: nobody needs the app
    there, though Chrome offers it), hidden in the installed app, and for 30 days after «بعداً».
    Shown before signing in, because an installed iPhone app keeps its own sign-in, and installing
    after signing in would cost a second code SMS. *Proposed by Claude, accepted with 15.3b's plan*
    (1405/08/07).
  - **The installed app opens without internet** and then shows «ارتباط با سرور برقرار نشد», as the
    page does (a service worker keeps the app's files, never the API's answers); a release reaches
    it by itself the next time it opens. *Proposed by Claude, accepted* (1405/08/07, 15.3b).
  - **The desk's QR code holds `https://pasargadgymplus.ir/qr`**, a fixed address of the public
    page that is redirected to the app, so the printed poster (`deploy/qr/`) never has to be
    printed again if the app moves. *Proposed by Claude, accepted* (1405/08/07, 15.3b).
  - **The public page's top bar has one «ورود / ثبت‌نام» button** that opens the app, since signing
    in and signing up start on the same screen there. *Decided by the developer* (1405/08/07). It
    links `pasargadgymplus.ir/app`, redirected like `/qr`, so the page never names the app's host.
- **A member's program can be seen and followed without internet**: the program last opened stays
  on the phone, and what the member marks done while offline is sent when the phone is online
  again. *Decided by the developer* (1405/08/03); *Open:* what "marking done" holds (15.11).

### Signing up and signing in
- **Anyone with an Iranian mobile number can sign up** (§2's phone rules). After the code is
  verified they give their full name and birth date, with §2's rules (required, not in the future,
  not more than 120 years ago). *Decided by the developer.*
- **A phone already in the panel links to that member**, never a duplicate (§2: phones are unique).
  *Decided by the developer* (2026-10-15).
  - **A linked member is not asked for a name or birth date again**; the panel's are shown, and
    corrections are made at the desk, so a sign-up never changes what the desk entered.
    *Decided by the developer* (1405/08/04, 15.1b).
- **A phone not in the panel creates a member there**, active, with no plan. They cannot enter the
  gym until they buy a plan: every visit is a check-in at the desk, which needs a plan or a single
  visit (§7), as for anyone. *Decided by the developer.*
- **Signing up sends no welcome SMS.** The welcome goes when the member's first membership plan is
  sold (§10 *The welcome*), unchanged; a member who signed up on the site gets it at the desk with
  their first plan, like anyone. *Checked against §10 on 1405/08/03; nothing changes there.*
- **Buying a plan on the site is not part of this phase** (2026-10-15, still for later).
- **The one-time code** (*Decided by the developer* on 1405/08/03, at the start of 15.1):
  - **5 digits, valid for 60 minutes**, as government sites (e.g. ADLIRAN) do; at most 5 wrong
    tries, then that code is dead and a new one is needed. (Claude proposed 2 minutes, then 3;
    the developer chose 60.)
  - **No new SMS while a code is still valid.** Asking again within the first 2 minutes is
    answered «کد قبلاً برای شما پیامک شده است» with the seconds left before «ارسال دوباره»; the
    screen tells the member to wait 2 minutes if the code has not come.
  - **«ارسال دوباره» after 2 minutes sends the same code again**, not a new one (so a late first
    SMS still works), with the minutes it has left. **It is not sent when Kavenegar has reported
    the first one delivered**: the answer is then that the code has reached the phone, and nothing
    is paid. A member who lost a delivered code gets a new one when it expires.
  - At most 5 code SMS an hour per phone, the sends again included; **20 code requests per 10
    minutes per IP** (generous, because Iranian mobile carriers put many phones behind one
    address); and **at most 300 code SMS a day for the whole gym**, so a flood cannot empty the
    Kavenegar credit overnight.
  - Every code is a paid SMS, so the limits are checked before Kavenegar is called.
  - **Sent as Kavenegar's verification template** (`verify/lookup`, made for codes), not as free
    text (Kavenegar's answer, 1405/08/03; it had refused templates before the site was up,
    1405/07/16). The template is `gymExpiring`, written by the developer:
    «کد تایید باشگاه پاسارگاد: %token» and «این کد تا %token2 دقیقه معتبر است.», where `token` is
    the code and `token2` the minutes it is valid for. It uses the gym's Kavenegar account and the
    same API key; Kavenegar sends it from the gym's dedicated line `9981600378`, since its shared
    international line `0018018949161` cannot send inside Iran (Kavenegar's answer, 1405/08/07).
    Every other SMS is free text from the same dedicated line (§10 *Sending*).
  - **The code fills itself in on the phone** (asked by the developer, 1405/08/03, for the site
    app of 15.3): iPhones offer it from the SMS by themselves; Android needs the template's last
    line to be `@app.pasargadgymplus.ir #%token`. Kavenegar approved `gymExpiring` with that third
    line (sent by the developer and approved 1405/08/07). To check on the first real code SMS: its
    last line is `@app.pasargadgymplus.ir #` and the code, with nothing after it.
  - The code itself is never written anywhere readable: not in the logs, not in the SMS history.
    Code SMS are not listed in the panel's «پیامک‌ها» history; only their count and cost are kept,
    and showing them in the panel is for 15.2. The database keeps only a random seed per code, from
    which the code is computed with a secret in the server's settings (`SITE_CODE_SECRET`), so a
    database backup alone reveals no code. *Decided by the developer* (1405/08/03).
  - **The right code is used up only when the sign-in is saved.** If anything fails after the code
    is checked, the same code still works, and no new, paid SMS is needed; a wrong code's try is
    counted at once. *Proposed by Claude, accepted by the developer* (1405/08/07, 15.3b), after a
    local error burnt a code.
  - **Two presses at once send one SMS**, and the gym's 300 is never passed: each phone's requests
    and checks run one at a time, and the sends of the whole gym too (15.1a). A request whose
    browser goes away once Kavenegar has been called is still finished and counted, since the SMS
    may already be paid for. *Decided by Claude* in 15.1a, after the code review.
  - **Development sends real codes** through the same Kavenegar account, only to the numbers in
    `Sms:AllowedReceptors` (§10 *Development and tests*), so the developer tests with their own
    phones; the automated tests always use the fake sender. *Decided by the developer*.
- **Staying signed in on the site: 60 days, renewed by every use**, because each new sign-in is a
  paid SMS (staff: 7 days, §1). *Decided by the developer* (1405/08/03).
  - **The site signs out only when the server says the session has ended**: «خروج», 60 days
    unused, the phone changed by the desk or the Owner, a reused copy of the session, or the
    browser's data cleared. With no internet, the server restarting during a release or too many
    requests from one address, the person stays signed in and sees «ارتباط با سرور برقرار نشد»
    with «دوباره امتحان کنید», instead of being sent to ask for a new, paid code. *Proposed by
    Claude, decided by the developer* (1405/08/06, 15.3a); the panel keeps signing out (§1).
- **When the desk changes a member's phone, every site session of that member ends** (within the
  access token's 15 minutes), and the member signs in again with the new number. The site finds a
  member by the phone the desk typed, which no code ever checked: a mistyped number may belong to a
  stranger, who could sign in as that member until the desk corrects it. *Proposed by Claude,
  decided by the developer* (1405/08/05, 15.1b). **No code is sent when the desk registers a
  member**: the desk types the phone as it does today. *Decided by the developer* (1405/08/05); not
  to be raised again for now.
- **A new phone has 15 minutes after its code to give its name and birth date**; after that it
  asks for a new code. *Proposed by Claude, accepted* (1405/08/04, 15.1b).
- **15 wrong tries in one of the gym's days close the phone until the next day**, counted over all
  its codes: no new code is sent to it, and the current one stops working. Someone guessing at one
  person's account then gets 15 guesses a day instead of 600; the real owner of the phone never
  types a code wrong 15 times. Counting tries rather than codes ended by 5 wrong tries, because
  stopping at 4 a code and waiting for the next would escape a count of codes (found in 15.1b's
  review). *Proposed by Claude, decided by the developer* (1405/08/04-05, 15.1b).
- **When the gym's 300 a day is reached**, no SMS goes to the Owner (SMS goes live as one complete
  version, §10); the panel shows it, in 15.2. With about 300 members, an ordinary day never comes
  near it: reaching it means an attack. *Decided by the developer* (1405/08/04).
- **What a member does on the site is recorded in the audit log as «سیستم»** (no staff user), as
  sign-ins are (§11). *Proposed by Claude, accepted* (1405/08/04, 15.1b).
- **A member deactivated in the panel** can still receive a code and sign in, but sees only a
  notice that their membership is inactive and that they should come to the desk; nothing else on
  the site opens for them. *Decided by the developer* (1405/08/03).
  - **This holds for a trainer too**: a trainer whose membership (same phone) the desk deactivated
    sees only that notice, and the trainer's area stays shut until the desk activates the member
    again. A trainer who is not a member is not affected. *Decided by the developer* (1405/08/06,
    15.3a).

### Trainers
- **The gym has one trainer or more, and they can change over time.** *Decided by the developer.*
- **Only the Owner makes someone a trainer**, in the panel; nobody signs up as a trainer.
  *Proposed by Claude.* The trainer then signs in on the site with the phone the Owner entered.
- **A trainer's fee is not in the system**: the trainer and the student settle it between
  themselves, as today. *Decided by the developer.*
- **Everything a trainer does is in their own area of the site**: their timetable, their students,
  and for each student the programs, photos and analyses. The system gives the tools; the trainer
  writes the content (their own exercises, templates and programs). *Decided by the developer.*
- **When the Owner stops someone being a trainer**, their students go back to having no trainer
  and keep everything recorded about them. *Decided by the developer* (1405/08/03).
- **A trainer is not a member, but may also be one** (*Decided by the developer*, 1405/08/05,
  15.2). A trainer has their own name and phone in «مربیان», and is not in the members' list, the
  reports or the members' SMS. A trainer who also trains at the gym is registered at the desk as a
  member too, as anyone; with the same phone, one code on the site opens both their areas.
  - Phones are unique among trainers, inactive ones included; a trainer's phone may be a member's.
  - **A trainer who is stopped can be made a trainer again**: a new period starts, and the earlier
    ones stay in their history with their dates. Students are not linked back by themselves.
    *Decided by the developer* (1405/08/05, 15.2).
  - **A stopped trainer loses the trainer's area within 15 minutes** (the access token's life); a
    trainer who is also a member keeps the member's area. *Proposed by Claude, 15.2.*
  - **A stopped trainer who is still signed in and is not a member** sees «مربی‌گری شما تمام شده
    است» and is signed out of the site. *Decided by the developer* (1405/08/05, 15.2); the screen
    comes with the site app (15.3), where «me» already answers with neither area.
  - **A stopped trainer who is not a member and signs in again** gets the sign-up screen, as anyone
    whose phone is not a member's (*Signing up and signing in*). *Decided by the developer*
    (1405/08/05, 15.2).
  - **When the Owner changes a trainer's phone**, the old phone is no longer the trainer's: its
    site sessions end, unless the old phone is also a member's, who stays signed in with the
    member's area and loses the trainer's within 15 minutes. *Decided by the developer*
    (1405/08/05, 15.2). The trainer signs in again with the new phone.
- **A member's profile shows whether they have a site account**, with the date it was made and the
  last sign-in; the members' list has no tag or filter for it. *Decided by the developer*
  (1405/08/05, 15.2).
- **The code SMS in the panel** (*Decided by the developer*, 1405/08/05, 15.2): «پیامک‌ها» shows
  today's code SMS out of the gym's 300, and this month's count and cost; when today's 300 is
  reached, «نیاز به اقدام» shows it too.

### A member and their trainer
- **Each member has at most one trainer at a time.** *Decided by the developer.* The database
  holds it too (one open link per member).
- **The member picks a trainer on the site, and the trainer accepts or declines.** *Proposed by
  Claude, accepted by the developer.* Changing trainer is a new request; when the new trainer
  accepts, the old link ends that day. Until then the old trainer stays the member's trainer.
- **The link has a start and an end**, so the history of who trained whom is kept, and a former
  trainer stops seeing the member from the day the link ends. *Proposed by Claude.* A link ends
  on the gym's today, so «from the day it ends» is at once.
- **The details, decided with 15.4's plan** (*Decided by the developer*, 1405/08/07, unless
  marked otherwise):
  - **One request waiting at a time**: picking another trainer while a request waits replaces it
    (the first is closed as «عضو مربی دیگری انتخاب کرد»). Pressing the same trainer again makes no
    second request.
  - **The member may take a waiting request back** («لغو»). *Proposed by Claude, accepted.*
  - **Either side may end the link with no new trainer**: the member («پایان مربی‌گری») or the
    trainer, for one student. Both ask before doing it (the screens, 15.4b). It ends that day.
  - **The trainer sees each student's name and phone** in «شاگردان من», and the same in the
    requests waiting for them.
  - **The trainer hears of a new request in the app and by SMS** (15.4c): its own kind,
    «انتخاب مربی», in «تنظیمات پیامک» under «پیامک به مربیان», off at first like every kind (§10),
    sent at once, at 10:00 the next morning when the request comes after 22:00. The text, chosen by the developer:
    «{نام کوچک مربی} عزیز، {نام کامل شاگرد} شما را به‌عنوان مربی خود در باشگاه پاسارگاد انتخاب کرده
    است.» with «لغو11» under it, as every SMS from the line (it stays: Kavenegar adds it anyway
    when the text leaves it out, the developer said 1405/08/07).
    **At most one such SMS a day for one member and one trainer**: further requests that day
    show in the app only. The day is the gym's day of the request; the database holds it (one row
    per member, trainer and day).
  - *Proposed by Claude, built with 15.4c* (1405/08/07): **the SMS goes only for a request still
    waiting when it is sent.** A request taken back, replaced, accepted or declined before 10:00
    (or before a busy minute passes) costs no SMS; nor does one whose trainer was stopped or whose
    member was deactivated meanwhile. The switch counts as it is when the request is made, and
    again when the SMS goes, like the welcome (§10). In «پیامک‌ها» the message is listed under the
    trainer's name, with no link to the member.
  - **At most 5 requests a day per member** (the gym's day), whatever became of them, so changing
    back and forth cannot cost many SMS (`Coaching.TooManyRequestsToday`).
  - *Proposed by Claude, accepted with the plan* (1405/08/07): a request does not expire; after a
    decline the member may ask again, the same trainer too; no plan is needed to ask (picking a
    trainer is not entering the gym, §7); a trainer who is also a member cannot pick themselves
    (same phone); only trainers now are listed, by name only until 14.1's photos and details.
  - **A member the desk deactivates** cannot ask (their area is shut, *Signing up and signing
    in*). Their link stays, and their trainer sees them marked «غیرفعال»; a request they made
    before is left out of the trainer's list and cannot be accepted until the desk activates them
    again. *Proposed by Claude, accepted with the plan.*
  - **When the Owner stops a trainer**, their links end that day and the requests waiting for them
    end too (*Trainers* above). The member's area shows no trainer, and they may pick another.
  - **Everything here is in the audit log as «سیستم»** (§11), as the site's other actions.
- **A new trainer sees the member's earlier assessments (the numbers), programs and attendance;
  earlier photos only if the member allows it.** *Proposed by Claude, accepted by the developer.*
- **The member sees everything their trainers record about them** (programs, analyses, photos,
  numbers) **except the trainer's private notes.** *Proposed by Claude, accepted by the developer.*

### Timetable and attendance
- **Every visit is a check-in at the desk and uses a session of the member's plan** (§7, unchanged).
  The site never checks anyone in and never uses a session. *Decided by the developer.*
- **The trainer sees attendance from the desk's check-ins**: whether each student came on the
  days of their sessions. The site does not keep a second record of entries. *Proposed by Claude,
  accepted by the developer.*
- *Open, decided in its task:* group sessions, private ones or both; capacity; who puts a student
  in a session (the trainer, or the member picking a slot); a weekly pattern and changes to a
  single day; the trainer cancelling a session, and whether an SMS goes; how a check-in counts as
  "on time", "late" or "absent" for a session; the absence alert to the trainer.

### Monthly assessment (ارزیابی ماهانه)
- **The trainer uploads the body analysis as the device's PDF**, and records progress photos, for
  each of their students. *Decided by the developer.*
- **The gym's device is an Anea BIA** (anea3d.com; «1204» printed on the sheet). Its PDF is a
  single picture with no text in it (checked on 1405/08/03), so the system cannot read the numbers
  from it, and a barcode reader cannot open the sheet's QR code (tried by the developer). Anea's
  site and manual mention no export (Excel, CSV, API); the device works with Anea's own 10-inch
  tablet app, which records each person by phone number and name. Anea's answer (1405/08/03): the
  software gives the PDF only; an Excel export is sold separately, 8 million once, for the gym's
  own device, with every earlier result in it. **The gym does not buy it for now** (decided by
  the developer, 1405/08/03). **The trainer types the main numbers beside the PDF**, and the
  charts are drawn from them. *Decided by the developer.* *Open:* the exact list (from the sheet:
  weight, muscle mass, fat mass, fat percentage, BMI, visceral fat, basal metabolic rate, body
  water, protein, minerals, and fat and lean mass of each arm, leg and the trunk).
- **Filling the numbers in automatically is for later**, never in place of the trainer: whatever
  fills them (Anea's Excel matched to members by phone, OCR on the gym's server, or an LLM
  reading the sheet, which would send health data off the server) only proposes, and the trainer
  confirms each number against the PDF. *Proposed by Claude*; nothing chosen.
- *Open:* the other parts of the assessment (circumferences, fitness tests, goals) and the
  reminder when a month has passed.

### Progress photos (عکس‌های پیشرفت)
*Proposed by Claude, accepted by the developer:*
- **Only the member and their current trainer see the photos**, and a later trainer only with the
  member's permission. Not the desk, and not the Owner through the panel.
- **A photo never has a public address**: every view is checked at that moment.
- **The member can have their photos deleted**, and they are then really removed, files included.
  This is the one deletion the system allows on purpose; photos are not financial records.
- **A member under 18 has photos like anyone else**, with no extra step. *Decided by the
  developer* (1405/08/03), instead of Claude's proposal of a guardian's agreement.

### Programs and nutrition (برنامه و تغذیه)
- **The trainer writes each student's programs**, for that student. *Decided by the developer.*
- *Proposed by Claude:* a workout program and a diet program, each built from the trainer's own
  templates and changed for the student; the member sees their current program and the earlier ones.
- *Proposed by Claude:* the member's nutrition report starts as a short daily check-in (kept to
  the program fully, partly or not; water; sleep; optional weight and a photo of a meal), with a
  weekly summary for the trainer; no calorie counting. *Open:* the fields, and a field for medical
  conditions on the member's file.
