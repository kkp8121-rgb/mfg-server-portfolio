using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MFG.Data;

namespace MFG.Server.Tests;

// S251-01 검증: POST /api/v1/save/migrate — 로컬 SaveData → 서버 1회 업로드
public sealed class SaveMigrateTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    public SaveMigrateTests(TestAppFactory factory) => _factory = factory;

    private HttpClient ClientAs(string devUid)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", devUid);
        return client;
    }

    private async Task<long> LoginAsync(HttpClient client)
    {
        var res = await client.PostAsync("/api/v1/auth/login", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<LoginShape>>();
        return body!.Data!.PlayerId;
    }

    [Fact]
    public async Task Migrate_FirstTime_CreatesProgressDataAndReturnsMigratedTrue()
    {
        var uid = "test-migrate-first-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);
        var playerId = await LoginAsync(client);

        var payload = new
        {
            saveData = "{\"playerData\":{\"level\":42,\"nickname\":\"테스터\"},\"currency\":{}}",
            clientTimestamp = DateTime.UtcNow
        };
        var res = await client.PostAsJsonAsync("/api/v1/save/migrate", payload);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<MigrateShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.Migrated.Should().BeTrue();
        body.Data.Version.Should().Be(1);
        body.Data.Reason.Should().BeNull();

        // DB 레벨 검증 — ProgressData row 실제 생성
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var progress = await db.ProgressData.AsNoTracking().FirstOrDefaultAsync(p => p.PlayerId == playerId);
        progress.Should().NotBeNull();
        progress!.SaveJson.Should().Be(payload.saveData);
        progress.Version.Should().Be(1);
    }

    [Fact]
    public async Task Migrate_WhenAlreadyExists_ReturnsAlreadyExistsReason()
    {
        var uid = "test-migrate-dup-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);
        await LoginAsync(client);

        var payload = new
        {
            saveData = "{\"playerData\":{\"level\":10}}",
            clientTimestamp = DateTime.UtcNow
        };

        // 1회차: migrated=true
        var first = await client.PostAsJsonAsync("/api/v1/save/migrate", payload);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponseShape<MigrateShape>>();
        firstBody!.Data!.Migrated.Should().BeTrue();

        // 2회차: migrated=false, reason="already_exists"
        var second = await client.PostAsJsonAsync("/api/v1/save/migrate", payload);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<ApiResponseShape<MigrateShape>>();
        secondBody!.Success.Should().BeTrue();
        secondBody.Data!.Migrated.Should().BeFalse();
        secondBody.Data.Reason.Should().Be("already_exists");
    }

    [Fact]
    public async Task Migrate_FutureTimestamp_Returns400()
    {
        var uid = "test-migrate-future-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);
        await LoginAsync(client);

        var payload = new
        {
            saveData = "{}",
            clientTimestamp = DateTime.UtcNow.AddHours(1) // 1시간 미래 (5분 허용치 초과)
        };
        var res = await client.PostAsJsonAsync("/api/v1/save/migrate", payload);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Migrate_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient(); // X-Dev-Uid 헤더 없음
        var payload = new { saveData = "{}", clientTimestamp = DateTime.UtcNow };
        var res = await client.PostAsJsonAsync("/api/v1/save/migrate", payload);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        public bool IsNewPlayer { get; set; }
    }

    private sealed class MigrateShape
    {
        public long PlayerId { get; set; }
        public bool Migrated { get; set; }
        public int Version { get; set; }
        public string? Reason { get; set; }
    }
}
