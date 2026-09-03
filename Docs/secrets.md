---
read_count: 0
last_read: "never"
status: active
---

# MFG 서버 Secrets 관리 (S22-04)

환경별로 민감 정보가 어디에 저장되고 런타임에 어떻게 주입되는지 정리한다.

## 3-Tier 전략

| 환경 | 저장소 | 주입 방식 |
|------|--------|----------|
| **Local Dev (내 PC)** | .NET User Secrets (`secrets.json`, 리포 외부) | `dotnet user-secrets` CLI |
| **CI (GitHub Actions)** | GitHub Secrets (repo settings) | `${{ secrets.NAME }}` → env var |
| **Production (Lightsail)** | `server/.env` + 볼륨 마운트 JSON | docker-compose env + bind mount |

**리포에 커밋 금지**: `serviceAccountKey.json`, `play-services.json`, `.env`, `secrets.json` — `.gitignore` 반영 필수.

## Configuration 키 ↔ 환경변수 매핑

ASP.NET Core Configuration은 `:` 구분자를 `__`로 환경변수화한다.

| Configuration 키 | 환경변수 | 값 출처 | 비고 |
|------------------|---------|---------|------|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | `.env` DB_* 조합 | MySQL 접속 문자열 |
| `Firebase:ProjectId` | `Firebase__ProjectId` | `.env` FIREBASE_PROJECT_ID | public 식별자, 민감 아님 |
| `Firebase:CredentialPath` | `Firebase__CredentialPath` | 볼륨 마운트 경로 | **JSON 자체가 비밀** |
| `GooglePlay:CredentialPath` | `GooglePlay__CredentialPath` | 볼륨 마운트 경로 | **JSON 자체가 비밀** |
| `GooglePlay:PackageName` | `GooglePlay__PackageName` | appsettings 기본 | public |
| `Slack:ErrorWebhook` | `Slack__ErrorWebhook` | `.env` SLACK_WEBHOOK_ERRORS | **URL 자체가 비밀** |
| `R2:AccessKey`, `R2:Secret` | `R2__AccessKey`, `R2__Secret` | `.env` R2_* | 업로드 CLI/관리자 도구에서만 사용 |
| `Admin:AllowedUserIds:0..N` | `Admin__AllowedUserIds__0..N` | `.env` ADMIN_UID_* | S261-04 관리자 화이트리스트. 빈 리스트 = deny-by-default |

## Local Dev — User Secrets

```bash
cd server/src/MFG.Server
dotnet user-secrets init
dotnet user-secrets set "Firebase:ProjectId" "mfg-dev-187b4"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=mfg;User=mfg;Password=mfg_dev"
```

- 저장 위치: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` (Windows)
- Development 환경에서 자동 로드 (ASP.NET Core 기본 동작)
- 커밋되지 않음 (리포 외부)

Firebase dev 프로젝트(`mfg-dev-187b4`)의 `serviceAccountKey.json`은 로컬 PC에 별도 저장하고 `appsettings.Development.json`에 경로를 지정하거나 User Secrets `Firebase:CredentialPath`로 주입.

## CI — GitHub Actions

Repo Settings → Secrets and variables → Actions에 등록.

필수:
- `LIGHTSAIL_HOST`, `LIGHTSAIL_USER`, `LIGHTSAIL_SSH_KEY`, `LIGHTSAIL_DEPLOY_PATH` (CD SSH 배포용)
- (선택) `SLACK_WEBHOOK_BUILDS` — 빌드 성공/실패 Slack 알림

워크플로에서만 해독되며 로그에는 자동 마스킹.

## Production — Lightsail

1. `server/.env.example` → `.env` 복사 후 실제 값 입력
2. `serviceAccountKey.json`, `play-services.json` 을 `/opt/mfg/server/` 에 scp
3. `docker-compose.prod.yml`이 `.env`를 자동 로드 + JSON 파일을 `:ro` 볼륨 마운트
4. 컨테이너 내부 경로: `/app/serviceAccountKey.json`, `/app/play-services.json`

```bash
# Lightsail에서
cd /opt/mfg/server
cp .env.example .env
nano .env   # 값 입력
# serviceAccountKey.json, play-services.json scp로 업로드 완료 상태여야 함
sudo docker compose -f docker-compose.prod.yml up -d
```

## 회전 (Rotation)

| 항목 | 주기 | 방법 |
|------|------|------|
| Firebase serviceAccountKey | 유출 의심 시 | Firebase Console → Service Accounts → 키 재발급 → `/opt/mfg/server/` 교체 → `docker restart mfg-api` |
| Google Play credential | 유출 의심 시 | GCP Console → Service Account 키 재발급 |
| Slack Webhook | 유출 의심 시 | Slack Apps → Incoming Webhooks → Regenerate |
| DB 비밀번호 | 연 1회 권장 | MySQL `ALTER USER ... IDENTIFIED BY ...` + `.env` 갱신 + `docker compose up -d --force-recreate api` |
| GitHub Secrets | 필요 시 | Repo Settings에서 Update |

## Admin 화이트리스트 (S261-04)

`/api/v1/admin/*` 엔드포인트(BalanceConfig 핫리로드, 스냅샷 조회)는 `AdminOnly` 정책으로 보호된다.
`user_id` 클레임이 `Admin:AllowedUserIds` 에 포함되어야만 200, 그 외 403.
**화이트리스트가 비어있으면 어떤 호출도 거부** (deny-by-default).

### Local Dev — User Secrets

```bash
cd server/src/MFG.Server
dotnet user-secrets set "Admin:AllowedUserIds:0" "your-firebase-uid-or-dev-uid"
```

Development 환경에서는 `X-Dev-Uid` 헤더로 user_id 가 주입되므로, 로컬 테스트 시 해당 값을 화이트리스트에 등록하면 된다.

### Production — Lightsail

`server/.env` 에 추가:

```env
# Admin 화이트리스트 (S261-04). Firebase UID 기준.
Admin__AllowedUserIds__0=firebase-uid-of-primary-admin
Admin__AllowedUserIds__1=firebase-uid-of-backup-admin
```

`docker-compose.prod.yml` 은 `.env` 를 자동 로드 → 컨테이너 환경변수 → ASP.NET Core Configuration 자동 주입.
재기동 필요:

```bash
cd /opt/mfg/server
sudo docker compose -f docker-compose.prod.yml up -d --force-recreate api
```

### 감사 로그

- `POST /admin/balance/reload` 호출 시 `[Admin/Audit] BalanceReload actor=<user_id> at=<UTC>` (Information)
- 화이트리스트 외 uid 접근 시 `[Admin] 접근 거부 user_id=<...>` (Warning)
- 로그는 `logs/mfg-*.log` 파일 및 Slack(`Slack__ErrorWebhook` 설정 시 Error 이상만)으로 전송

### 회전

- 관리자 교체 시 `.env` ADMIN_UID_* 편집 + `docker compose up -d --force-recreate api`
- **신규 관리자 추가 전 기존 감사 로그 스냅샷 확보**(기존 actor 활동 기록 보존)

---

## 체크리스트 (출시 전)

- [ ] `.gitignore`에 `.env`, `serviceAccountKey.json`, `play-services.json`, `secrets.json` 포함
- [ ] GitHub repo는 **private**
- [ ] Lightsail `.env` 파일 권한 `600` (`chmod 600 .env`)
- [ ] Let's Encrypt 발급 완료 → Cloudflare Proxy ON (DDoS 방어)
- [ ] 로컬 dev에서 `dotnet user-secrets list` 로 주입 확인
- [ ] Lightsail `.env`에 `Admin__AllowedUserIds__0..N` 세팅 (최소 1명) + 컨테이너 재기동
