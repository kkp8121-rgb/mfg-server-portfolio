using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MFG.Data;
using MFG.Server.Configuration;
using MFG.Server.Services;

namespace MFG.Server.Tests;

// S261-04 보강: DailyCurrencyEarnCaps 핫리로드.
// GetCurrentDailyCap 의 소스 선택 로직(BalanceConfig 우선 + BalanceTables 폴백)을 직접 검증.
public sealed class ValidationServiceTests
{
    private static ValidationService BuildService(Dictionary<string, long>? liveCaps)
    {
        // 이 헬퍼는 DB 를 터치하지 않는 GetCurrentDailyCap 만 사용하므로 InMemoryDatabase 로 충분.
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"vs-test-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(dbOptions);

        if (liveCaps is null)
        {
            return new ValidationService(db);
        }

        var config = new BalanceConfig { DailyCurrencyEarnCaps = liveCaps };
        var monitor = new StaticOptionsMonitor<BalanceConfig>(config);
        return new ValidationService(db, monitor);
    }

    [Fact]
    public void GetCurrentDailyCap_UsesBalanceConfig_WhenDefined()
    {
        // 라이브 설정의 값이 BalanceTables 기본(1억 Gold)을 덮어써야 함.
        var svc = BuildService(new Dictionary<string, long> { ["Gold"] = 500L });
        svc.GetCurrentDailyCap("Gold").Should().Be(500L);
    }

    [Fact]
    public void GetCurrentDailyCap_FallsBackToBalanceTables_WhenConfigEmpty()
    {
        // BalanceConfig.DailyCurrencyEarnCaps 미지정 → BalanceTables(Gold=1억, Ruby=5만) 사용.
        var svc = BuildService(new Dictionary<string, long>());
        svc.GetCurrentDailyCap("Gold").Should().Be(100_000_000L);
        svc.GetCurrentDailyCap("Ruby").Should().Be(50_000L);
    }

    [Fact]
    public void GetCurrentDailyCap_FallsBackToBalanceTables_WhenMonitorMissing()
    {
        // IOptionsMonitor 주입 자체가 없는 경로(기존 생성자). BalanceTables 만 사용.
        var svc = BuildService(null);
        svc.GetCurrentDailyCap("Gold").Should().Be(100_000_000L);
    }

    [Fact]
    public void GetCurrentDailyCap_ReturnsNull_WhenUnknownCurrency()
    {
        // 상한 미정의 재화는 null — "제한 없음" 의미.
        var svc = BuildService(null);
        svc.GetCurrentDailyCap("UnknownCurrency_X").Should().BeNull();
    }

    [Fact]
    public void GetCurrentDailyCap_ConfigTakesPrecedence_ForPartialOverride()
    {
        // 라이브 설정이 Gold만 덮어쓰고 Ruby 는 누락 → Ruby 는 BalanceTables 폴백 유지.
        var svc = BuildService(new Dictionary<string, long> { ["Gold"] = 777L });
        svc.GetCurrentDailyCap("Gold").Should().Be(777L);
        svc.GetCurrentDailyCap("Ruby").Should().Be(50_000L);
    }

    [Fact]
    public void GetDailyLimits_UseBalanceConfig_WhenPositive()
    {
        // 던전/퀵헌트 상한도 BalanceConfig 라이브 값 우선.
        var svc = BuildWithLimits(freeRuns: 2, maxRuns: 9, quickHunt: 15);
        svc.GetDailyEventDungeonFreeRuns().Should().Be(2);
        svc.GetDailyEventDungeonMaxRuns().Should().Be(9);
        svc.GetDailyQuickHuntMaxRuns().Should().Be(15);
    }

    [Fact]
    public void GetDailyLimits_FallBackToBalanceTables_WhenZeroOrNotConfigured()
    {
        // 값이 0 이면 "미설정" 해석 → BalanceTables (1 / 5 / 10) 폴백.
        var svc = BuildWithLimits(freeRuns: 0, maxRuns: 0, quickHunt: 0);
        svc.GetDailyEventDungeonFreeRuns().Should().Be(BalanceTables.DailyEventDungeonFreeRuns);
        svc.GetDailyEventDungeonMaxRuns().Should().Be(BalanceTables.DailyEventDungeonMaxRuns);
        svc.GetDailyQuickHuntMaxRuns().Should().Be(BalanceTables.DailyQuickHuntMaxRuns);
    }

    private static ValidationService BuildWithLimits(int freeRuns, int maxRuns, int quickHunt)
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"vs-limits-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(dbOptions);
        var config = new BalanceConfig
        {
            DailyEventDungeonFreeRuns = freeRuns,
            DailyEventDungeonMaxRuns = maxRuns,
            DailyQuickHuntMaxRuns = quickHunt,
        };
        return new ValidationService(db, new StaticOptionsMonitor<BalanceConfig>(config));
    }

    // IOptionsMonitor 최소 구현 — 테스트용. CurrentValue 만 제공, OnChange 는 무동작.
    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value) { CurrentValue = value; }
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string?> listener) => new NullDisposable();
        private sealed class NullDisposable : IDisposable { public void Dispose() { } }
    }
}
