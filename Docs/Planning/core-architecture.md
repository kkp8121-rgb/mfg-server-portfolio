---
read_count: 0
last_read: "never"
status: active
---

# MFG 서버 Core Architecture — 3-Tier 데이터 모델

> **작성 2026-04-20.** CDN-First + 서버 권위 아키텍처 상세. Phase 26 설계의 기반 문서.
> 관련: [`roadmap.md`](roadmap.md) Phase 26 / [`data-pipeline.md`](data-pipeline.md) / [`offline-reward-model.md`](offline-reward-model.md)

## 배경 문제의식

2026-04-20 사용자 피드백 정리:

1. **긴급 패치 문제**: SO 수정을 위해 앱 재빌드 + 스토어 심사 필요하면 라이브 운영 불가능. 이름 오타/아바타 버그/확률 조정 모두 즉시 반영되어야 함.
2. **개발 속도 문제**: 로컬 SO 편집은 빠름. 클라 플레이테스트 중이라 데이터 흐름을 한꺼번에 바꿀 수 없음.
3. **보안 요구**: 가챠 확률은 절대 클라에 노출되면 안 됨. 해킹/치트 방지.

**해결**: 데이터를 "보안 민감도 + 갱신 빈도" 기준으로 **3계층 분리**. 각 계층별로 저장소/접근 방식/CI 파이프라인이 다름.

---

## 3-Tier 분류

```
┌────────────────────────────────────────────────────────┐
│ 📦 App Bundle (스토어 심사 대상)                       │
│   - Unity 엔진 + IL2CPP 네이티브                       │
│   - C# 게임 로직 (계산 구조, 값 아님)                  │
│   - UI 프레임워크 (레이아웃 트리, 내용 아님)            │
│   - Splash/Loading 화면 리소스                         │
│   - 🆘 최소 오프라인 폴백 JSON (1~2KB)                 │
├────────────────────────────────────────────────────────┤
│ 🎨 Tier 1: Visual (CDN Addressables)                   │
│   - SPUM 파츠 스프라이트 (body/head/weapon/armor)      │
│   - VFX/애니메이션 클립                                │
│   - UI 아이콘 이미지                                   │
│   - 사운드 (BGM/SFX)                                   │
│   - 맵 배경                                            │
├────────────────────────────────────────────────────────┤
│ 🪪 Tier 2: Config (CDN JSON)                           │
│   - characters.json — SPUM 아바타 구성 메타            │
│   - monsters.json — 몬스터 스펙 (이름/아이콘ID/등급)   │
│   - skills.json — 스킬 설명/툴팁/쿨타임(표시용)        │
│   - stages.json — 스테이지 구조                        │
│   - items.json — 아이템 이름/등급/아이콘 매핑          │
│   - quests.json — 퀘스트 조건/보상 설명                │
├────────────────────────────────────────────────────────┤
│ 🔒 Tier 3: Balance (서버 DB 전용, API만 노출)          │
│   - 가챠 확률 테이블                                   │
│   - 드롭률                                             │
│   - 강화 성공률 (스타포스 25단계)                      │
│   - 실제 데미지 공식                                   │
│   - 레벨별 필요 경험치                                 │
│   - 일일 재화 상한                                     │
│   - 아레나 매칭 로직                                   │
│   - 오프라인 보상 배율                                 │
└────────────────────────────────────────────────────────┘
```

---

## Tier별 상세

### 📦 App Bundle (스토어 배포용)

**포함**:
- Unity 6000.3.10f1 엔진
- IL2CPP C# 네이티브
- 게임 로직 코드 (`Assets/Scripts/**`)
- UI UXML/USS (UI Toolkit) + uGUI Prefab
- Addressables Local 그룹 (첫 3챕터 필수 에셋)
- Splash/Loading 리소스
- **최소 폴백 JSON** (`Assets/Resources/fallback_*.json`)

**미포함**:
- 챕터 4+ 에셋 (CDN)
- Config JSON 원본 (CDN)
- Balance 데이터 (서버)

**크기 목표**: Android AAB 30MB, iOS IPA 35MB (Unity 엔진 포함 기준)

**재빌드 필요 시점**:
- Unity 엔진 버그 수정
- 네이티브 크래시 수정
- 결제/로그인 플로우 변경
- OS 호환성 업데이트

**재빌드 불필요 (CDN/서버로 커버)**:
- 밸런스 조정
- 이름/설명/아이콘 교체
- 신규 캐릭터/몬스터 추가 (동일 스킬 구조 내)
- 확률 조정, 이벤트 구성

---

### 🎨 Tier 1: Visual (CDN Addressables)

**저장소**: Cloudflare R2 `cdn.mf-game.com/addressables/{platform}/`

**파일 형식**:
- `.bundle` (Unity Asset Bundle)
- `catalog_{hash}.json` (Addressables 카탈로그)

**접근 방식**: Unity Addressables 런타임
- `Addressables.LoadAssetAsync<Sprite>("monster_01_body")`
- `AssetReference.InstantiateAsync()`

**버전 관리**:
- Addressables 자체의 카탈로그 해시 + 각 bundle 해시
- 앱 부팅 시 `Addressables.CheckForCatalogUpdates()` → 변경된 bundle만 다운

**갱신 빈도**: 이벤트/시즌마다 (월 1~4회)

**예시**:
```
cdn.mf-game.com/addressables/Android/catalog_a1b2c3d4.json
cdn.mf-game.com/addressables/Android/remote_audio_bgm_assets_a1b2c3d4.bundle
cdn.mf-game.com/addressables/Android/remote_chapter4_monsters_e5f6g7h8.bundle
```

**보안**: 누구나 다운로드 가능 (공개 에셋). 치트 방지 목적 없음.

---

### 🪪 Tier 2: Config (CDN JSON)

**저장소**: Cloudflare R2 `cdn.mf-game.com/config/`

**파일명 규칙**: **Content-Addressable URL** — 해시 포함
- `characters-{hash}.json`
- `monsters-{hash}.json`
- `skills-{hash}.json`
- 해시 바뀌면 URL 바뀜 → CDN 캐시 자동 무효화

**접근 방식**: 
1. 앱 부팅 시 서버 `GET /api/v1/data/version` 호출
2. 응답 예시: `{"characters": "a1b2", "monsters": "c3d4", "skills": "e5f6", "stages": "g7h8"}`
3. 로컬 캐시 해시 비교
4. 다른 것만 `HttpClient`로 CDN에서 다운
5. `Application.persistentDataPath/cache/config/`에 저장

**버전 관리**: 해시 기반. 서버는 시트/DB에서 계산한 현행 해시만 반환.

**갱신 빈도**: 핫픽스 시 언제든 (주 1~10회)

**예시 JSON** — `characters.json`:
```json
{
  "version": "a1b2c3d4",
  "characters": [
    {
      "id": "warrior_01",
      "name": "전사",
      "description": "검을 든 근접 전사",
      "iconId": "icon_warrior",
      "spumParts": {
        "body": "body_warrior_01",
        "head": "head_warrior_01",
        "weapon": "weapon_sword_01",
        "armor": "armor_warrior_01"
      },
      "defaultAnimations": ["idle", "attack", "skill_01"],
      "classLine": "warrior"
    }
  ]
}
```

> 주의: `spumParts` 값은 Addressables 주소 (Tier 1 참조). Config JSON이 Visual bundle을 인덱싱.

**예시 JSON** — `monsters.json`:
```json
{
  "version": "c3d4e5f6",
  "monsters": [
    {
      "id": "slime_01",
      "name": "슬라임",
      "description": "기본 몬스터",
      "iconId": "icon_slime",
      "grade": "normal",
      "spriteRef": "monster_slime_01",
      "sfxHit": "sfx_slime_hit"
    }
  ]
}
```

> 여기에 **HP/ATK/DEF는 없음** — 그것은 Tier 3 (서버 계산).

**보안**: 공개. 다른 유저가 다운로드해도 무방 (아이콘/이름은 게임 하면 보이는 정보).

**❗ 주의**: Config JSON에는 **어떤 밸런스 수치도 포함 금지**. 오로지 표시용 메타데이터.

---

### 🔒 Tier 3: Balance (서버 DB 전용)

**저장소**: MySQL `balance_tables` 테이블 + `BalanceTables.cs` 상수

**접근 방식**: **오로지 서버 내부**. 클라는 결과만 받음.

**클라가 호출하는 엔드포인트**:
- `POST /api/v1/gacha/pull` → 서버가 확률 적용 → 결과 itemId 반환
- `POST /api/v1/equipment/enhance` → 서버가 성공률 판정 → 결과 반환
- `POST /api/v1/arena/battle-result` → 서버가 데미지 계산 → 승패 반환

**클라가 받는 데이터**:
- "엑스칼리버 획득" (itemId)
- "강화 성공" (bool)
- "승리, 250 MMR 획득" (결과 수치)

**클라가 **받지 않는** 데이터**:
- "엑스칼리버 뽑을 확률 0.5%"
- "강화 27단계 성공률 12.3%"
- "레벨 차이 10 시 데미지 배율 1.7x"

**갱신 빈도**: 시즌/이벤트 시 (월 1~8회)

**갱신 방식**:
1. 시트 Balance 탭 수정 (기획자 또는 개발자)
2. CI가 시트 읽어 서버 `BalanceTables` PR 생성 or DB seed 업데이트
3. 서버 `IOptionsMonitor<BalanceConfig>` 핫리로드 (재기동 불필요)

**보안**: 
- DB 접근 권한 = 서버 프로세스만
- 로그/telemetry에 밸런스 값 출력 금지
- API 응답에 확률 포함 금지 (결과만)

---

## 실전 예시 1 — 무기 "엑스칼리버"

| 필드 | Tier | 저장소 | 갱신 방법 |
|------|------|--------|----------|
| 스프라이트 `weapon_excalibur.png` | 🎨 1 | CDN bundle | Addressables 재빌드 |
| 이름 "엑스칼리버" | 🪪 2 | CDN `items.json` | 시트 수정 → CI → CDN 업로드 |
| 설명 "전설의 성검" | 🪪 2 | CDN `items.json` | 위와 동일 |
| 등급 "SSR" | 🪪 2 | CDN `items.json` | 위와 동일 |
| 아이콘ID `icon_excalibur` | 🪪 2 | CDN `items.json` | 위와 동일 |
| 아이콘 이미지 자체 | 🎨 1 | CDN bundle | Addressables 재빌드 |
| **기본 공격력 300** | 🔒 3 | 서버 DB | 시트 → 서버 DB seed |
| **강화 성공률 30%** | 🔒 3 | 서버 DB | 위와 동일 |
| **드롭 테이블** | 🔒 3 | 서버 DB | 위와 동일 |

**핵심**: 같은 "무기"라도 필드별로 Tier가 다름.

---

## 실전 예시 2 — SPUM 캐릭터 "warrior_01"

SPUM 파츠는 **여러 스프라이트를 동적으로 조립**해 캐릭터 한 명을 만드는 시스템.

```
warrior_01 (캐릭터 ID)
  ├─ body: body_warrior_01      🎨 Tier 1 (CDN bundle)
  ├─ head: head_warrior_01      🎨 Tier 1 (CDN bundle)
  ├─ weapon: weapon_sword_01    🎨 Tier 1 (CDN bundle)
  ├─ armor: armor_warrior_01    🎨 Tier 1 (CDN bundle)
  └─ animations: [idle, attack, skill_01]  🎨 Tier 1 (CDN bundle)

characters.json (CDN Tier 2):
  "warrior_01": {
    "name": "전사",
    "spumParts": { body: "body_warrior_01", ... },  // Tier 1 인덱스
    "defaultAnimations": ["idle", "attack", "skill_01"]
  }

서버 DB (Tier 3):
  warrior 직업 기본 HP: 100
  warrior 레벨당 HP 증가: +10
  warrior 직업 기본 ATK: 20
  warrior 스킬 "skill_01" 데미지 배율: 1.5x
```

**클라 렌더링 플로우**:
1. 서버에서 "내 캐릭터 = warrior_01" 응답 받음
2. CDN `characters.json`에서 `warrior_01` 찾아 `spumParts` 읽음
3. Addressables로 각 파츠(`body_warrior_01` 등) 로드
4. SPUM Animator에 조립

**긴급 패치 예시**: 전사 무기 스프라이트 버그
- **Tier 1 CDN**: `weapon_sword_01.bundle` 재업로드 → 유저 재접속 시 반영
- **앱 재빌드 X, 심사 X**

**밸런스 패치 예시**: 전사 기본 ATK 20 → 25
- **Tier 3 서버**: BalanceTables 업데이트 → 서버 hot-reload → 다음 호출부터 반영
- **앱 재빌드 X, 심사 X**

---

## 실전 예시 3 — 가챠 보안 플로우

```
[클라]                           [서버]
POST /gacha/pull
  playerId=123, poolType="weapon" ─────→
                                     ① 플레이어 재화 확인
                                     ② Balance 테이블 조회 (확률)
                                     ③ RNG (서버 난수 생성)
                                     ④ 결과 확정: "item_12345"
                                     ⑤ 재화 차감 + gacha_history 기록
                                     ⑥ 천장(pity) 카운터 갱신
                ←──────────────── { itemId: "item_12345", grade: "SSR" }

⑦ 클라: itemId 받음
⑧ 클라: Config JSON의 items.json에서 조회
   → "엑스칼리버", SSR, iconId=icon_excalibur
⑨ 클라: Addressables로 icon_excalibur 스프라이트 로드
⑩ 클라: 반짝이 연출 + 결과 화면 표시
```

**보안 포인트**:
- 확률 테이블은 단 한 번도 클라에 전송되지 않음
- 클라 메모리를 덤프해도 확률 알 수 없음
- 클라 네트워크 패킷 캡처해도 "결과만" 보임
- 서버 응답 조작 시도: JWT 서명 검증 + HTTPS로 차단

**클라에 있어도 되는 것**:
- "현재 픽업 배너: 엑스칼리버 가챠 v2" — 공개 정보 (포스터 같은 것)
- "소환권 5장 보유" — 서버에서 매번 확인 (클라 표시는 캐시)
- "SSR 등급은 반짝이는 연출" — 연출 로직

---

## 클라-서버 통신 규약

### 매 부팅 시

1. `GET /health` — 서버 생존 확인
2. `GET /api/v1/data/version` — 3-Tier 버전 해시
3. `GET /api/v1/system/notice` — 점검 공지 (있으면)
4. CDN에서 Config JSON 차등 다운 (변경분만)
5. Addressables 카탈로그 차등 다운
6. `POST /api/v1/auth/login` — JWT 발급
7. `GET /api/v1/event/offline-reward/preview` — 오프라인 보상 예상치
8. 유저 메인 진입

### 매 게임 액션

- 가챠 → `POST /gacha/pull`
- 재화 소비 → `POST /currency/spend`
- 강화 → `POST /equipment/enhance`
- 전투 시작 → `POST /combat/start` (또는 로컬 시뮬 + 결과만 검증)
- 전투 결과 → `POST /combat/result` or `/arena/battle-result`

### 백그라운드

- 60초마다 `POST /save/sync` (서버 Phase 25-1 적용 시 즉시)
- 앱 종료 → `POST /auth/logout` (LastLogoutAt 기록)

---

## 환경별 동작 (dev/stg/prd)

| 환경 | 빌드 종류 | Tier 1 Visual | Tier 2 Config | Tier 3 Balance | 접속 서버 |
|------|-----------|--------------|--------------|---------------|-----------|
| **dev** | Editor Play / Dev Build | 로컬 Addressables (Local 경로) | 로컬 SO 직접 | 로컬 `BalanceTables` + Mock 서버 | `localhost:5062` 또는 dev Lightsail |
| **stg** | TestFlight / Internal Track | CDN `stg.cdn.mf-game.com` 또는 prefix | 동일 CDN staging 경로 | stg DB | `stg-api.mf-game.com` |
| **prd** | App Store / Google Play | CDN `cdn.mf-game.com` | 동일 CDN | 프로덕션 DB | `api.mf-game.com` |

**핵심**: 같은 APK/IPA가 아님. **빌드 타입별로 데이터 소스 분기**. 클라 `BuildConfig.DataSource` enum.

```csharp
public enum DataSource { Local, Remote }

public static class BuildConfig {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public const DataSource Source = DataSource.Local;
    public const string ServerUrl = "http://localhost:5062/api/v1";
    public const string CdnUrl = "(unused, local SO)";
#elif STAGING
    public const DataSource Source = DataSource.Remote;
    public const string ServerUrl = "https://stg-api.mf-game.com/api/v1";
    public const string CdnUrl = "https://stg.cdn.mf-game.com";
#else
    public const DataSource Source = DataSource.Remote;
    public const string ServerUrl = "https://api.mf-game.com/api/v1";
    public const string CdnUrl = "https://cdn.mf-game.com";
#endif
}
```

---

## 오프라인 폴백 최소 스펙

앱이 처음 실행되었을 때 CDN/서버 모두 접근 불가하면 **앱 내장 폴백** 사용. 목적은 "에러 화면만큼은 뜨게":

`Assets/Resources/fallback_config.json`:
```json
{
  "version": "fallback",
  "app_name": "MFG",
  "min_required_version": "1.0.0",
  "error_messages": {
    "no_network": "네트워크 연결을 확인해주세요.",
    "server_maintenance": "점검 중입니다.\n잠시 후 다시 시도해주세요.",
    "cdn_unavailable": "게임 데이터를 다운로드할 수 없습니다.",
    "retry": "다시 시도",
    "support": "문제가 지속되면 고객센터에 문의하세요."
  },
  "support_email": "support@mf-game.com",
  "splash_copyright": "© 2026 MFG"
}
```

**크기 목표**: 1~2KB. 

**금지 사항**:
- 이 JSON에 캐릭터/몬스터/스킬 실제 데이터 넣기 금지
- 밸런스 값 금지
- 실제 게임 플레이 가능한 로직 금지

**허용**:
- 에러 메시지
- 최소 앱 메타데이터
- 타이틀 화면 문구

---

## CDN 라우팅 및 캐시 전략

### URL 구조

```
https://cdn.mf-game.com/
  ├─ config/                           # Tier 2
  │   ├─ characters-{hash}.json
  │   ├─ monsters-{hash}.json
  │   └─ skills-{hash}.json
  ├─ addressables/                     # Tier 1
  │   ├─ Android/
  │   │   ├─ catalog_{hash}.json
  │   │   └─ *.bundle
  │   └─ iOS/
  │       ├─ catalog_{hash}.json
  │       └─ *.bundle
  └─ fallback/                         # 비상시 Backup
      └─ last-known-good.json
```

### 캐시 정책 (Cloudflare)

- Config JSON: `Cache-Control: public, max-age=31536000, immutable` (해시 URL이라 영구 캐시)
- Addressables: 동일 (bundle 해시 URL)
- Fallback: `max-age=300` (5분, 긴급 교체 대비)

### 클라 로컬 캐시

- 저장 위치: `Application.persistentDataPath/cache/config/`
- 무효화: 서버 응답 해시 ≠ 캐시 해시 시 새로 다운
- 삭제 타이밍: 없음 (자동 교체). 앱 재설치 시만 초기화.

---

## 보안 체크리스트

### ❌ 클라에 노출 금지
- 가챠 확률 값
- 드롭률 값
- 강화 성공률 테이블
- 데미지 공식 계수
- 레벨별 필요 경험치
- 일일 재화 상한
- 아레나 매칭 수식

### ✅ 클라에 있어도 됨
- 캐릭터/몬스터/아이템 이름
- 아이콘/스프라이트
- 스킬 설명 (효과는 표시용 텍스트만, 실제 수치 X)
- 쿨타임 표시 (서버가 내려준 값)
- 등급 (SSR/SR/R 등)
- UI 레이아웃

### 🔒 서버만
- 실제 계산식
- RNG
- 재화 잔액 권위
- 세이브 데이터 원천

---

## 이 문서의 역할

- Phase 26 구현 시 "이 필드는 어느 Tier?" 판단 기준
- 신규 기능 추가 시 **가장 먼저 이 문서 참조**하여 데이터 배치 결정
- 보안 리뷰 시 체크리스트
- 클라 ↔ 서버 계약 변경 시 갱신

**관련 문서**:
- [`roadmap.md`](roadmap.md) — Phase 26 Sprint별 구현 순서
- [`data-pipeline.md`](data-pipeline.md) — CI 파이프라인 상세
- [`offline-reward-model.md`](offline-reward-model.md) — 오프라인 특수 처리
