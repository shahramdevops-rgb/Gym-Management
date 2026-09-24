#!/usr/bin/env bash
# Runs ON THE SERVER, from the directory that holds docker-compose.prod.yml and .env
# (/opt/gym). deploy/release.sh copies it there and calls it; you can also run it by hand.
#
#   ./server.sh release <tag>   load the release's images, migrate, start the new version
#   ./server.sh rollback        start the previous version again
#
# Set GYM_DIR to run it against another directory (the local rehearsal does).

set -euo pipefail

GYM_DIR="${GYM_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)}"
cd "$GYM_DIR"

COMPOSE=(docker compose -f docker-compose.prod.yml)
PREVIOUS_TAG_FILE=".previous_tag"

die() { echo "error: $*" >&2; exit 1; }

# .env holds the database password and the token signing key. Docker Compose reads it as the
# user running this script, and set_tag rewrites the TAG line in it, so that user has to own it:
# a root-owned .env fails here rather than half-way through a release.
check_env_file() {
  [ -f .env ] || die ".env is missing in $GYM_DIR (copy deploy/env.example and fill it in)."

  local mode
  mode="$(stat -c '%a' .env)"
  # Only the owner may see it. 600 and 400 both pass; 640 and 644 do not.
  [ "${mode: -2}" = "00" ] || die ".env has mode $mode, so others can read your secrets. Run: chmod 600 .env"

  [ -r .env ] || die ".env is not readable by $(id -un). Run: sudo chown $(id -un) $GYM_DIR/.env"
  [ -w .env ] || die ".env is not writable by $(id -un), and a release rewrites its TAG line. Run: sudo chown $(id -un) $GYM_DIR/.env && chmod 600 $GYM_DIR/.env"
}

current_tag() { sed -n 's/^TAG=//p' .env | tail -n 1; }

set_tag() {
  if grep -q '^TAG=' .env; then
    sed -i "s|^TAG=.*|TAG=$1|" .env
  else
    printf '\nTAG=%s\n' "$1" >> .env
  fi
}

# Recreates only what changed, and waits until every service with a health check reports healthy.
start_stack() {
  "${COMPOSE[@]}" up -d --wait --wait-timeout 180
}

release() {
  local tag="${1:-}"
  [ -n "$tag" ] || die "usage: server.sh release <tag>"
  check_env_file
  [ -f efbundle ] || die "efbundle is missing in $GYM_DIR."
  chmod 755 efbundle

  if [ -f "images-$tag.tar.gz" ]; then
    echo "==> Loading images for $tag"
    gunzip -c "images-$tag.tar.gz" | docker load
    rm -f "images-$tag.tar.gz"
  fi
  docker image inspect "gym-api:$tag" >/dev/null 2>&1 || die "image gym-api:$tag is not on this server."
  docker image inspect "gym-web:$tag" >/dev/null 2>&1 || die "image gym-web:$tag is not on this server."

  local old
  old="$(current_tag)"
  # Set first, so the migrate service below runs this release's image and bundle.
  set_tag "$tag"

  # Migrations run before the new API starts and never at application startup. If this fails
  # the old containers are still running and the new tag has not been started.
  echo "==> Applying migrations with the release's bundle"
  if ! "${COMPOSE[@]}" run --rm migrate; then
    [ -n "$old" ] && set_tag "$old"
    die "migration failed; the previous version is still running and TAG is back to '${old:-none}'."
  fi

  # Recorded only now: a release that never got past its migration is not something to roll
  # back to, and .previous_tag would otherwise name the version that is still running. Only a
  # tag whose image is actually here can be a target — on the first release .env still holds
  # the placeholder from env.example.
  if [ -n "$old" ] && [ "$old" != "$tag" ] && docker image inspect "gym-api:$old" >/dev/null 2>&1; then
    echo "$old" > "$PREVIOUS_TAG_FILE"
  fi

  echo "==> Starting $tag"
  if ! start_stack; then
    echo "The new version did not become healthy. Logs:" >&2
    "${COMPOSE[@]}" logs --tail 40 api >&2 || true
    die "start failed. To go back to '${old:-the previous tag}': ./server.sh rollback"
  fi

  prune_old_images
  echo "==> Released $tag (previous: ${old:-none})"
}

rollback() {
  check_env_file
  [ -f "$PREVIOUS_TAG_FILE" ] || die "there is no previous release recorded ($PREVIOUS_TAG_FILE)."

  local target current
  target="$(cat "$PREVIOUS_TAG_FILE")"
  current="$(current_tag)"
  docker image inspect "gym-api:$target" >/dev/null 2>&1 || die "image gym-api:$target is no longer on this server."

  echo "==> Rolling back $current -> $target"
  set_tag "$target"
  echo "$current" > "$PREVIOUS_TAG_FILE"
  start_stack || die "the rolled-back version did not become healthy either."

  echo "==> Running $target."
  echo "Note: a rollback changes the application, not the database. Migrations that release added"
  echo "stay applied. That is safe only if they are compatible with the older code; if not,"
  echo "restore the last backup (docs, task 6.3)."
}

# Keeps the three newest images of each kind. The running and previous releases are always
# among them, and Docker refuses to remove an image a container uses.
prune_old_images() {
  local repo
  for repo in gym-api gym-web; do
    docker images "$repo" --format '{{.Tag}}' | tail -n +4 | while read -r old_tag; do
      docker rmi "$repo:$old_tag" >/dev/null 2>&1 || true
    done
  done
}

case "${1:-}" in
  release) shift; release "$@" ;;
  rollback) rollback ;;
  *) die "usage: server.sh release <tag> | rollback" ;;
esac
