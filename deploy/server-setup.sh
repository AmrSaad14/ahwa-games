#!/usr/bin/env bash
# Runs on the game server (Ubuntu) as root, from the "Deploy server" workflow: the first time it installs
# everything; every time after, it rebuilds and restarts the game with the new code. Safe to run again.
#   /opt/board-battles/app        the code (replaced on every deploy)
#   /opt/board-battles/env        the settings and secrets (from the SERVER_ENV secret, plus what is generated here)
#   /opt/board-battles/backups    a copy of the database every night, the last 14 kept
#   docker volume board-battles_domino-data   the live database (survives every deploy)
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
BASE=/opt/board-battles
cd "$BASE"

# ---- the first time: Docker, sqlite3 for backups, a firewall, a little swap for building on a small server ----
if ! command -v docker >/dev/null 2>&1; then
  echo "== installing Docker"
  apt-get update -q
  apt-get install -y -q ca-certificates curl sqlite3 ufw
  curl -fsSL https://get.docker.com | sh
  systemctl enable --now docker
fi
command -v sqlite3 >/dev/null 2>&1 || apt-get install -y -q sqlite3
if ! swapon --show | grep -q .; then
  echo "== adding 2 GB of swap"
  fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile >/dev/null && swapon /swapfile
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi
if command -v ufw >/dev/null 2>&1 && ! ufw status | grep -q "Status: active"; then
  echo "== firewall: ssh, web, voice relay"
  ufw allow OpenSSH >/dev/null; ufw allow 80/tcp >/dev/null; ufw allow 443 >/dev/null
  ufw allow 3478 >/dev/null; ufw allow 49160:49200/udp >/dev/null
  ufw --force enable >/dev/null
fi

# ---- settings: the ones from GitHub, plus the server's own secrets generated once and kept ----
touch env.secrets && chmod 600 env.secrets
keep() { grep -q "^$1=" env.secrets || echo "$1=$(openssl rand -hex 32)" >> env.secrets; }
keep TURN_SECRET
keep ADMIN_KEY
{
  echo "DOMAIN=${DOMAIN}"
  echo "SERVER_IP=$(curl -4 -s -m 5 https://api.ipify.org || hostname -I | awk '{print $1}')"
  cat env.secrets
  [ -f env.github ] && grep -v '^DOMAIN=' env.github   # what is set in GitHub wins over the generated ones
  echo "DOMAIN=${DOMAIN}"   # the address from the workflow, always (PUBLIC_URL and the voice relay come from it)
} > app/.env
chmod 600 app/.env env.github 2>/dev/null || true

# ---- build and start ----
cd app
echo "== building and starting the game"
docker compose -p board-battles up -d --build --remove-orphans
docker image prune -f >/dev/null

# ---- the nightly database copy ----
cat > /etc/cron.daily/board-battles-backup <<'CRON'
#!/bin/sh
DB=$(docker volume inspect board-battles_domino-data -f '{{.Mountpoint}}')/domino.db
mkdir -p /opt/board-battles/backups
[ -f "$DB" ] && sqlite3 "$DB" ".backup /opt/board-battles/backups/domino-$(date +%F).db" && gzip -f /opt/board-battles/backups/domino-$(date +%F).db
ls -1t /opt/board-battles/backups/domino-*.db.gz 2>/dev/null | tail -n +15 | xargs -r rm -f
CRON
chmod +x /etc/cron.daily/board-battles-backup

# ---- is it up? ----
echo "== waiting for the game to answer"
for i in $(seq 1 40); do
  if docker compose -p board-battles exec -T domino wget -qO- http://127.0.0.1:8080/healthz >/dev/null 2>&1; then
    echo "== the game server is up"; docker compose -p board-battles ps --format '{{.Service}}: {{.Status}}'; exit 0
  fi
  sleep 3
done
echo "!! the game server did not answer; its last lines:"; docker compose -p board-battles logs --tail 60 domino; exit 1
