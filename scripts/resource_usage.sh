#!/usr/bin/env bash
#
# Shows a one-time snapshot of CPU, memory, network and disk use for each service.
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

exit "$status"
