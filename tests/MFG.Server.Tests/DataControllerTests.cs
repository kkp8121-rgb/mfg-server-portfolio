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

// Phase 26 Sprint 26-1 S261-01 검증.
// GET /api/v1/data/version — 3-Tier 버전 해시 반환.
public sealed class DataControllerTests
{
    [Fact]
    public async Task GetVersion_ReturnsConfiguredHashes()
    {
        // 알려진 해시를 appsettings 오버라이드로 주입.
        // IOptionsSnapshot 경로 검증 — InMemoryCollection 이 Configuration에 병합되어 Value로 나와야 함.
        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["DataVersions:Visual"]  = "aaaa1111",
            ["DataVersions:Config"]  = "bbbb2222",
            ["DataVersions:Balance"] = "cccc3333",
            ["DataVersions:Catalog"] = "dddd4444",
        });

        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/data/version");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<DataVersionShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.Visual.Should().Be("aaaa1111");
        body.Data.Config.Should().Be("bbbb2222");
        body.Data.Balance.Should().Be("cccc3333");
        body.Data.Catalog.Should().Be("dddd4444");
        body.Data.ServerTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GetVersion_AllowsAnonymous()
    {
        // [AllowAnonymous] — X-Dev-Uid 헤더 없어도 200.
        // TestAppFactory의 Dev 미들웨어는 헤더 미설정시 "dev-player-001" 기본을 주입하므로
        // 순수 [AllowAnonymous] 검증은 별도 클라이언트로 수행 (헤더 없음).
        await using var factory = new CustomFactory(new Dictionary<string, string?>());
        var client = factory.CreateClient();
        // 헤더 미설정

        var res = await client.GetAsync("/api/v1/data/version");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // appsettings 오버라이드가 가능한 커스텀 팩토리.
    // TestAppFactory를 그대로 쓰면 Configure 순서 때문에 Value 주입이 불안정해 별도 작성.
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

    private sealed class DataVersionShape
    {
        public string Visual { get; set; } = "";
        public string Config { get; set; } = "";
        public string Balance { get; set; } = "";
        public string Catalog { get; set; } = "";
        public DateTime ServerTime { get; set; }
    }
}
