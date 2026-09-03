using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MFG.Data;

namespace MFG.Server.Tests;

// 통합 테스트용 WebApplicationFactory.
// - ASPNETCORE_ENVIRONMENT=Development: Dev 바이패스 미들웨어 통과용
// - UseTestDatabase=true: Program.cs가 MySQL AddDbContext 블록을 스킵 → 여기서 InMemory로 등록
// - X-Dev-Uid 헤더로 가짜 사용자 식별
public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"mfg-test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("UseTestDatabase", "true");

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(DatabaseName));
        });
    }
}
