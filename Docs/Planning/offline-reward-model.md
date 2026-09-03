---
read_count: 0
last_read: "never"
status: active
---

# MFG 오프라인 보상 모델

> **작성 2026-04-20.** "오프라인 플레이 = 접속하지 않은 시간" 모델 상세.
> 관련: [`roadmap.md`](roadmap.md) Phase 25 Sprint 25-3 / [`core-architecture.md`](core-architecture.md)

## 핵심 정의

**오프라인 플레이는 개념상 존재하지 않는다.**

대신:
- **오프라인 시간** = 마지막 로그아웃 시각부터 재접속 시각까지의 경과 시간
- **오프라인 보상** = 서버가 경과 시간 × 방치 보상 공식으로 **재접속 시 일괄 계산해 지급**
- **실제 게임 실행** = 접속(온라인) 상태에서만 가능

이 모델의 효과:
- 클라는 오프라인 계산 로직 0 → 치트/해킹 경로 원천 차단
- 서버 단일 진실 소스 → 밸런스 조정 즉시 반영
- 네트워크 필수 설계 정당화 → 서버 권위 모드와 일관

**현업 참조**: 메이플키우기, 쿠키런 킹덤 탐험, Idle Heroes 등 아이들 RPG 표준.

---

## 데이터 모델

### Player 엔티티 관련 필드

```csharp
// server/src/MFG.Domain/Entities/Player.cs (기존)
public class Player : BaseEntity {
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogoutAt { get; set; }  // 마지막 로그아웃. null이면 한번도 로그아웃 안 함
    public int Level { get; set; } = 1;
    // ... 기타 필드
}
```

### BalanceTables (기존)

```csharp
// server/src/MFG.Server/Services/BalanceTables.cs:205
public static float GetOfflineRewardMultiplier(int level) => level switch {
    < 10  => 1.0f,
    < 20  => 1.2f,
    < 30  => 1.5f,
    < 50  => 2.0f,
    < 70  => 2.5f,
    < 100 => 3.0f,
    _     => 4.0f
};
```

### 공식 (현재)

`server/src/MFG.Server/Controllers/EventController.cs:81-88`:

```csharp
case "OfflineReward":
    var elapsed = DateTime.UtcNow - (player.LastLogoutAt ?? player.LastLoginAt);
    var hours = Math.Min(elapsed.TotalHours, 24); // 최대 24시간
    var multiplier = BalanceTables.GetOfflineRewardMultiplier(player.Level);
    long offlineGold = (long)(hours * 1000 * player.Level * 0.9 * multiplier);
    rewards.Add(new RewardEntry { Type = "Gold", Amount = offlineGold });
    break;
```

**현재 공식 분해**:
```
offlineGold = hours × 1000 × level × 0.9 × multiplier

예) 레벨 50 유저가 12시간 방치:
  hours = 12
  multiplier = 2.0 (50레벨 구간)
  offlineGold = 12 × 1000 × 50 × 0.9 × 2.0 = 1,080,000 Gold
```

**누적 상한**: 12시간 (→ 24시간으로 설정됨, **기획 상 12시간이 적절한지 재검토 필요**)

---

## API 흐름

### 기존 엔드포인트 (완료)

| 메서드 | 경로 | 용도 |
|--------|------|------|
| `POST` | `/api/v1/auth/logout` | LastLogoutAt 기록 |
| `POST` | `/api/v1/event/claim` | body: `{eventType:"OfflineReward"}` → 계산 + 재화 지급 |

### 신설 필요 (Phase 26 S261-03)

| 메서드 | 경로 | 용도 |
|--------|------|------|
| `GET` | `/api/v1/event/offline-reward/preview` | 현재 예상 보상 미리 확인 (지급 X) |

**preview 응답 예시**:
```json
{
  "elapsedSeconds": 43200,
  "elapsedHours": 12.0,
  "cappedHours": 12.0,
  "maxHours": 24.0,
  "rewards": [
    { "type": "Gold", "amount": 1080000 },
    { "type": "Exp", "amount": 12500 }
  ],
  "multiplier": 2.0,
  "serverTime": "2026-04-20T05:30:00Z"
}
```

**preview 구현 포인트**:
- 읽기 전용. DB 상태 변경 금지 (claim과 명확히 분리)
- 계산 로직은 `POST /event/claim` OfflineReward 분기와 **동일한 함수 재사용**
- 유저가 보는 "받기 전 확인" UI용

---

## 재접속 플로우 (상세)

### 앱 종료 직전

```
[클라]
  OnApplicationQuit() / OnApplicationPause(true)
  → POST /api/v1/auth/logout
    (fire-and-forget, 실패해도 로컬 lastLogoutUtc 폴백)
[서버]
  player.LastLogoutAt = DateTime.UtcNow
  SaveChanges
```

### 앱 재실행

```
T시간 경과 (오프라인)

[클라]
1. Splash 표시
2. 서버 헬스/버전 체크 (앞 단계)
3. POST /api/v1/auth/login
   → JWT + Player data
   → 클라가 player.LastLogoutAt 값 알 수 있음 (표시용)

4. GET /api/v1/event/offline-reward/preview
   [서버]
     elapsed = now() - (LastLogoutAt ?? LastLoginAt)
     hours = min(elapsed, 24)
     rewards 계산
     return preview (DB 변경 X)
   
5. 클라: "12시간 방치, 108만 골드 획득 가능" UI
   [ 받기 ] 버튼

6. 유저 탭 → POST /api/v1/event/claim {eventType: "OfflineReward"}
   [서버]
     동일 계산 (preview와 같은 함수)
     재화 지급 (CurrencyService.EarnAsync)
     player.LastLogoutAt = DateTime.UtcNow ← 🆕 재계산 방지 (현재는 미구현)
     return { actualRewards }

7. 클라: 획득 연출 + 재화 업데이트

8. Main 씬 진입
```

### ❗ 현재 구현 미완 사항

**문제**: `POST /event/claim` OfflineReward 처리 후 `LastLogoutAt`을 갱신하지 않으면:
- 유저가 다시 방치 → 같은 시간대 중복 claim 가능

**해결 (Phase 25-3 구현 시)**:
```csharp
case "OfflineReward":
    var elapsed = DateTime.UtcNow - (player.LastLogoutAt ?? player.LastLoginAt);
    // ... 계산
    rewards.Add(...);
    
    // 🆕 claim 후 LastLogoutAt을 현재 시각으로 갱신 → 재계산 방지
    player.LastLogoutAt = DateTime.UtcNow;
    // 또는 별도 LastOfflineRewardClaimedAt 필드 신설
    break;
```

---

## 엣지 케이스

### 케이스 1: LastLogoutAt이 null (신규 유저)

**조건**: 가입 후 첫 세션.

**동작**: `player.LastLogoutAt ?? player.LastLoginAt` → LastLoginAt 사용 (가입 시각)

**결과**: 가입 직후 12시간 방치 → 보상 정상 지급

### 케이스 2: 0초 경과 (바로 재접속)

**조건**: 로그아웃 직후 바로 재로그인.

**동작**: `elapsed.TotalHours < 0.001` → `hours * 1000 * ... = 0`

**결과**: 보상 0원. Preview에서도 0 표시. 클라 UI는 "받기" 버튼 비활성화 권장.

### 케이스 3: 미래 시각 (시스템 시계 조작)

**조건**: 디바이스 시간을 미래로 바꿔 서버에 전송?

**방어**: 서버는 DateTime.UtcNow 기준으로 계산. 클라 시간 무시.

### 케이스 4: 12시간 초과 방치

**조건**: 30일간 접속 없음.

**동작**: `Math.Min(elapsed.TotalHours, 24)` → 24시간으로 캡

**결과**: 30일 방치해도 24시간분만 지급. "더 자주 접속 유도"가 의도.

**기획 결정 필요**: 24시간 vs 12시간 vs 48시간 중 어느 것이 리텐션에 최적? 현재 24시간 설정됨.

### 케이스 5: 레벨업 전후 접속

**조건**: 방치 중 레벨 변화는 없음 (오프라인이므로). 재접속 시 현재 레벨 기준.

**동작**: `GetOfflineRewardMultiplier(player.Level)` — 현재 저장된 레벨 사용.

**결과**: 방치 시작 시 49레벨이었어도, 재접속 시 50레벨이면 50레벨 배율 적용.

### 케이스 6: 일일 재화 상한 도달

**조건**: 이미 오늘 골드 1억 획득 (상한 도달) + 오프라인 보상 지급 시도.

**동작**: `CurrencyService.EarnAsync`에서 `ValidationService.CheckDailyCap` → 초과분 감액 또는 reject.

**현재 동작**: `InvalidOperationException` throw (완전 거부). 기획상 "초과분 이월"이 유저 친화적일 수 있으나 악용 가능.

**기획 결정 필요**: 일일 상한 초과 시 
- (a) 완전 거부 (현재)
- (b) 상한까지만 지급 + 초과분 버림
- (c) 상한까지만 지급 + 초과분 다음날 이월

---

## 보안

### ❌ 클라가 할 수 없는 일

- 자기 LastLogoutAt 수정
- 경과 시간 조작
- Level 속여서 배율 올리기
- 보상 공식 파악

### 🔒 서버가 하는 일

- DateTime.UtcNow 기준 계산 (클라 시간 무시)
- Player 엔티티에서 Level 직접 읽기
- 재화 지급 시 `CurrencyService.EarnAsync` 경유 → 일일 상한 검증
- 지급 이력 `CurrencyTransaction` 테이블에 기록 → 감사 가능

### ✅ 클라가 볼 수 있는 것

- 자기 LastLogoutAt 값 (표시용)
- Preview 응답의 예상 보상 (실제 계산값)
- 지급 결과 (수령한 재화)

### ❌ 클라가 볼 수 없는 것

- `GetOfflineRewardMultiplier` 함수 내용
- 공식 계수 (1000, 0.9)
- 다른 유저의 보상 내역

---

## 클라-서버 계약

### 클라 구현 체크리스트

- [ ] `OnApplicationQuit`, `OnApplicationPause(true)` → `POST /auth/logout` (fire-and-forget)
- [ ] 재접속 시 `POST /auth/login` 성공 후 `GET /event/offline-reward/preview` 호출
- [ ] 0 보상일 때 "받기" 버튼 비활성화 (서버 호출 낭비 방지)
- [ ] 받기 버튼 → `POST /event/claim {eventType:"OfflineReward"}` 
- [ ] 수령 성공 시 재화 UI 갱신
- [ ] 수령 실패 시 에러 처리 (네트워크/일일 상한 등)

### 서버 구현 체크리스트

- [x] `Player.LastLogoutAt` 필드 (완료)
- [x] `POST /auth/logout` → LastLogoutAt 기록 (완료)
- [x] `POST /event/claim {eventType:"OfflineReward"}` → 지급 (완료)
- [x] `BalanceTables.GetOfflineRewardMultiplier` (완료)
- [ ] `GET /event/offline-reward/preview` (Phase 26 S261-03)
- [ ] Claim 후 `LastLogoutAt` 갱신 (재계산 방지)
- [ ] Preview/Claim 공통 계산 함수 분리 (DRY)
- [ ] 일일 상한 초과 시 정책 확정

---

## 리팩토링 제안 (Phase 25-3 구현 시)

현재 EventController에 인라인 계산. 서비스로 분리:

```csharp
// 신설: Services/OfflineRewardService.cs
public class OfflineRewardCalculation {
    public double ElapsedHours { get; set; }
    public double CappedHours { get; set; }
    public double Multiplier { get; set; }
    public List<RewardEntry> Rewards { get; set; }
}

public class OfflineRewardService {
    public OfflineRewardCalculation Calculate(Player player) {
        var elapsed = DateTime.UtcNow - (player.LastLogoutAt ?? player.LastLoginAt);
        var hours = Math.Min(elapsed.TotalHours, BalanceTables.OfflineRewardMaxHours);
        var multiplier = BalanceTables.GetOfflineRewardMultiplier(player.Level);
        
        var rewards = new List<RewardEntry>();
        long offlineGold = (long)(hours * 1000 * player.Level * 0.9 * multiplier);
        rewards.Add(new RewardEntry { Type = "Gold", Amount = offlineGold });
        
        // TODO: Exp 추가? 재화 외 보상 추가?
        
        return new OfflineRewardCalculation {
            ElapsedHours = elapsed.TotalHours,
            CappedHours = hours,
            Multiplier = multiplier,
            Rewards = rewards
        };
    }
}
```

**재사용**:
- `EventController.Claim` (OfflineReward 분기)
- 신설 `OfflineRewardPreviewController`

---

## 기획 미확정 사항 (팀 논의 필요)

| 항목 | 현재 | 대안 |
|------|------|------|
| 누적 최대 시간 | 24h | 12h (과한 방치 억제) / 48h (주말 복귀 배려) |
| 일일 상한 초과 | Reject | 상한까지만 지급 (권장) |
| Exp 지급 여부 | 없음 | 추가? (레벨업도 오프라인 진행?) |
| 재화 종류 | Gold만 | Ruby/Diamond 추가? (논란) |
| 프리미엄 보상 | 없음 | VIP/구독 유저 배율 +50% |
| 광고 시청 시 | 없음 | 광고 1회 시청 → 보상 1.5배 |
| 귀환 보상 | 없음 | 7일 이상 방치 시 특별 보상 (리텐션 유도) |

---

## 관련 문서

- [`roadmap.md`](roadmap.md) Phase 25 Sprint 25-3, Phase 26 S261-03
- [`core-architecture.md`](core-architecture.md) Tier 3 Balance
- `server/src/MFG.Server/Controllers/EventController.cs` 기존 구현
- `server/src/MFG.Server/Services/BalanceTables.cs` 배율 테이블
