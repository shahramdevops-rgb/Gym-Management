# Deployment and operations

Everything after the code is written: releasing to the server, going back, the accounts that can
only be managed from the server, and backups. Preparing a bare server comes first and is in its own
guide.

| Guide | When |
|---|---|
| [SERVER-SETUP.md](SERVER-SETUP.md) | Once per server: from a bare VPS to a host that can take a release, with the reason for each step |
| [SERVER-SETUP-QUICK.md](SERVER-SETUP-QUICK.md) | The same steps, commands only, for the second time |
| This file | Every release, and whatever happens on the server after that |

## The shape

One Docker Compose stack (API, Postgres, Caddy) on a single rented Iranian VPS, reachable over a
real domain with an automatically issued certificate, with images built on the development machine
and transferred rather than built on the server. The reasoning, the alternatives and what the
choice costs are in [ADR 0003](adr/0003-deployment-topology.md).

**The server holds real data.** Member registration (عضوگیری) started on 1405/07/10 (2026-10-02),
after the trial data was wiped. From then on the server is never wiped and nothing is restored
over it: a migration that meets old rows has to carry them forward, never refuse them. Take
`./backup.sh run` before every release, and check that the gym PC's copy (`deploy/pull-backup.ps1`)
is recent.

## Releasing

A release is one command from the development machine:

```bash
deploy/release.sh <user>@<server> --with-postgres    # --with-postgres on the first release only
```

It builds the images here, builds the EF migration bundle, copies everything over, runs the
migrations and starts the new version, then waits until every service reports healthy.

### Step by step

Releases happen **late at night**, so a problem can be fixed before the gym opens. Committing and
merging is not releasing: code can sit on `main` for days before it goes out. Each step on the
server is said out loud before it is run.

On the development machine, before the night:

1. Every test is green and there are no warnings: `dotnet test`, and in `web/` `npm test`,
   `npm run lint`, `npm run format:check`, `npm run build`.
2. `npm run e2e` (in `web/`) is green: the front desk in a real browser against the production
   images. It needs Docker and free ports 80 and 443.
3. `npm run screenshots` (in `web/`) takes the README's pictures again from the made-up gym
   (`admin seed-demo`, [ADR 0009](adr/0009-demo-data-through-the-real-handlers.md)), and the new
   `docs/screenshots/*.png` are committed. The pictures show the clock and who is inside at that
   moment, so they look most natural taken while a gym would be open.
4. Everything is committed; `release.sh` refuses a dirty working tree.

On the night:

5. Look at the server first: the running tag (`TAG` in `/opt/gym/.env`), API and Postgres healthy,
   free disk and memory.
6. For each new migration, a read-only query that shows the existing rows satisfy it (a new
   constraint must accept every row already there).
7. `./backup.sh run` on the server, copy the dump to the development machine or the gym PC, and
   check both `sha256sum` match.
8. `deploy/release.sh <user>@<server>`.
9. After it: the panel and `/health` answer 200, the API log has no errors, the row counts are what
   they were before.
10. Record the release in docs/ROADMAP.md: its tag, the previous tag, what it carries, the checks
    above and their results.

### Going back

```bash
./server.sh rollback        # on the server: start the previous version again
```

A rollback changes the application, not the database. The migrations the release added stay
applied, which is safe only if the older code can still read the schema. When it cannot (a column
the old code requires was removed, a row shape the old code does not know), the way out is a fix
forward, or restoring the backup taken before the release (*Restore* below), which loses whatever
was recorded since. Each release record in the roadmap says whether a rollback to the previous tag
is safe.

## Domains

Live since 2026-09-25, with Let's Encrypt certificates Caddy obtains and renews by itself. One
image serves six names, and which is which comes from `/opt/gym/.env`:

| Variable | Serves |
|---|---|
| `DOMAIN` | the staff's application, and nothing else. Also the API's `AllowedHosts` |
| `SITE_DOMAIN` | the members' and trainers' app (`web/dist-site`) and only the API's `/api/site`; the API's second `AllowedHosts` (roadmap 15.3b) |
| `PUBLIC_DOMAIN` | the gym's public page: static files from `web/public-site`, no API |
| `www.$PUBLIC_DOMAIN` | redirects to the public page |
| `REDIRECT_DOMAIN` | redirects to the public page |
| `www.$REDIRECT_DOMAIN` | redirects to the public page (since 15.3b) |

`https://$PUBLIC_DOMAIN/qr` and `/app` are temporary redirects (302) to the app: the address printed
under the desk's QR code (`deploy/qr/`) and the public page's «ورود / ثبت‌نام», so neither the poster
nor the page names the app's host.

The application lives on its own host (`panel.…`) rather than the bare domain. The refresh cookie
is scoped to a single host, so one canonical name is not a preference — two names would sign
staff out whenever they typed the other. Giving the application its own host also keeps the staff
login form from being what a visitor sees first, and lets only that host carry
`X-Robots-Tag: noindex`. That is separation, not security: the login rate limit, lockout,
`fail2ban` and the firewall are what protect the application.

**Changing any of those names** is one edit to `/opt/gym/.env` followed by
`docker compose -f docker-compose.prod.yml up -d`. No release, no rebuild; Caddy gets the new
certificate on its own. Changing `DOMAIN` signs every user out, so do it before opening time and
tell staff the new address first.

## The panel on a phone (13.4)

The panel installs as an app: a dark icon named «Management», full screen with no address bar.
Nothing on the server is needed; each person does this once on their own phone.

**Android, in Chrome, with a VPN on for the install only.** Chrome does not build the app itself:
it sends the address to a Google server, which makes a small Android app (a WebAPK) and sends it
back. From Iran that server does not answer, and Chrome then quietly makes a shortcut instead,
with Chrome's badge on the icon, missing from the app list and without the long-press shortcuts.
Found on the developer's Samsung phone on 1405/08/03: without a VPN, the badge; with one, the app.
1. Remove any old «مدیریت باشگاه» or «Management» icon from the home screen.
2. Turn the VPN on.
3. Open `https://panel.pasargadgymplus.ir` in Chrome, then the menu (⋮) → «Install» (the item is
   called «Install app» or «Install and create shortcut» depending on Chrome's version; never «Add
   to home screen», which always makes the badged shortcut).
4. Wait for the icon to appear, then turn the VPN off. The app works without it: only building it
   needed Google; the panel itself is on the Iranian server.
5. Check: no Chrome badge on the icon, «Management» in the app list, and a long press on the icon
   shows «ورود با کمد», «داخل باشگاه» and «اعضا».

**iPhone, in Safari** (not Chrome): Share → «Add to Home Screen» → «Add». No VPN: Safari builds
the app on the phone. Sign in once inside the app; it keeps its own sign-in, apart from Safari's.

**The desk computer**, if wanted: Chrome shows an install icon at the end of the address bar.

The app shows the same live panel as the browser, with no copy kept offline, so after a release it
is the new version the next time it opens; nobody installs again.

## Accounts from the server

The web app cannot unlock, reset or rename the Owner, so these run on the server itself (over SSH,
from `/opt/gym`), for any account:

```bash
./server.sh unlock <user>                # clear a lockout; the password stays
./server.sh set-password <user>          # asks twice, never on the command line; ends every session
./server.sh rename <user> <new-user>     # e.g. away from a guessable "Owner"
```

They run the API's own image once with `admin …` instead of the web server
(`src/Gym.Api/Admin/AdminConsole.cs`); the rules are in BUSINESS_RULES.md §1 *Lockout*. The same
console has `admin seed-demo`, which fills an empty database with a made-up gym; it refuses any
database that holds data and, in Production, anything without `Demo:Allowed`. It has no
`server.sh` command and is never run on the server.

**After a reboot** nothing has to be done by hand: every container carries
`restart: unless-stopped`, and the stack came back on its own from a deliberate `sudo reboot`.

## Backup and restore

Two copies, no cloud (ADR 0003). Every night the server dumps the database to
`/opt/gym/backups` (`gym-YYYYmmdd-HHMMSS.dump`, the newest 120 kept, about four months). The gym's
Windows computer then **pulls** the newest dump onto its own disk and onto a flash drive (the newest
30 kept). The dumps contain members' names and phone numbers: keep the flash drive somewhere safe,
and consider BitLocker To Go on it.

The database dump does not contain `/opt/gym/.env` (database password, token signing key). The
Owner keeps one copy of that file, **not** on the flash drive with the dumps.

### One-time setup on the server

Both scripts arrive with every release (`deploy/release.sh` copies them to `/opt/gym`).

```bash
# 1. Nightly dump at 03:00 Tehran time, as the user that owns /opt/gym.
crontab -e
#   CRON_TZ=Asia/Tehran
#   0 3 * * * cd /opt/gym && ./backup.sh run >> /opt/gym/backups/backup.log 2>&1

# 2. A read-only account for the gym's computer. It is not in the docker group and cannot read .env.
sudo adduser --disabled-password --gecos "" gymbackup
# The deploy user must be in the group, or backup.sh's chgrp fails and the nightly dump stops.
# Log out and in again before running ./backup.sh by hand; cron picks the group up on its own.
sudo usermod -aG gymbackup "$USER"
sudo install -d -m 700 -o gymbackup -g gymbackup /home/gymbackup/.ssh
echo 'restrict <the public key from the gym computer>' | sudo tee /home/gymbackup/.ssh/authorized_keys
sudo chown gymbackup:gymbackup /home/gymbackup/.ssh/authorized_keys
sudo chmod 600 /home/gymbackup/.ssh/authorized_keys
sudo chmod o+x /opt/gym          # lets it reach backups/ only; .env stays mode 600

# 3. /opt/gym and .env must belong to the user you deploy as (the one deploy/release.sh ssh's
#    in as). Compose reads .env as that user, and a release rewrites its TAG line.
sudo chown -R "$USER" /opt/gym && chmod 600 /opt/gym/.env
./backup.sh run                  # the first dump also fixes the group of backups/
```

### One-time setup on the gym's computer (Windows)

```powershell
# Copy deploy\pull-backup.ps1 to C:\GymBackup\, then:
ssh-keygen -t ed25519 -N '""' -f C:\GymBackup\id_ed25519      # press Enter; no passphrase, it runs unattended
icacls C:\GymBackup\id_ed25519 /inheritance:r /grant:r "$($env:USERNAME):R"   # ssh refuses a key others can read
# Put the contents of id_ed25519.pub in the server's authorized_keys (above), then accept the
# server's host key once, by hand:
ssh -i C:\GymBackup\id_ed25519 gymbackup@gym.example.ir exit

$arguments = '-NoProfile -ExecutionPolicy Bypass -File C:\GymBackup\pull-backup.ps1 ' +
  '-RemoteHost gymbackup@gym.example.ir -KeyFile C:\GymBackup\id_ed25519 ' +
  '-LocalDir C:\GymBackups -FlashDir E:\GymBackups'
$action   = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments
# This computer may be switched off at night, so it also runs at every log-on, and a missed run starts when it can.
$triggers = @((New-ScheduledTaskTrigger -Daily -At 4:30am), (New-ScheduledTaskTrigger -AtLogOn))
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -RunOnlyIfNetworkAvailable
Register-ScheduledTask -TaskName 'Gym backup pull' -Action $action -Trigger $triggers -Settings $settings
Start-ScheduledTask -TaskName 'Gym backup pull'               # try it now
```

Read the result in Task Scheduler ("Last Run Result") or in `C:\GymBackups\pull-backup.log`:
`0` fine, `1` failed or the server's newest backup is over 36 hours old (the nightly job has
stopped), `2` saved on the computer but the flash drive was not attached.

### Restore

Do this once, before you need it. A dump is only a backup if it has been restored.

```bash
# On the server, with the dump copied next to the scripts (scp it from the gym computer if needed):

# A. Check a dump in a scratch database. The live database is not touched.
./backup.sh restore-scratch gym-20260924-030001.dump      # prints the rows per table

# B. Replace the live database with a dump (the app is stopped while it runs).
./backup.sh restore gym-20260924-030001.dump --yes
```

`restore` first saves the current database as `backups/pre-restore-*.dump`, stops the API and
Caddy, recreates the database from the dump, applies any migrations the dump does not have
(`efbundle`), and starts everything again. Users, members, payments and the audit log all come
back as they were in the dump; anything after the dump's time is lost.

### If the whole server is lost

Provision a new one ([SERVER-SETUP.md](SERVER-SETUP.md)), put `.env` back, run
`deploy/release.sh <user@host> --with-postgres` from the development machine, then restore the
newest dump as in B.

## Record: the go-live wipe

Kept as a record; it must not be run again. On 2026-10-02 the trial data was removed before
member registration started. Lockers, the price list row and the eight expense categories were
seeded by migrations, which never run twice, so they were reset in place rather than truncated:

```sql
BEGIN;
TRUNCATE members, subscriptions, attendances, service_charges, payments, cafe_orders,
  cafe_order_items, products, product_categories, expenses, audit_logs,
  refresh_tokens, trusted_devices CASCADE;
DELETE FROM expense_categories WHERE id NOT IN (
  '0b3f6685-b1a2-4979-acd9-c1fe848f156b', '5e5cf0e4-0d67-4b55-830a-8d0a6919fd00',
  'b74beca9-8e56-440f-9a19-e3ab97690134', '627d2be7-d824-422a-ba2c-8d17c98633b5',
  '270c9d15-6962-4eae-a492-483541b8d516', '97813c23-681f-4e8e-84eb-fa8ecd73c3ac',
  'c4e2af4c-deb5-420e-8087-a7780b0356fd', '1dfd1cc7-0587-4096-a384-73e0449b23cd');
UPDATE lockers SET is_out_of_service = false;
UPDATE price_lists SET session_price = NULL, single_visit_price = NULL;
DELETE FROM users WHERE id NOT IN (
  SELECT ur.user_id FROM user_roles ur JOIN roles r ON r.id = ur.role_id WHERE r.name = 'Owner');
COMMIT;
```

The trial dumps from before that day were moved to `backups/trial/`, so nobody restores one by
mistake.
