#!/usr/bin/env bash
# MFG MySQL 복원 (S22-05 복원 테스트 / 재해 복구용)
#
# 사용:
#   ./restore-db.sh <백업파일|r2경로>
#   예1: ./restore-db.sh /opt/mfg/server/backups/mfg-20260415-030000.sql.gz
#   예2: ./restore-db.sh r2:mfg-backups/mfg-20260415-030000.sql.gz
#
# ⚠️ 대상 DB를 덮어쓴다. 월 1회 복원 테스트는 별도 staging DB에서 수행 권장.

set -euo pipefail

SRC="${1:-}"
if [ -z "$SRC" ]; then
  echo "usage: $0 <local-sql-gz | r2:bucket/object>"
  exit 2
fi

STACK_DIR="${STACK_DIR:-/opt/mfg/server}"
DB_CONTAINER="${DB_CONTAINER:-mfg-db}"
DB_NAME="${DB_NAME:-mfg}"
DB_USER="${DB_USER:-root}"
if [ -f "$STACK_DIR/.env" ]; then
  # shellcheck disable=SC1090
  set -a; . "$STACK_DIR/.env"; set +a
fi
DB_PASSWORD="${DB_ROOT_PASSWORD:-}"

TMP=$(mktemp --suffix=.sql.gz)
trap 'rm -f "$TMP"' EXIT

if [[ "$SRC" == r2:* || "$SRC" == *:*/* ]]; then
  echo "[restore] rclone copyto $SRC → $TMP"
  rclone copyto "$SRC" "$TMP"
else
  cp "$SRC" "$TMP"
fi

echo "[restore] gunzip + mysql ($DB_NAME)"
gunzip -c "$TMP" | docker exec -i -e MYSQL_PWD="$DB_PASSWORD" "$DB_CONTAINER" \
  mysql -u "$DB_USER" "$DB_NAME"

echo "[restore] done"
