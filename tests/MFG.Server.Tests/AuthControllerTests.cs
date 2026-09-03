using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MFG.Data;

namespace MFG.Server.Tests;

public sealed class AuthControllerTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    public AuthControllerTests(TestAppFactory factory) => _factory = factory;

    private HttpClient ClientAs(string devUid)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", devUid);
        return client;
    }

    [Fact]
    public async Task Login_NewUser_CreatesPlayerWithInitialCurrencies()
    {
        var uid = "test-new-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);

        var res = await client.PostAsync("/api/v1/auth/login", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<LoginShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.IsNewPlayer.Should().BeTrue();
        body.Data.PlayerId.Should().BeGreaterThan(0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currencies = await db.Currencies.Where(c => c.PlayerId == body.Data.PlayerId).ToListAsync();
        currencies.Should().HaveCount(2);
        currencies.Should().Contain(c => c.Type == "Gold" && c.Amount == 10000);
        currencies.Should().Contain(c => c.Type == "Ruby" && c.Amount == 10000);
    }

    [Fact]
    public async Task Login_ExistingUser_IsNewPlayerFalse()
    {
        var uid = "test-existing-" + Guid.NewGuid().ToString("N")[..8];
        var client = ClientAs(uid);

        await client.PostAsync("/api/v1/auth/login", null);
        var res = await client.PostAsync("/api/v1/auth/login", null);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<LoginShape>>();
        body!.Data!.IsNewPlayer.Should().BeFalse();
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
        public string Nickname { get; set; } = "";
        public bool IsNewPlayer { get; set; }
        public DateTime ServerTime { get; set; }
    }
}
