# Server setup — quick reference

The commands only, in order. The reasoning behind each one is in
[SERVER-SETUP.md](SERVER-SETUP.md); read that the first time, use this afterwards.

Replace `<server-ip>` throughout. Steps 2 and 3 can lock you out — **keep your first session
open until the new login is proven.**

---

## 1. Look before you change

```bash
ssh <user>@<server-ip>
cat /etc/os-release; nproc; free -h; df -h /; swapon --show; timedatectl
command -v docker ufw
```

## 2. Deploy user + SSH key

On Windows:

```powershell
ssh-keygen -t ed25519 -C "<you>@gym-deploy"
Get-Content $env:USERPROFILE\.ssh\id_ed25519.pub
```

On the server, as root:

```bash
adduser gym                       # set a real password: sudo asks for it
usermod -aG sudo gym
install -d -m 700 -o gym -g gym /home/gym/.ssh
echo 'ssh-ed25519 AAAA... <you>@gym-deploy' > /home/gym/.ssh/authorized_keys
chown gym:gym /home/gym/.ssh/authorized_keys
chmod 600 /home/gym/.ssh/authorized_keys
```

Verify in a **second** window before step 3: `ssh gym@<server-ip>` then `whoami`, `sudo -v`.

## 3. Keys only (root session still open)

```bash
cat > /etc/ssh/sshd_config.d/00-hardening.conf <<'CONF'
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
PubkeyAuthentication yes
CONF
chmod 644 /etc/ssh/sshd_config.d/00-hardening.conf
sshd -t && systemctl restart ssh
sshd -T | grep -Ei 'permitrootlogin|passwordauth|kbdinteractive|pubkeyauth'
```

Verify: `ssh gym@<server-ip> whoami` works, `ssh root@<server-ip>` is refused.

## 4. Firewall (allow 22 **before** enabling)

```bash
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443                     # no /tcp: also UDP, for HTTP/3
ufw enable
ufw status verbose
```

## 5. Updates + fail2ban

```bash
apt update && apt upgrade -y      # reboot if /var/run/reboot-required appears
apt install -y fail2ban python3-systemd
tee /etc/fail2ban/jail.local > /dev/null <<'CONF'
[DEFAULT]
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

Verify with **two** (not six) failed logins from Windows:

```powershell
ssh -o BatchMode=yes -o ConnectTimeout=5 definitelynotauser@<server-ip> exit
```

`Total failed` must move. Unban yourself: `fail2ban-client set sshd unbanip <ip>`.

## 6. Swap

```bash
findmnt -no FSTYPE /              # expect ext4
fallocate -l 2G /swapfile
chmod 600 /swapfile
mkswap /swapfile && swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
swapoff /swapfile && swapon -a    # test the fstab line before a reboot depends on it
echo 'vm.swappiness=10' > /etc/sysctl.d/99-swappiness.conf
sysctl -p /etc/sysctl.d/99-swappiness.conf
free -h
```

## 7. Clock and timezone

```bash
timedatectl set-timezone Asia/Tehran
timedatectl timesync-status       # must be synchronised: JWTs use ClockSkew = Zero
```

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
usermod -aG docker gym            # log out and back in, then: docker ps
```

## 9. DNS

One A record per domain → `<server-ip>`. No `www`, no AAAA without real IPv6, TTL 300, **no CDN
proxy**.

The nameservers at the registry must be the ones that actually hold the zone. Leave the glue-IP
fields **empty** — the nameserver names are outside your domain, so no glue is needed.

```powershell
Resolve-DnsName <domain> -Type NS -Server 1.1.1.1     # delegated to whom?
Resolve-DnsName <domain> -Type A  -Server 1.1.1.1     # must be <server-ip> exactly
```

| Answer | Broken layer |
|---|---|
| `NXDOMAIN` + the TLD's own SOA | no delegation at the registry |
| `REFUSED` from the delegated nameserver | that server does not hold the zone |
| `NOERROR`, no records | zone is fine, the record is missing |

Check the provider does not block inbound 80 (Let's Encrypt needs it):

```bash
docker run --rm -p 80:80 nginx:alpine     # curl http://<server-ip> from outside, then Ctrl+C
```

## 10. `/opt/gym` and `.env`

```bash
sudo install -d -o gym -g gym -m 755 /opt/gym
cd /opt/gym
umask 077

PG_PW="$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 40)"   # no $: compose eats it
JWT="$(openssl rand -base64 48)"
OWNER_PW="$(openssl rand -base64 32 | tr -dc 'A-Za-z0-9' | head -c 20)"

cat > /opt/gym/.env <<EOF
DOMAIN=<canonical-domain>
REDIRECT_DOMAIN=<second-domain>

POSTGRES_DB=gym
POSTGRES_USER=gym
POSTGRES_PASSWORD=$PG_PW

JWT_SIGNING_KEY=$JWT

SEED_OWNER_USERNAME=owner
SEED_OWNER_PASSWORD=$OWNER_PW

TAG=latest
GYM_SUBNET=172.28.0.0/24

POSTGRES_SHARED_BUFFERS=512MB
POSTGRES_EFFECTIVE_CACHE_SIZE=1536MB
POSTGRES_MEM_LIMIT=1536m
API_MEM_LIMIT=768m
CADDY_MEM_LIMIT=128m
EOF

chmod 600 /opt/gym/.env
echo "owner password: $OWNER_PW"
```

The memory values above are for a 4 GB server; `deploy/env.example` has the 2 GB column.

**Copy `.env` off the server and keep it safe — no backup contains it.**

## 11. Automatic security updates

```bash
apt install -y unattended-upgrades
cat /etc/apt/apt.conf.d/20auto-upgrades          # both "1"
```

---

## Final check

```bash
ssh gym@<server-ip> whoami        # gym
ssh root@<server-ip>              # Permission denied (publickey)
sudo ufw status verbose           # active; 22/tcp, 80/tcp, 443 only
timedatectl                       # System clock synchronized: yes
free -h                           # Swap: 2.0Gi
docker ps                         # works without sudo
ls -l /opt/gym/.env               # -rw------- gym gym
```

Then, from the development machine:

```bash
deploy/release.sh gym@<server-ip> --with-postgres
```
