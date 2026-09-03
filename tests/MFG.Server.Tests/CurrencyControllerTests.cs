using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace MFG.Server.Tests;

public sealed class CurrencyControllerTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    public CurrencyControllerTests(TestAppFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInClientAsync()
    {
        var uid = "test-cur-" + Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Uid", uid);
        // 신규 유저 생성 + 초기 재화(Gold/Ruby 10000) 지급
        var login = await client.PostAsync("/api/v1/auth/login", null);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    [Fact]
    public async Task Balance_NewPlayer_Returns10000Each()
    {
        var client = await LoggedInClientAsync();

        var res = await client.GetAsync("/api/v1/currency/balance");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<BalanceShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.Currencies.Should().Contain(c => c.Type == "Gold" && c.Amount == 10000);
        body.Data.Currencies.Should().Contain(c => c.Type == "Ruby" && c.Amount == 10000);
    }

    [Fact]
    public async Task Spend_ValidAmount_DecreasesBalance()
    {
        var client = await LoggedInClientAsync();

        var res = await client.PostAsJsonAsync("/api/v1/currency/spend", new
        {
            currencyType = "Gold",
            amount = 3000,
            reason = "test-spend"
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<TransactionShape>>();
        body!.Success.Should().BeTrue();
        body.Data!.BalanceAfter.Should().Be(7000);
    }

    [Fact]
    public async Task Spend_InsufficientFunds_Returns400()
    {
        var client = await LoggedInClientAsync();

        var res = await client.PostAsJsonAsync("/api/v1/currency/spend", new
        {
            currencyType = "Gold",
            amount = 999_999,
            reason = "test-overspend"
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<object>>();
        body!.Success.Should().BeFalse();
        body.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Earn_IncreasesBalance()
    {
        var client = await LoggedInClientAsync();

        var res = await client.PostAsJsonAsync("/api/v1/currency/earn", new
        {
            currencyType = "Gold",
            amount = 500,
            reason = "test-earn"
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponseShape<TransactionShape>>();
        body!.Data!.BalanceAfter.Should().Be(10500);
    }

    private sealed class ApiResponseShape<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
    }

    private sealed class BalanceShape
    {
        public List<CurrencyShape> Currencies { get; set; } = [];
    }

    private sealed class CurrencyShape
    {
        public string Type { get; set; } = "";
        public long Amount { get; set; }
    }

    private sealed class TransactionShape
    {
        public string CurrencyType { get; set; } = "";
        public long Amount { get; set; }
        public long BalanceAfter { get; set; }
    }
}
