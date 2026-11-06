#!/usr/bin/env bash
#
# Dumps the database to a gzip file in the db_backups volume, verifies it,
# then removes backups older than the retention period. Safe to run repeatedly.
#
# Usage:     ./scripts/backup.sh
# Settings:  BACKUP_RETENTION_DAYS (environment, then .env, then 7)
# Exit code: 0 success, 1 backup failed or database not running,
#            2 Docker unavailable or invalid configuration
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

log() { printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"; }
fail() {
  local code="$1"
  shift
  log "ERROR: $*" >&2
  exit "$code"
}

read_retention() {
  if [[ -n "${BACKUP_RETENTION_DAYS:-}" ]]; then
    printf '%s' "$BACKUP_RETENTION_DAYS"
  elif [[ -f .env ]] && grep -q '^BACKUP_RETENTION_DAYS=' .env; then
    grep '^BACKUP_RETENTION_DAYS=' .env | tail -n 1 | cut -d= -f2- | tr -d '\r'
  else
    printf '7'
  fi
}

retention_days="$(read_retention)"
# A value of 0 would delete every earlier backup, so it is rejected along with non-numbers.
[[ "$retention_days" =~ ^[1-9][0-9]*$ ]] ||
  fail 2 "BACKUP_RETENTION_DAYS must be a positive integer, got '$retention_days'."

command -v docker >/dev/null 2>&1 || fail 2 "docker command not found."
docker info >/dev/null 2>&1 || fail 2 "Cannot reach the Docker daemon."
[[ -n "$(docker compose ps --status running --quiet db 2>/dev/null)" ]] ||
  fail 1 "Container 'db' is not running. Start it with: docker compose up -d"

log "Starting backup (retention: ${retention_days} days)"

# Runs inside the container. Writing to a temporary name means a failure never
# leaves a file that looks like a valid backup.
# shellcheck disable=SC2016  # variables must expand inside the container
if ! file="$(docker compose exec -T -e "RETENTION_DAYS=$retention_days" db sh -euc '
  set -o pipefail
  name="backup_${POSTGRES_DB}_$(date -u +%Y%m%d_%H%M%S).sql.gz"
  tmp="/backups/.${name}.$$.tmp"
  trap "rm -f \"$tmp\"" EXIT

  pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner --no-privileges | gzip -9 > "$tmp"
  gzip -t "$tmp"
  gunzip -c "$tmp" | tail -n 5 | grep -q "PostgreSQL database dump complete"
  chmod 600 "$tmp"
  mv "$tmp" "/backups/$name"

  find /backups -maxdepth 1 -type f -name "backup_*.sql.gz" \
    -mmin "+$((RETENTION_DAYS * 1440))" -print -delete >&2
  # Leftovers from runs that were killed before their cleanup could run.
  find /backups -maxdepth 1 -type f -name ".backup_*.tmp" -mmin +60 -delete
  printf "%s" "$name"
')"; then
  fail 1 "Backup failed. No new backup file was created."
fi

log "Backup complete: $file"
docker compose exec -T db sh -c 'ls -lh /backups | tail -n +2' 2>/dev/null || true
