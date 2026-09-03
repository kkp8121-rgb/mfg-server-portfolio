using System.Net;
using FluentAssertions;

namespace MFG.Server.Tests;

public sealed class HealthCheckTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    public HealthCheckTests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("healthy");
    }
}
