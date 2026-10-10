# Security review

Task 11.2, 1405/07/18 (2026-10-10). The panel has been on the public internet since 2026-09-24
and has held the gym's real data since 2026-10-02. This file records what protects it, the
evidence for each protection (a test, a setting, a command), and the risks that were accepted on
purpose. Run the review again after any change to authentication, the Caddyfile or the compose
file. The checklist is the OWASP Top 10, 2025 edition.

## What changed in this review

- **Content-Security-Policy on the panel** (`deploy/Caddyfile`): `script-src 'self'`, so the
  browser runs only scripts served by the panel itself. If a bug ever let someone's text into
  the page as HTML, the script would not run and could not read the access token held in memory.
  The theme script moved out of `web/index.html` into `web/public/theme-init.js` to make this
  possible. `ContentSecurityPolicyTests` fails if either half drifts.
  Proven in Chrome against the production images on `https://localhost`: login, the forced
  password change, a dialog, the dashboard and the Jalali calendar work with no violation, and a
  script injected into the page is refused (`script-src-elem`).
- **Permissions-Policy and Cross-Origin-Opener-Policy** on the panel; a CSP, `X-Frame-Options`
  and `Permissions-Policy` on the public page, which runs no script at all.
- **Rate limit on refresh and logout**: 60 a minute per IP address, in one bucket
  (BUSINESS_RULES.md §1). These are the two anonymous endpoints that reach the database without
  a password check. `SessionRateLimitTests`.
- **`npm audit fix`**: `source-map-js`, a development-only dependency (high, GHSA-68fv-2mgg-jv7q).
  It never reached the production bundle.
- **Decided:** user-name discovery through `Auth.LockedOut` is an accepted risk
  (BUSINESS_RULES.md §1, and *Accepted risks* below). The Hangfire dashboard stays
  Development-only.

## Checklist

### A01 Broken access control
- Every endpoint names a policy (`OwnerOnly`, `StaffOrOwner`, `PasswordChangeAllowed`) or says
  `AllowAnonymous()`. The fallback policy requires a signed-in user, so a forgotten policy fails
  closed. Evidence: `EndpointAuthorizationTests`.
- Anonymous endpoints: login, refresh, logout and `/health`, and nothing else. All three auth
  endpoints are rate-limited.
- A user with a temporary password is refused everywhere except change password and logout
  (`PasswordChangedRequirement` on every policy but one).
- There is no per-record ownership to check. Both roles work on all of the gym's records, and
  the Owner-only actions are role checks.

### A02 Security misconfiguration
- CORS exists only in Development (`CorsConfiguration`). In production the API and the app share
  one origin, so no cross-origin request carries credentials.
- Scalar, the OpenAPI document and the Hangfire dashboard are mapped only in Development, and
  Caddy proxies only `/api/*` and `/health`.
- `AllowedHosts` is the panel's own name. `X-Forwarded-For` is trusted only from Caddy's pinned
  subnet (`ForwardedHeadersTests`).
- Headers on the panel: HSTS, `nosniff`, `X-Frame-Options: DENY`, CSP, Referrer-Policy,
  Permissions-Policy, COOP and `X-Robots-Tag`; Caddy's `Server` header is removed.
- Postgres publishes no port. The API runs as the image's non-root user (`USER $APP_UID`).
  The server has key-only SSH, `ufw` allowing only 22/80/443, and fail2ban (task 6.0).

### A03 Software supply chain failures
- `dotnet list package --vulnerable --include-transitive`: none (2026-10-10).
- `npm audit`: none, after the fix above. `npm ci` installs exactly the lockfile.
- New packages are only added with the developer's approval (CLAUDE.md, `docs/ARCHITECTURE.md`).
- Base images are official: `mcr.microsoft.com/dotnet/*`, `node`, `caddy` and `postgres`.

### A04 Cryptographic failures
- HTTPS only. Caddy gets the certificate from Let's Encrypt and redirects HTTP; HSTS is one year.
- Passwords use ASP.NET Core Identity's PBKDF2 hasher. Refresh tokens and device secrets are
  random values, stored only as hashes.
- JWT: HS256 only (`ValidAlgorithms`), with issuer, audience, lifetime and the signing key all
  checked, and `ClockSkew = 0`. The key is at least 32 bytes (`JwtOptionsValidator`).
- Cookies are `Secure`, `HttpOnly`, `SameSite=Strict` and `Path=/api/auth`. The access token is
  kept in memory only, never in `localStorage`.

### A05 Injection
- All queries go through EF Core, which sends parameters. The one raw SQL call
  (`AppDbContext`, `SET CONSTRAINTS`) uses a constant name, not input.
- React escapes everything it renders. There is no `dangerouslySetInnerHTML`, `innerHTML` or
  `eval` in `web/src`. CSP is the second line of defence.
- Input is validated by FluentValidation at the edge and again by the entities.

### A06 Insecure design
- Business rules are enforced by the database too: unique and partial unique indexes, check
  constraints and `xmin` (CLAUDE.md).
- Money is never deleted, only refunded or voided with a reason. The audit log is append-only
  (a trigger refuses `UPDATE` and `DELETE`).

### A07 Authentication failures
- Password policy after NIST: 12–128 characters, with a blocklist of 100,000 common passwords
  (task 11.5).
- Lockout split by device, so an attacker can lock only the untrusted door (ADR 0004,
  task 11.6).
- Login is limited to 10 a minute per IP; refresh and logout to 60.
- Refresh tokens rotate on every use, and reuse revokes the whole family.
- An unknown user name costs the same hash time as a wrong password (`UserAuthenticator`).
- Guessable user names are refused, and the Owner's seeded name was changed on the server.

### A08 Software or data integrity failures
- Releases are built on the developer's machine from a commit and shipped as image archives
  tagged with that commit (`deploy/release.sh`). The previous tag stays for `./server.sh rollback`.
- Backups run before every release (`./backup.sh run`), and a copy is pulled to the gym PC.

### A09 Security logging and alerting failures
- The audit log records every change with its user, time and IP, and the Owner reads it on
  «گزارش تغییرات» (task 11.1).
- Refresh-token reuse is logged as a warning. Request logs carry a correlation id.
- The Kavenegar API key never reaches the logs (`RemoveAllLoggers`, with a test).
- Gap: nothing alerts anyone. Logs are read when someone looks. Task 11.3 read four days of
  production logs (`docs/performance-review.md`): two errors, both a cancelled request wrongly
  logged as a 500, now fixed.

### A10 Mishandling of exceptional conditions
- Business failures are `Result`/`Error` values with stable codes. An unexpected exception
  becomes a 500 ProblemDetails that carries only the correlation id, never the message
  (`GlobalExceptionHandler`).
- An SMS send never throws for a failed send. A request that left with no usable answer is
  `Unknown`, never retried.
- Startup fails on missing or malformed settings (signing key, forwarded network, SMS provider)
  rather than running half-configured.

## Accepted risks

- **`Auth.LockedOut` reveals that a user name exists** (BUSINESS_RULES.md §1). Hiding it would
  need a pretend counter for every name typed, or a real user being told "wrong password" while
  their right one is refused. Mitigated by the guessable-name rule, the device-split lockout and
  the login rate limit.
- **`style-src 'unsafe-inline'`.** Radix's dialog injects a `<style>` tag to lock scrolling. A
  style cannot run code. The realistic harm, reading text through CSS selectors, needs injected
  HTML in the first place.
- **A refresh refused with 429 signs that tab out.** The frontend treats any failed refresh as
  the end of the session. At 60 a minute per address a real desk never reaches it.
- **Rate limits are per IP and kept in memory.** The front desk shares one address, and the
  limits restart with the API. There is one API instance, so that is enough.
- **HSTS has no `includeSubDomains` and no preload**, so a future subdomain without HTTPS is not
  locked out by accident. Each name Caddy serves gets its own HSTS header.

## Still open

- Alerting on failed logins or 5xx errors: left for later by the developer after 11.3 read the
  logs (`docs/performance-review.md`, *Still open*).
- The Kavenegar API key restricted to the server's IP (10.6).
