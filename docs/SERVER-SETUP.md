# Server setup (roadmap task 6.0)

How a bare VPS becomes a host this project can be released to. Run this once per server — on
the first one, and again if the server is ever lost and has to be rebuilt.

Already done it once and just need the commands? Use
[SERVER-SETUP-QUICK.md](SERVER-SETUP-QUICK.md) instead — same steps, no explanations.

It stops where `deploy/release.sh` starts. Releasing, backups and restoring are in the README
("Deployment", "Backup and restore"); going live is roadmap task 6.4.

Every command below was run on the real server on 2026-09-24. Where reality differed from the
plan, the plan was corrected rather than the note left out.

## The server this was done on

| | |
|---|---|
| Provider | Iranian VPS, public IP |
| OS | Ubuntu 26.04.1 LTS (`resolute`) |
| CPU / RAM / disk | 2 cores, 3.8 GiB usable, 58 GB (`ext4`) |
| Domains | `pasargadgymplus.ir` (canonical), `pasargadgymplus.com` (redirects to it) |
| Deploy user | `gym` — the user `deploy/release.sh <user@host>` logs in as |

ADR 0003 was written against a 2 GB machine. This one has 4 GB, which changes only the memory
values in `.env`; see step 10.

## 1. First login, and what to check

```bash
ssh <user>@<server-ip>          # accept the host key once
cat /etc/os-release             # confirm the provider's label: Ubuntu LTS is assumed throughout
nproc; free -h; df -h /; swapon --show
timedatectl                     # "System clock synchronized" matters (see step 7)
command -v docker ufw
```

Reinstalling from the provider's panel is cheap now and expensive later, so confirm the OS
before configuring anything.

## 2. A non-root user with an SSH key

On the development machine:

```powershell
ssh-keygen -t ed25519 -C "<you>@gym-deploy"        # a passphrase is worth the ssh-agent step
Get-Content $env:USERPROFILE\.ssh\id_ed25519.pub
```

On the server, as root:

```bash
adduser gym                                        # give it a real password: sudo asks for it
usermod -aG sudo gym
install -d -m 700 -o gym -g gym /home/gym/.ssh
echo 'ssh-ed25519 AAAA... <you>@gym-deploy' > /home/gym/.ssh/authorized_keys
chown gym:gym /home/gym/.ssh/authorized_keys
chmod 600 /home/gym/.ssh/authorized_keys
```

`sshd` silently ignores a key file that others can read (StrictModes), which is the usual cause
of an unexplained "Permission denied (publickey)".

**Prove the new login works in a second window before step 3.** Keep the root session open.

`deploy/server.sh` requires `/opt/gym/.env` to be owned by this user, because a release rewrites
its `TAG` line — the deployment is built around a named non-root account.

## 3. Keys only

```bash
cat > /etc/ssh/sshd_config.d/00-hardening.conf <<'CONF'
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
PubkeyAuthentication yes
CONF
chmod 644 /etc/ssh/sshd_config.d/00-hardening.conf
sshd -t && systemctl restart ssh
sshd -T | grep -Ei 'permitrootlogin|passwordauthentication|kbdinteractive|pubkeyauthentication'
```

The file is named `00-` because in `sshd_config` the **first** value wins and the drop-in
directory is read in alphabetical order: a provider file re-enabling passwords would otherwise
sort ahead of ours. `KbdInteractiveAuthentication no` closes the PAM side door that
`PasswordAuthentication no` alone leaves open.

Check `sshd -T` — the effective, merged configuration — not the file you just wrote.

## 4. Firewall

```bash
ufw default deny incoming
ufw default allow outgoing        # apt, Let's Encrypt, and the Phase 10 SMS provider
ufw allow 22/tcp                  # deliberately not "limit": a release makes ~5 connections
ufw allow 80/tcp                  # Let's Encrypt validation, and the HTTP to HTTPS redirect
ufw allow 443                     # no /tcp: Caddy also publishes 443/udp for HTTP/3
ufw enable
ufw status verbose
```

**Docker bypasses `ufw`.** A published container port is reachable from the internet whatever
`ufw status` says, because Docker writes its rules into a chain consulted first. This is why
`docker-compose.prod.yml` gives Postgres no `ports:` at all and only Caddy publishes anything.

## 5. `fail2ban`

```bash
apt update && apt upgrade -y      # reboot if /var/run/reboot-required appears
apt install -y fail2ban python3-systemd
tee /etc/fail2ban/jail.local > /dev/null <<'CONF'
[DEFAULT]
# Ubuntu no longer installs rsyslog, so there is no /var/log/auth.log: sshd logs to the journal.
backend  = systemd
bantime  = 1h
findtime = 10m
maxretry = 5
bantime.increment = true

[sshd]
enabled = true
CONF
systemctl restart fail2ban
fail2ban-client status sshd
```

`python3-systemd` is what lets it read the journal. Without it the jail runs and sees nothing.

Honest weighting: with key-only login this is log hygiene, not a load-bearing defence. Step 3 is
the one that matters.

Verify by generating two failures (`maxretry` is 5, so stop well short of a self-ban):

```powershell
ssh -o BatchMode=yes -o ConnectTimeout=5 definitelynotauser@<server-ip> exit
```

`Total failed` counts only the **current** run of the server, so a restart resets it, and the
jail looks back only `findtime` when it starts. Zero straight after a restart means nothing. To
undo a self-ban: `fail2ban-client set sshd unbanip <ip>`.

## 6. Swap

```bash
findmnt -no FSTYPE /              # ext4 assumed; btrfs/zfs need different handling
fallocate -l 2G /swapfile
chmod 600 /swapfile               # swap holds whatever was in RAM, including secrets
mkswap /swapfile && swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
swapoff /swapfile && swapon -a    # test the fstab line now: a bad one breaks the next boot
echo 'vm.swappiness=10' > /etc/sysctl.d/99-swappiness.conf
sysctl -p /etc/sysctl.d/99-swappiness.conf
```

Swap is not extra RAM. It exists so a memory spike degrades into slowness instead of the OOM
killer choosing the largest process — which here is Postgres. 2 GB is enough for that on a 4 GB
box; more would only let a runaway process thrash for longer before dying.

`swappiness=10` keeps the database's pages in RAM until there is real pressure.

## 7. Clock and timezone

```bash
timedatectl set-timezone Asia/Tehran
timedatectl timesync-status       # a real server, a small offset
```

The clock must stay synchronised because the API validates tokens with
`ClockSkew = TimeSpan.Zero` (`JwtOptions`). Issuer and validator are the same process here, so
steady drift is harmless; what zero tolerance removes is the cushion that hides a **step**
correction after a long unsynchronised stretch. An active NTP daemon slews instead of stepping.

The host timezone does not affect the application: moments are stored as UTC `timestamptz`, and
business dates come from `IGymCalendar` using `Gym:TimeZone`. It is changed for the humans —
`backup.sh` names dumps with the host's local time (`date +%Y%m%d-%H%M%S`), so on UTC the 03:00
Tehran dump would carry the previous day's date.

## 8. Docker

```bash
apt install -y ca-certificates curl
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
  > /etc/apt/sources.list.d/docker.list
apt update
apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
usermod -aG docker gym            # then log out and back in
```

Docker's own repository, not `docker.io`, because the distribution package does not ship the
Compose v2 plugin — and `docker compose` is what every script here calls. Docker publishes a
`resolute` channel, so no codename substitution was needed.

**The `docker` group is equivalent to root**: a member can mount the host filesystem into a
container. `gym` is already a sudoer so nothing new is granted, but the `gymbackup` account
created for backups stays out of this group deliberately.

Docker Hub **is** reachable from this data centre (verified with `docker run --rm hello-world`).
Releases still ship images as a file (`docker save`/`load`), which does not depend on it.

## 9. DNS

One A record per domain, pointing at the server's IP. No `www` — a second name needs its own
certificate and redirect for no benefit here. No AAAA unless `ip -6 addr show scope global`
shows a real address; an unreachable AAAA makes browsers wait for a timeout before falling back.

Keep TTL at 300 while setting up, so a mistake expires in five minutes.

One canonical name, with the second domain redirected by Caddy (`REDIRECT_DOMAIN`): the refresh
cookie is scoped to a single host, so serving the app under both names signs staff out whenever
they type the other one.

**No CDN or proxy in front of it** (Cloudflare's orange cloud and similar). The API accepts
`X-Forwarded-For` only from the Compose network with `ForwardLimit = 1` — exactly one proxy,
Caddy. A second hop makes the CDN's edge address look like the client, which puts every user in
one login-rate-limit bucket and records the CDN as the actor in the audit log. It would also take
TLS termination away from Caddy.

Diagnosing DNS: the answer names the broken layer.

| Answer | Broken layer |
|---|---|
| `NXDOMAIN` with the **TLD's** SOA in the authority section | No delegation at the registry |
| `REFUSED` from the delegated nameserver | That server does not hold the zone ("lame delegation") |
| `NOERROR` with no answer records | Zone exists; the record is missing or misnamed |

```powershell
Resolve-DnsName <domain> -Type NS -Server 1.1.1.1     # is it delegated, and to whom?
Resolve-DnsName <domain> -Type A -Server <their-ns>   # does that server hold the zone?
Resolve-DnsName <domain> -Type A -Server 1.1.1.1      # end to end
```

`whois <domain>` shows the `nserver` lines the registry holds — note that a delegation can be
recorded in the registry's database and still be absent from the published zone.

Independently of DNS, confirm the provider does not block inbound 80 (some do until an identity
check is completed), because Let's Encrypt validates over it:

```bash
docker run --rm -p 80:80 nginx:alpine     # then curl http://<server-ip> from outside; Ctrl+C
```

## 10. `/opt/gym` and the `.env` file

```bash
sudo install -d -o gym -g gym -m 755 /opt/gym     # owned by the deploy user; 755 so the
cd /opt/gym                                       # gymbackup account can reach backups/
umask 077                                         # the file is never briefly world-readable
```

Copy `deploy/env.example` and fill it in, generating the secrets on the server:

```bash
openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 40    # POSTGRES_PASSWORD
openssl rand -base64 48                                      # JWT_SIGNING_KEY
openssl rand -base64 32 | tr -dc 'A-Za-z0-9' | head -c 20    # SEED_OWNER_PASSWORD
chmod 600 /opt/gym/.env
```

**The Postgres password is letters and digits only.** Docker Compose interpolates variables in
`.env`, so a `$` in a value silently becomes part of another variable and the database receives a
different password than the file shows. Base64 has no `$`, so the JWT key is safe as-is.

`SEED_OWNER_PASSWORD` must satisfy `PasswordPolicy`: at least 8 characters with a letter and a
digit. It is changed at the first login.

On a 4 GB server, use the 4 GB column from `deploy/env.example`:

```
POSTGRES_SHARED_BUFFERS=512MB
POSTGRES_EFFECTIVE_CACHE_SIZE=1536MB    # a hint to the planner, not an allocation
POSTGRES_MEM_LIMIT=1536m
API_MEM_LIMIT=768m
CADDY_MEM_LIMIT=128m
```

**Copy `.env` off the server and keep it somewhere safe.** No backup contains it: the nightly
dump holds the database, and without this file the database password and the token signing key
are gone. Not on the flash drive that carries the dumps — one theft should not yield both.

## 11. Automatic security updates

```bash
apt install -y unattended-upgrades
cat /etc/apt/apt.conf.d/20auto-upgrades          # both values "1"
systemctl status apt-daily.timer apt-daily-upgrade.timer --no-pager
```

Only Ubuntu's security pocket is upgraded automatically. Docker's repository is not in the
allowed origins, so `docker-ce` is never upgraded behind your back — which is what you want,
since a daemon upgrade restarts every container.

Automatic **reboot** is left off until the stack has been seen to come back from a deliberate
reboot (task 6.4). Until then, check `/var/run/reboot-required` when deploying.

## Done when

```bash
ssh gym@<server-ip> whoami        # key login works, prints "gym"
ssh root@<server-ip>              # refused: Permission denied (publickey)
sudo ufw status verbose           # active; only 22/tcp, 80/tcp, 443
timedatectl                       # System clock synchronized: yes
free -h                           # Swap: 2.0Gi
docker ps                         # works without sudo
ls -l /opt/gym/.env               # -rw------- gym gym
```

The server is then ready for `deploy/release.sh gym@<server-ip> --with-postgres`.
