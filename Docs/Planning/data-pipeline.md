---
read_count: 0
last_read: "never"
status: active
---

# MFG 데이터 파이프라인 — SO → Sheets → CDN/서버

> **작성 2026-04-20.** Phase 26 CI/CD 파이프라인 상세 설계. 개발자는 로컬 SO로 빠르게 작업하고, git push 한 번에 Sheets/CDN/서버 DB까지 자동 동기화.
> 관련: [`roadmap.md`](roadmap.md) Phase 26 / [`core-architecture.md`](core-architecture.md) 3-Tier

## 설계 원칙

1. **Git = 단일 원천**: `.asset` (SO)이 진실. 시트/CDN/서버 DB는 파생물.
2. **개발 속도 보존**: 클라는 Unity Editor에서 SO 자유 수정. git push 외 추가 절차 없음.
3. **push 트리거 단일화**: git push → CI가 3갈래로 분해(Visual/Config/Balance). 개발자 수동 작업 0.
4. **해시 기반 URL**: `characters-{hash}.json` — 해시 변경 시 URL 변경 → CDN 캐시 자동 무효화.
5. **시트는 뷰어**: 기획자는 시트를 읽기용으로만 사용. 시트 직접 편집은 다음 push 때 덮어씌워짐.
6. **dev/stg/prd 분기**: 동일 코드 다른 빌드 타입. stg/prd는 CDN/서버 필수, dev는 로컬.

---

## 전체 흐름

```
┌─────────────────────────────────────────────────────────┐
│ [로컬 개발] Unity Editor (개발자/클라 에이전트)           │
│   - SO (.asset) 편집                                    │
│   - Resources/ 이미지/사운드 추가                       │
│   - 로컬 Play 모드로 바로 테스트                         │
└─────────────────────┬───────────────────────────────────┘
                      │ git commit + git push
                      ▼
┌─────────────────────────────────────────────────────────┐
│ [CI Stage] GitHub Actions (push 트리거)                 │
│                                                         │
│ 1. Validation (pre-flight)                              │
│    - SO 포맷 검증 (필수 필드/범위)                       │
│    - 중복 ID 체크                                       │
│                                                         │
│ 2. 3-갈래 분해                                          │
│    ├─ Visual → GameCI Addressables Build (Unity)        │
│    │           → .bundle + catalog_{hash}.json           │
│    │                                                    │
│    ├─ Config → SO 파싱 (.NET CLI) → JSON export         │
│    │           → characters-{hash}.json 등              │
│    │                                                    │
│    └─ Balance → SO 파싱 → balance-{hash}.json           │
│                 → 서버 레포 PR 생성 or /admin/reload    │
│                                                         │
│ 3. 동시 배포                                            │
│    ├─ R2 업로드 (rclone): Visual bundle + Config JSON   │
│    ├─ Sheets Write (서비스 계정): 변경분 시트 업데이트   │
│    └─ 서버 Balance hot-reload (IOptionsMonitor)         │
└─────────────────────┬───────────────────────────────────┘
                      │ (3~10분 소요)
                      ▼
┌─────────────────────────────────────────────────────────┐
│ [프로덕션] 유저 앱 실행                                 │
│   - GET /api/v1/data/version → 해시 비교                │
│   - 변경된 Config JSON만 CDN에서 다운                   │
│   - Addressables 카탈로그 비교 → 변경된 bundle만 다운    │
│   - 서버 API 호출 → Balance는 서버 최신값으로 계산       │
└─────────────────────────────────────────────────────────┘
```

---

## Stage 1: 로컬 개발

**도구**: Unity Editor 6000.3.10f1

**작업 흐름**:
1. `Assets/Data/Monsters/Slime_01.asset` 수정 (예: HP 100→120)
2. `Assets/Resources/Sprites/slime_01.png` 교체
3. Play 모드에서 즉시 테스트
4. `git commit` + `git push`

**중요**: 개발자는 **이 단계만 수행**. 시트/CDN/서버는 CI가 처리.

---

## Stage 2: CI Validation (pre-flight)

**도구**: GitHub Actions + .NET CLI

**목적**: 잘못된 데이터 push 시 일찍 fail해서 CDN 오염 방지.

**검증 항목**:
- 필수 필드 존재 (id, name, iconId 등)
- 값 범위 (레벨 1~139, HP > 0 등)
- 중복 ID 없음
- 시트 스키마와 일치 (컬럼 명 변경 시 알림)
- 참조 무결성 (skillId → skills.json에 존재)

**구현**:
```yaml
# .github/workflows/data-validate.yml
- name: Validate SO data
  run: |
    dotnet run --project Tools/DataValidator -- \
      --input Assets/Data \
      --schema Tools/DataValidator/schema.json
```

**실패 시**: CI fail → push reject (workflow dispatch만 가능)

---

## Stage 3: 3-갈래 분해

### 🎨 분해 (a): Visual → Addressables Build

**도구**: GameCI (`unityci/editor:6000.3.10f1-android-3`)

**입력**: `Assets/` 내 Addressable 지정된 모든 에셋

**출력**: `ServerData/Android/` 폴더
- `catalog_{hash}.json`
- `{groupName}_{hash}.bundle` (여러 개)

**구현** (`.github/workflows/addressables-build.yml`):
```yaml
- uses: game-ci/unity-builder@v4
  env:
    UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
    UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
    UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
  with:
    targetPlatform: Android
    buildMethod: AddressablesBuildCI.BuildAll
    allowDirtyBuild: true
```

**Unity 측 build method** (`Assets/Scripts/Editor/Addressables/AddressablesBuildCI.cs`):
```csharp
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public static class AddressablesBuildCI {
    public static void BuildAll() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if (!string.IsNullOrEmpty(result.Error)) {
            EditorApplication.Exit(1);
        }
    }
}
```

**Unity 라이선스**: Personal 무료. 단 CI용 `.ulf` 수동 발급 필요 (사용자 작업).

**소요 시간**: 첫 빌드 10~15분 (Library 캐싱), 이후 3~5분.

---

### 🪪 분해 (b): Config → JSON Export

**도구**: .NET CLI 도구 (`Tools/ConfigExporter/`)

**입력**: `Assets/Data/**/*.asset` (SO) + `.meta` 파일

**출력**: `dist/config/` 폴더
- `characters-{hash}.json`
- `monsters-{hash}.json`
- `skills-{hash}.json`
- `stages-{hash}.json`
- `items-{hash}.json`

**구현 방법** (Unity 없이):

Unity `.asset` 파일은 YAML. .NET에서 직접 파싱 가능.

```csharp
// Tools/ConfigExporter/Program.cs (.NET CLI)
using YamlDotNet.Serialization;

public class MonsterData {
    public string id { get; set; }
    public string name { get; set; }
    public string iconId { get; set; }
    public int hp { get; set; }  // 이건 Balance Tier로 분류하여 제외
    // ...
}

static void Main() {
    var monsters = new List<MonsterData>();
    foreach (var assetPath in Directory.EnumerateFiles("Assets/Data/Monsters", "*.asset")) {
        var yaml = File.ReadAllText(assetPath);
        var deserializer = new DeserializerBuilder().Build();
        var data = deserializer.Deserialize<MonsterData>(yaml);
        monsters.Add(data);
    }
    
    // Tier 2만 추출 (이름/아이콘/설명)
    var configView = monsters.Select(m => new {
        m.id, m.name, m.iconId, m.description, m.grade
    }).ToList();
    
    var json = JsonSerializer.Serialize(configView);
    var hash = ComputeHash(json);
    File.WriteAllText($"dist/config/monsters-{hash}.json", json);
}
```

**해시 계산**: SHA-256 앞 8자 (예: `a1b2c3d4`). 같은 내용은 같은 해시 → 불필요한 업로드 회피.

**Tier 분리 로직**: 이 도구가 SO 필드를 읽을 때 **"어느 필드가 Tier 2/3인지"** 명시적으로 결정해야 함. 별도 설정 파일:

`Tools/ConfigExporter/tier-map.json`:
```json
{
  "MonsterDataSO": {
    "tier2": ["id", "name", "description", "iconId", "grade"],
    "tier3": ["hp", "attack", "defense", "goldReward", "expReward"]
  },
  "SkillDataSO": {
    "tier2": ["id", "name", "description", "iconId", "cooldownDisplay"],
    "tier3": ["damageMultiplier", "realCooldown", "buffAtkRate"]
  }
}
```

**소요 시간**: 수초 ~ 30초 (SO 1000개 기준).

---

### 🔒 분해 (c): Balance → 서버 DB Seed

**입력**: 같은 SO들에서 **Tier 3 필드만** 추출

**출력**: `dist/balance/balance-{hash}.json`
```json
{
  "version": "e5f6g7h8",
  "monsters": {
    "slime_01": {"hp": 120, "attack": 10, "defense": 5, "goldReward": 10, "expReward": 5}
  },
  "skills": {
    "skill_01": {"damageMultiplier": 1.5, "realCooldown": 3.0}
  },
  "gacha": {
    "weapon_pool_v2": {
      "legendary_weight": 5,
      "rare_weight": 200,
      "normal_weight": 795,
      "pity_legendary": 60,
      "pity_rare": 10
    }
  }
}
```

**반영 방식 (2가지)**:

**옵션 A: 서버 레포 PR 생성**
- CI가 `BalanceTables.cs` 또는 `balance.json` 파일 생성
- 자동 PR 생성 (`gh pr create`)
- 개발자/서버 에이전트가 리뷰 후 머지
- 머지 시 서버 CD 트리거 → 배포 시 반영

**옵션 B: `/admin/balance/reload` API 호출** ⭐ 추천
- CI가 R2에 `balance-{hash}.json` 업로드
- `POST https://api.mf-game.com/admin/balance/reload` (JWT admin)
- 서버가 R2에서 다운로드 → `IOptionsMonitor<BalanceConfig>` 갱신
- **재기동 불필요, 즉시 반영**

**선택**: **옵션 B** — 핫픽스 속도. 단, 잘못된 값 반영 시 롤백 절차 필요 (이전 해시로 재호출).

---

## Stage 4: 동시 배포

### R2 업로드

**도구**: `rclone` 또는 `aws s3 sync`

**대상**:
- `dist/addressables/Android/*.bundle` → `cdn.mf-game.com/addressables/Android/`
- `dist/addressables/Android/catalog_*.json` → 동일
- `dist/config/*.json` → `cdn.mf-game.com/config/`

**구현**:
```yaml
- name: Upload to R2
  env:
    AWS_ACCESS_KEY_ID: ${{ secrets.R2_ACCESS_KEY }}
    AWS_SECRET_ACCESS_KEY: ${{ secrets.R2_SECRET }}
    AWS_ENDPOINT_URL: https://<accountid>.r2.cloudflarestorage.com
  run: |
    aws s3 sync dist/config/ s3://mfg-cdn/config/ --cache-control "public, max-age=31536000, immutable"
    aws s3 sync dist/addressables/ s3://mfg-cdn/addressables/ --cache-control "public, max-age=31536000, immutable"
```

**주의**: 
- 해시 URL이므로 캐시 max-age 최대화
- 기존 파일 **삭제하지 않음** — 롤백 대비 이전 버전 보존

---

### Sheets Write

**도구**: Google Sheets API (.NET `Google.Apis.Sheets.v4`)

**목적**: 기획자 뷰어 용도. **읽기 전용 미러**.

**구현**:
```yaml
- name: Write to Sheets
  env:
    GCP_SERVICE_ACCOUNT: ${{ secrets.GCP_SHEETS_SA }}
  run: |
    dotnet run --project Tools/SheetsWriter -- \
      --input dist/config/ \
      --spreadsheet-id ${{ vars.MASTER_SHEET_ID }} \
      --credentials "$GCP_SERVICE_ACCOUNT"
```

**Sheets 탭 구조**:
- `Characters` — characters.json 내용
- `Monsters` — monsters.json 내용 (Tier 2 필드만)
- `Balance_Monsters` — monsters의 Tier 3 필드 (기획자가 수치 확인용)
- `Gacha_Probability` — 가챠 확률
- `Meta` — 해시/timestamp/version

**서비스 계정 권한**: 해당 시트 1개에만 Editor. GCP 프로젝트 전체 권한 X.

**쿼터**: 500 req/100초. SO 1000개 카테고리 5개 → 20 requests → 충분히 여유.

---

### 서버 Balance hot-reload

**구현**:

서버 측 `BalanceReloadController.cs`:
```csharp
[ApiController]
[Route("admin/balance")]
[Authorize(Roles = "Admin")]  // 또는 IP 제한
public class BalanceReloadController : ControllerBase {
    private readonly IBalanceService _balance;
    
    [HttpPost("reload")]
    public async Task<IActionResult> Reload([FromBody] ReloadRequest req) {
        // R2에서 balance-{hash}.json 다운로드
        var json = await DownloadFromR2(req.Hash);
        
        // 검증
        if (!BalanceValidator.IsValid(json)) 
            return BadRequest("Invalid balance data");
        
        // IOptionsMonitor 대체
        await _balance.ReloadAsync(json);
        
        return Ok(new { reloaded = true, hash = req.Hash });
    }
}
```

**호출 주체**: CI.

**인증**: JWT Admin role 또는 CI 전용 API Key.

---

## Stage 5: 런타임 동작

### 앱 부팅 시퀀스

```
[Splash 표시]
  ↓
1. GET https://api.mf-game.com/health
   ├─ 200 OK → 계속
   └─ 5xx/timeout → 3회 재시도 → 실패 시 "서버 점검" 폴백 화면

2. GET /api/v1/system/notice
   └─ 점검 공지 있으면 팝업 표시

3. GET /api/v1/data/version
   응답: { characters: "a1b2", monsters: "c3d4", skills: "e5f6", 
           stages: "g7h8", items: "i9j0", catalog: "k1l2" }

4. 로컬 캐시 해시 읽기 (Application.persistentDataPath/cache/config/manifest.json)

5. 해시 비교:
   for each type:
     if (server_hash != local_hash):
       download CDN https://cdn.mf-game.com/config/{type}-{server_hash}.json
       save to cache/config/
     else:
       use cached

6. Addressables.CheckForCatalogUpdates() → 변경 있으면 차등 다운

7. POST /api/v1/auth/login → JWT + Player data

8. GET /api/v1/event/offline-reward/preview
   → 예상 보상 표시 UI

9. 유저 확인 → POST /api/v1/event/claim → 실제 지급

10. Main 씬 진입
```

### 갱신 감지 (세션 중 서버 배포 시)

옵션 1: 주기적 polling (30분마다 `/data/version` 재호출, 변경 시 재접속 유도)
옵션 2: 서버 push (WebSocket/SSE) — 과도함
옵션 3: 다음 로그인 시까지 대기

**추천**: 옵션 1 + **중요 API 호출 실패 시 재확인**

---

## dev/stg/prd 환경 분기

### dev (로컬 개발)

- SO 직접 사용 (Addressables 우회 가능 or Local 그룹만)
- 서버: `localhost:5062` (Docker MySQL) 또는 `dev-api.mf-game.com`
- CDN: 없음 (Resources.Load)
- CI 트리거: `develop` 브랜치 (별도 R2 경로 `stg.cdn.mf-game.com/dev/` 선택)

### stg (내부 테스트)

- Config: CDN `stg.cdn.mf-game.com` (별도 버킷 또는 prefix)
- 서버: `stg-api.mf-game.com` (별도 Lightsail 인스턴스 or 같은 서버 다른 포트)
- DB: 별도 `mfg-stg` 스키마
- CI 트리거: `main` 브랜치 머지 시 자동

### prd (출시)

- Config: CDN `cdn.mf-game.com`
- 서버: `api.mf-game.com`
- DB: `mfg` 스키마
- CI 트리거: `v*.*.*` 태그 시 (수동 승인)

**환경 분기 방식**:
- 클라 빌드 타입 (`UNITY_EDITOR` / `DEVELOPMENT_BUILD` / `STAGING` scripting define / Release)
- 서버 `ASPNETCORE_ENVIRONMENT` (Development / Staging / Production)
- CDN URL은 클라 빌드에 하드코딩 (동적 변경 불가, 번들이 다르므로)

---

## 해시 URL 패턴 상세

### 파일명 규칙

- Config: `{type}-{hash}.json`
  - 예: `characters-a1b2c3d4.json`
  - 해시: SHA-256(파일 내용) 앞 8자
  
- Addressables: Addressables 시스템 자체 규칙 (변경 X)
  - 예: `monsters_assets_e5f6g7h8.bundle`

### 이전 버전 보존

**원칙**: **삭제하지 않음**. 롤백 대비.

**보존 기간**: 30일 (R2 Lifecycle Rule로 자동 삭제)

**롤백 방법**: 서버 `/data/version` 응답을 이전 해시로 되돌림 (DB 또는 config 파일)

---

## 실패 시 처리

### 클라 측

| 실패 유형 | 폴백 |
|----------|------|
| `/data/version` 호출 실패 | 3회 재시도 → 이전 로컬 캐시 사용 → 이마저 없으면 앱 내장 fallback |
| CDN 다운로드 실패 | 이전 로컬 캐시 유지 |
| `/event/offline-reward/preview` 실패 | 0초 경과로 간주, 보상 없이 진입 |
| JWT 만료 (401) | Firebase refresh → 재시도 → 실패 시 로그인 화면 |

### 서버 측

| 실패 유형 | 폴백 |
|----------|------|
| `BalanceTables` hot-reload 실패 | 이전 버전 유지 (rollback transaction) |
| MySQL 연결 끊김 | `/health` 503 반환, retry |
| Balance 검증 실패 (CI) | PR reject, Slack 알림 |

### CI 측

| 실패 유형 | 폴백 |
|----------|------|
| Validation fail | push reject, 개발자에게 알림 |
| Unity 빌드 실패 | CI fail, 이전 CDN 상태 유지 |
| R2 업로드 실패 | CI fail, 3회 재시도 후 수동 개입 |
| Sheets Write 실패 | 비차단 경고 (Visual/Config 업데이트는 계속) |

---

## 긴급 패치 시나리오 (검증 대상)

### 시나리오 1: 몬스터 이름 오타 수정

1. 개발자: `Assets/Data/Monsters/Slime_01.asset` 이름 수정
2. `git commit` + `git push`
3. CI (3분): 
   - Validation pass
   - Config JSON export: `monsters-{newhash}.json` 생성
   - R2 업로드
   - Sheets write
4. 유저: 30분 polling 또는 재접속 시 새 해시 감지 → 새 JSON 다운 → 반영
5. **총 소요**: 3분 (CI) + 최대 30분 (유저 polling) = **~33분**
6. **앱 재빌드/심사: 0**

### 시나리오 2: 가챠 SSR 확률 1% → 2% (이벤트)

1. 개발자/서버 에이전트: `Assets/Data/Gacha/WeaponPoolV2.asset` 확률 수정
2. push
3. CI:
   - Validation
   - Balance JSON export
   - R2 업로드 (balance-{newhash}.json)
   - **서버 `/admin/balance/reload` 호출** ⚡
   - 서버 hot-reload 성공
4. 유저: **다음 가챠 pull부터 즉시 반영** (폴링 불필요, 서버가 이미 최신값)
5. **총 소요**: **3~5분**
6. **앱 재빌드/심사: 0**

### 시나리오 3: SPUM 파츠 스프라이트 버그 수정

1. 개발자: `Assets/Art/SPUM/body_warrior_01.png` 교체
2. push
3. CI (10~15분):
   - Validation
   - Addressables Build (Unity)
   - R2 업로드 (새 bundle + 새 catalog)
4. 유저: 재접속 시 Addressables 카탈로그 차이 감지 → 변경된 bundle만 다운 (~500KB)
5. **총 소요**: **15~45분**
6. **앱 재빌드/심사: 0**

### 시나리오 4: Unity 엔진 크래시 수정

1. 개발자: C# 코드 수정
2. push
3. **앱 재빌드 필요** → Android AAB / iOS IPA 생성
4. **스토어 심사 1~3일 소요**
5. 유저: 스토어에서 앱 업데이트

이 시나리오는 **피할 수 없음**. 따라서 "가능하면 데이터로, 불가피하면 코드"가 원칙.

---

## 구현 체크리스트 (Phase 26 진행 시)

### 사용자 수동 작업 (블로커)
- [ ] 클라 GitHub 리포 생성 or 프로젝트 루트 git화 (GameCI 빌드 대상)
- [ ] Unity Personal 라이선스 `.ulf` 발급 + GitHub Secrets 등록
- [ ] Cloudflare R2 API Token 발급 (Config/Addressables 업로드용, DB 백업과 별도)
- [ ] GCP 서비스 계정 키 (시트 Write) + GitHub Secrets 등록
- [ ] 마스터 시트 생성 + 탭 스키마 합의

### 서버 에이전트
- [ ] S261-01 `GET /data/version`
- [ ] S261-02 `GET /data/config/latest`
- [ ] S261-03 `GET /event/offline-reward/preview`
- [ ] S261-04 `BalanceTables` hot-reload + `/admin/balance/reload`
- [ ] S261-05 `GET /system/notice`
- [ ] stg 환경 구성 (appsettings.Staging + stg DB + stg CDN URL)

### 인프라 에이전트 (신설)
- [ ] `Tools/ConfigExporter/` .NET CLI 도구
- [ ] `Tools/SheetsWriter/` .NET CLI 도구
- [ ] `Tools/DataValidator/` .NET CLI 도구
- [ ] `.github/workflows/data-pipeline.yml` 통합 워크플로우
- [ ] `.github/workflows/addressables-build.yml` (GameCI)
- [ ] `Assets/Scripts/Editor/Addressables/AddressablesBuildCI.cs`

### 클라 에이전트 (기존, 플레이테스트 병렬)
- [ ] `Assets/Scripts/Core/Data/ConfigLoader.cs`
- [ ] `Assets/Scripts/Core/BuildConfig.cs`
- [ ] 앱 부팅 시퀀스 수정 (Splash → 버전 체크 → Config 다운 → 로그인)
- [ ] 폴백 JSON (`Assets/Resources/fallback_config.json`)
- [ ] 오프라인 보상 Preview UI

---

## 성능 예상치

| 단계 | 소요 시간 | 비고 |
|------|---------|------|
| Validation | 10~30초 | .NET CLI 1회 실행 |
| Config JSON export | 30초~1분 | SO 1000개 기준 |
| Balance export | 10초 | Config와 통합 가능 |
| Addressables build (첫 실행) | 10~15분 | Library 캐싱 |
| Addressables build (증분) | 3~5분 | 변경된 그룹만 |
| R2 업로드 | 30초~2분 | 총 50MB 기준 |
| Sheets Write | 10~30초 | 5개 탭 기준 |
| 서버 hot-reload | 5초 | 다운 + 검증 + 스왑 |
| **합계 (평균)** | **5~10분** | 코드 변경 없는 데이터 push |
| **합계 (Addressables 포함 첫 빌드)** | **15~20분** | 신규 에셋 추가 시 |

---

## 비용 예상치 (월)

| 항목 | 비용 | 근거 |
|------|------|------|
| GitHub Actions (Private) | $0 | 2000분 무료. CI 평균 7분 × 월 30회 = 210분 |
| Unity Personal | $0 | 연매출 $100K 미만 |
| Cloudflare R2 저장 | $0~$0.05 | 무료 10GB. Config+Addressables 초기 500MB |
| Cloudflare R2 Class A 요청 | $0 | CI 월 300 요청, 무료 월 1000만 |
| Cloudflare R2 Class B 요청 (유저 다운) | $0~$0.10 | DAU 1000 × 5개 파일 = 5000/일 × 30 = 15만/월 (무료 월 1000만) |
| Google Sheets API | $0 | 무료 쿼터 충분 |
| GCP Service Account | $0 | 무료 |
| **합계** | **$0~$0.15/월** | DAU 1000 기준 |

---

## 관련 문서

- [`roadmap.md`](roadmap.md) Phase 26 — Sprint 단위 태스크
- [`core-architecture.md`](core-architecture.md) — Tier 분류 기준
- [`offline-reward-model.md`](offline-reward-model.md) — 오프라인 플로우
- [`../secrets.md`](../secrets.md) — 시크릿 관리 (UNITY_LICENSE 등 추가 예정)
