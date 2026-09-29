#!/usr/bin/env bash
# Deploys the database from scratch or upgrades it in place. Every script is re-runnable.
# Used by docker compose (db-init service), the integration tests and CI, so all environments
# are built the same way.
#
# Required env: MSSQL_SA_PASSWORD, APP_DB_USER, APP_DB_PASSWORD
# Optional env: DB_SERVER (default localhost), DB_NAME (default ShipManagement),
#               SEED_SAMPLE_DATA (default 1), ROTATE_DEV_KEYS (default 0), SQLCMD (path to sqlcmd)
set -euo pipefail
shopt -s nullglob

: "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD is required}"
: "${APP_DB_USER:?APP_DB_USER is required}"
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD is required}"

DB_SERVER="${DB_SERVER:-localhost}"
DB_NAME="${DB_NAME:-ShipManagement}"
SEED_SAMPLE_DATA="${SEED_SAMPLE_DATA:-1}"
ROTATE_DEV_KEYS="${ROTATE_DEV_KEYS:-0}"
SQLCMD="${SQLCMD:-/opt/mssql-tools18/bin/sqlcmd}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Password via environment, never on the command line (not visible in the process list).
export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"

run_script() {
  local database="$1" file="$2"
  echo ">> [$database] ${file#"$SCRIPT_DIR"/}"
  # -b abort on error, -I QUOTED_IDENTIFIER ON (required for filtered indexes),
  # -f 65001 UTF-8 input (multinational names), -C trust the local dev certificate.
  "$SQLCMD" -S "$DB_SERVER" -U sa -d "$database" -C -b -I -f 65001 -i "$file" \
    -v DB_NAME="$DB_NAME" APP_DB_USER="$APP_DB_USER" APP_DB_PASSWORD="$APP_DB_PASSWORD" \
    ROTATE_DEV_KEYS="$ROTATE_DEV_KEYS"
}

run_script master "$SCRIPT_DIR/00_create_database.sql"
for f in 01_schemas.sql 02_reference_tables.sql 03_core_tables.sql 04_finance_tables.sql 05_functions.sql; do
  run_script "$DB_NAME" "$SCRIPT_DIR/$f"
done
# Internal [dbo] procedures first: the [app] procedures call them.
for f in "$SCRIPT_DIR"/procedures/dbo.*.sql "$SCRIPT_DIR"/procedures/app.*.sql; do
  run_script "$DB_NAME" "$f"
done
run_script "$DB_NAME" "$SCRIPT_DIR/06_security.sql"
run_script "$DB_NAME" "$SCRIPT_DIR/07_seed_reference.sql"
if [[ "$SEED_SAMPLE_DATA" == "1" ]]; then
  run_script "$DB_NAME" "$SCRIPT_DIR/08_sample_data.sql"
  run_script "$DB_NAME" "$SCRIPT_DIR/tests/verify_sample_data.sql"
  # Random local development API keys, printed once (never stored in the repository).
  run_script "$DB_NAME" "$SCRIPT_DIR/09_dev_api_keys.sql"
fi
echo ">> Database '$DB_NAME' deployed successfully."
