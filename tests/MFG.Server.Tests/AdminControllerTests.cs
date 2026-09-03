using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MFG.Data;

namespace MFG.Server.Tests;

// Phase 26 Sprint 26-1 S261-04 검증.
// GET  /api/v1/admin/balance/snapshot — BalanceConfig 현재 값 조회
// POST /api/v1/admin/balance/reload   — IConfigurationRoot 재로드 후 현재 값 반환
// AdminOnly 정책: Admin:AllowedUserIds 화이트리스트 검증.
public sealed class AdminControllerTests
{
    private const string AdminUid = "admin-test-001";

    // 기본 AdminConfig 오버라이드: AdminUid 를 허용.
    private static Dictionary<string, string?> WithAdminAllow(params (string key, string? value)[] overrides)
    {
        var d = new Dictionary<string, string?>
        {
            ["Admin:AllowedUserIds:0"] = AdminUid,
        };
        foreach (var (k, v) in overrides) d[k] = v;
        return d;
    }

    [Fact]
    public async Task GetSnapshot_ReturnsConfiguredValues_WhenAdmin()
    {
        await using var factory = new CustomFactory(WithAdminAllow(
            ("Balance:OfflineRewardMaxHours", "9.5"),
            ("Balance:OfflineRewardGoldPerHourCoef", "1234.5")
        ));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", AdminUid);

        var res = await client.GetAsync("/api/v1/admin/balance/snapshot");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<BalanceReloadShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.OfflineRewardMaxHours.Should().Be(9.5);
        body.Data.OfflineRewardGoldPerHourCoef.Should().Be(1234.5);
        body.Data.OfflineRewardTierCount.Should().BeGreaterThanOrEqualTo(1);
        body.Data.ReloadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task PostReload_ReturnsFreshSnapshot_WhenAdmin()
    {
        await using var factory = new CustomFactory(WithAdminAllow(
            ("Balance:OfflineRewardMaxHours", "6.0"),
            ("Balance:OfflineRewardGoldPerHourCoef", "500.0")
        ));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", AdminUid);

        var res = await client.PostAsync("/api/v1/admin/balance/reload", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<BalanceReloadShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.OfflineRewardMaxHours.Should().Be(6.0);
        body.Data.OfflineRewardGoldPerHourCoef.Should().Be(500.0);
    }

    [Fact]
    public async Task GetSnapshot_UsesDefaults_WhenNotConfigured_ButAdmin()
    {
        // Balance 섹션 미오버라이드 시 appsettings.json 기본값 사용. 최소 12h/900 coef 불변만 검증.
        await using var factory = new CustomFactory(WithAdminAllow());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", AdminUid);

        var res = await client.GetAsync("/api/v1/admin/balance/snapshot");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<BalanceReloadShape>>();
        body!.Data!.OfflineRewardMaxHours.Should().BeGreaterThan(0);
        body.Data.OfflineRewardGoldPerHourCoef.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSnapshot_Returns403_WhenUidNotInAllowlist()
    {
        // 화이트리스트에 admin-test-001만 등록. 다른 uid 로 호출 시 거부.
        await using var factory = new CustomFactory(WithAdminAllow());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", "some-random-user");

        var res = await client.GetAsync("/api/v1/admin/balance/snapshot");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostReload_Returns403_WhenAllowlistIsEmpty()
    {
        // deny-by-default: 화이트리스트 미설정이면 어떤 uid 로도 접근 불가.
        await using var factory = new CustomFactory(new Dictionary<string, string?>());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", AdminUid);

        var res = await client.PostAsync("/api/v1/admin/balance/reload", null);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSnapshot_MatchesSecondEntry_InMultiAdminAllowlist()
    {
        // Firebase UID 여러 명 등록 시 뒤쪽 엔트리도 매칭되어야 함.
        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["Admin:AllowedUserIds:0"] = "primary-admin-uid",
            ["Admin:AllowedUserIds:1"] = "backup-admin-uid",
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", "backup-admin-uid");

        var res = await client.GetAsync("/api/v1/admin/balance/snapshot");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSnapshot_Returns403_WhenUidCaseDiffers()
    {
        // 현재 AdminOnlyHandler는 List.Contains 기본 비교자(대소문자 구분) 사용.
        // Firebase UID 는 대소문자 민감이므로 의도된 동작 — 대소문자 혼동 방어.
        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["Admin:AllowedUserIds:0"] = "AdminCaseSensitive",
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", "admincasesensitive");

        var res = await client.GetAsync("/api/v1/admin/balance/snapshot");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed class CustomFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _overrides;
        public string DatabaseName { get; } = $"mfg-test-{Guid.NewGuid():N}";

        public CustomFactory(Dictionary<string, string?> overrides)
        {
            _overrides = overrides;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("UseTestDatabase", "true");

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(_overrides);
            });

            builder.ConfigureServices(services =>
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(DatabaseName));
            });
        }
    }

    private sealed class ApiResponseShape<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
    }

    private sealed class BalanceReloadShape
    {
        public DateTime ReloadedAt { get; set; }
        public double OfflineRewardMaxHours { get; set; }
        public double OfflineRewardGoldPerHourCoef { get; set; }
        public int OfflineRewardTierCount { get; set; }
    }
}
