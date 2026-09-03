using System.Globalization;
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

// Phase 26 Sprint 26-1 S261-05 검증.
// GET /api/v1/system/notice — 점검 공지 제공.
public sealed class SystemControllerTests
{
    [Fact]
    public async Task GetNotice_WhenInactive_ReturnsActiveFalse()
    {
        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["SystemNotice:Active"] = "false",
            ["SystemNotice:Title"]  = "should-not-leak",
            ["SystemNotice:Body"]   = "should-not-leak",
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/system/notice");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<NoticeShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.Active.Should().BeFalse();
        body.Data.Title.Should().Be("", "비활성 공지 내용은 클라에 노출하지 않음");
        body.Data.Body.Should().Be("", "비활성 공지 내용은 클라에 노출하지 않음");
    }

    [Fact]
    public async Task GetNotice_WhenActiveInWindow_ReturnsActiveTrue()
    {
        // 시간 윈도우: 1시간 전 시작 ~ 1시간 후 종료 → 현재 시각 포함
        var starts = DateTime.UtcNow.AddHours(-1).ToString("o", CultureInfo.InvariantCulture);
        var ends   = DateTime.UtcNow.AddHours(1).ToString("o", CultureInfo.InvariantCulture);

        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["SystemNotice:Active"]   = "true",
            ["SystemNotice:Title"]    = "점검 공지",
            ["SystemNotice:Body"]     = "서버 점검 중입니다.",
            ["SystemNotice:StartsAt"] = starts,
            ["SystemNotice:EndsAt"]   = ends,
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/system/notice");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<NoticeShape>>();
        body!.Data!.Active.Should().BeTrue();
        body.Data.Title.Should().Be("점검 공지");
        body.Data.Body.Should().Be("서버 점검 중입니다.");
        body.Data.StartsAt.Should().NotBeNull();
        body.Data.EndsAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetNotice_WhenActiveButStartsAtFuture_ReturnsActiveFalse()
    {
        // StartsAt이 미래 — 아직 윈도우 진입 전이므로 비활성이어야 함.
        // 예약 공지를 시간 전에 미리 등록해두는 시나리오.
        var starts = DateTime.UtcNow.AddHours(2).ToString("o", CultureInfo.InvariantCulture);
        var ends   = DateTime.UtcNow.AddHours(5).ToString("o", CultureInfo.InvariantCulture);

        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["SystemNotice:Active"]   = "true",
            ["SystemNotice:Title"]    = "미래 예약 공지",
            ["SystemNotice:Body"]     = "아직 시간 전이므로 노출 금지",
            ["SystemNotice:StartsAt"] = starts,
            ["SystemNotice:EndsAt"]   = ends,
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/system/notice");
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<NoticeShape>>();
        body!.Data!.Active.Should().BeFalse("StartsAt이 미래이므로 윈도우 진입 전");
        body.Data.Title.Should().Be("", "비활성 시 내용 노출 금지");
        // 단, StartsAt/EndsAt 자체는 반환 — 클라가 언제 열릴지 UI 카운트다운 구현 가능.
        body.Data.StartsAt.Should().NotBeNull();
        body.Data.EndsAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetNotice_WhenActiveButOutsideWindow_ReturnsActiveFalse()
    {
        // Active=true지만 시간 윈도우가 과거 → 비활성으로 취급해야 함.
        var starts = DateTime.UtcNow.AddHours(-5).ToString("o", CultureInfo.InvariantCulture);
        var ends   = DateTime.UtcNow.AddHours(-1).ToString("o", CultureInfo.InvariantCulture);

        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["SystemNotice:Active"]   = "true",
            ["SystemNotice:Title"]    = "만료된 공지",
            ["SystemNotice:Body"]     = "이미 종료된 점검",
            ["SystemNotice:StartsAt"] = starts,
            ["SystemNotice:EndsAt"]   = ends,
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/system/notice");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<NoticeShape>>();
        body!.Data!.Active.Should().BeFalse("EndsAt이 과거이므로 윈도우 밖");
        body.Data.Title.Should().Be("");
    }

    [Fact]
    public async Task GetNotice_ReturnsMinClientVersion_EvenWhenInactive()
    {
        // Phase 27 Sprint 27-6 선행 — MinClientVersion/ForceUpdateMessage 는 Active 게이트 독립적.
        // 공지 미활성이어도 강제 업데이트 정보는 항상 반환되어야 한다 (부팅 시 클라 비교용).
        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["SystemNotice:Active"] = "false",
            ["SystemNotice:MinClientVersion"] = "1.2.3",
            ["SystemNotice:ForceUpdateMessage"] = "앱 업데이트가 필요합니다.",
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/system/notice");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<NoticeShape>>();
        body!.Data!.Active.Should().BeFalse();
        body.Data.MinClientVersion.Should().Be("1.2.3");
        body.Data.ForceUpdateMessage.Should().Be("앱 업데이트가 필요합니다.");
    }

    [Fact]
    public async Task GetNotice_EmptyMinClientVersion_WhenNotConfigured()
    {
        // 미설정 시 빈 문자열. 클라는 빈 문자열을 "강제 업데이트 없음"으로 해석.
        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["SystemNotice:Active"] = "false",
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/system/notice");
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<NoticeShape>>();
        body!.Data!.MinClientVersion.Should().Be("");
        body.Data.ForceUpdateMessage.Should().Be("");
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

    private sealed class NoticeShape
    {
        public bool Active { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public DateTime ServerTime { get; set; }
        public string MinClientVersion { get; set; } = "";
        public string ForceUpdateMessage { get; set; } = "";
    }
}
