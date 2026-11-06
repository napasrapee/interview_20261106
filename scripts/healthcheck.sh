#!/usr/bin/env bash
#
# Reports the container health of each service and calls the API health endpoint.
#
# Usage:     ./scripts/healthcheck.sh
# Settings:  API_PORT (environment, then .env, then 8080)
# Exit code: 0 everything healthy, 1 a service is not ready, 2 Docker unavailable
set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

SERVICES=(db api)

read_port() {
  if [[ -n "${API_PORT:-}" ]]; then
    printf '%s' "$API_PORT"
  elif [[ -f .env ]] && grep -q '^API_PORT=' .env; then
    grep '^API_PORT=' .env | tail -n 1 | cut -d= -f2- | tr -d '\r'
  else
    printf '8080'
  fi
}

command -v docker >/dev/null 2>&1 || { echo "ERROR: docker command not found." >&2; exit 2; }
docker info >/dev/null 2>&1 || { echo "ERROR: Cannot reach the Docker daemon." >&2; exit 2; }

status=0

for service in "${SERVICES[@]}"; do
  container="$(docker compose ps --all --quiet "$service" 2>/dev/null)"
  if [[ -z "$container" ]]; then
    state="missing"
  else
    state="$(docker inspect --format '{{.State.Status}}/{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$container")"
  fi

  if [[ "$state" == "running/healthy" ]]; then
    printf 'OK    %-8s %s\n' "$service" "$state"
  else
    printf 'FAIL  %-8s %s\n' "$service" "$state"
    status=1
  fi
done

url="http://localhost:$(read_port)/health"
http_code="$(curl --silent --output /dev/null --max-time 8 --write-out '%{http_code}' "$url")"
if [[ "$http_code" == "200" ]]; then
  printf 'OK    %-8s %s -> %s\n' "http" "$url" "$http_code"
else
  printf 'FAIL  %-8s %s -> %s\n' "http" "$url" "$http_code"
  status=1
fi

exit "$status"
