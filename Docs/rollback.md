---
read_count: 0
last_read: "never"
status: active
---

# MFG 롤백 런북 (S22-06)

> **목표**: 배포 직후 장애 발생 시 **1분 내 이전 버전 복귀**.

## 태그 전략

CI 워크플로(`.github/workflows/ci.yml`)는 GHCR에 다음 태그를 동시 푸시한다.

| 태그 | 트리거 | 용도 |
|------|--------|------|
| `latest` | main 푸시마다 갱신 | CD 기본 대상 |
| `sha-<7자리>` | main 푸시마다 | 정확한 커밋 고정 |
| `v{major}.{minor}.{patch}` | git tag `v*.*.*` 푸시 | 릴리스 고정점, **롤백 기준점** |

### 릴리스 태깅 절차

```bash
cd server
git tag v1.0.0 -m "first prod release"
git push origin v1.0.0
# → CI가 ghcr.io/<owner>/mfg-server:v1.0.0 + :latest 동시 푸시
# → 이전 v0.9.x 이미지는 GHCR에 영구 보존 (수동 삭제 전까지)
```

매 프로덕션 배포는 **semver 태그를 선행**하고 `latest` 대신 명시적 버전 태그로 배포한다(workflow_dispatch).

## 롤백 시나리오

### A) 배포 직후 `/health` 실패 — CD가 자동 감지

CD 워크플로의 health check(`for i in 1..6; sleep 10; curl /health`)가 6회 실패 시 exit 1.
배포는 이미 `docker compose up -d`를 실행했으므로 **자동 되감기는 없음**.
즉시 수동 롤백(B) 시행.

### B) 수동 롤백 — GitHub Actions `workflow_dispatch` (권장)

GitHub → Actions → **cd** → Run workflow →
- `image_tag`: 이전 정상 버전 (예: `v1.0.0` 또는 `sha-abc1234`)
- Run workflow 클릭 → 40초 내 pull + up -d + health 재확인 완료

이 경로가 "1분 내 롤백" 표준.

### C) SSH 긴급 롤백 — GitHub Actions 장애 시

```bash
ssh -i LightsailDefaultKey-*.pem ubuntu@api.mf-game.com
cd /opt/mfg/server

# 직전 이미지 해시 확인 (docker ps)
sudo docker ps --format '{{.Image}}' | grep mfg-server

# 이전 태그 명시로 다시 up
sudo API_IMAGE=ghcr.io/<owner>/mfg-server:v1.0.0 \
     docker compose -f docker-compose.prod.yml up -d api

# 헬스
curl https://api.mf-game.com/health
```

`docker-compose.prod.yml`의 `image:` 라인이 `${API_IMAGE:-ghcr.io/...:latest}` 형태이므로 환경변수 오버라이드만으로 특정 버전 고정 가능.

### D) DB 마이그레이션까지 반영된 경우

> ⚠️ 앱 컨테이너 교체만으로 해결 안 되는 경우 — 신규 버전이 **새 컬럼/테이블**을 추가했고, 롤백하려는 구 버전이 그 스키마를 모르는 상황.

선택지 2가지:
1. **Forward fix** (권장) — hotfix 브랜치 → 새 `v*.*.*` 태그로 재배포 (주로 10분 내 가능)
2. **Hard rollback** — 백업 시점까지 DB 복원 + 데이터 손실 감수
   - `server/Docs/backup.md` 참조
   - 백업 시각 이후 유저 활동은 전부 소실 → 공지 필수

EF Core migration은 기본적으로 **non-destructive 추가만 수행**(add column, add index)하므로 대부분 1번 경로 유효. 파괴적 변경(drop column/table) 시에는 다음 규칙 준수:
- 1단계: 신규 컬럼 추가만 배포 (구 버전과 호환)
- 2단계: 앱 로직 구 컬럼 미사용 배포
- 3단계: 구 컬럼 drop 마이그레이션 배포

3단계 배포로 언제든 2단계까지 안전 롤백.

## 사전 체크리스트 (매 릴리스)

- [ ] 이전 `:latest`의 실제 sha 또는 semver를 `#mfg-builds` Slack 또는 로컬 메모에 기록
- [ ] 새 마이그레이션이 파괴적이면 위 3단계 규칙을 따랐는지 확인
- [ ] 릴리스 태그 `git tag v*.*.*` 후 push
- [ ] CI/CD 완료 후 `/health` + 주요 API 1개 수동 호출 (스모크 테스트)

## 롤백 후 조치

1. `#mfg-alerts` Slack 포스트: 어떤 버전으로 롤백했는지 + 재발 방지 조치 일정
2. GitHub Issue 생성: 원인 분석 / 재현 / 수정 PR 링크
3. 롤백된 이미지는 **삭제하지 말 것** — 재현 디버깅용 보존
