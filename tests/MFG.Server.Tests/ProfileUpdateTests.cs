using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MFG.Data;

namespace MFG.Server.Tests;

// S23-08 검증: PATCH /api/v1/auth/profile JobId 반영 + Level/CombatPower 0값 스킵 방어
public sealed class ProfileUpdateTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    public ProfileUpdateTests(TestAppFactory factory) => _factory = factory;

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
    public async Task UpdateProfile_WithJobId_PersistsToDatabase()
    {
        var uid = "test-profile-jobid-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);
        var playerId = await LoginAndGetPlayerIdAsync(client);

        var payload = new { jobId = "warrior", nickname = "용사테스트" };
        var res = await client.PatchAsJsonAsync("/api/v1/auth/profile", payload);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<ProfileShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.JobId.Should().Be("warrior");
        body.Data.Nickname.Should().Be("용사테스트");

        // DB 레벨 검증 — 응답뿐 아니라 실제 저장됐는지
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.AsNoTracking().FirstAsync(p => p.Id == playerId);
        player.JobId.Should().Be("warrior");
        player.Nickname.Should().Be("용사테스트");
    }

    [Fact]
    public async Task UpdateProfile_LevelZero_IsSkipped()
    {
        // 시나리오: JsonUtility가 int 기본값 0을 보낸 경우 — 서버는 무시해야 함 (clientDev 2026-04-15 요청)
        var uid = "test-profile-lv0-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);
        var playerId = await LoginAndGetPlayerIdAsync(client);

        // 1단계: 먼저 레벨 50으로 설정
        var setup = await client.PatchAsJsonAsync("/api/v1/auth/profile", new { level = 50 });
        setup.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2단계: level=0 보내면 **레벨 유지** (덮어쓰지 않음)
        var payload = new { level = 0, combatPower = 0L, jobId = "archer" };
        var res = await client.PatchAsJsonAsync("/api/v1/auth/profile", payload);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.AsNoTracking().FirstAsync(p => p.Id == playerId);
        player.Level.Should().Be(50, "level=0은 JsonUtility 미전송 아티팩트로 간주하고 스킵해야 함");
        player.JobId.Should().Be("archer", "JobId는 정상 반영");
    }

    [Fact]
    public async Task UpdateProfile_JobIdTooLong_Returns400()
    {
        var uid = "test-profile-long-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);
        await LoginAndGetPlayerIdAsync(client);

        var tooLong = new string('x', 33); // 32자 초과
        var res = await client.PatchAsJsonAsync("/api/v1/auth/profile", new { jobId = tooLong });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    private sealed class ProfileShape
    {
        public long PlayerId { get; set; }
        public string Nickname { get; set; } = "";
        public int Level { get; set; }
        public long CombatPower { get; set; }
        public string JobId { get; set; } = "";
    }
}
