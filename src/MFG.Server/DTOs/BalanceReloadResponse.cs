namespace MFG.Server.DTOs;

/// <summary>
/// POST /api/v1/admin/balance/reload 및 GET /api/v1/admin/balance/snapshot 응답.
/// 현재 IOptionsMonitor&lt;BalanceConfig&gt; 스냅샷 요약 — 전체 배율 티어 직접 반환하지 않고 개수만 포함(진단용).
/// Phase 26 Sprint 26-1 S261-04.
/// </summary>
public class BalanceReloadResponse
{
    public DateTime ReloadedAt { get; init; }
    public double OfflineRewardMaxHours { get; init; }
    public double OfflineRewardGoldPerHourCoef { get; init; }
    public int OfflineRewardTierCount { get; init; }
}
