#!/usr/bin/env bash
# Runs on the DEVELOPMENT machine (Git Bash, WSL or Linux). Builds a release and ships it.
#
#   deploy/release.sh <user@host> [--with-postgres] [--tag <tag>] [--dir <remote dir>] [--allow-dirty]
#
# What it does, in order:
#   1. builds the API and web images here (the server has 2 GB of RAM: `dotnet publish` and
#      `npm run build` do not fit beside a running Postgres),
#   2. builds the EF migration bundle for linux-x64,
#   3. saves the images to one archive and copies archive, bundle, compose file and server.sh over,
#   4. runs `server.sh release <tag>` there: load, migrate, start, wait for healthy.
#
# --with-postgres also ships postgres:18. Needed on the first deploy, and whenever the
# server cannot pull from Docker Hub. The Caddy base image is already inside gym-web.
#
# Needs: docker, dotnet with the dotnet-ef tool, ssh and scp with key-based login to the server.

set -euo pipefail

usage() {
  sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
}

HOST=""
TAG=""
REMOTE_DIR="/opt/gym"
WITH_POSTGRES=false
ALLOW_DIRTY=false

while [ $# -gt 0 ]; do
  case "$1" in
    --with-postgres) WITH_POSTGRES=true ;;
    --allow-dirty) ALLOW_DIRTY=true ;;
    --tag) TAG="${2:?--tag needs a value}"; shift ;;
    --dir) REMOTE_DIR="${2:?--dir needs a value}"; shift ;;
    -h|--help) usage ;;
    -*) echo "unknown option: $1" >&2; usage ;;
    *) [ -z "$HOST" ] && HOST="$1" || usage ;;
  esac
  shift
done
[ -n "$HOST" ] || usage

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# A release has to be reproducible from a commit, so uncommitted changes are refused.
if [ "$ALLOW_DIRTY" = false ] && [ -n "$(git status --porcelain)" ]; then
  echo "error: the working tree has uncommitted changes. Commit them, or pass --allow-dirty." >&2
  exit 1
fi

[ -n "$TAG" ] || TAG="$(date +%Y%m%d-%H%M)-$(git rev-parse --short HEAD)"
echo "==> Release $TAG to $HOST:$REMOTE_DIR"

echo "==> Building images"
docker build -f src/Gym.Api/Dockerfile -t "gym-api:$TAG" .
docker build -f web/Dockerfile -t "gym-web:$TAG" .

echo "==> Building the migration bundle (linux-x64)"
mkdir -p artifacts
dotnet ef migrations bundle --self-contained -r linux-x64 --force \
  --project src/Gym.Infrastructure --startup-project src/Gym.Api -o artifacts/efbundle

IMAGES=("gym-api:$TAG" "gym-web:$TAG")
if [ "$WITH_POSTGRES" = true ]; then
  docker image inspect postgres:18 >/dev/null 2>&1 || docker pull postgres:18
  IMAGES+=("postgres:18")
fi

echo "==> Saving images"
ARCHIVE="artifacts/images-$TAG.tar.gz"
docker save "${IMAGES[@]}" | gzip > "$ARCHIVE"

echo "==> Copying to the server"
ssh "$HOST" "mkdir -p '$REMOTE_DIR'"
# The bundle is a 140 MB single file; -C compresses it on the wire.
scp -C "$ARCHIVE" "$HOST:$REMOTE_DIR/"
scp -C artifacts/efbundle "$HOST:$REMOTE_DIR/efbundle"
scp docker-compose.prod.yml deploy/server.sh "$HOST:$REMOTE_DIR/"

echo "==> Releasing on the server"
ssh "$HOST" "cd '$REMOTE_DIR' && chmod +x server.sh && ./server.sh release '$TAG'"

echo "==> Done. If something is wrong: ssh $HOST 'cd $REMOTE_DIR && ./server.sh rollback'"
