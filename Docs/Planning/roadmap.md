---
read_count: 0
last_read: "never"
status: active
---

# MFG 서버 로드맵

> **단일 서버 원천.** 2026-04-20 `client/Docs/Planning/roadmap.md`에서 서버 전용 Phase를 이관.
> 클라-서버 교차 Phase(23/24)는 양쪽에 참조 링크. 서버 전용 Phase(18/19/20/22/26)는 이 문서가 유일 원천.
> 원본 Phase 번호는 그대로 유지하여 클라 로드맵과의 대응 관계 보존.

## 현재 상태 스냅샷 (2026-04-20)

| 영역 | 상태 |
|------|------|
| Phase 18 Core Backend | ✅ 완료 (2026-04-02 ~ 04-08) |
| Phase 19 Competitive/Social | ✅ 완료 |
| Phase 20 Enhancement + Deploy | ✅ 완료 (2026-04-16 Lightsail 프로덕션 가동) |
| Phase 22 DevOps Foundation | 🟢 대부분 완료 (S22-07 Slack 수동 작업 잔여) |
| Phase 23 S23-08 JobId | ✅ 완료 (2026-04-20) |
| Phase 24 Sprint 24-5 SheetsSync | ⚪ Phase 26 파이프라인에 흡수 예정 |
| Phase 25 Sprint 25-1 S251-01 Save Migrate | ✅ 완료 (2026-04-20) |
| Phase 25 Sprint 25-3 오프라인 정책 | 🔨 진행 필요 |
| Phase 25 Sprint 25-4 밸런스 일치 | ⚪ Phase 26 선행 |
| **Phase 26 3-Tier 데이터 파이프라인** | 🆕 도입 (2026-04-20) — Sprint 26-1 3/5 완료 (2026-04-21), 26-2 인프라 신설 대기 |
| **Phase 27 스토어 출시 + LiveOps** | 🆕 **신규 도입 (2026-04-21)** — 서버 세션 풀 owner. 출시 D-90부터 가동 예정 |

## 핵심 설계 원칙 (2026-04-20 확정)

1. **CDN-First**: 앱 번들은 런처 + 엔진 + 최소 폴백만. 모든 게임 데이터/에셋은 CDN에서 실시간 다운로드.
2. **서버 권위 (Server-Authoritative)**: 가챠/전투/강화/재화/세이브 모든 상태 변경은 서버 왕복. 클라는 결과 연출만.
3. **3-Tier 데이터 분리**:
   - 🎨 **Visual** (SPUM 파츠/VFX/UI): CDN Addressables
   - 🪪 **Config** (이름/설명/아이콘 매핑/아바타 구성 JSON): CDN JSON
   - 🔒 **Balance** (확률/배율/공식): 서버 DB 전용, 클라 무노출
4. **오프라인 플레이 = 접속하지 않은 시간**: 재접속 시 서버가 경과 시간 × 방치 보상 공식으로 일괄 지급. 실제 오프라인 게임플레이 없음.
5. **긴급 패치는 스토어 심사 없이**: 데이터/에셋 수정 = CDN 즉시 반영. 코드 수정만 앱 재빌드.
6. **환경 분리**: `dev`(로컬 SO) / `stg`(TestFlight + 스테이징 서버) / `prd`(App Store + 프로덕션 서버).

## 관련 문서

- [`core-architecture.md`](core-architecture.md) — 3-Tier 데이터 아키텍처 상세
- [`data-pipeline.md`](data-pipeline.md) — SO → Sheets → CDN/서버 CI 파이프라인
- [`offline-reward-model.md`](offline-reward-model.md) — 오프라인 보상 모델
- [`../roadmap-index.md`](../roadmap-index.md) — 서버 로드맵 포인터 (경량 인덱스)
- [`../deploy.md`](../deploy.md) / [`../rollback.md`](../rollback.md) / [`../backup.md`](../backup.md) / [`../secrets.md`](../secrets.md) — 운영 런북

---

## Phase 18: Server Phase 1 — Core Backend ✅

> **완료.** .NET 10 + Firebase Auth + MySQL 8.0. 상세는 원본 `client/Docs/Planning/roadmap.md:636` 참조.

| Sprint | 산출 | 파일 |
|--------|------|------|
| 18-1 스캐폴딩 | MFG.Server/Domain/Data 3-프로젝트 분리 | `server/src/MFG.*` |
| 18-2 Firebase Auth | JWT Bearer, POST /auth/login | `AuthController.cs`, `Program.cs` |
| 18-3 가챠 | POST /gacha/pull, 천장(60회 Legendary, 10회 Rare) | `GachaController.cs`, `GachaService.cs` |
| 18-4 재화 | POST /currency/spend, /earn, GET /balance | `CurrencyController.cs`, `CurrencyService.cs` |
| 18-5 IAP+저장+출석 | POST /iap/verify, /save/sync, /attendance/check | `IapController.cs`, `SaveController.cs`, `AttendanceController.cs` |
| 18-6 클라 연동 | ApiClient + ServerDtos, GachaManager/CurrencyManager `*ServerAsync` | `client/Assets/Scripts/Core/Net/` |

---

## Phase 19: Server Phase 2 — Competitive/Social ✅

> **완료.** 아레나 + 길드.

| ID | 산출 |
|----|------|
| S19-01 | ArenaController — match-candidates, battle-result, status, leaderboard |
| S19-02 | GuildController — create, join, donate, boss-result, info |
| S19-03 | 클라 ArenaManager/GuildManager 서버 async |

---

## Phase 20: Server Phase 3 — Enhancement + Deploy ✅

> **완료.** 프로덕션 라이브. `api.mf-game.com/health` 200 OK.

| ID | 산출 |
|----|------|
| S20-01 | EquipmentController 스타포스 25단계 확률 테이블 |
| S20-02 | EventController — 던전/배틀패스/오프라인 보상 |
| S20-03 | Lightsail `mfg-server-prod` + Caddy SSL + docker-compose.prod |
| S20-04 | CORS + appsettings.Production |

인프라: Lightsail <server-ip> (ap-northeast-2a) / 512MB + 2GB swap / mfg-api + mfg-db + mfg-caddy 컨테이너.

---

## Phase 22: DevOps Foundation 🟢 대부분 완료

> 1인 개발 현실에 맞춘 최소 필수 DevOps. 월 추가 $0~1. 원본 상세 `client/Docs/Planning/roadmap.md:744`.

### 출시 전 필수 (S22-01 ~ S22-08)

| ID | 태스크 | 상태 | 비고 |
|----|--------|------|------|
| S22-01 | GitHub Actions CI (dotnet test + GHCR push) | ✅ | `.github/workflows/server-ci.yml`. sha/latest/semver 태그 |
| S22-02 | GitHub Actions CD (workflow_run + SSH deploy + /health retry 6회) | ✅ | `.github/workflows/server-cd.yml`. Secrets: LIGHTSAIL_* |
| S22-03 | 환경 분리 (dev=mfg-dev-187b4 / prod=mfg-prod-4cb30) | ✅ | `appsettings.{Env}.json` + Program.cs 명시적 throw |
| S22-04 | Secrets 관리 3-tier (User Secrets / GitHub / .env+볼륨) | ✅ | `Docs/secrets.md` 매핑표 + 회전 절차 |
| S22-05 | DB 백업 자동화 (Lightsail 스냅샷 + R2 mysqldump + 로컬) | ✅ | `scripts/backup-db.sh`, `Docs/backup.md`, cron 03:00 UTC, 30일 보존 |
| S22-06 | 롤백 런북 + 이미지 태그 관리 (workflow_dispatch image_tag) | ✅ | `Docs/rollback.md` A/B/C/D 시나리오, 1분 롤백 |
| S22-07 | 알림 허브 (Slack) | 🟡 부분 | CI/CD 노티 + SlackWebhookSink 코드 있음. **남음**: Slack Workspace 생성 + `#mfg-builds`/`#mfg-errors`/`#mfg-alerts` 채널 + Webhook 발급 (수동) |
| S22-08 | 레이트 리밋 + WAF | 🟡 부분 | `AddRateLimiter` global 60/min + gacha 20/min + iap 10/min. **남음**: Cloudflare WAF Free 규칙 |

### 출시 후 (S22-09 ~ S22-12)

| ID | 태스크 | 우선순위 |
|----|--------|---------|
| S22-09 | Firebase Remote Config 연동 | 중 — Phase 26과 연계 시 재고 |
| S22-10 | 관리자 대시보드 (ASP.NET Razor Pages) | 중 — DAU 1K 도달 후 |
| S22-11 | Sentry/GlitchTip 에러 추적 | 낮 — 무료 5K 이벤트/월 |
| S22-12 | k6 부하 테스트 | 낮 — 스케일업 직전 |

---

## Phase 23 Sprint: Onboarding (서버 측 부분) ✅

> 클라 주도. 서버는 API 제공. 원본: `client/Docs/Planning/roadmap.md:926`.

| ID | 태스크 | 상태 |
|----|--------|------|
| S23-08 | PATCH /api/v1/auth/profile JobId 필드 + Level/CombatPower 0값 스킵 | ✅ 완료 (2026-04-20 commit 33f60cd) |

---

## Phase 26: 3-Tier 데이터 아키텍처 + CDN Config Delivery 🆕

> **신규 도입 (2026-04-20).** Phase 21(CDN) + Phase 24(Sheets) + Phase 25-4(밸런스 일치)를 통합 재설계한 결과물.
> **목적**: 데이터/에셋 수정 → 앱 재빌드/스토어 심사 없이 즉시 반영 가능한 구조.
> **전제**: 사용자가 "긴급 패치 시 앱 재빌드는 비현실적"이라는 문제의식 제기 → CDN-First 아키텍처 확정.

### 목표 상태

- 앱 번들 = Unity 엔진 + 네이티브 + 최소 폴백(1~2KB JSON, 서버 불가 안내용)
- 게임 데이터/에셋 = CDN에서 런타임 다운로드 (해시 기반 URL, 버전 자동 갱신)
- 밸런스(확률/배율) = 서버 DB 전용, 클라 무노출
- 긴급 패치(이름 오타, 아바타 버그, 확률 조정) = git push → 3~10분 반영

### Sprint 26-1: Server Side — Config Delivery API

| ID | 태스크 | 상태 | 설명 |
|----|--------|------|------|
| S261-01 | `GET /api/v1/data/version` | ✅ 완료 (2026-04-21) | 3-Tier 버전 해시 반환. `DataController.GetVersion()` / `IOptionsSnapshot<DataVersionsConfig>` |
| S261-02 | `GET /api/v1/data/config/latest` | ✅ 완료 (2026-04-21) | `?type=` 필터 + `{base}/{type}-{hash}.json` URL 조합. `DataController.GetConfigLatest()` + `DataConfigLatestResponse` |
| S261-03 | `GET /api/v1/event/offline-reward/preview` | ✅ 완료 (2026-04-21) | `OfflineRewardService.Calculate()` 공통화 → Preview/Claim 양쪽 재사용. 12h 상한 Balance 상수화 |
| S261-04 | `BalanceTables` hot-reload (`IOptionsMonitor<BalanceConfig>`) | ✅ 완료 (2026-04-21) | `BalanceConfig` 신설 + `IOptionsMonitor` 주입 → `reloadOnChange=true` 자동 반영. 수동 트리거는 `POST /admin/balance/reload` |
| S261-05 | `Notice` 점검 공지 API (`GET /api/v1/system/notice`) | ✅ 완료 (2026-04-21) | `SystemController.GetNotice()` / `IOptionsSnapshot<SystemNoticeConfig>` |

### Sprint 26-2: CI Pipeline — SO → 3-갈래 분해

| ID | 태스크 | 상태 | 설명 |
|----|--------|------|------|
| S262-01 | GitHub Action: SO → Config JSON export | ⚪ 미진행 | Unity headless 또는 .NET CLI로 `.asset` 파싱 → `characters.json`, `monsters.json`, `skills.json`, `stages.json` 생성. 해시 파일명(`characters-{hash}.json`) |
| S262-02 | GitHub Action: SO → Sheets Write (뷰어용) | ⚪ 미진행 | 변경된 카테고리만 시트 탭 업데이트 (diff 기반). 서비스 계정 키 GitHub Secrets |
| S262-03 | GitHub Action: SO → Balance JSON → 서버 DB seed | ⚪ 미진행 | Balance 카테고리(확률/배율)만 추출해 서버 레포에 PR 자동 생성 또는 서버 `/admin/balance/reload` 호출 |
| S262-04 | GitHub Action: Addressables Build (GameCI) | ⚪ 미진행 | Unity headless로 Addressables Build → `.bundle` + `catalog.json` 생성. Unity Personal 라이선스 필요 (사용자 수동 `.ulf` 발급 선행) |
| S262-05 | GitHub Action: Config JSON + Addressables → R2 업로드 | ⚪ 미진행 | `rclone copy` 또는 `aws s3 sync`로 Cloudflare R2에 업로드. `https://cdn.mf-game.com/config/`, `/addressables/` |

### Sprint 26-3: Client Runtime — Config Loader + Bootstrap

> 이 Sprint는 **클라 에이전트 작업**. 서버 관점에선 "클라가 이 계약을 지켜 호출한다"는 계약서.

| ID | 태스크 | 상태 | 서버 계약 |
|----|--------|------|----------|
| S263-01 | `ConfigLoader` — 부팅 시 `/data/version` 호출 → 해시 비교 → 다른 것만 CDN 다운 | ⚪ 클라 | S261-01 응답 포맷 준수 |
| S263-02 | 해시 기반 URL 캐싱 (`persistentDataPath/cache/config/`) | ⚪ 클라 | URL 포맷: `{CDN}/config/{type}-{hash}.json` |
| S263-03 | 다운 실패 시 이전 캐시 또는 앱 내장 폴백 사용 | ⚪ 클라 | 폴백 스키마 서버와 동일 |
| S263-04 | Addressables Remote 카탈로그 체크 + 차등 다운 | ⚪ 클라 | S261-01에 `catalog` 해시 포함 |
| S263-05 | 오프라인 보상 Preview UI — `/event/offline-reward/preview` → 받기 버튼 → `/event/claim` | ⚪ 클라 | S261-03 계약 준수 |

### Sprint 26-4: 환경 분기 (dev/stg/prd)

| ID | 태스크 | 상태 |
|----|--------|------|
| S264-01 | `BuildConfig.DataSource` enum — dev=Local, stg/prd=Remote | ⚪ 클라 |
| S264-02 | `DataManager` 분기 — `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` | ⚪ 클라 |
| S264-03 | 스테이징 CDN 경로 분리 (`cdn-stg.mf-game.com` 또는 `/stg/` prefix) | ⚪ 서버/인프라 |
| S264-04 | 서버 `appsettings.Staging.json` — stg DB + stg CDN URL | ⚪ 서버 |

### Sprint 26-5: 출시 검증

| ID | 태스크 | 상태 |
|----|--------|------|
| S265-01 | 긴급 패치 드릴 — 몬스터 이름 오타 수정 → push → 3~5분 내 반영 검증 | ⚪ |
| S265-02 | Config 해시 불일치 시 fallback 동작 검증 | ⚪ |
| S265-03 | CDN 전체 장애 시나리오 — 앱 내장 폴백으로 타이틀 진입 검증 | ⚪ |
| S265-04 | 오프라인 보상 경계값 테스트 (0초 / 12시간 / 30일) | ⚪ |

### 의존성

- **선행**: Phase 22 CI/CD ✅ 완료, Phase 20 프로덕션 배포 ✅ 완료
- **Unity 라이선스**: Personal 무료, 단 CI용 `.ulf` 발급 사용자 수동 작업 (10분)
- **GitHub Secrets (클라 리포)**: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`
- **클라 리포**: 현재 프로젝트 루트는 git 저장소 아님 → 클라 코드용 GitHub 리포 필요 또는 프로젝트 루트 git화 (mfg-client)
- **Cloudflare R2 쓰기 권한**: 기존 DB 백업용 외 별도 API Token (Config 업로드용)
- **GCP 서비스 계정 (시트 쓰기)**: 시트 1개에만 Editor 권한 제한

### 예상 비용 (월)

| 항목 | 비용 | 비고 |
|------|------|------|
| GitHub Actions | $0 | Public 무제한 or Private 2000분/월 무료 |
| Unity Personal 라이선스 | $0 | 연매출 $100K 미만 |
| Cloudflare R2 (Config + Addressables) | $0~$0.10 | 무료 10GB + 월 1000만 Class A 요청. 초기 DAU < 1K는 완전 무료 |
| Google Sheets API | $0 | 무료 쿼터 충분 |
| **합계** | **$0~$0.10** | |

### 구현 순서 (8주, 클라 플레이테스트 병렬)

| 주차 | 서버 에이전트 | 인프라 에이전트 (신설) | 클라 에이전트 (기존) |
|------|--------------|------------------------|---------------------|
| 1 | S261-01 `/data/version` 스펙 + 스텁 | SO → JSON Export Editor 도구 (로컬 수동) | **계속 플레이테스트** |
| 2 | S261-03 `/event/offline-reward/preview` | GitHub Action: SO → Sheets write (dotnet CLI) | **계속 플레이테스트** |
| 3 | S261-02 `/data/config/latest` | SO → Config JSON export CI 단계 | **계속 플레이테스트** |
| 4 | S261-04 BalanceTables hot-reload | GameCI Addressables Build + R2 업로드 | 플레이테스트 + S263-01 ConfigLoader 설계 착수 |
| 5 | S262-03 Balance → DB seed | Remote Addressables 파일럿 (이벤트 배너) | S263-01 구현 |
| 6 | S261-05 Notice API | 해시 URL 패턴 + 캐시 무효화 검증 | S263-02 캐싱 |
| 7 | Staging 환경 구성 (stg DB + stg CDN) | stg CI 파이프라인 분기 | S263-03~04 폴백 |
| 8 | 출시 QA + S265 검증 | 긴급 패치 드릴 실연 | S263-05 오프라인 Preview UI |

---

## Phase 24 Sprint 24-5: Sheets Sync Service (Phase 26에 흡수)

> **원본** `client/Docs/Planning/roadmap.md:1058`의 S245-01~03은 **Phase 26 Sprint 26-2** (SO → Sheets Write + Balance → DB seed)로 통합되어 관리된다.
> 별도 구현 X. Phase 24 Sprint 24-1~4 (클라 측 Sheets 임포트 도구)는 역방향으로 재해석: **SO가 원천, 시트는 뷰어**.

---

## Phase 25: 서버 권위 전환 + 단일 진실 소스

> 원본 `client/Docs/Planning/roadmap.md:1093`. 아래는 **서버 관점 현황 + 책임 범위**.

### Sprint 25-1: 데이터 소스 일원화 (클라 주도, 서버 대기)

| ID | 태스크 | 주체 | 상태 |
|----|--------|------|------|
| S251-01 | 최초 로그인 시 로컬 SaveData → 서버 마이그레이션 | 클라 호출 + 서버 엔드포인트 | ✅ 서버 `POST /save/migrate` 완료 (2026-04-20). 클라 전환 대기 |
| S251-02 | LocalSaveProvider → ReadOnlyCache | 클라 | ⚪ |
| S251-03 | 서버 sync 이전 게임 진입 차단 | 클라 | ⚪ |
| S251-04 | AutoSave → 서버 `/save/sync` 직통 | 클라 | ⚪ (서버는 이미 Phase 18-5에서 완료) |

### Sprint 25-2: 로컬 계산 경로 제거 (클라 주도)

서버 API는 Phase 18/19/20에서 모두 완비. **클라가 `*ServerAsync` 단일 경로로 전환하면 됨**. 서버 측 추가 작업 없음.

### Sprint 25-3: 오프라인 정책 확정 🔨 진행 필요

> **핵심 재정의 (2026-04-20)**: "오프라인 플레이"는 개념상 존재하지 않음. 오프라인 = 접속하지 않은 시간 → 재접속 시 서버 계산으로 일괄 지급.
> 상세: [`offline-reward-model.md`](offline-reward-model.md)

| ID | 태스크 | 주체 | 상태 |
|----|--------|------|------|
| S253-01 | `LoginManager._allowOfflineFallback = false` | 클라 | ⚪ |
| S253-02 | 서버 연결 실패 재접속 팝업 + 재시도 루프 | 클라 | ⚪ 서버 Notice API(S261-05) 페치 |
| S253-03 | NetworkReachability 감지 + 자동 재동기화 | 클라 | ⚪ |
| S253-04 | JWT 만료 → Firebase refresh → 재시도 | 클라 + 서버 JWT | ⚪ S23-02 Firebase SDK 선행 |

### Sprint 25-4: 밸런스 일치 보장 (Phase 26에 흡수)

> **원본 S254-01~03**은 Phase 26 Sprint 26-1 (S261-01 `/data/version`) + Sprint 26-2 (S262-03 Balance → DB seed)로 통합.

### Sprint 25-5: QA + 롤아웃

| ID | 태스크 | 주체 | 상태 |
|----|--------|------|------|
| S255-01 | `/run` 봇 End-to-End 2시간 무중단 플레이 | 클라 | ⚪ |
| S255-02 | 수동 QA 체크리스트 (서버 다운 / JWT 만료 / DB 락) | 공동 | ⚪ |
| S255-03 | 로컬 세이브 마이그레이션 테스트 | 공동 | ⚪ `/save/migrate` 준비됨 |
| S255-04 | 프로덕션 배포 + 24시간 모니터링 | 서버 | ⚪ |

---

## Phase 27: 스토어 출시 + LiveOps 🆕 (서버 세션 통합 owner)

> **신규 도입 (2026-04-21).** 사용자 결정으로 `/starts` `/ends` 세션이 Google Play + App Store 출시 관리 + LiveOps 운영까지 풀 owner로 흡수. 기존 잔여 항목(S-B3 RTDN, ASSN V2, S22-07 Slack 출시 알림 등)도 본 Phase에 통합 관리.
> 클라 빌드/배포 자동화(GameCI Android/iOS)도 서버 세션이 owner. 클라 에이전트는 Unity Editor 작업 + 수동 검증만.

### 핵심 원칙

1. **서버 세션 = 출시 관리 사령탑**: starts/ends/heartbeat이 스토어 운영, IAP 백엔드, LiveOps, 빌드 파이프라인까지 통합 진행.
2. **자동화 우선**: 본인인증/결제수단 등록 외 모든 빌드/업로드/메타데이터 sync는 Fastlane + GameCI로 자동화.
3. **단계별 출시**: 내부 → 클로즈드 → 오픈 베타 → 프로덕션 (1% → 5% → 20% → 100%).
4. **출시 후 LiveOps 통합**: 시즌/이벤트는 Phase 26 CDN-First 데이터 파이프라인 활용 (앱 재빌드 없음).

### 사용자 수동 블로커 (자동화 불가, 사용자 직접 작업)

| 항목 | 비용 | 시점 | 비고 |
|------|------|------|------|
| Google Play Console 가입 | $25 일회성 | 출시 D-90 | 본인인증 + 결제수단 |
| Apple Developer Program | $99/년 | 출시 D-90 | 법인 또는 개인 명의 결정 |
| 본인인증 (스토어별) | $0 | 가입 시 | 전화/이메일/문서 |
| 결제수단 등록 | $0 | 가입 시 | 수수료 정산 계좌 |
| 개인정보 처리방침 작성 + URL 호스팅 | 변호사 비용 권장 | D-30 | https://mf-game.com/privacy 등 |
| 이용약관 작성 + URL 호스팅 | 동일 | D-30 | |
| 운영자 정보 + CS 채널 | $0 | D-30 | support@mf-game.com 등 |

### 예상 운영 비용 (월)

| 항목 | 비용 | 비고 |
|------|------|------|
| Apple Developer Program | $8.25/월 환산 | $99/년 |
| Google Play Developer | $0 | $25 일회성 (감가) |
| GitHub Actions macOS Runner (iOS 빌드) | $5~20/월 | 빌드당 약 10분 × 월 10회 |
| Crashlytics | $0 | Firebase 무료 |
| **합계 (출시 직후)** | **$15~30/월** | Phase 22 인프라($6~8) + Phase 27 |

---

### Sprint 27-1: Google Play Console 셋업

| ID | 태스크 | 자동화 | 상태 |
|----|--------|--------|------|
| S271-01 | 개발자 계정 등록 ($25, 본인인증) | ❌ 사용자 | ⚪ |
| S271-02 | 앱 생성 + 패키지명 `com.mfg.client` 등록 | ❌ 사용자 | ⚪ |
| S271-03 | 콘텐츠 등급 (IARC 설문) + 데이터 안전 섹션 | ❌ 사용자 | ⚪ |
| S271-04 | IAP 상품 등록 (서버 SKU 매핑 — Ruby/SummonTicket/HotDeal 등) | 🟡 반자동 | ⚪ S-B3 선행 |
| S271-05 | RTDN (Real-time Developer Notifications) — PubSub 토픽 + Subscription | ✅ 서버 | ⚪ S-B3 후속 흡수 |
| S271-06 | 내부 테스트 트랙 + 테스터 그룹 (Google Group) | ❌ 사용자 | ⚪ |
| S271-07 | Play 콘솔 API Service Account + GitHub Secret `PLAY_CONSOLE_SA` | 🟡 반자동 | ⚪ Sprint 27-4 선행 |

### Sprint 27-2: App Store Connect 셋업

| ID | 태스크 | 자동화 | 상태 |
|----|--------|--------|------|
| S272-01 | Apple Developer Program 가입 ($99/년) | ❌ 사용자 | ⚪ |
| S272-02 | Bundle ID `com.mfg.client` + App ID 등록 + Capabilities (IAP/Push) | ❌ 사용자 | ⚪ |
| S272-03 | 앱 생성 + TestFlight 그룹 + 외부 베타 검토 신청 | 🟡 반자동 | ⚪ |
| S272-04 | IAP 상품 등록 (Android와 SKU 동일 매핑) | 🟡 반자동 | ⚪ |
| S272-05 | ASSN V2 webhook (`POST /api/v1/iap/apple-notification`) — JWS 서명 검증 | ✅ 서버 | ⚪ 기존 ASSN V2 항목 흡수 |
| S272-06 | ATT (App Tracking Transparency) plist + 개인정보 라벨 | ❌ 사용자 | ⚪ |
| S272-07 | App Store Connect API Key + GitHub Secret `ASC_API_KEY` | 🟡 반자동 | ⚪ Sprint 27-4 선행 |

### Sprint 27-3: 스토어 메타데이터

| ID | 태스크 | 자동화 | 상태 |
|----|--------|--------|------|
| S273-01 | 앱 아이콘 (Android Adaptive 512px + iOS 1024px) | 🟡 디자인 사용자 | ⚪ |
| S273-02 | 스크린샷 (Phone × 한/영, Tablet 옵션) — Unity Recorder로 자동 캡처 가능 | 🟡 캡처 자동 + 텍스트 사용자 | ⚪ |
| S273-03 | 스토어 설명 (제목/짧은설명/상세설명/키워드, 한/영) | ❌ 사용자 | ⚪ |
| S273-04 | 프로모션 영상 (30s, 선택) | 🟡 사용자 | ⚪ |
| S273-05 | 개인정보 처리방침 + 이용약관 URL 호스팅 (Cloudflare Pages 무료) | ✅ 서버 인프라 | ⚪ |
| S273-06 | 운영자 정보 + CS 채널 (`support@mf-game.com` MX 레코드) | 🟡 반자동 | ⚪ |
| S273-07 | Fastlane metadata 디렉토리 (`fastlane/metadata/{ko,en-US}/`) — 메타 git화 | ✅ 서버 | 🟢 스캐폴딩 완료 (2026-04-21). TODO 콘텐츠 대기 (S273-01/02/03/04/06) |

### Sprint 27-4: 빌드 업로드 자동화

| ID | 태스크 | 자동화 | 상태 |
|----|--------|--------|------|
| S274-01 | GameCI Android AAB build (`mfg-client/.github/workflows/build-android.yml`) | ✅ 서버 | ⚪ Sprint 26-2 GameCI 셋업 재활용 |
| S274-02 | Fastlane Android `supply` (Play Console API → 내부테스트 트랙) | ✅ 서버 | ⚪ S271-07 선행 |
| S274-03 | GameCI iOS IPA build (`build-ios.yml` — macOS runner) | ✅ 서버 | ⚪ Mac runner 비용 |
| S274-04 | Fastlane iOS `pilot` (TestFlight 업로드) + `deliver` (App Store) | ✅ 서버 | ⚪ S272-07 선행 |
| S274-05 | 빌드 버전 자동 증가 (semver + buildNumber 태그 트리거) | ✅ 서버 | ⚪ |
| S274-06 | 서명 자동화 — Android keystore (`PlayKeystore.jks` GitHub Secret), iOS Match (`fastlane match`) | ✅ 서버 + 사용자 키 발급 | ⚪ |
| S274-07 | Slack 빌드 알림 (S22-07 통합) — `#mfg-builds` 채널 | ✅ 서버 | ⚪ |

### Sprint 27-5: 심사 + 단계별 출시

| ID | 태스크 | 자동화 | 상태 |
|----|--------|--------|------|
| S275-01 | 내부 테스트 (20명, 가족/지인) — 1주 운영 | 🟡 반자동 | ⚪ |
| S275-02 | 클로즈드 알파 (50~100명) → 오픈 베타 (수백~수천) | 🟡 반자동 | ⚪ |
| S275-03 | 프로덕션 단계 출시 (1% → 5% → 20% → 50% → 100%) | ✅ 서버 (Fastlane rollout) | ⚪ |
| S275-04 | 심사 거부 대응 — 정책 위반 분석 + 재제출 | ❌ 사용자 + 서버 | ⚪ |
| S275-05 | 출시 발표 (Twitter/Discord/공식 사이트) + 보도자료 | ❌ 사용자 | ⚪ |
| S275-06 | 출시 D-Day 모니터링 24시간 대기조 (서버 + Crashlytics) | ✅ 서버 | ⚪ S22-08 WAF 사전 활성화 |

### Sprint 27-6: LiveOps + 출시 후 운영

| ID | 태스크 | 자동화 | 상태 |
|----|--------|--------|------|
| S276-01 | Firebase Crashlytics SDK 통합 + Slack `#mfg-errors` 알림 | ✅ 서버 | ⚪ |
| S276-02 | ANR/Crash 추적 대시보드 (Crashlytics + 자체 admin) | ✅ 서버 | ⚪ S22-10 통합 |
| S276-03 | 리뷰 응답 (Google Play Console + ASC API) — 부정 리뷰 자동 알림 | 🟡 알림 자동 + 응답 사용자 | ⚪ |
| S276-04 | 시즌/이벤트 운영 — Phase 26 CDN-First 데이터 패치 | ✅ 서버 | ⚪ Phase 26 의존 |
| S276-05 | 강제 업데이트 트리거 (`/system/notice` Active + minVersion) | ✅ 서버 | ⚪ S261-05 확장 |
| S276-06 | KPI 모니터링 (DAU/MAU/ARPU/Retention) — Firebase Analytics + 자체 admin | ✅ 서버 | ⚪ S22-10 통합 |
| S276-07 | A/B 테스트 — 가챠 확률 / 신규 유저 보상 (Phase 26 Balance hot-reload 활용) | ✅ 서버 | ⚪ S261-04 의존 |
| S276-08 | 분기별 보안 회전 — GCP/AWS/R2 키 + Unity .ulf | 🟡 반자동 + 사용자 confirm | ⚪ |

---

### 의존성 그래프

```
[Phase 27 Sprint 27-1/27-2]  ← 사용자 가입 + 본인인증 (D-90)
                ↓
[Sprint 27-3 메타데이터]     ← 디자인 + 카피 작업 (D-60)
                ↓
[Sprint 27-4 빌드 자동화]    ← Sprint 26-2 GameCI 재활용 (D-30)
                ↓
[Sprint 27-5 심사 + 출시]    ← Apple 1~3일, Google 1~7일 (D-7 ~ D-Day)
                ↓
[Sprint 27-6 LiveOps]        ← 영구 운영 (D+1 이후)
```

### Phase 27 ↔ 기존 Phase 흡수 매핑

| 기존 항목 | Phase 27 흡수 위치 |
|----------|-------------------|
| S-B3 IAP 구독 DB 반영 | Sprint 27-1 S271-04 + Sprint 27-2 S272-04 |
| ASSN V2 (Apple webhook) | Sprint 27-2 S272-05 |
| PLAY-04 (Play Console RTDN) | Sprint 27-1 S271-05 |
| S22-07 Slack 알림 | Sprint 27-4 S274-07 + Sprint 27-6 S276-01 |
| S22-08 Cloudflare WAF | Sprint 27-5 S275-06 (출시 사전 활성화) |
| S22-10 관리자 대시보드 | Sprint 27-6 S276-02 / S276-06 |
| S22-11 Sentry/GlitchTip | Sprint 27-6 S276-01 (Crashlytics 우선, Sentry 보조) |
| S22-12 k6 부하 테스트 | Sprint 27-5 S275-06 사전 검증 |

---

## 잔여 서버 미해결 TODO (Phase 27에 흡수됨, 참고용 보존)

### S-B3 IAP 구독 DB 반영 (출시 후 OK)

- 현재: `PubSubSubscriberService.cs` — PubSub 메시지 수신 + 파싱 + `SubscriptionNotificationType` enum(13종) + 구조화 로깅 (2026-04-20 commit 33f60cd)
- 미완: Player 엔티티 `SubscriptionTier/Status/ExpiresAt` 컬럼 + IapReceipt `PurchaseToken` 컬럼 + Migration
- 블로커: Player/IapReceipt 스키마 변경은 프로덕션 DB 영향 → 출시 타이밍 조율 후 진행. 선행 작업 PLAY-04 (Play Console RTDN 연동).

### ASSN V2 (Apple 서버-서버 알림)

- Apple IAP 구독 갱신/취소 webhook 엔드포인트 신설 필요
- `POST /api/v1/iap/apple-notification` — Apple이 호출, JWS 서명 검증 후 IapReceipt 상태 반영
- Apple Developer 계정 + App Store Connect 구성 선행 필요

---

## 우선순위 요약

| 순위 | 작업 | 범위 |
|------|------|------|
| 🟢 1 | **Phase 26 Sprint 26-1 잔여** (S261-02 `/data/config/latest`, S261-04 `/admin/balance/reload`) | 서버, 1주 |
| 🟢 2 | **Phase 26 Sprint 26-2** (CI 파이프라인 — SO→JSON, R2 업로드) | 인프라 신설 에이전트, 2~3주 |
| 🟢 3 | **Phase 27 Sprint 27-1/27-2** (Google Play + App Store Console 셋업) | 사용자 가입 + 서버 자동화, D-90 시작 |
| 🟡 4 | **Phase 26 Sprint 26-4** (stg 환경 구성) | 서버 + 인프라, 1주 |
| 🟡 5 | **Phase 27 Sprint 27-3** (스토어 메타데이터 + 정책 URL) | 사용자 + 서버 호스팅, D-60 |
| 🟡 6 | **Phase 27 Sprint 27-4** (Fastlane 빌드 업로드 자동화) | 서버, D-30 |
| 🔴 7 | **Phase 27 Sprint 27-5** (심사 + 단계별 출시) | 서버 + 사용자 대응, D-7 ~ D-Day |
| 🔴 8 | **Phase 27 Sprint 27-6** (LiveOps + 출시 후 운영) | 서버 영구 | 

> **Phase 26 Sprint 26-1 (3/5)**: ✅ S261-01/03/05 완료 (2026-04-21). 잔여 S261-02/04는 Sprint 26-2 R2 업로드 흐름과 묶임.

---

## 갱신 규칙

- **이 문서(server/Docs/Planning/roadmap.md)가 서버 관련 모든 Phase의 단일 원천**
- 클라-서버 교차 Phase(23/25)는 원본(`client/Docs/Planning/roadmap.md`)에 "→ server/Docs/Planning/roadmap.md 참조" 링크 유지
- 새 서버 Sprint 추가 시:
  1. 이 문서에 Sprint 추가
  2. `../roadmap-index.md` 표에 한 줄 반영
  3. 관련 systems/*.md가 있으면 업데이트
- Phase 26 Sprint 진행 시 [`data-pipeline.md`](data-pipeline.md) 흐름도 동기화
