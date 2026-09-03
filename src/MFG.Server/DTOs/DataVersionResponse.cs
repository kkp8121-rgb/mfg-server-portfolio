namespace MFG.Server.DTOs;

/// <summary>
/// GET /api/v1/data/version 응답.
/// 클라 부팅 시 호출 → 로컬 캐시된 해시와 비교해 갱신 여부 판단.
/// Phase 26 Sprint 26-1 S261-01.
/// </summary>
public class DataVersionResponse
{
    public string Visual { get; init; } = "00000000";
    public string Config { get; init; } = "00000000";
    public string Balance { get; init; } = "00000000";
    public string Catalog { get; init; } = "00000000";
    public DateTime ServerTime { get; init; }
}
