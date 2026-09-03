using Microsoft.Extensions.Options;
using MFG.Domain.Entities;
using MFG.Server.Configuration;
using MFG.Server.DTOs;

namespace MFG.Server.Services;

/// <summary>
/// 오프라인 보상 계산. 읽기 전용.
/// Preview / Claim 두 엔드포인트에서 동일한 계산식을 공유하기 위한 순수 계산기.
/// DB 상태 변경 없음 — 호출자(Claim)에서 재화 지급 + LastLogoutAt 갱신 수행.
/// 튜닝 상수는 IOptionsMonitor&lt;BalanceConfig&gt; 로 핫리로드 (S261-04). Options 미주입 시 BalanceTables 정적 상수로 폴백.
/// 관련: server/Docs/Planning/offline-reward-model.md
/// </summary>
public class OfflineRewardService
{
    private readonly IOptionsMonitor<BalanceConfig>? _balance;

    public OfflineRewardService() { }

    public OfflineRewardService(IOptionsMonitor<BalanceConfig> balance)
    {
        _balance = balance;
    }

    /// <summary>
    /// Player의 LastLogoutAt(또는 LastLoginAt) 기준으로 경과 시간과 보상을 계산한다.
    /// </summary>
    /// <param name="player">대상 플레이어 (Level, LastLogoutAt/LastLoginAt 사용).</param>
    /// <param name="nowUtc">기준 시각(테스트 주입용). 기본은 DateTime.UtcNow.</param>
    public OfflineRewardCalculation Calculate(Player player, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var anchor = player.LastLogoutAt ?? player.LastLoginAt;
        var elapsed = now - anchor;

        // 음수 방지 (시계 조작/clock-skew 방어)
        var elapsedSeconds = Math.Max(0.0, elapsed.TotalSeconds);
        var elapsedHours = elapsedSeconds / 3600.0;

        var cfg = _balance?.CurrentValue;
        var maxHours = (cfg is { OfflineRewardMaxHours: > 0 })
            ? cfg.OfflineRewardMaxHours
            : BalanceTables.OfflineRewardMaxHours;
        var goldCoef = (cfg is { OfflineRewardGoldPerHourCoef: > 0 })
            ? cfg.OfflineRewardGoldPerHourCoef
            : BalanceTables.OfflineRewardGoldPerHourCoef;
        var multiplier = (cfg is { OfflineRewardMultiplierTiers.Count: > 0 })
            ? cfg.GetOfflineRewardMultiplier(player.Level)
            : BalanceTables.GetOfflineRewardMultiplier(player.Level);

        var cappedHours = Math.Min(elapsedHours, maxHours);
        var capped = elapsedHours > maxHours;

        // offlineGold = hours × coef × level × multiplier
        long coinReward = (long)(cappedHours
                                 * goldCoef
                                 * player.Level
                                 * multiplier);

        // 현재 스펙 상 Gem 보상은 미지급(0). 추후 기획 확정 시 확장.
        long gemReward = 0L;

        var rewards = new List<RewardEntry>
        {
            new() { Type = "Gold", Amount = coinReward }
        };

        return new OfflineRewardCalculation
        {
            PlayerId = player.Id,
            CoinReward = coinReward,
            GemReward = gemReward,
            ElapsedSeconds = (long)elapsedSeconds,
            CappedSeconds = (long)(cappedHours * 3600.0),
            LastLogoutAt = anchor,
            ServerTime = now,
            Capped = capped,
            Multiplier = multiplier,
            Rewards = rewards
        };
    }
}

/// <summary>
/// 오프라인 보상 계산 결과 (순수 값 객체).
/// Controller에서 DTO로 변환해 반환하거나, Claim에서 실제 지급 대상으로 사용.
/// </summary>
public class OfflineRewardCalculation
{
    public long PlayerId { get; set; }
    public long CoinReward { get; set; }
    public long GemReward { get; set; }
    public long ElapsedSeconds { get; set; }   // 원본 경과 초 (상한 미적용)
    public long CappedSeconds { get; set; }    // 상한 적용 후 초
    public DateTime LastLogoutAt { get; set; }
    public DateTime ServerTime { get; set; }
    public bool Capped { get; set; }
    public float Multiplier { get; set; }
    public List<RewardEntry> Rewards { get; set; } = [];
}
