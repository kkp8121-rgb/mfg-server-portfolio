namespace MFG.Server.Configuration;

/// <summary>
/// 3-Tier 데이터 버전 해시 + Config CDN 전달 메타. appsettings "DataVersions" 섹션에서 주입.
/// Phase 26 Sprint 26-1 S261-01(버전 반환) + S261-02(Config 타입별 URL 반환).
/// 관련: server/Docs/Planning/data-pipeline.md
/// </summary>
public class DataVersionsConfig
{
    // --- S261-01: 3-Tier 전체 해시 ---
    public string Visual { get; set; } = "00000000";
    public string Config { get; set; } = "00000000";
    public string Balance { get; set; } = "00000000";
    public string Catalog { get; set; } = "00000000";

    // --- S261-02: Config 타입별 CDN URL 구성 ---
    // CDN 루트. 말미 슬래시 있어도/없어도 정규화. 환경별 override (dev/stg/prd)
    public string ConfigCdnBaseUrl { get; set; } = "https://cdn.mf-game.com/config";

    // 타입별 해시. 키: characters/monsters/skills/stages 등.
    // 최종 URL = {ConfigCdnBaseUrl}/{type}-{hash}.json
    // 전체 Config 해시(.Config)와 분리된 이유: 개별 타입 단위 재다운로드 유도(대역폭 절약).
    public Dictionary<string, string> ConfigTypes { get; set; } = new()
    {
        ["characters"] = "00000000",
        ["monsters"] = "00000000",
        ["skills"] = "00000000",
        ["stages"] = "00000000",
    };
}
