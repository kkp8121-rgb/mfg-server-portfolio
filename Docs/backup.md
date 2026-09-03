---
read_count: 0
last_read: "never"
status: active
---

# MFG DB 백업/복원 런북 (S22-05)

> **목표**: 일 1회 자동 오프사이트 백업 + 월 1회 복원 테스트 + 주 1회 Lightsail 스냅샷 다중화.

## 3-Tier 백업

| 계층 | 주기 | 저장소 | 보존 | 용도 |
|------|------|--------|------|------|
| **L1 Lightsail 스냅샷** | 주 1회 (콘솔 자동) | AWS Lightsail | 4주 | 인스턴스 + 디스크 전체 롤백 |
| **L2 mysqldump → R2** | 일 1회 (cron 03:00 UTC) | Cloudflare R2 | 30일 | 테이블 수준 복원, 오프사이트 |
| **L3 로컬 스냅샷** | 일 1회 (backup-db.sh) | Lightsail 디스크 `backups/` | 30일 | 빠른 복원 (R2 미사용 시) |

## L1 — Lightsail 스냅샷

Lightsail 콘솔 → 인스턴스 → Snapshots 탭 → **Enable automatic snapshots** 토글 ON.
비용: 인스턴스 요금의 약 5% (무시 수준). 최신 7개 자동 보존.

## L2 — mysqldump → R2 (자동화)

### 사전 준비

1. **R2 버킷** (Cloudflare 대시보드 → R2 → Create bucket): `mfg-backups`
2. **R2 API Token** (R2 → Manage API Tokens): Object Read/Write 권한
3. **rclone 설치** (Lightsail):
   ```bash
   sudo apt install -y rclone
   rclone config    # New remote → name=r2, type=s3, provider=Cloudflare
                    # access_key_id / secret_access_key = R2 토큰
                    # endpoint = https://<accountid>.r2.cloudflarestorage.com
   ```
4. **테스트**: `rclone ls r2:mfg-backups` → 에러 없으면 OK

### cron 등록

```bash
# crontab -e
0 3 * * * /opt/mfg/server/scripts/backup-db.sh >> /var/log/mfg-backup.log 2>&1
```

매일 UTC 03:00 (KST 12:00) 실행. 로그는 `/var/log/mfg-backup.log`.

### 스크립트 동작

`scripts/backup-db.sh`:
1. `docker exec mfg-db mysqldump` — `--single-transaction` (온라인 일관성)
2. gzip 압축 → `backups/mfg-YYYYMMDD-HHMMSS.sql.gz`
3. `rclone copy` → `r2:mfg-backups/`
4. 로컬 + R2 모두 30일 초과 파일 자동 삭제

### 예상 크기

초기 DAU < 1K 기준 dump ≤ 10MB (gzip). R2 요금:
- 저장: $0.015 / GB·월 — 30일치 300MB ≈ $0.005/월
- 쓰기 A 클래스: $4.50 / 100만 — 월 30회 무시
- **복원 시 egress 무료** (R2의 핵심 장점)

## L3 — 로컬 스냅샷

`backup-db.sh`가 R2 업로드와 동시에 `$STACK_DIR/backups/`에도 보관. 네트워크 장애/R2 장애 시 즉시 복원.

## 복원 (`restore-db.sh`)

### 일상 복원 (동일 인스턴스)

```bash
cd /opt/mfg/server
./scripts/restore-db.sh backups/mfg-20260415-030000.sql.gz
```

### 재해 복구 (신규 인스턴스에서 R2 다이렉트)

```bash
# 새 Lightsail 인스턴스 + docker compose 기동 후
rclone config    # r2 remote 재구성
./scripts/restore-db.sh r2:mfg-backups/mfg-20260415-030000.sql.gz
```

## 월 1회 복원 테스트 (필수)

매월 1일 수동 실행:

```bash
# 1. 최신 R2 백업 하나 선택
BACKUP=$(rclone lsf r2:mfg-backups/ | sort | tail -1)
echo "testing restore of: $BACKUP"

# 2. staging DB 컨테이너 임시 기동 (3307 포트)
docker run --rm -d --name mfg-db-staging -p 3307:3306 \
  -e MYSQL_ROOT_PASSWORD=staging \
  -e MYSQL_DATABASE=mfg_restore \
  mysql:8.0
sleep 15

# 3. 복원
rclone copyto "r2:mfg-backups/$BACKUP" /tmp/restore.sql.gz
gunzip -c /tmp/restore.sql.gz | docker exec -i mfg-db-staging \
  mysql -u root -pstaging mfg_restore

# 4. sanity check — players 테이블 존재 + row count 비교
docker exec mfg-db-staging mysql -u root -pstaging -e \
  "SELECT COUNT(*) FROM mfg_restore.players;"

# 5. 정리
docker stop mfg-db-staging
```

결과는 `/var/log/mfg-restore-test.log`에 기록. 실패 시 Slack `#mfg-alerts`로 수동 통보.

## 체크리스트

- [ ] Lightsail 자동 스냅샷 ON
- [ ] R2 버킷 `mfg-backups` 생성 + API Token 발급
- [ ] Lightsail에 rclone 설치 + r2 remote 구성
- [ ] `scripts/backup-db.sh` 실행 권한 (`chmod +x`)
- [ ] cron 등록 + 1회 수동 실행으로 성공 확인
- [ ] `/var/log/mfg-backup.log` rotate (logrotate 7d)
- [ ] 월 1회 복원 테스트 자동화 (optional: 별도 cron `0 4 1 * *`)
