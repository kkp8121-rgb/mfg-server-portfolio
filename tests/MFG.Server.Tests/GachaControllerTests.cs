using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace MFG.Server.Tests;

public sealed class GachaControllerTests : IClassFixture<TestAppFactory>
{
    private const long InitialRuby = 10000;
    private const long SingleCost = 300;
    private const long TenCost = 2700;

    private readonly TestAppFactory _factory;
    public GachaControllerTests(TestAppFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInClientAsync()
    {
        var uid = "test-gacha-" + Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", uid);
        (await client.PostAsync("/api/v1/auth/login", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    [Fact]
    public async Task Pull_Single_ReturnsOneResult_AndSpendsRuby()
    {
        var client = await LoggedInClientAsync();

        var res = await client.PostAsJsonAsync("/api/v1/gacha/pull", new
        {
            poolType = "Equipment",
            pullCount = 1
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<PullShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.Results.Should().HaveCount(1);
        body.Data.CurrencySpent.Amount.Should().Be(SingleCost);
        body.Data.CurrencyRemaining.Should().Be(InitialRuby - SingleCost);
        body.Data.PityCount.Should().Be(1);
    }

    [Fact]
    public async Task Pull_Ten_ReturnsTenResults_WithRareGuarantee()
    {
        var client = await LoggedInClientAsync();

        var res = await client.PostAsJsonAsync("/api/v1/gacha/pull", new
        {
            poolType = "Equipment",
            pullCount = 10
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<PullShape>>();
        body!.Data!.Results.Should().HaveCount(10);
        body.Data.CurrencySpent.Amount.Should().Be(TenCost);
        body.Data.CurrencyRemaining.Should().Be(InitialRuby - TenCost);
        body.Data.PityCount.Should().Be(10);

        // 10연차 Rare 이상 보장 (Rare/Epic/Unique/Legendary/Mythic 중 하나 포함)
        var guaranteedGrades = new[] { "Rare", "Epic", "Unique", "Legendary", "Mythic" };
        body.Data.Results.Should().Contain(r => guaranteedGrades.Contains(r.Grade));
    }

    [Fact]
    public async Task Pull_InvalidPullCount_Returns400()
    {
        var client = await LoggedInClientAsync();

        var res = await client.PostAsJsonAsync("/api/v1/gacha/pull", new
        {
            poolType = "Equipment",
            pullCount = 5
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pull_InvalidPool_Returns400()
    {
        var client = await LoggedInClientAsync();

        var res = await client.PostAsJsonAsync("/api/v1/gacha/pull", new
        {
            poolType = "NonExistentPool",
            pullCount = 1
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pull_InsufficientRuby_Returns400()
    {
        var client = await LoggedInClientAsync();

        // 10000 루비를 spend로 소진 (가챠는 3번 10연차 = 8100만 가능하므로 이 경로가 필요)
        var spend = await client.PostAsJsonAsync("/api/v1/currency/spend", new
        {
            currencyType = "Ruby",
            amount = InitialRuby,
            reason = "test-drain"
        });
        spend.StatusCode.Should().Be(HttpStatusCode.OK);

        var res = await client.PostAsJsonAsync("/api/v1/gacha/pull", new
        {
            poolType = "Equipment",
            pullCount = 1
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<object>>();
        body!.Success.Should().BeFalse();
        body.Error.Should().Contain("루비");
    }

    private sealed class ApiResponseShape<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
    }

    private sealed class PullShape
    {
        public List<ResultShape> Results { get; set; } = [];
        public SpentShape CurrencySpent { get; set; } = new();
        public long CurrencyRemaining { get; set; }
        public int PityCount { get; set; }
    }

    private sealed class ResultShape
    {
        public string ItemId { get; set; } = "";
        public string Grade { get; set; } = "";
        public bool IsNew { get; set; }
    }

    private sealed class SpentShape
    {
        public string Type { get; set; } = "";
        public long Amount { get; set; }
    }
}
