#!/bin/sh
#
# Runs inside the secrets-init container on `docker compose up`. Creates the secrets
# on the first start, so `docker compose up` works on a fresh clone and the repository
# never holds a secret. Values set in .env are used instead of random ones.
#
# Each file holds one setting. The API loads the files whose names contain "__" as
# configuration keys, for example Jwt__Secret becomes Jwt:Secret.
set -eu

secrets_dir=/run/app-secrets
ready_file="$secrets_dir/.ready"
api_group=1654 # APP_UID of the dotnet/aspnet image

if [ -f "$ready_file" ]; then
  echo "Secrets already exist. Remove the volume with 'docker compose down -v' to create new ones."
  exit 0
fi

random_hex() {
  tr -dc 'a-f0-9' </dev/urandom | head -c "$1"
}

write_secret() {
  printf '%s' "$2" >"$secrets_dir/$1"
  chown "root:$api_group" "$secrets_dir/$1"
  chmod 440 "$secrets_dir/$1"
}

db_password="${POSTGRES_PASSWORD:-$(random_hex 48)}"

chown "root:$api_group" "$secrets_dir"
chmod 750 "$secrets_dir"

write_secret postgres_password "$db_password"
write_secret ConnectionStrings__Default \
  "Host=db;Port=5432;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$db_password"
write_secret Jwt__Secret "${JWT_SECRET:-$(random_hex 96)}"
write_secret Admin__Password "${ADMIN_PASSWORD:-$(random_hex 32)}"

# Written last, so an interrupted run starts again instead of leaving partial secrets.
touch "$ready_file"
echo "Secrets created."
