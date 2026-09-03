using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MFG.Data;

namespace MFG.Server.Tests;

// Phase 26 Sprint 26-1 S261-03 검증.
// GET /api/v1/event/offline-reward/preview
// - 인증된 유저에 대해 계산된 보상을 반환
// - DB 상태(LastLogoutAt 등) 변경 없음 — 순수 미리보기
public sealed class OfflineRewardPreviewTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    public OfflineRewardPreviewTests(TestAppFactory factory) => _factory = factory;

    private HttpClient ClientAs(string devUid)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", devUid);
        return client;
    }

    private async Task<long> LoginAndGetPlayerIdAsync(HttpClient client)
    {
        var res = await client.PostAsync("/api/v1/auth/login", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<LoginShape>>();
        return body!.Data!.PlayerId;
    }

    [Fact]
    public async Task GivenAuthenticated_WhenPreview_ThenReturnsCalculatedReward()
    {
        var uid = "test-preview-ok-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);
        var playerId = await LoginAndGetPlayerIdAsync(client);

        // 플레이어 레벨을 50으로 설정 + LastLogoutAt을 6시간 전으로 세팅
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(p => p.Id == playerId);
            player.Level = 50;
            player.LastLogoutAt = DateTime.UtcNow.AddHours(-6);
            await db.SaveChangesAsync();
        }

        var res = await client.GetAsync("/api/v1/event/offline-reward/preview");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<PreviewShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.PlayerId.Should().Be(playerId);
        body.Data.CoinReward.Should().BeGreaterThan(0, "6시간 방치 → Gold 보상 양수");
        body.Data.DurationSeconds.Should().BeInRange(21500, 21700, "약 6시간(21,600초) ±허용오차");
        body.Data.Capped.Should().BeFalse("6시간은 12시간 상한 미만");
    }

    [Fact]
    public async Task GivenOver12Hours_WhenPreview_ThenCappedIsTrue()
    {
        var uid = "test-preview-cap-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);
        var playerId = await LoginAndGetPlayerIdAsync(client);

        // 48시간 전 로그아웃 → 12시간 상한 적용되어야 함
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(p => p.Id == playerId);
            player.Level = 30;
            player.LastLogoutAt = DateTime.UtcNow.AddHours(-48);
            await db.SaveChangesAsync();
        }

        var res = await client.GetAsync("/api/v1/event/offline-reward/preview");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<PreviewShape>>();
        body!.Data!.Capped.Should().BeTrue("48시간은 12시간 상한 초과");
        body.Data.DurationSeconds.Should().BeInRange(43100, 43300, "12시간(43,200초) 상한 적용");
    }

    [Fact]
    public async Task GivenPreview_WhenCalled_ThenDoesNotMutateLastLogoutAt()
    {
        // 스펙 핵심: 미리보기는 DB 상태를 변경해서는 안 됨.
        var uid = "test-preview-nomutate-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);
        var playerId = await LoginAndGetPlayerIdAsync(client);

        var beforeLogoutAt = DateTime.UtcNow.AddHours(-3);
        int beforeLevel;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(p => p.Id == playerId);
            player.Level = 25;
            player.LastLogoutAt = beforeLogoutAt;
            beforeLevel = player.Level;
            await db.SaveChangesAsync();
        }

        // preview 호출 (2회 호출해도 상태 변경 없어야 함)
        var res1 = await client.GetAsync("/api/v1/event/offline-reward/preview");
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var res2 = await client.GetAsync("/api/v1/event/offline-reward/preview");
        res2.StatusCode.Should().Be(HttpStatusCode.OK);

        // DB 재조회 — LastLogoutAt / Level 불변 확인
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.AsNoTracking().FirstAsync(p => p.Id == playerId);
            player.LastLogoutAt.Should().NotBeNull();
            // 밀리초 오차 허용 — 하지만 호출 전 값과 사실상 동일해야 함
            player.LastLogoutAt!.Value.Should().BeCloseTo(beforeLogoutAt, TimeSpan.FromMilliseconds(100));
            player.Level.Should().Be(beforeLevel);
        }
    }

    [Fact]
    public async Task GivenNoLastLogout_WhenPreview_ThenUsesLastLoginAt()
    {
        // 신규 유저 (LastLogoutAt null) — LastLoginAt을 기준으로 계산해야 함.
        var uid = "test-preview-newuser-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);
        var playerId = await LoginAndGetPlayerIdAsync(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(p => p.Id == playerId);
            player.Level = 10;
            player.LastLogoutAt = null;
            player.LastLoginAt = DateTime.UtcNow.AddHours(-2);
            await db.SaveChangesAsync();
        }

        var res = await client.GetAsync("/api/v1/event/offline-reward/preview");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<PreviewShape>>();
        body!.Data!.DurationSeconds.Should().BeInRange(7100, 7300, "LastLoginAt 기준 약 2시간");
        body.Data.CoinReward.Should().BeGreaterThan(0);
    }

    private sealed class ApiResponseShape<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
    }

    private sealed class LoginShape
    {
        public long PlayerId { get; set; }
    }

    private sealed class PreviewShape
    {
        public long PlayerId { get; set; }
        public long CoinReward { get; set; }
        public long GemReward { get; set; }
        public long DurationSeconds { get; set; }
        public DateTime LastLogoutAt { get; set; }
        public DateTime ServerTime { get; set; }
        public bool Capped { get; set; }
    }
}
