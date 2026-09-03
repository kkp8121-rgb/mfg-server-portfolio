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

// Phase 26 Sprint 26-1 S261-02 검증.
// GET /api/v1/data/config/latest — Config 타입별 최신 CDN URL 목록.
public sealed class DataConfigLatestTests
{
    [Fact]
    public async Task GetConfigLatest_ReturnsAllTypes_WhenNoFilter()
    {
        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["DataVersions:ConfigCdnBaseUrl"] = "https://cdn.test/config",
            ["DataVersions:ConfigTypes:characters"] = "aaaa1111",
            ["DataVersions:ConfigTypes:monsters"]   = "bbbb2222",
            ["DataVersions:ConfigTypes:skills"]     = "cccc3333",
            ["DataVersions:ConfigTypes:stages"]     = "dddd4444",
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/data/config/latest");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<DataConfigLatestShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.Entries.Should().HaveCount(4);

        var characters = body.Data.Entries.Single(e => e.Type == "characters");
        characters.Hash.Should().Be("aaaa1111");
        characters.Url.Should().Be("https://cdn.test/config/characters-aaaa1111.json");

        var monsters = body.Data.Entries.Single(e => e.Type == "monsters");
        monsters.Url.Should().Be("https://cdn.test/config/monsters-bbbb2222.json");

        body.Data.ServerTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GetConfigLatest_FiltersByType()
    {
        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["DataVersions:ConfigCdnBaseUrl"] = "https://cdn.test/config",
            ["DataVersions:ConfigTypes:characters"] = "aaaa1111",
            ["DataVersions:ConfigTypes:monsters"]   = "bbbb2222",
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/data/config/latest?type=characters");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<DataConfigLatestShape>>();
        body!.Data!.Entries.Should().HaveCount(1);
        body.Data.Entries[0].Type.Should().Be("characters");
        body.Data.Entries[0].Url.Should().Be("https://cdn.test/config/characters-aaaa1111.json");
    }

    [Fact]
    public async Task GetConfigLatest_ReturnsEmpty_WhenUnknownType()
    {
        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["DataVersions:ConfigCdnBaseUrl"] = "https://cdn.test/config",
            ["DataVersions:ConfigTypes:characters"] = "aaaa1111",
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/data/config/latest?type=unknown_type_xyz");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<DataConfigLatestShape>>();
        body!.Data!.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConfigLatest_TrimsTrailingSlashFromBaseUrl()
    {
        // 설정에 말미 슬래시가 있어도 URL 조합 시 이중 슬래시 발생하지 않아야 함.
        await using var factory = new CustomFactory(new Dictionary<string, string?>
        {
            ["DataVersions:ConfigCdnBaseUrl"] = "https://cdn.test/config/",
            ["DataVersions:ConfigTypes:skills"] = "cccc3333",
        });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/data/config/latest?type=skills");
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<DataConfigLatestShape>>();
        body!.Data!.Entries[0].Url.Should().Be("https://cdn.test/config/skills-cccc3333.json");
    }

    [Fact]
    public async Task GetConfigLatest_AllowsAnonymous()
    {
        await using var factory = new CustomFactory(new Dictionary<string, string?>());
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/data/config/latest");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
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

    private sealed class DataConfigLatestShape
    {
        public List<DataConfigEntryShape> Entries { get; set; } = new();
        public DateTime ServerTime { get; set; }
    }

    private sealed class DataConfigEntryShape
    {
        public string Type { get; set; } = "";
        public string Hash { get; set; } = "";
        public string Url { get; set; } = "";
    }
}
