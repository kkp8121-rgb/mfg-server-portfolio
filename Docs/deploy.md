---
read_count: 0
last_read: "never"
status: active
---

# MFG 서버 배포 런북 (AWS Lightsail)

> **대상**: 운영자(사용자)가 SSH로 Lightsail 인스턴스에 접속해 MFG 백엔드를 배포/갱신할 때 참고.
> **전제**: 크롬 에이전트 TASK `AWS-02` 완료 (고정 IP + 방화벽 22/80/443 + SSH `.pem` 다운로드).
> **목표**: `https://api.mf-game.com/health` → 200 응답.

---

## 1. 배포 아키텍처 한눈에

```
유저 단말 ──(HTTPS:443)──▶ Cloudflare DNS (회색 구름, Proxy OFF)
                                  │
                                  ▼  api.mf-game.com A 레코드
                             Lightsail 고정 IP
                                  │
                                  ▼  :443/80
                             ┌────────────────┐
                             │ Caddy 2-alpine │  ← Let's Encrypt 자동 발급
                             └───────┬────────┘
                                     │ reverse_proxy :8080
                             ┌───────▼────────┐
                             │ api (MFG.Server)│  ← .NET 10 ASP.NET Core
                             │  /app           │  mounted: serviceAccountKey / play-services / logs
                             └───────┬────────┘
                                     │ :3306 (docker network)
                             ┌───────▼────────┐
                             │ mysql:8.0       │  ← persistent volume mysqldata
                             └────────────────┘
```

3 컨테이너: `mfg-caddy`, `mfg-api`, `mfg-db`. 모두 `restart: unless-stopped`.

---

## 2. 선행 조건 체크

- [ ] `AWS-02` 완료: Lightsail 인스턴스 `mfg-server-prod` Running, 고정 IP `mfg-static-ip` 할당, 방화벽 TCP 22/80/443
- [ ] SSH 키 `LightsailDefaultKey-ap-northeast-2.pem` 로컬 보관 (권한 600)
- [ ] `DNS-API` 완료: Cloudflare에 `api.mf-game.com` A 레코드 = Lightsail 고정 IP, **Proxy OFF(회색 구름)**
  - Let's Encrypt HTTP-01 챌린지가 오리진 IP에 직접 도달해야 하므로 초기 발급 시 Proxy 반드시 OFF
- [ ] 로컬에 다음 시크릿 파일 준비:
  - `server/src/MFG.Server/serviceAccountKey.json` (Firebase Admin SDK)
  - `server/src/MFG.Server/play-services.json` (Google Play IAP 검증)
  - `server/.env` (MySQL 비밀번호, R2 키, Firebase project id 등)

---

## 3. 초기 배포 (최초 1회)

### 3-1. SSH 접속 + Docker 설치

로컬 터미널 (Windows PowerShell 또는 Git Bash):

```bash
# SSH 키 권한 조정 (Windows는 icacls 사용)
chmod 600 LightsailDefaultKey-ap-northeast-2.pem  # Linux/Mac

# 접속
ssh -i LightsailDefaultKey-ap-northeast-2.pem ubuntu@<고정IP>
```

Lightsail 인스턴스 내부:

```bash
# Docker + compose plugin 설치
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu

# 로그아웃 → 재접속해야 도커 그룹 적용
exit
```

재접속 후:

```bash
docker --version           # Docker version 27.x
docker compose version     # v2.x (plugin 내장)
```

### 3-2. 배포 디렉토리 생성 + 리포 업로드

**옵션 A — Git clone (public repo거나 deploy key 설정 시)**:

```bash
sudo mkdir -p /opt/mfg && sudo chown ubuntu:ubuntu /opt/mfg
cd /opt/mfg
git clone <repo-url> .
cd server
```

**옵션 B — 로컬에서 rsync 업로드 (private repo, CI 미구축 시 임시 경로)**:

```bash
# 로컬에서 실행 (Windows는 WSL 또는 Git Bash)
rsync -avz --exclude 'bin' --exclude 'obj' --exclude '.env' \
  -e "ssh -i LightsailDefaultKey-ap-northeast-2.pem" \
  ./server/ ubuntu@<고정IP>:/opt/mfg/server/
```

### 3-3. 시크릿 파일 업로드 (git에 없는 것들)

로컬에서:

```bash
# scp로 3개 파일 전송
scp -i LightsailDefaultKey-ap-northeast-2.pem \
  ./server/src/MFG.Server/serviceAccountKey.json \
  ./server/src/MFG.Server/play-services.json \
  ./server/.env \
  ubuntu@<고정IP>:/opt/mfg/server/src/MFG.Server/
```

(`.env`는 `/opt/mfg/server/` 루트에 두는 게 compose 관례 — 아래 경로로 조정.)

```bash
# Lightsail에서
mv /opt/mfg/server/src/MFG.Server/.env /opt/mfg/server/.env
chmod 600 /opt/mfg/server/.env
chmod 600 /opt/mfg/server/src/MFG.Server/serviceAccountKey.json
chmod 600 /opt/mfg/server/src/MFG.Server/play-services.json
```

### 3-4. .env 주요 값 최종 확인

```bash
cd /opt/mfg/server
cat .env | grep -v '^#' | grep -v '^$'
```

필수 채움 항목:
- `DOMAIN=api.mf-game.com`
- `DB_ROOT_PASSWORD` / `DB_PASSWORD` — **실제 강한 비밀번호**
- `FIREBASE_PROJECT_ID=mfg-prod-4cb30`
- `R2_ACCESS_KEY` / `R2_SECRET` — server/.env 원본에서 복사
- `SLACK_WEBHOOK_ERRORS` — MON-02 완료 후 추가 (빈 값이어도 기동 가능)

### 3-5. docker-compose 볼륨 경로 확인

현재 `docker-compose.prod.yml`의 api 서비스 volumes는 compose 디렉토리 기준 상대 경로.
시크릿이 `src/MFG.Server/` 밑에 있으므로, 운영자 편의상 compose 파일 옆으로 심볼릭 링크:

```bash
cd /opt/mfg/server
ln -sf src/MFG.Server/serviceAccountKey.json serviceAccountKey.json
ln -sf src/MFG.Server/play-services.json     play-services.json
```

(또는 `docker-compose.prod.yml`의 volumes를 `./src/MFG.Server/...`로 수정.)

### 3-6. 빌드 + 기동

```bash
cd /opt/mfg/server
docker compose -f docker-compose.prod.yml up -d --build
```

- 첫 빌드: 3~5분 ($5 인스턴스 기준)
- 세 컨테이너 Running 확인:

```bash
docker compose -f docker-compose.prod.yml ps
```

### 3-7. Let's Encrypt 발급 확인

```bash
docker logs mfg-caddy | grep -i "certificate"
# ✓ "certificate obtained successfully" 라인 나오면 성공

# 외부 확인
curl https://api.mf-game.com/health
# → {"status":"healthy","timestamp":"..."}
```

발급 실패 시:
- Cloudflare Proxy가 ON(주황 구름)이면 챌린지 실패 → OFF 전환 후 `docker restart mfg-caddy`
- 방화벽에 80이 안 열려 있으면 챌린지 실패 → Lightsail Networking 탭 확인
- DNS 전파 미완 → `dig api.mf-game.com +short` 로 IP 확인

### 3-8. Cloudflare Proxy 재활성 (선택)

Let's Encrypt 발급 완료 후 Cloudflare Proxy ON(주황 구름) 전환:
- DDoS 방어, 캐싱, 경로 숨김 이득
- Caddy ↔ Cloudflare 간 TLS 연결 유지됨 (Caddy가 발급한 실 인증서가 origin cert 역할)
- Cloudflare SSL/TLS 모드: **Full (strict)** 추천

---

## 4. 갱신 배포 (코드 변경 시)

로컬에서 커밋 후 Lightsail로 재업로드 + 재기동.

```bash
# Lightsail에서
cd /opt/mfg/server
git pull   # 옵션 A일 때
# 또는 로컬에서 rsync 재실행 (옵션 B)

docker compose -f docker-compose.prod.yml up -d --build
```

EF Core 마이그레이션은 api 컨테이너 기동 시 `Program.cs`의 `db.Database.Migrate()` 호출로 자동 적용됨 (S-A7에서 Relational 전용으로 가드 처리). 새 마이그레이션 추가 시 이 동작이 그대로 스키마 갱신.

---

## 5. 운영 시 자주 쓰는 명령

```bash
# 로그 실시간
docker logs -f mfg-api

# Serilog 파일 로그 (7일 보관)
docker exec mfg-api ls -la /app/logs
docker exec mfg-api tail -f /app/logs/mfg-$(date +%Y%m%d).log

# DB 콘솔
docker exec -it mfg-db mysql -u root -p   # 비밀번호는 .env의 DB_ROOT_PASSWORD

# 재시작 (설정 변경 후)
docker compose -f docker-compose.prod.yml restart api

# 전체 종료
docker compose -f docker-compose.prod.yml down

# 백업 — mysqldata 볼륨
docker run --rm -v mfg_mysqldata:/from -v $(pwd):/to alpine \
  tar czf /to/mysqldata-$(date +%Y%m%d).tar.gz -C /from .
```

---

## 6. 헬스체크 + 스모크 검증 + Uptime Robot 연동

### 헬스체크 (단일 엔드포인트)
- `GET /health` — DB 연결 상태까지 포함한 응답 반환
- Uptime Robot이 5분마다 체크 (MON-01 완료 후)
- 실패 시 `#mfg-alerts` Slack 채널로 알림

### 배포 직후 스모크 (11 체크)
```bash
# 로컬에서 prod 대상 실행
bash server/scripts/post-deploy-smoke.sh
# 또는 다른 베이스
BASE=https://api.mf-game.com bash server/scripts/post-deploy-smoke.sh
```

체크 항목: Phase 26 data/version, data/config/latest(+?type=), system/notice(+minClientVersion), auth guards 401/403.
상세 절차는 [release-checklist.md](release-checklist.md) 참조.

---

## 7. 롤백 절차

```bash
# 이전 커밋 해시로 복원
cd /opt/mfg/server
git log --oneline | head -5
git checkout <이전해시>

docker compose -f docker-compose.prod.yml up -d --build

# EF 마이그레이션 롤백이 필요하다면
docker exec mfg-api dotnet ef database update <이전마이그레이션명> \
  --project src/MFG.Data --startup-project src/MFG.Server
```

마이그레이션 롤백은 신중히 — 칼럼 drop 이후 데이터 복구 불가.

---

## 8. 보안 체크리스트

- [ ] `.env` 권한 600 (`-rw-------`)
- [ ] 시크릿 JSON 2종 권한 600
- [ ] MySQL 포트 3306이 호스트에 노출되지 않음 (`docker-compose.prod.yml`에 ports 매핑 없음 — 확인)
- [ ] Lightsail 방화벽에 22는 본인 IP만 허용하도록 좁히기 (옵션)
- [ ] Ubuntu 자동 업데이트 활성화: `sudo dpkg-reconfigure -plow unattended-upgrades`
- [ ] 정기 MySQL 백업 스케줄 (cron 또는 Lightsail 스냅샷)

---

## 9. 트러블슈팅

| 증상 | 확인 |
|------|------|
| `/health` 502 Bad Gateway | `docker logs mfg-api` — 시작 에러 (DB 연결, Firebase 초기화 등) |
| `/health` 404 | Caddy → api 프록시 실패. `docker logs mfg-caddy` |
| Let's Encrypt 실패 | Cloudflare Proxy OFF 확인, 방화벽 80 개방, DNS 전파 완료 |
| MySQL 접속 실패 | `.env`의 `DB_PASSWORD` 불일치. api 컨테이너 env 확인: `docker inspect mfg-api \| grep -i password` |
| IAP 검증 실패 | `/app/play-services.json` 마운트 확인: `docker exec mfg-api ls -la /app/*.json` |

---

## 10. 후속 자동화 (CI/CD)

현재 `.github/workflows/server-ci.yml`에 GHCR Docker push까지 구성됨.
Lightsail 자동 배포를 위해 다음 중 택1:

- **A. GitHub Actions → SSH 배포**: `appleboy/ssh-action@v1` 사용. SSH 프라이빗 키를 GitHub Secret으로 저장. push 후 `docker compose pull && up -d` 원격 실행
- **B. Watchtower 컨테이너**: Lightsail에 watchtower 추가 → GHCR 이미지 새 버전 감지 시 자동 업데이트
- **C. 수동 배포 유지**: 현재 런북 방식

출시 초기엔 C(수동) → 안정화 후 A 전환 권장.

---

## 11. Phase 26 API 엔드포인트 현황 (2026-04-21 배포 후)

Phase 26 Sprint 26-1 완료로 프로덕션에서 다음 엔드포인트가 활성화되었다.

### 공개 (AllowAnonymous)
- `GET  /health` — 상태 체크
- `GET  /api/v1/data/version` — 3-Tier 버전 해시 (visual/config/balance/catalog)
- `GET  /api/v1/data/config/latest?type=characters|monsters|skills|stages` — Config 타입별 CDN URL
- `GET  /api/v1/system/notice` — 점검 공지 + `minClientVersion` + `forceUpdateMessage` (Phase 27-6 강제 업데이트 선행)

### 인증 필요 (JWT Bearer 또는 Dev 헤더 `X-Dev-Uid`)
- `GET /api/v1/event/offline-reward/preview` — Preview 계산 (DB 변경 없음)
- 기타 auth/currency/gacha/save/equipment/arena/guild/iap/attendance/hotdeal

### 관리자 전용 (`[Authorize(Policy = "AdminOnly")]`)
- `GET  /api/v1/admin/balance/snapshot` — BalanceConfig 현재 값
- `POST /api/v1/admin/balance/reload` — IConfigurationRoot.Reload() 후 스냅샷 반환

**⚠️ Admin 화이트리스트 필수 세팅**: `Admin:AllowedUserIds` 가 빈 리스트면 전원 거부(deny-by-default).
프로덕션에서는 `.env` 에 최소 1명 등록:

```bash
cd /opt/mfg/server
nano .env
# Admin__AllowedUserIds__0=<Firebase UID of primary admin>
sudo docker compose -f docker-compose.prod.yml up -d --force-recreate api
```

상세 가이드: [secrets.md — Admin 화이트리스트 섹션](secrets.md)

### 밸런스 핫리로드 (S261-04)
`appsettings.*.json` 의 `Balance` 섹션은 `IOptionsMonitor<BalanceConfig>` 로 소비되어
**파일 변경 시 재기동 없이 자동 반영**된다. 수동 트리거가 필요할 때만 `/admin/balance/reload` 호출.

관련: [release-checklist.md — D. 긴급 패치 드릴](release-checklist.md)
