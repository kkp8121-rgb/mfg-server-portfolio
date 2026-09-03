---
read_count: 0
last_read: "never"
status: active
---

# MFG 출시/핫픽스 체크리스트 (Phase 26 Sprint 26-5 / Phase 27 Sprint 27-5)

> **범위**: 프로덕션 배포 전·중·후 검증 절차 단일 원천. 긴급 패치 드릴 + 스토어 출시(단계별 롤아웃) 공통 항목.
> **권장 사용**: git tag 푸시 전 "전"/배포 직후 "중"/24h 모니터링 "후" 순으로 체크.

## 공통 원칙

1. **Semver 태그로 배포**: `latest` 대신 `v{major}.{minor}.{patch}` 명시 — 롤백 기준점 확보
2. **단계별 롤아웃**: 내부 테스트 → 1% → 5% → 20% → 100% (Google Play 기준)
3. **드릴 주기**: 월 1회 긴급 패치 드릴(데이터 수정 → push → 3~5분 반영 검증) 실시
4. **롤백 결정 기준**: `/health` 503 지속 60s OR 5xx 에러율 > 5% → 즉시 롤백 (`server/Docs/rollback.md`)

---

## A) 배포 전 (Pre-flight, D-1 ~ D-0)

### 코드/테스트
- [ ] `dotnet build` 에러 0 (경고 5 기존 허용)
- [ ] `dotnet test` 통합 테스트 전원 PASS
- [ ] `git log origin/main..HEAD` 확인 — 의도하지 않은 커밋 없음
- [ ] 유출 스캔: `git grep -E 'AKIA|private_key|BEGIN.*PRIVATE'` 영 (0 match)

### 서버 계약
- [ ] `client/Assets/Scripts/Core/Net/ServerDtos.cs` 와 서버 DTO 필드 일치 — 추가만 있으면 역호환, 삭제/rename은 클라 동시 배포 필요
- [ ] `api/v1` prefix 유지 — breaking change 시 `api/v2` 분기
- [ ] `[Authorize]` / `[AllowAnonymous]` 재확인 — 실수로 anon 노출된 관리 API 없음

### 밸런스 영향
- [ ] `BalanceConfig.OfflineRewardMaxHours` / `GoldPerHourCoef` / 티어 값 변경 시 유저 영향 사전 고지 또는 A/B 테스트 대상
- [ ] `DailyCurrencyEarnCaps` 변경 시 어뷰징 재검토

### 인프라
- [ ] Lightsail 디스크 사용률 < 80% (`df -h`)
- [ ] MySQL 백업 최근 24h 내 성공 (`/opt/mfg/backups/latest-*.sql.gz`)
- [ ] 롤백 대상 직전 이미지 태그 기록 (`docker images | grep mfg-server`)

### 시크릿 (출시 전 D-7)
- [ ] GCP `mfg-sheets-writer` 키 회전 (채팅 로그 노출된 key 교체)
- [ ] R2 Secret Access Key 회전
- [ ] AWS Access Key `AKIA****XPG73I4` 회전
- [ ] Lightsail `.env` 의 `Admin__AllowedUserIds__0..N` 최소 1명 세팅 (deny-by-default 상태 탈출)

---

## B) 배포 중 (Push → CI → CD → Health, 0~5분)

```bash
# 릴리스 태깅 + 푸시
cd server
git tag v{X.Y.Z} -m "release: <요약>"
git push origin v{X.Y.Z}
git push origin main
```

### CI (GitHub Actions, ~1분 30초)
- [ ] `.github/workflows/server-ci.yml` 녹색 (dotnet test + GHCR push)
- [ ] GHCR `ghcr.io/kkp8121-rgb/mfg-server:v{X.Y.Z}` + `:latest` 양쪽 생성 확인

### CD (workflow_run → SSH deploy, ~35초)
- [ ] `.github/workflows/server-cd.yml` 녹색
- [ ] `/health` 200 retry 6회 모두 통과

### 즉시 스모크 (curl, 0분)
```bash
BASE=https://api.mf-game.com
curl -s -o /dev/null -w "%{http_code}\n" $BASE/health                          # 200
curl -s $BASE/api/v1/data/version | head -c 200                                 # 4 해시
curl -s $BASE/api/v1/data/config/latest | head -c 300                           # 4 entries
curl -s $BASE/api/v1/system/notice | head -c 300                                # active:false + minClientVersion
curl -s -o /dev/null -w "%{http_code}\n" $BASE/api/v1/event/offline-reward/preview  # 401 (인증 필요)
curl -s -o /dev/null -w "%{http_code}\n" $BASE/api/v1/admin/balance/snapshot       # 401/403
```

> 자동화: `bash server/scripts/post-deploy-smoke.sh` — 11 체크(health/data/notice/auth). `BASE` 환경변수로 로컬/stg 재사용 가능.

---

## C) 배포 후 (Post-deploy, 1h → 24h → 7d)

### 1h 모니터링
- [ ] Serilog `logs/mfg-*.log` 에 500 에러 급증 없음 (`grep -c "\"Error\"" logs/mfg-*.log`)
- [ ] Rate limit 429 빈도 평상시 대비 이상치 없음
- [ ] 감사 로그 `[Admin/Audit]` 정상 기록 (BalanceReload 호출 시)

### 24h 지표
- [ ] 유저 로그인 성공률 > 99%
- [ ] Offline Reward Preview p95 < 200ms
- [ ] Save/sync 30s 쓰로틀 거부율 < 1%
- [ ] DAU/신규가입 평일 대비 −20% 이상 하락 없음 (급락 시 롤백 검토)

### 7d 누적
- [ ] DB 증가분 예상 범위 내
- [ ] Admin reload 호출 이력 검토 (감사 로그 grep)

---

## D) 긴급 패치 드릴 (Phase 26 S265-01, 월 1회)

**목표**: Config JSON 수정 → push → 3~5분 내 클라 반영 검증.

### 단계
1. **변경 시뮬레이션**: `characters.json` 에서 몬스터 이름 1건 오타 수정 → CDN 재업로드 + `DataVersions:Config` 해시 갱신
2. **appsettings 수정 + push**: `appsettings.Production.json` 의 `DataVersions:Config` 업데이트 → git push
3. **자동 배포 대기** (~2~3분): CI/CD
4. **검증**: `curl /api/v1/data/version` 새 해시 반환 확인 → 신규 클라가 CDN에서 최신 JSON 다운로드 → 앱 재시작 없이 반영
5. **롤백 리허설**: 구해시로 되돌리고 같은 절차 반복

### 드릴 성공 기준
- [ ] 수정 푸시 → CDN 반영까지 < 5분
- [ ] 클라 재기동 없이 인게임 이름 변경 관찰 (Phase 26 Sprint 26-3 ConfigLoader 완성 후)
- [ ] 롤백 시 원본 해시 URL 여전히 CDN에 존재 (영구 파일 원칙)

---

## E) 스토어 출시 단계별 롤아웃 (Phase 27 Sprint 27-5)

### Google Play Staged Rollout
| 단계 | 비율 | 대기 시간 | 중단 조건 |
|------|-----|----------|----------|
| Internal test | 테스터 그룹 | 무기한 | 크래시 리포트 > 0 또는 QA 거부 |
| Closed beta | 초대 100~500 | 3~7일 | 주요 버그 발견 |
| Open beta | 무제한 지원자 | 3~7일 | Crashlytics 크래시율 > 1% |
| Production 1% | 일부 유저 | 24h | ANR율 > 0.5% |
| Production 5% | 20배 확대 | 24h | 수익 지표 하락 |
| Production 20% | 4배 | 24h | — |
| Production 100% | 전체 | 영구 | — |

### App Store Phased Release
- Apple 승인 후 자동 Phased Release (7일간 2% → 5% → 10% → 20% → 50% → 100%)
- Pause/Resume 버튼으로 언제든 중단 가능
- 심사 거부 시 `fastlane deliver --reject_if_possible` 로 이전 빌드 유지

### 공통 거부 대응
- [ ] Play/App Store 심사 거부 카테고리별 답변 템플릿 준비 (개인정보 처리방침, 결제 수수료, 광고 표기 등)
- [ ] `fastlane/metadata/review_information/` 에 심사자용 테스트 계정 + 시연 동영상

---

## F) 관련 문서

- `server/Docs/rollback.md` — 1분 내 롤백 런북 (A/B/C/D 시나리오)
- `server/Docs/deploy.md` — Lightsail 배포 런북
- `server/Docs/backup.md` — DB 백업/복원
- `server/Docs/secrets.md` — Secrets 3-tier + Admin 화이트리스트
- `server/Docs/Planning/roadmap.md` — Phase 26/27 상세
- `client/fastlane/README.md` — 스토어 메타 디렉토리 가이드
