namespace MFG.Server.Configuration;

/// <summary>
/// 핫리로드 가능한 서버 밸런스 설정.
/// appsettings "Balance" 섹션에서 주입되고, 파일 변경 시 IOptionsMonitor 가 자동으로 재평가 + OnChange 알림.
/// 즉시 반영이 필요한 튜닝 값(오프라인 보상 상한/계수/배율 등) 전용 — 대용량 테이블은 BalanceTables 정적 필드 유지.
/// Phase 26 Sprint 26-1 S261-04.
/// 관련: server/Docs/Planning/data-pipeline.md, offline-reward-model.md
/// </summary>
public class BalanceConfig
{
    /// <summary>오프라인 보상 누적 최대 시간(시간 단위). 상한 초과분은 잘림.</summary>
    public double OfflineRewardMaxHours { get; set; } = 12.0;

    /// <summary>시간당 Gold 계수. offlineGold = hours × coef × level × multiplier.</summary>
    public double OfflineRewardGoldPerHourCoef { get; set; } = 900.0;

    /// <summary>
    /// 레벨별 오프라인 보상 배율 티어. level &lt; MaxLevelExclusive 인 첫 티어 적용.
    /// 티어는 오름차순으로 정렬되어 있어야 한다.
    /// </summary>
    public List<OfflineRewardTier> OfflineRewardMultiplierTiers { get; set; } = new();

    /// <summary>
    /// 일일 재화 획득 상한. 키=currencyType(Gold/Ruby 등), 값=24h 기준 최대 획득량.
    /// 미정의 재화는 상한 없음(무제한). 어뷰징 대응 핫픽스 경로 — IOptionsMonitor 로 재기동 없이 조정.
    /// </summary>
    public Dictionary<string, long> DailyCurrencyEarnCaps { get; set; } = new();

    /// <summary>일일 이벤트 던전 무료 입장 횟수. 0 이하 = BalanceTables 폴백.</summary>
    public int DailyEventDungeonFreeRuns { get; set; } = 0;
    /// <summary>일일 이벤트 던전 최대 입장 횟수(소탕권 포함). 0 이하 = BalanceTables 폴백.</summary>
    public int DailyEventDungeonMaxRuns { get; set; } = 0;
    /// <summary>일일 빠른사냥 최대 횟수. 0 이하 = BalanceTables 폴백.</summary>
    public int DailyQuickHuntMaxRuns { get; set; } = 0;

    /// <summary>레벨 기반 배율 조회. 티어 미설정 시 1.0 반환.</summary>
    public float GetOfflineRewardMultiplier(int level)
    {
        if (OfflineRewardMultiplierTiers.Count == 0) return 1.0f;
        foreach (var tier in OfflineRewardMultiplierTiers)
        {
            if (level < tier.MaxLevelExclusive) return tier.Multiplier;
        }
        return OfflineRewardMultiplierTiers[^1].Multiplier;
    }
}

public class OfflineRewardTier
{
    /// <summary>이 배율이 적용되는 상한(배타). 예: 10이면 level 0..9 적용.</summary>
    public int MaxLevelExclusive { get; set; }
    public float Multiplier { get; set; } = 1.0f;
}
