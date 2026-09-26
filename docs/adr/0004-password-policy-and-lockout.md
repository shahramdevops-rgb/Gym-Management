# 0004. Check passwords by length and blocklist, and lock out unknown devices only

- Status: Accepted
- Date: 2026-09-26

## Context

The panel is on the public internet (ADR 0003), and it is protected by a password alone. The
developer decided against a second factor by SMS: if a message does not arrive, a staff member
cannot open the front desk, and check-in stops. So the password and the lockout around it carry
all the weight.

Two weaknesses were found in the rules from Phase 1:

1. **The password policy** was 8 characters with a letter and a digit. `ali12345` passed it and is
   among the first guesses in any attack list.
2. **The lockout** (5 wrong passwords lock the account for 15 minutes) protects against guessing,
   but can be turned against the gym. Anyone who knows a user name can send five wrong passwords
   every 15 minutes and keep that person out indefinitely. The per-IP rate limit does not help: five
   requests every 15 minutes is far below it. The Owner's user name is `Owner`, so it is known.

## Decision

**Passwords follow NIST SP 800-63B**: length and a blocklist, no composition rules. At least 12
characters (NIST says 15 for a password-only login; the developer chose 12 for the front desk),
English only, not containing the user name, not a repetition or keyboard run, and not one of the
100,000 most common leaked passwords or a word for this gym. The blocklist ships inside
`Gym.Domain` as an embedded resource, so no password is sent anywhere to be checked. A login
whose stored password fails the current policy must change it (BUSINESS_RULES.md §1).

**Lockout is split by device** (OWASP's "device cookie" defence):

- After a successful login, the browser gets a `gym_device` cookie holding a random secret. The
  database stores its SHA-256 hash per user it has logged in (`trusted_devices`), like refresh
  tokens. The cookie is HttpOnly, Secure, SameSite=Strict and sent only to `/api/auth`.
- A login from a device trusted for that user counts its wrong passwords on that device's row,
  and only that device is locked after five.
- A login from anywhere else counts on the user's Identity lockout, as before, and that lockout
  stops only untrusted devices.

An attacker on the internet has no device cookie, so however many wrong passwords they send, they
lock only the "unknown devices" door. The front-desk PC and the Owner's phone keep working. Trust
lasts 90 days from the last time the device was used (a login, or a session refresh from it), and
ends for every other device when the password is changed, and for all of them when it is reset.
The secret in the cookie is replaced at every login and every refresh, so a copied cookie stops
naming the device the next time the real browser is used.

Three ways to undo a lockout complete it: an «باز کردن قفل» button for the Owner on staff accounts,
and `./server.sh unlock` and `./server.sh set-password` on the server for anyone, the Owner
included, for the day the Owner is the one locked out.

## Alternatives considered

- **Keep the per-account lockout and nothing more.** Rejected: the attack above is a small script.
- **Lock by IP address instead of by account.** Rejected: all front-desk staff share one IP, and an
  attacker can change theirs. The per-IP rate limit stays, as a limit on the endpoint, not as
  the lockout.
- **No lockout, only growing delays (throttling).** Rejected: the delay still applies to the real
  user, who would be made to wait by the attacker's attempts, which is the same problem more slowly.
- **CAPTCHA after a few failures.** Rejected: a new outside dependency, and the free ones are
  unreliable from Iran.
- **A second factor by SMS or an authenticator app.** Rejected by the developer for now: a
  missing SMS would stop the front desk. It can still be added later without undoing any of this.
- **Composition rules (upper case, digit, symbol).** Rejected: NIST advises against them, because
  people meet them predictably (`Ali@12345`), and they make passwords harder to type on the front
  desk's Persian keyboard without making them harder to guess.

## Consequences

- One more table (`trusted_devices`) and one more cookie. Clearing the browser's cookies makes a
  trusted device unknown again until the next successful login.
- Someone who copies the device cookie from the front-desk PC (developer tools can show it) gets
  no way in, since the password is still needed. Until the real PC next logs in or refreshes (at
  most 15 minutes while anyone is signed in there), the copy can count wrong passwords at that PC's
  door and lock the people who use it. Rotation keeps that window short; it cannot close it.
- Each login and refresh on a trusted device writes an audit row for the device (its secret's hash
  is excluded from the audit log by name), the same as refresh tokens already do.
- The blocklist file is data in the repository (~360 KB). It is regenerated from SecLists, never
  edited by hand; its header says how.
- Passwords set before this policy keep working until their owner next logs in.
