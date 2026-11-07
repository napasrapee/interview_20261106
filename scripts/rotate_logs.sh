#!/usr/bin/env bash
#
# Keeps logs from filling the disk. Safe to run repeatedly, for example hourly from cron.
#
# Usage:
#   ./scripts/rotate_logs.sh             Rotate logs/*.log and delete old system_logs rows
#   ./scripts/rotate_logs.sh --simulate  Demonstrate rotation with generated logs; real logs
#                                        and data are not touched
#
# Settings:  LOG_RETENTION_DAYS (environment, then .env, then 30)
# Exit code: 0 success, 1 rotation failed or database not running,
#            2 logrotate or Docker unavailable, or invalid usage or configuration
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

readonly MEBIBYTE=1048576
readonly SIMULATED_ROUNDS=9
readonly SIMULATED_CHUNK=$((15 * MEBIBYTE))
readonly EXPECTED_ROTATIONS=7
readonly CONTAINER_LOG_WRITTEN=$((40 * MEBIBYTE))
readonly CONTAINER_LOG_LIMIT=$((30 * MEBIBYTE))
readonly SIMULATOR_CONTAINER=analytics-log-simulator

log() { printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"; }
fail() {
  local code="$1"
  shift
  log "ERROR: $*" >&2
  exit "$code"
}

read_retention() {
  if [[ -n "${LOG_RETENTION_DAYS:-}" ]]; then
    printf '%s' "$LOG_RETENTION_DAYS"
  elif [[ -f .env ]] && grep -q '^LOG_RETENTION_DAYS=' .env; then
    grep '^LOG_RETENTION_DAYS=' .env | tail -n 1 | cut -d= -f2- | tr -d '\r'
  else
    printf '30'
  fi
}

require_docker() {
  command -v docker >/dev/null 2>&1 || fail 2 "docker command not found."
  docker info >/dev/null 2>&1 || fail 2 "Cannot reach the Docker daemon."
}

# cron's default PATH does not include /usr/sbin.
find_logrotate() {
  local path
  path="$(command -v logrotate 2>/dev/null || true)"
  [[ -z "$path" && -x /usr/sbin/logrotate ]] && path=/usr/sbin/logrotate
  [[ -n "$path" ]] || fail 2 "logrotate not found. Install it, for example: sudo apt-get install logrotate"
  printf '%s' "$path"
}

size_in_mib() { printf '%d' $(($(wc -c <"$1") / MEBIBYTE)); }

rotate_real_logs() {
  local logrotate retention deleted
  retention="$(read_retention)"
  [[ "$retention" =~ ^[1-9][0-9]*$ ]] ||
    fail 2 "LOG_RETENTION_DAYS must be a positive integer, got '$retention'."
  logrotate="$(find_logrotate)"
  require_docker

  mkdir -p logs
  # logrotate run as root ignores a config that others can write, which is the case on /mnt
  # drives in WSL. A private copy with mode 644 works for every user.
  config_copy="$(mktemp)"
  trap 'rm -f "$config_copy"' EXIT
  cp config/logrotate.conf "$config_copy"
  chmod 644 "$config_copy"
  "$logrotate" --state logs/.logrotate.state "$config_copy" ||
    fail 1 "logrotate failed."
  log "Rotated logs/*.log"

  [[ -n "$(docker compose ps --status running --quiet db 2>/dev/null)" ]] ||
    fail 1 "Container 'db' is not running, so old system_logs rows were not deleted."

  # shellcheck disable=SC2016  # variables must expand inside the container
  deleted="$(docker compose exec -T -e "RETENTION_DAYS=$retention" db sh -c '
    psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -v ON_ERROR_STOP=1 -c "
      DELETE FROM system_logs WHERE created_at < now() - make_interval(days => $RETENTION_DAYS)"
  ')" || fail 1 "Could not delete old system_logs rows."
  log "system_logs older than $retention days: ${deleted:-DELETE 0}"
}

simulate_log_files() {
  local logrotate before after rotated disk written=0 round
  logrotate="$(find_logrotate)"
  simulation_dir="$(mktemp -d)"
  trap 'rm -rf "$simulation_dir"' EXIT

  mkdir "$simulation_dir/logs"
  cp config/logrotate.conf "$simulation_dir/logrotate.conf"
  chmod 644 "$simulation_dir/logrotate.conf"
  printf 'below the size limit\n' >"$simulation_dir/logs/small.log"

  echo "1. Log files: writes $((SIMULATED_CHUNK / MEBIBYTE)) MiB each round, rotation limit is 10 MiB"
  printf '   %-6s %-14s %-13s %-14s %s\n' Round 'Before (MiB)' 'After (MiB)' 'Rotated files' 'On disk (MiB)'
  for ((round = 1; round <= SIMULATED_ROUNDS; round++)); do
    # head closing the pipe makes base64 exit with SIGPIPE, which is expected here.
    base64 /dev/urandom | head -c "$SIMULATED_CHUNK" >>"$simulation_dir/logs/app.log" || true
    written=$((written + SIMULATED_CHUNK))
    before="$(size_in_mib "$simulation_dir/logs/app.log")"
    (cd "$simulation_dir" && "$logrotate" --state logrotate.state logrotate.conf) ||
      fail 1 "logrotate failed."
    after="$(size_in_mib "$simulation_dir/logs/app.log")"
    rotated="$(find "$simulation_dir/logs" -name 'app.log.*.gz' | wc -l | tr -d ' ')"
    disk="$(($(du -sk "$simulation_dir/logs" | cut -f1) / 1024))"
    printf '   %-6s %-14s %-13s %-14s %s\n' "$round" "$before" "$after" "$rotated" "$disk"
  done

  [[ "$rotated" -eq "$EXPECTED_ROTATIONS" ]] ||
    fail 1 "Expected $EXPECTED_ROTATIONS rotated files, found $rotated."
  [[ "$after" -eq 0 ]] || fail 1 "The active log was not truncated."
  [[ ! -e "$simulation_dir/logs/small.log.1.gz" ]] || fail 1 "A file below the limit was rotated."
  [[ "$disk" -lt $((written / MEBIBYTE)) ]] || fail 1 "Rotation did not reduce disk use."
  echo "   Result: wrote $((written / MEBIBYTE)) MiB, kept $disk MiB. Files below the limit were left alone."
}

simulate_container_logs() {
  local retained
  require_docker
  docker rm -f "$SIMULATOR_CONTAINER" >/dev/null 2>&1 || true
  trap 'docker rm -f "$SIMULATOR_CONTAINER" >/dev/null 2>&1 || true; rm -rf "$simulation_dir"' EXIT

  echo
  echo "2. Container logs: writes $((CONTAINER_LOG_WRITTEN / MEBIBYTE)) MiB with the logging options in docker-compose.yml"
  docker compose run -d -T --name "$SIMULATOR_CONTAINER" log-simulator >/dev/null 2>&1 ||
    fail 1 "Could not start the log simulator."
  docker wait "$SIMULATOR_CONTAINER" >/dev/null
  retained="$(docker logs "$SIMULATOR_CONTAINER" 2>&1 | wc -c | tr -d ' ')"

  [[ "$retained" -gt 0 && "$retained" -le "$CONTAINER_LOG_LIMIT" ]] ||
    fail 1 "Container kept $retained bytes of logs, above the limit of $CONTAINER_LOG_LIMIT."
  echo "   Result: wrote $((CONTAINER_LOG_WRITTEN / MEBIBYTE)) MiB, Docker kept $((retained / MEBIBYTE)) MiB (limit: 3 files of 10 MiB)."
}

case "${1:-}" in
  "")
    rotate_real_logs
    ;;
  --simulate)
    simulate_log_files
    simulate_container_logs
    echo
    log "Simulation passed."
    ;;
  *)
    fail 2 "Unknown option '$1'. Usage: $0 [--simulate]"
    ;;
esac
