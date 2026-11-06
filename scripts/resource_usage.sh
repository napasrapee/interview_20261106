#!/usr/bin/env bash
#
# Shows a one-time snapshot of CPU, memory, network and block I/O for each service,
# followed by the disk space used by the database, the backups and Docker itself.
#
# Usage:     ./scripts/resource_usage.sh
# Exit code: 0 all services shown, 1 a service is not running, 2 Docker unavailable
set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

SERVICES=(db api)

command -v docker >/dev/null 2>&1 || { echo "ERROR: docker command not found." >&2; exit 2; }
docker info >/dev/null 2>&1 || { echo "ERROR: Cannot reach the Docker daemon." >&2; exit 2; }

status=0
containers=()

for service in "${SERVICES[@]}"; do
  container="$(docker compose ps --status running --quiet "$service" 2>/dev/null)"
  if [[ -n "$container" ]]; then
    containers+=("$container")
  else
    echo "WARNING: service '$service' is not running." >&2
    status=1
  fi
done

if ((${#containers[@]} > 0)); then
  docker stats --no-stream \
    --format 'table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.MemPerc}}\t{{.NetIO}}\t{{.BlockIO}}' \
    "${containers[@]}" || status=1
fi

# Disk figures are informational, so failing to read them does not change the exit code.
if [[ -n "$(docker compose ps --status running --quiet db 2>/dev/null)" ]]; then
  echo
  echo "Volumes"
  docker compose exec -T db sh -c 'du -sh /var/lib/postgresql /backups' ||
    echo "WARNING: could not read volume sizes." >&2
  echo
  echo "Free space on the database volume"
  docker compose exec -T db sh -c 'df -h /var/lib/postgresql' ||
    echo "WARNING: could not read free space." >&2
fi

echo
echo "Docker disk use"
docker system df || echo "WARNING: could not read Docker disk use." >&2

exit "$status"
