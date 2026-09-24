#!/usr/bin/env bash
# Runs ON THE SERVER, from the directory that holds docker-compose.prod.yml and .env (/opt/gym).
# deploy/release.sh copies it there.
#
#   ./backup.sh run                          take a dump now (cron runs this every night)
#   ./backup.sh restore <file> --yes         REPLACE the live database with a dump
#   ./backup.sh restore-scratch <file> [db]  load a dump into a separate scratch database and
#                                            show row counts; the live database is not touched
#
# Dumps are pg_dump custom-format files in ./backups (gym-YYYYmmdd-HHMMSS.dump). The newest 60
# are kept. The gym's computer pulls them off this server (deploy/pull-backup.ps1).
#
# Set GYM_DIR to run it against another directory, BACKUP_KEEP to keep a different number.

# -E so the ERR trap in restore() is inherited by functions. Without it the trap never fires,
# because the command that fails there is db(), a function, and the operator loses the one
# message that says where the pre-restore dump is.
set -Eeuo pipefail

GYM_DIR="${GYM_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)}"
cd "$GYM_DIR"

BACKUP_DIR="$GYM_DIR/backups"
KEEP="${BACKUP_KEEP:-60}"
COMPOSE=(docker compose -f docker-compose.prod.yml)

die() { echo "error: $*" >&2; exit 1; }

# Runs a command inside the Postgres container. The image's own POSTGRES_USER / POSTGRES_DB
# are used, and local connections inside the container need no password, so no secret is
# ever on a command line.
db() { "${COMPOSE[@]}" exec -T postgres "$@"; }

prepare_dir() {
  mkdir -p "$BACKUP_DIR"
  chmod 750 "$BACKUP_DIR"
  # The read-only account that the gym's computer logs in as (see README) reads through this group.
  if getent group gymbackup >/dev/null 2>&1; then
    chgrp gymbackup "$BACKUP_DIR"
  fi
}

# A dump that pg_restore cannot even list is worth nothing, so every dump is checked before it
# is allowed to become a backup.
verify_dump() {
  db pg_restore --list < "$1" > /dev/null 2>&1 || return 1
}

dump_to() {
  db sh -c 'pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB"' > "$1"
}

run() {
  prepare_dir
  local stamp tmp final
  stamp="$(date +%Y%m%d-%H%M%S)"
  # Written under a name the pull script never matches, then renamed: a half-written file can
  # never be picked up as if it were a finished backup.
  tmp="$BACKUP_DIR/.gym-$stamp.dump.partial"
  final="$BACKUP_DIR/gym-$stamp.dump"
  trap 'rm -f "$tmp"' EXIT

  dump_to "$tmp" || die "pg_dump failed."
  [ -s "$tmp" ] || die "the dump is empty."
  verify_dump "$tmp" || die "the dump cannot be read back by pg_restore; it was not kept."

  chmod 640 "$tmp"
  if getent group gymbackup >/dev/null 2>&1; then
    chgrp gymbackup "$tmp"
  fi
  mv "$tmp" "$final"
  trap - EXIT

  rotate
  echo "$(date '+%F %T') backup ok: $final ($(stat -c %s "$final") bytes)"
}

# Keeps the newest $KEEP dumps. Only gym-*.dump files are ever removed, so the safety dumps
# that restore takes (pre-restore-*) and anything else in the directory are left alone.
rotate() {
  # "|| true": with no dumps at all the glob stays literal and ls exits non-zero, which
  # pipefail would turn into a failed backup run.
  { ls -1t "$BACKUP_DIR"/gym-*.dump 2>/dev/null || true; } | tail -n +"$((KEEP + 1))" | while read -r old; do
    rm -f "$old"
  done
}

restore() {
  local file="${1:-}" confirm="${2:-}"
  [ -n "$file" ] || die "usage: backup.sh restore <file> --yes"
  [ -f "$file" ] || die "no such file: $file"
  [ "$confirm" = "--yes" ] || die "restore REPLACES the live database with $file. Run it again with --yes if that is what you want."
  [ -f .env ] || die ".env is missing in $GYM_DIR."
  verify_dump "$file" || die "$file is not a readable pg_dump custom-format file."

  prepare_dir
  local safety="$BACKUP_DIR/pre-restore-$(date +%Y%m%d-%H%M%S).dump"
  echo "==> Saving the current database first: $safety"
  dump_to "$safety" || die "could not take the safety dump; nothing was changed."
  verify_dump "$safety" || die "the safety dump is unreadable; nothing was changed."

  echo "==> Stopping the application"
  "${COMPOSE[@]}" stop caddy api

  echo "==> Replacing the database"
  # The whole failure window is between DROP and the end of pg_restore. If it fails, the
  # database is empty or partial, the application is still stopped, and $safety holds what
  # was there before.
  trap 'echo "The restore did not finish. The database may be incomplete and the application is
still stopped. What was there before this restore is in '"$safety"'; put it back with:
  ./backup.sh restore '"$safety"' --yes" >&2' ERR
  db sh -c 'psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 \
    -c "DROP DATABASE IF EXISTS \"$POSTGRES_DB\" WITH (FORCE)" \
    -c "CREATE DATABASE \"$POSTGRES_DB\""'
  db sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner --exit-on-error' < "$file"

  # From here the data is in. What is left can only fail to bring the application back, which
  # needs a different answer from the operator, so the message changes with the phase.
  trap 'echo "The data from '"$file"' is restored, but the application did not come back up.
Look at the error above, then: docker compose -f docker-compose.prod.yml up -d --wait" >&2' ERR

  # A dump from before a release does not have that release's migrations. The bundle is a
  # no-op when nothing is pending, so it always runs when it is there.
  if [ -f efbundle ]; then
    echo "==> Applying any migrations the dump does not have yet"
    "${COMPOSE[@]}" run --rm migrate
  fi

  echo "==> Starting the application"
  "${COMPOSE[@]}" up -d --wait --wait-timeout 180
  trap - ERR
  echo "==> Restored from $file. The state before the restore is in $safety."
}

restore_scratch() {
  local file="${1:-}" scratch="${2:-gym_scratch}"
  [ -n "$file" ] || die "usage: backup.sh restore-scratch <file> [database]"
  [ -f "$file" ] || die "no such file: $file"
  [[ "$scratch" =~ ^[a-z_][a-z0-9_]*$ ]] || die "database name must be lower-case letters, digits and underscores."
  verify_dump "$file" || die "$file is not a readable pg_dump custom-format file."

  local live
  live="$(db sh -c 'printf %s "$POSTGRES_DB"')"
  [ "$scratch" != "$live" ] || die "'$scratch' is the live database; pick another name."

  echo "==> Loading $file into '$scratch' (the live database '$live' is not touched)"
  db sh -c "psql -U \"\$POSTGRES_USER\" -d postgres -v ON_ERROR_STOP=1 \
    -c 'DROP DATABASE IF EXISTS $scratch WITH (FORCE)' -c 'CREATE DATABASE $scratch'"
  db sh -c "pg_restore -U \"\$POSTGRES_USER\" -d $scratch --no-owner --exit-on-error" < "$file"

  echo "==> Rows per table in '$scratch'"
  db sh -c "psql -U \"\$POSTGRES_USER\" -d $scratch -At -F ' ' -c \"
    select table_name, (xpath('/row/c/text()', query_to_xml(
      format('select count(*) as c from %I.%I', table_schema, table_name), false, true, '')))[1]::text
    from information_schema.tables
    where table_schema in ('public', 'hangfire') and table_type = 'BASE TABLE'
    order by table_schema, table_name\""
  echo "==> Drop it when you are done: docker compose -f docker-compose.prod.yml exec postgres sh -c 'psql -U \"\$POSTGRES_USER\" -d postgres -c \"DROP DATABASE $scratch\"'"
}

case "${1:-}" in
  run) run ;;
  restore) shift; restore "$@" ;;
  restore-scratch) shift; restore_scratch "$@" ;;
  *) die "usage: backup.sh run | restore <file> --yes | restore-scratch <file> [database]" ;;
esac
