#!/usr/bin/env bash
# MFG MySQL 일일 백업 → Cloudflare R2 업로드 (S22-05)
#
# 실행 위치: Lightsail 서버 (docker-compose.prod.yml과 같은 경로)
# 의존성:    docker, rclone (R2 remote "r2" 사전 구성, rclone.conf)
# cron 등록 예:  0 3 * * * /opt/mfg/server/scripts/backup-db.sh >> /var/log/mfg-backup.log 2>&1
#
# rclone 초기 설정:
#   rclone config → New remote → name: r2, type: s3, provider: Cloudflare
#   access_key_id / secret_access_key = R2 API Token
#   endpoint = https://<accountid>.r2.cloudflarestorage.com

set -euo pipefail

# ─── 설정 ───────────────────────────────────────────────
STACK_DIR="${STACK_DIR:-/opt/mfg/server}"
DB_CONTAINER="${DB_CONTAINER:-mfg-db}"
DB_NAME="${DB_NAME:-mfg}"
DB_USER="${DB_USER:-root}"
# root 비밀번호는 .env 에서 로드 (docker-compose.prod.yml과 동일한 DB_ROOT_PASSWORD 사용)
if [ -f "$STACK_DIR/.env" ]; then
  # shellcheck disable=SC1090
  set -a; . "$STACK_DIR/.env"; set +a
fi
DB_PASSWORD="${DB_ROOT_PASSWORD:-}"

R2_REMOTE="${R2_REMOTE:-r2}"
R2_BUCKET="${R2_BUCKET:-mfg-backups}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"

LOCAL_DIR="${STACK_DIR}/backups"
mkdir -p "$LOCAL_DIR"

TIMESTAMP=$(date -u +%Y%m%d-%H%M%S)
OUT="${LOCAL_DIR}/mfg-${TIMESTAMP}.sql.gz"

# ─── 백업 ───────────────────────────────────────────────
echo "[backup] mysqldump → $OUT"
docker exec -e MYSQL_PWD="$DB_PASSWORD" "$DB_CONTAINER" \
  mysqldump --single-transaction --quick --routines --triggers \
            -u "$DB_USER" "$DB_NAME" \
  | gzip -9 > "$OUT"

SIZE=$(du -h "$OUT" | cut -f1)
echo "[backup] local size=$SIZE"

# ─── R2 업로드 ──────────────────────────────────────────
echo "[backup] rclone copy → ${R2_REMOTE}:${R2_BUCKET}/"
rclone copy "$OUT" "${R2_REMOTE}:${R2_BUCKET}/" --checksum

# ─── 보존 정리 (로컬 + R2) ──────────────────────────────
echo "[backup] pruning local >${RETENTION_DAYS}d"
find "$LOCAL_DIR" -name 'mfg-*.sql.gz' -mtime "+${RETENTION_DAYS}" -delete

echo "[backup] pruning R2 >${RETENTION_DAYS}d"
rclone delete "${R2_REMOTE}:${R2_BUCKET}/" --min-age "${RETENTION_DAYS}d"

echo "[backup] done"
