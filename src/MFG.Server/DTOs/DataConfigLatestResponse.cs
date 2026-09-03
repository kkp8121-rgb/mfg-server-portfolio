namespace MFG.Server.DTOs;

/// <summary>
/// GET /api/v1/data/config/latest 응답.
/// 클라가 버전 해시 불일치 감지 시 호출 → 타입별 최신 CDN URL로 재다운.
/// Phase 26 Sprint 26-1 S261-02.
/// </summary>
public class DataConfigLatestResponse
{
    public List<DataConfigEntry> Entries { get; init; } = new();
    public DateTime ServerTime { get; init; }
}

public class DataConfigEntry
{
    public string Type { get; init; } = "";
    public string Hash { get; init; } = "";
    public string Url { get; init; } = "";
}
