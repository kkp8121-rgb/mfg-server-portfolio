#!/usr/bin/env bash
# MFG 배포 직후 스모크 테스트 (release-checklist.md B 단계 자동화)
#
# 사용법:
#   bash scripts/post-deploy-smoke.sh                     # prod 기본 (api.mf-game.com)
#   BASE=http://localhost:5062 bash scripts/post-deploy-smoke.sh   # 로컬
#   BASE=https://api.mf-game.com bash scripts/post-deploy-smoke.sh
#
# 반환:
#   0 = 전체 통과
#   1 = 한 개 이상 실패 (상세 내역은 stdout)
#
# Phase 26 Sprint 26-1 (S261-01/02/03/05 + S261-04) + /health + 인증 가드 검증.
# 깊은 사용자 플로우(가챠/세이브 등)는 포함하지 않음 — 토큰/DB 상태 의존.

set -uo pipefail

BASE="${BASE:-https://api.mf-game.com}"
FAIL=0
PASS=0

# ─── 유틸 ─────────────────────────────────────────────

check_status() {
  local desc="$1" expected="$2" method="$3" path="$4"
  shift 4
  local actual
  # -o /dev/null: 응답 본문 버림. -w %{http_code}: 상태 코드만 추출.
  actual=$(curl -s -o /dev/null -w "%{http_code}" -X "$method" "$BASE$path" "$@" 2>/dev/null || echo "000")
  if [ "$actual" = "$expected" ]; then
    echo "  ✅ $desc → $actual"
    PASS=$((PASS + 1))
  else
    echo "  ❌ $desc → expected $expected, got $actual ($method $path)"
    FAIL=$((FAIL + 1))
  fi
}

check_json_field() {
  local desc="$1" path="$2" field="$3"
  local body
  body=$(curl -s "$BASE$path" 2>/dev/null || echo "")
  # field 가 응답 JSON 에 존재하는지만 확인(값 비교는 테스트 스위트의 몫).
  # grep -oE '"field"\s*:' 로 간단 매칭. 인용부호 내 파이프 이슈 없음.
  if echo "$body" | grep -qE "\"$field\"[[:space:]]*:"; then
    echo "  ✅ $desc — field \"$field\" present"
    PASS=$((PASS + 1))
  else
    echo "  ❌ $desc — field \"$field\" missing (GET $path)"
    echo "     body head: $(echo "$body" | head -c 200)"
    FAIL=$((FAIL + 1))
  fi
}

echo "========================================"
echo " MFG post-deploy smoke — BASE=$BASE"
echo "========================================"

# ─── 1. Health ─────────────────────────────────────────
echo ""
echo "[1] Health"
check_status "/health 200" 200 GET /health

# ─── 2. Phase 26 Data Delivery (AllowAnonymous) ────────
echo ""
echo "[2] Phase 26 Data Delivery API"
check_status    "/api/v1/data/version 200"        200 GET /api/v1/data/version
check_json_field "/data/version has catalog hash"  /api/v1/data/version catalog

check_status    "/api/v1/data/config/latest 200"  200 GET /api/v1/data/config/latest
check_json_field "/data/config/latest has entries" /api/v1/data/config/latest entries

# ?type= 필터 검증 (알려진 타입 1개만 반환되어야 함)
check_status    "/api/v1/data/config/latest?type=characters 200" 200 GET "/api/v1/data/config/latest?type=characters"

# ─── 3. System Notice (+ minClientVersion Phase 27-6) ──
echo ""
echo "[3] System Notice"
check_status    "/api/v1/system/notice 200"       200 GET /api/v1/system/notice
check_json_field "notice has minClientVersion"     /api/v1/system/notice minClientVersion
check_json_field "notice has forceUpdateMessage"   /api/v1/system/notice forceUpdateMessage

# ─── 4. 인증 가드 ─────────────────────────────────────
echo ""
echo "[4] Authorization guards (no JWT header)"
# 인증 필요 엔드포인트 — JWT 미첨부 시 401 이어야 함. dev 환경에서만 X-Dev-Uid 로 우회 가능.
check_status "/event/offline-reward/preview → 401" 401 GET /api/v1/event/offline-reward/preview

# 관리자 화이트리스트 (미세팅 상태 = 모든 호출 403, 세팅 상태 = 200)
# JWT 없이 호출 시 401 또는 403 (deny-by-default 또는 Authorization 실패). 둘 다 허용.
echo "  ℹ️  /admin/balance/snapshot 예상: 401 (JWT 없음) 또는 403 (화이트리스트 미세팅)"
actual=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/v1/admin/balance/snapshot" 2>/dev/null || echo "000")
if [ "$actual" = "401" ] || [ "$actual" = "403" ]; then
  echo "  ✅ /admin/balance/snapshot → $actual (거부 정상)"
  PASS=$((PASS + 1))
else
  echo "  ❌ /admin/balance/snapshot → $actual (401/403 기대)"
  FAIL=$((FAIL + 1))
fi

# ─── 결과 ──────────────────────────────────────────────
echo ""
echo "========================================"
echo " PASS: $PASS / FAIL: $FAIL"
echo "========================================"

if [ "$FAIL" -gt 0 ]; then
  echo "❌ 배포 검증 실패 — 롤백 검토 (server/Docs/rollback.md)"
  exit 1
fi

echo "✅ 배포 검증 통과"
exit 0
