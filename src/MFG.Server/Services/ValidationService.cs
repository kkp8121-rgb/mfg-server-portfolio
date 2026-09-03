using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MFG.Data;
using MFG.Server.Configuration;

namespace MFG.Server.Services;

/// <summary>
/// 일일 제한 검증 및 밸런스 유효성 검사 서비스.
/// DailyCurrencyEarnCaps 는 IOptionsMonitor&lt;BalanceConfig&gt; 우선 소비 + BalanceTables 폴백 (S261-04 핫리로드).
/// </summary>
public class ValidationService
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<BalanceConfig>? _balance;

    public ValidationService(AppDbContext db)
    {
        _db = db;
    }

    public ValidationService(AppDbContext db, IOptionsMonitor<BalanceConfig> balance)
    {
        _db = db;
        _balance = balance;
    }

    /// <summary>
    /// 특정 재화의 현재 일일 상한 조회. BalanceConfig 에 정의된 값 우선, 없으면 BalanceTables 정적 딕셔너리.
    /// 반환 null = 상한 없음.
    /// </summary>
    public long? GetCurrentDailyCap(string currencyType)
    {
        var configCaps = _balance?.CurrentValue.DailyCurrencyEarnCaps;
        if (configCaps is { Count: > 0 } && configCaps.TryGetValue(currencyType, out var liveCap))
            return liveCap;
        return BalanceTables.DailyCurrencyEarnCaps.TryGetValue(currencyType, out var staticCap)
            ? staticCap
            : null;
    }

    /// <summary>
    /// 오늘 특정 Reason 접두사로 획득한 재화 총액 조회.
    /// </summary>
    public async Task<long> GetDailyEarnedAsync(
        long playerId, string currencyType, string reasonPrefix, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        return await _db.CurrencyTransactions
            .Where(t => t.PlayerId == playerId
                     && t.CurrencyType == currencyType
                     && t.Amount > 0
                     && t.Reason.StartsWith(reasonPrefix)
                     && t.CreatedAt >= today)
            .SumAsync(t => t.Amount, ct);
    }

    /// <summary>
    /// 일일 재화 획득 상한 초과 여부 검사.
    /// true = 지급 가능, false = 상한 초과.
    /// </summary>
    public async Task<bool> CheckDailyEarnCapAsync(
        long playerId, string currencyType, long amountToAdd, CancellationToken ct)
    {
        var cap = GetCurrentDailyCap(currencyType);
        if (cap is null)
            return true; // 상한 미정의 재화는 제한 없음

        var today = DateTime.UtcNow.Date;

        var earned = await _db.CurrencyTransactions
            .Where(t => t.PlayerId == playerId
                     && t.CurrencyType == currencyType
                     && t.Amount > 0
                     && t.CreatedAt >= today)
            .SumAsync(t => t.Amount, ct);

        return (earned + amountToAdd) <= cap.Value;
    }

    /// <summary>이벤트 던전 무료 입장 횟수 (라이브 우선 / BalanceTables 폴백).</summary>
    public int GetDailyEventDungeonFreeRuns() =>
        (_balance?.CurrentValue.DailyEventDungeonFreeRuns ?? 0) > 0
            ? _balance!.CurrentValue.DailyEventDungeonFreeRuns
            : BalanceTables.DailyEventDungeonFreeRuns;

    /// <summary>이벤트 던전 최대 입장 횟수 (라이브 우선 / BalanceTables 폴백).</summary>
    public int GetDailyEventDungeonMaxRuns() =>
        (_balance?.CurrentValue.DailyEventDungeonMaxRuns ?? 0) > 0
            ? _balance!.CurrentValue.DailyEventDungeonMaxRuns
            : BalanceTables.DailyEventDungeonMaxRuns;

    /// <summary>빠른사냥 최대 횟수 (라이브 우선 / BalanceTables 폴백).</summary>
    public int GetDailyQuickHuntMaxRuns() =>
        (_balance?.CurrentValue.DailyQuickHuntMaxRuns ?? 0) > 0
            ? _balance!.CurrentValue.DailyQuickHuntMaxRuns
            : BalanceTables.DailyQuickHuntMaxRuns;

    /// <summary>
    /// 오늘 이벤트 던전 클리어 횟수 조회.
    /// CurrencyTransaction의 Reason이 "Event_EventDungeon"인 건수 기준.
    /// </summary>
    public async Task<int> GetDailyEventDungeonRunsAsync(long playerId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        return await _db.CurrencyTransactions
            .CountAsync(t => t.PlayerId == playerId
                          && t.Reason == "Event_EventDungeon"
                          && t.CreatedAt >= today, ct);
    }

    /// <summary>레벨 유효 범위 검증 (1 ~ MaxLevel)</summary>
    public static bool IsValidLevel(int level) =>
        level >= 1 && level <= BalanceTables.MaxLevel;

    /// <summary>해당 레벨의 필요 경험치 조회. 범위 밖이면 long.MaxValue 반환.</summary>
    public static long GetRequiredExp(int level)
    {
        if (level < 1 || level > BalanceTables.MaxLevel)
            return long.MaxValue;

        return BalanceTables.RequiredExpByLevel[level - 1];
    }
}
