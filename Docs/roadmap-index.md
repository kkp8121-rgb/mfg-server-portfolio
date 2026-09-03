---
read_count: 0
last_read: "never"
status: active
---

# 서버 로드맵 인덱스

> **2026-04-20 재편**: 서버 전용 로드맵이 `server/Docs/Planning/`으로 이관. 이 파일은 **경량 포인터**로만 동작.
> `/starts` 세션 진입 시 이것부터 읽고, 깊이 필요하면 Planning/ 문서로 점프.

## 🗂️ 문서 구조

| 파일 | 목적 |
|------|------|
| [`Planning/roadmap.md`](Planning/roadmap.md) | **서버 로드맵 단일 원천**. Phase 18~26 전체 |
| [`Planning/core-architecture.md`](Planning/core-architecture.md) | 3-Tier 데이터 아키텍처 (Visual/Config/Balance) |
| [`Planning/data-pipeline.md`](Planning/data-pipeline.md) | SO → Sheets → CDN/서버 CI 파이프라인 |
| [`Planning/offline-reward-model.md`](Planning/offline-reward-model.md) | 오프라인 보상 모델 ("접속 안 한 시간" 계산) |
| [`deploy.md`](deploy.md) | Lightsail 배포 런북 |
| [`rollback.md`](rollback.md) | 1분 내 롤백 런북 |
| [`backup.md`](backup.md) | DB 백업/복원 |
| [`secrets.md`](secrets.md) | 3-tier Secrets 관리 |
| [`release-checklist.md`](release-checklist.md) | 🆕 출시/핫픽스/드릴/스토어 단계 롤아웃 체크리스트 |

## 📊 Phase 현황 (2026-04-20)

| Phase | 주제 | 상태 | 위치 |
|-------|------|------|------|
| **Phase 18** | Server Phase 1 — Core Backend (.NET 스캐폴딩 / Auth / 가챠 / 재화 / IAP / 저장 / 출석 / 클라 연동) | ✅ 완료 | Planning/roadmap.md |
| **Phase 19** | Server Phase 2 — Competitive/Social (아레나 / 길드) | ✅ 완료 | Planning/roadmap.md |
| **Phase 20** | Server Phase 3 — Enhancement + Deploy (스타포스 / Lightsail 배포) | ✅ 완료 | Planning/roadmap.md |
| **Phase 22** | DevOps Foundation (CI/CD / Secrets / 백업 / 롤백 / 알림 / 레이트리밋) | 🟢 대부분 완료 (S22-07 Slack 수동 작업 잔여) | Planning/roadmap.md |
| **Phase 23 S23-08** | PATCH /auth/profile JobId 필드 | ✅ 완료 (2026-04-20 commit 33f60cd) | Planning/roadmap.md |
| **Phase 25 S251-01** | POST /save/migrate | ✅ 완료 (2026-04-20) | Planning/roadmap.md |
| **Phase 25 Sprint 25-3** | 오프라인 정책 확정 (/event/offline-reward/preview 신설 필요) | 🔨 진행 필요 | Planning/offline-reward-model.md |
| **Phase 26** 🆕 | 3-Tier 데이터 아키텍처 + CDN Config Delivery | 🟢 Sprint 26-1 **5/5 완료** (S261-01~05, 2026-04-21). 26-2 CI 파이프라인 대기 | Planning/roadmap.md / core-architecture.md / data-pipeline.md |
| **Phase 27** 🆕 | 스토어 출시 (Google Play + App Store) + LiveOps | 🆕 도입 (2026-04-21). 서버 세션 풀 owner. 27-1~27-6 모두 미진행 | Planning/roadmap.md Phase 27 |

## 🎯 잔여 서버 작업 (우선순위)

### 🟢 단기 (당장 가능)
1. ✅ **Phase 26 Sprint 26-1 전체 5/5 완료** (S261-01/02/03/04/05, 2026-04-21)
2. **Phase 26 Sprint 26-2** CI 파이프라인 착수 (~2~3주, 인프라 에이전트 신설 권장)
3. **Phase 27 Sprint 27-1/27-2** — 사용자 가입 작업 시작 (D-90 권장)
   - Google Play Console ($25 일회성)
   - Apple Developer Program ($99/년)

### 🟡 중기 (선행 조건 있음)
4. **Phase 26 Sprint 26-2** (2~3주, 인프라 에이전트 신설 필요) — SO→JSON CI 파이프라인 + Addressables Build + R2 업로드
5. **Phase 27 Sprint 27-3** (D-60) — 스토어 메타데이터, 정책 URL, 아이콘/스크린샷
6. **Phase 27 Sprint 27-4** (D-30) — Fastlane Android/iOS 빌드 업로드 자동화
7. **Phase 26 Sprint 26-4** (1주) — Staging 환경 구성

### 🔴 출시 직전 ~ 출시 후
8. **Phase 27 Sprint 27-5** (D-7~D-Day) — 심사 + 단계별 출시 (1%→100%)
9. **Phase 27 Sprint 27-6** (D+1 영구) — LiveOps, Crashlytics, KPI 모니터링, A/B 테스트
10. **Phase 25 Sprint 25-5** — 프로덕션 배포 QA + 24시간 모니터링 (Phase 27 통합)

## 🚧 사용자 수동 작업 블로커

### Phase 26 (모두 ✅ 완료, 2026-04-21)
- [x] 클라 GitHub 리포 (mfg-client) ✅
- [x] Unity Personal 라이선스 `.ulf` ✅ (Hub 활성화 → C:\ProgramData\Unity\Unity_lic.ulf)
- [x] GitHub Secrets `UNITY_LICENSE` / `UNITY_EMAIL` / `DLFJS18SHA2_A`(=UNITY_PASSWORD) ✅
- [x] Cloudflare R2 API Token + Custom Domain `cdn.mf-game.com` ✅
- [x] GCP Service Account `mfg-sheets-writer` + JSON key + GitHub Secret `GCP_SHEETS_SA` ✅
- [x] Master Google Sheet `MFG Master Balance` 4탭 ✅

### 🔴 출시 전 보안 회전 (D-7 권장)
- [ ] GCP `mfg-sheets-writer` 키 회전 (private_key 채팅 노출)
- [ ] R2 Secret Access Key 회전 (2026-04-20 노출)
- [ ] AWS Access Key `AKIA****XPG73I4` 회전 (2026-04-20 노출)

### Phase 27 출시 관리 사용자 블로커
- [ ] **Google Play Developer Account** 등록 ($25 일회성, 본인인증)
- [ ] **Apple Developer Program** 가입 ($99/년, 본인인증)
- [ ] **결제수단 등록** (스토어별 수수료 정산용)
- [ ] **개인정보 처리방침** 작성 + URL 호스팅 (변호사 검토 권장)
- [ ] **이용약관** 작성 + URL 호스팅
- [ ] **운영자 정보** + CS 채널 (`support@mf-game.com` MX 레코드)
- [ ] **앱 아이콘/스크린샷** 디자인 (Sprint 27-3, 디자인 리소스 필요 시)

### Phase 22 출시 전 잔여
- [ ] S22-07 Slack Workspace + Webhook (30분, Phase 27-4/27-6 통합)
- [ ] S22-08 Cloudflare WAF Free 규칙 (Phase 27-5 사전 활성화)

## 🔗 클라-서버 교차 Phase

| Phase | 서버 책임 | 클라 책임 |
|-------|----------|----------|
| Phase 21 Addressables | 없음 (CDN은 R2) | [client/Docs/Planning/roadmap.md Phase 21](../../client/Docs/Planning/roadmap.md) |
| Phase 23 Onboarding | ✅ S23-08 완료 | [client/Docs/Planning/roadmap.md Phase 23](../../client/Docs/Planning/roadmap.md) Sprint 23 |
| Phase 24 Sheets 이관 | Sprint 24-5 → Phase 26에 흡수 | [client/Docs/Planning/roadmap.md Phase 24](../../client/Docs/Planning/roadmap.md) |
| Phase 25 서버 권위 | Sprint 25-1/3 서버 엔드포인트 제공 | [client/Docs/Planning/roadmap.md Phase 25](../../client/Docs/Planning/roadmap.md) Sprint 25-1/2/3 클라 전환 |
| Phase 26 데이터 파이프라인 | 서버 API 5종 + CI/CD | **Sprint 26-3** ConfigLoader + Bootstrap 플로우 (클라 에이전트) |

## 📝 갱신 규칙

- **서버 전용 Phase**: `Planning/roadmap.md`가 원천. 이 인덱스는 표만 동기화.
- **클라-서버 교차 Phase**: 서버 측은 `Planning/roadmap.md`, 클라 측은 `client/Docs/Planning/roadmap.md`. 양쪽 상호 링크 유지.
- 새 서버 Sprint 추가 시: 
  1. `Planning/roadmap.md` 상세 작성
  2. 이 파일 표에 한 줄 추가
  3. 관련 Planning/*.md 있으면 업데이트
- Sprint 상태 변경 시: `Planning/roadmap.md`가 우선, 이 인덱스는 주기적 동기화

## 🚀 `/starts` 세션 루틴

1. 이 파일 읽기 (30초) → 서버 현황 파악
2. 필요하면 `Planning/roadmap.md` Phase 열어서 상세 확인
3. 작업 착수 시 `/go` 또는 수동 진행
4. 종료 시 `/ends` — 상태 동기화
