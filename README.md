# MFG Server

> ⚠️ **중단된 개인 프로토타입 (2026-04, 단독 개발).** 학습·포트폴리오 목적으로 공개합니다. 상용 서비스로 출시된 적은 없으며, 아래 "현재 상태 / 완성도"와 "시크릿 관리" 절을 먼저 확인해 주세요.

MFG (Mobile Fantasy Game) 백엔드 — ASP.NET Core 10 + EF Core + MySQL 8.0 + Firebase Auth. 2026-04-15~04-21(약 1주) 동안 **14커밋 전량 단독 개발(solo)**. 클라이언트는 별도 레포 [`mfg-client`](../mfg-client)(Unity 6000.3.10f1) 참고.

컨트롤러 15개, xUnit 통합 테스트 11개 파일, GitHub Actions CI/CD → GHCR → AWS Lightsail 배포 파이프라인까지 **구성됨(프로토타입)** — 실제 상시 가동 여부는 "현재 상태" 절 참고.

---

## 현재 상태 / 완성도

**구현됨(프로토타입)**
- 컨트롤러 15개 전체에 대해 엔드포인트가 구현되어 있고, 각 도메인 대응 xUnit 통합 테스트 파일이 존재
- GitHub Actions CI(빌드+테스트) / CD(GHCR 이미지 → Lightsail SSH 배포) 워크플로가 파일로 구성되어 있고, 로드맵 문서상 Phase 18~20(Core Backend / 아레나·길드 / 강화+배포)은 "완료"로 표기됨
- Google Play 영수증 검증(`GooglePlayReceiptVerifier`) + Pub/Sub RTDN 수신(`PubSubSubscriberService`) 로직 존재
- Caddy + Let's Encrypt 리버스 프록시, R2 일일 백업 스크립트(`scripts/backup-db.sh`) 존재

**미착수**
- 로드맵(`Docs/roadmap-index.md`) 기준 **Phase 27(스토어 출시: Google Play/App Store 등록, LiveOps)은 전부 미착수** — 즉 이 백엔드로 실제 스토어 출시가 이루어진 적은 없음
- Phase 26 Sprint 26-2(SO→JSON CI 데이터 파이프라인) 진행 필요 상태로 남음

**확인 필요**
- Lightsail 서버가 실제로 상시 가동 중인지, 백업 복원 드릴(`Docs/backup.md`)이 실제로 수행된 이력이 있는지 — `[소유자 확인]`
- 프로덕션 트래픽 하에서의 부하/장애 대응 이력 — `[소유자 확인]`

---

## 스택

- **.NET 10** ASP.NET Core Web API
- **MySQL 8.0** + EF Core (snake_case 네이밍)
- **Firebase Auth** JWT 검증 + Admin SDK (커스텀 클레임, 토큰 revoke)
- **Serilog** 구조화 로깅 (Console + 일일 롤링 파일)
- **Caddy 2** 리버스 프록시 + Let's Encrypt 자동 발급
- **Google Play Developer API** IAP 영수증 검증
- **Google Cloud Pub/Sub** Play RTDN (구독 갱신/취소/환불) 실시간 수신

---

## 아키텍처 (3계층)

```
src/
├── MFG.Domain/   ← 엔티티 (Player, Currency, Gacha, Arena, Guild, IAP ...) — 프레임워크 비의존
├── MFG.Data/     ← EF Core AppDbContext + Migrations — 영속성
└── MFG.Server/   ← ASP.NET Core — Controllers / Services / DTOs / Middleware / Logging
```

`MFG.Server`가 `MFG.Data`(영속성)와 `MFG.Domain`(엔티티)에 의존하는 단방향 3계층 구조. `Middleware/GlobalExceptionHandler.cs`가 전역 예외를 `ApiResponse<T>` 포맷으로 통일 응답.

### 컨트롤러 15개 (`src/MFG.Server/Controllers/`, 각 라우트/엔드포인트 기준 실측)

| 컨트롤러 | 라우트 | 역할 |
|---|---|---|
| `AuthController` | `/api/v1/auth` | 로그인/로그아웃, 프로필 갱신 (Firebase 인증) |
| `SaveController` | `/api/v1/save` | 세이브 동기화(`sync`)/마이그레이션(`migrate`)/로드 |
| `GachaController` | `/api/v1/gacha` | 가챠 뽑기 처리 |
| `CurrencyController` | `/api/v1/currency` | 재화 잔액 조회/소비/획득 |
| `EquipmentController` | `/api/v1/equipment` | 장비 강화 처리 |
| `ArenaController` | `/api/v1/arena` | 아레나 리더보드/매칭 후보 조회/전투 결과 처리/상태 조회 |
| `GuildController` | `/api/v1/guild` | 길드 생성/가입/기부/보스전 결과 처리/정보 조회 |
| `AttendanceController` | `/api/v1/attendance` | 출석 체크 처리 |
| `EventController` | `/api/v1/event` | 이벤트 보상 수령, 오프라인 보상 미리보기 |
| `HotDealController` | `/api/v1/hotdeal` | 핫딜 카탈로그 조회/구매 |
| `IapController` | `/api/v1/iap` | Google Play 인앱결제 영수증 검증 |
| `DataController` | `/api/v1/data` | 데이터 버전 조회 + 최신 Config 전달 (3-Tier 데이터 아키텍처) |
| `SystemController` | `/api/v1/system` | 시스템 공지 제공 |
| `AdminController` | `/api/v1/admin` | 밸런스 설정 스냅샷 조회/핫리로드 (관리자 화이트리스트 인가) |
| `HealthController` | `/api/v1/health` | 헬스체크 |

---

## API 개요

- 공통 응답 포맷: `ApiResponse<T> { success, data, error }`
- 인증: Firebase ID Token(`Authorization: Bearer ...`), Dev 모드는 `X-Dev-Uid` 헤더로 바이패스
- `AdminController`류는 `AdminOnly` 정책(화이트리스트 기반) 추가 인가 필요 — 화이트리스트가 비어 있으면 전체 거부(deny-by-default)
- 상세 요청/응답 스키마는 각 `DTOs/*.cs` 및 `swagger`(로컬 `http://localhost:8080/swagger`) 참고

---

## 로컬 개발

```bash
docker compose up -d        # MySQL 8.0 로컬 기동 (port 3306)
dotnet run --project src/MFG.Server
# → http://localhost:8080/health, http://localhost:8080/swagger
```

Dev 모드는 JWT 인증 바이패스 — `X-Dev-Uid: <uid>` 헤더로 가짜 사용자 식별.

---

## 테스트 전략

```bash
dotnet test MFG.Server.slnx
```

`WebApplicationFactory`(`TestAppFactory.cs`) + EF Core InMemory 기반 통합 테스트. 실제 HTTP 파이프라인(미들웨어, 라우팅, 모델 바인딩 포함)을 띄우고 인메모리 DB로 대체해 컨트롤러 단위로 검증하는 방식 — 실제 MySQL이나 외부 서비스(Firebase/Google Play) 없이 CI에서 실행 가능.

`tests/MFG.Server.Tests/` 11개 파일: `AdminControllerTests`, `AuthControllerTests`, `CurrencyControllerTests`, `DataConfigLatestTests`, `DataControllerTests`, `GachaControllerTests`, `HealthCheckTests`, `OfflineRewardPreviewTests`, `ProfileUpdateTests`, `SaveMigrateTests`, `SystemControllerTests` (+ `ValidationServiceTests`, `TestAppFactory` 인프라).

---

## CI/CD 흐름

```
push main / tag v*.*.*
        │
        ▼
 [ci.yml] dotnet restore → build → test (실패 시 중단)
        │  (main push만 해당)
        ▼
 Docker build & push → GHCR (ghcr.io/<owner>/mfg-server, sha/latest/semver 태그)
        │  workflow_run: ci 완료 성공 시 트리거
        ▼
 [cd.yml] SSH → AWS Lightsail
        │  docker compose -f docker-compose.prod.yml pull api && up -d (db/caddy/api)
        ▼
 post-deploy-smoke.sh 헬스체크 → 실패 시 Slack 알림(SLACK_WEBHOOK_BUILDS 설정 시)
```

`workflow_dispatch`로 특정 `image_tag`를 지정한 수동 배포/롤백도 가능 (`Docs/rollback.md`).

---

## 배포

`main` 브랜치 push → GitHub Actions CI가 GHCR에 Docker 이미지 빌드·푸시 → CD가 Lightsail SSH로 `docker compose pull && up -d`.
릴리스는 `git tag v*.*.*` 후 push → CI가 `v1.0.0` + `latest` 동시 푸시.

---

## 운영 런북

| 문서 | 용도 |
|------|------|
| [`Docs/deploy.md`](Docs/deploy.md) | Lightsail 최초 배포 + 트러블슈팅 |
| [`Docs/rollback.md`](Docs/rollback.md) | 1분 내 롤백 (workflow_dispatch image_tag 지정) + 파괴적 마이그레이션 3단계 규칙 |
| [`Docs/backup.md`](Docs/backup.md) | mysqldump → R2 일일 백업(cron 03:00 UTC) + 월 1회 staging 복원 테스트 |
| [`Docs/secrets.md`](Docs/secrets.md) | 3-tier Secrets (User Secrets / GitHub Secrets / .env + 볼륨) + 회전 절차 |
| [`Docs/release-checklist.md`](Docs/release-checklist.md) | 출시/핫픽스/드릴/스토어 단계 롤아웃 체크리스트 |

---

## 시크릿 관리

`.env`, `serviceAccountKey.json`, `play-services.json`, `secrets.json`은 `.gitignore`에 등록되어 있고, 저장소에 커밋된 `.env.example`/`appsettings.*.json`을 직접 확인한 결과 값은 전부 빈 문자열 또는 플레이스홀더(예: `여기에_root_비밀번호`)로 되어 있어 **평문 자격증명 자체는 확인되지 않았습니다.**

**단, 아래 항목은 공개 전환 전 반드시 재확인이 필요합니다 — 공개 스냅샷에는 자격증명·서버 IP·폴백 비밀번호를 포함하지 않았습니다(전체 히스토리 스캔 후 미러링).**

- `Docs/release-checklist.md`와 `Docs/roadmap-index.md`에 **과거(2026-04-20) 노출 이력이 있다고 문서 스스로 기록한 자격증명 회전 항목**(AWS Access Key, R2 Secret Access Key, GCP 서비스 계정 키)이 **미체크(회전 미완료로 추정되는 상태)**로 남아 있습니다.
- 그중 하나는 자격증명 식별자(AWS Access Key ID) 자체가 해당 문서 본문에 **평문으로 남아 있습니다.** 이 README와 `README-notes.md`에는 값을 옮기지 않았으며, 리포를 공개하기 전 해당 키를 (아직 회전하지 않았다면) 회전하고 두 문서에서 실제 식별자를 마스킹 처리할 것을 권장합니다.

## 주요 디렉토리

```
src/
├── MFG.Domain/       ← 엔티티 (Player, Currency, Gacha, Arena, Guild, IAP ...)
├── MFG.Data/         ← EF Core AppDbContext + Migrations
└── MFG.Server/       ← ASP.NET Core (Controllers, Services, Middleware, DTOs, Logging/SlackWebhookSink)
tests/
└── MFG.Server.Tests/ ← xUnit 통합 테스트
scripts/
├── backup-db.sh      ← mysqldump + rclone R2 업로드 (cron)
└── restore-db.sh     ← 로컬/R2 경로에서 복원
Docs/
├── deploy.md         ← Lightsail 배포 런북
├── rollback.md       ← 롤백 시나리오 A/B/C/D
├── backup.md         ← DB 백업/복원 런북
├── secrets.md        ← Secrets 관리 매핑표
└── release-checklist.md ← 출시/핫픽스 체크리스트
```

## 환경 변수

`.env.example` 참고 → `.env` 복사 후 실제 값 주입.
필수: `DB_*`, `FIREBASE_PROJECT_ID`, `DOMAIN`.
선택: `SLACK_WEBHOOK_ERRORS`, `R2_*`, `GOOGLE_PLAY_*`.

## 클라이언트 연동

Unity 클라이언트는 별도 레포 [`mfg-client`](../mfg-client) — `Core/Net/ApiClient.cs`가 이 서버의 `api/v1` 엔드포인트를 UniTask 기반으로 호출하며, 세이브 데이터는 `SaveController`(`sync`/`migrate`/`load`)를 통해 로컬↔서버 간 동기화됩니다.

## 중단 사유 · 배운 점

`[소유자 작성]`

## 라이선스

리포지토리에 `LICENSE` 파일 없음. `[라이선스 미정 — 소유자 확인 필요]`

---

## English Summary

**MFG Server** — a discontinued solo prototype (Apr 2026, ~1 week, 14 commits) backend for a mobile idle RPG, built with ASP.NET Core 10, EF Core, MySQL 8, and Firebase Auth. Published for portfolio/learning purposes; it was never launched on an app store. Three-layer architecture (`MFG.Domain` / `MFG.Data` / `MFG.Server`), 15 controllers covering auth, save sync/migration, gacha, currency, equipment, arena, guild, attendance, events, IAP receipt verification, and admin config reload. 11 xUnit integration test files run against `WebApplicationFactory` + EF Core InMemory. CI/CD is wired end-to-end: GitHub Actions builds and tests on push, pushes a Docker image to GHCR, and a second workflow deploys it to an AWS Lightsail instance over SSH behind a Caddy reverse proxy. No committed plaintext secrets were found in `.env.example` or `appsettings.*.json` (placeholders only), but the repo's own operational docs record credential-rotation checklist items (AWS/R2/GCP keys) that appear unresolved — see "시크릿 관리" above before making this repository public.
