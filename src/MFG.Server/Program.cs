using System.Security.Claims;
using System.Threading.RateLimiting;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MFG.Data;
using MFG.Server.Authorization;
using MFG.Server.Configuration;
using MFG.Server.Logging;
using MFG.Server.Middleware;
using MFG.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog 구조화 로깅 ---
// appsettings의 Serilog 섹션을 읽어 Console + 일일 롤링 파일로 출력.
// Slack:ErrorWebhook 이 설정돼 있으면 Error 이상 로그를 Slack으로도 전송 (rate limit 10/min 내장).
// 요청 로깅 미들웨어는 하단 UseSerilogRequestLogging에서 등록.
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext();

    var slackWebhook = ctx.Configuration["Slack:ErrorWebhook"];
    if (!string.IsNullOrWhiteSpace(slackWebhook))
    {
        cfg.WriteTo.Sink(new SlackWebhookSink(slackWebhook), LogEventLevel.Error);
    }
});

// --- Firebase Admin SDK ---
// serviceAccountKey.json이 있으면 초기화 (Production 필수, Dev는 선택).
// 초기화 실패는 치명 아님 — FirebaseUserService가 IsAvailable로 가드.
var firebaseCredentialPath = builder.Configuration["Firebase:CredentialPath"] ?? "serviceAccountKey.json";
var firebaseProjectIdBoot = builder.Configuration["Firebase:ProjectId"];
if (File.Exists(firebaseCredentialPath) && !string.IsNullOrWhiteSpace(firebaseProjectIdBoot))
{
    // 테스트 병렬 실행 시 재초기화 방지 — 이미 초기화돼 있으면 ArgumentException 발생하므로 try/catch
    try
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromFile(firebaseCredentialPath),
            ProjectId = firebaseProjectIdBoot
        });
        Console.WriteLine($"[Firebase] Admin SDK 초기화 완료 project={firebaseProjectIdBoot}");
    }
    catch (ArgumentException)
    {
        // 이미 초기화됨 — 동일 프로세스 내 중복 호출 (테스트 병렬 실행 등)
    }
}
else if (FirebaseApp.DefaultInstance is null)
{
    Console.WriteLine($"[Firebase] Admin SDK 스킵 (credentialExists={File.Exists(firebaseCredentialPath)}, projectId={firebaseProjectIdBoot ?? "null"})");
}

// --- Database (MySQL) ---
// 테스트에서는 UseTestDatabase=true 설정으로 이 블록을 스킵하고 TestAppFactory가 InMemory로 재등록.
var useTestDb = builder.Configuration.GetValue<bool>("UseTestDatabase");
if (!useTestDb)
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySQL(connectionString)
               .UseSnakeCaseNamingConvention());
}

// --- Authentication ---
if (builder.Environment.IsDevelopment())
{
    // Development: 인증 바이패스 (Firebase 설정 불필요)
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();
}
else
{
    // Production: Firebase JWT 검증 — ProjectId는 반드시 환경변수/appsettings로 주입해야 함.
    var firebaseProjectId = builder.Configuration["Firebase:ProjectId"];
    if (string.IsNullOrWhiteSpace(firebaseProjectId))
        throw new InvalidOperationException("Firebase:ProjectId 설정이 비어있습니다. appsettings.Production.json 또는 환경변수 Firebase__ProjectId를 설정하세요.");
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
                ValidateAudience = true,
                ValidAudience = firebaseProjectId,
                ValidateLifetime = true
            };
        });
}
builder.Services.AddAuthorization(options =>
{
    // S261-04 admin 가드: user_id 클레임이 AdminConfig.AllowedUserIds 에 있는 경우에만 통과.
    options.AddPolicy("AdminOnly", policy => policy.Requirements.Add(new AdminOnlyRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, AdminOnlyHandler>();

// --- Services ---
builder.Services.AddScoped<GachaService>();
builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<ValidationService>();
builder.Services.AddScoped<HotDealService>();
builder.Services.AddScoped<OfflineRewardService>();
builder.Services.AddSingleton<FirebaseUserService>();
builder.Services.AddSingleton<GooglePlayReceiptVerifier>();

// --- Options (Phase 26 Sprint 26-1) ---
// IOptionsSnapshot 으로 주입 → 요청 단위 갱신. appsettings 수정 시 재기동 없이 반영.
builder.Services.Configure<DataVersionsConfig>(builder.Configuration.GetSection("DataVersions"));
builder.Services.Configure<SystemNoticeConfig>(builder.Configuration.GetSection("SystemNotice"));
// S261-04: 핫리로드 튜닝값. IOptionsMonitor 로 소비 → 파일 변경 자동 반영 + /admin/balance/reload 로 수동 트리거 가능.
builder.Services.Configure<BalanceConfig>(builder.Configuration.GetSection("Balance"));
// S261-04 admin 화이트리스트. 기본 빈 리스트 → deny-by-default.
builder.Services.Configure<AdminConfig>(builder.Configuration.GetSection("Admin"));

// Pub/Sub RTDN 구독 — Production 환경에서만 기동. 테스트/Dev에서는 스킵해 네트워크 호출 회피.
if (builder.Environment.IsProduction())
{
    builder.Services.AddHostedService<PubSubSubscriberService>();
}

// --- CORS (WebGL + 개발용) ---
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                             ?? ["http://localhost:3000", "http://localhost:5500"];
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --- Rate Limiting ---
// 글로벌: IP 기준 1분 60회. 남용 경로(가챠/IAP)는 user 기준 추가 정책으로 더 빠듯하게 제한.
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));

    // 가챠: 유저당 1분 20회 (10연차 기준 약 2회 여유) — 자동 클릭/스팸 차단
    options.AddPolicy("gacha", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.FindFirst("user_id")?.Value
                         ?? context.Connection.RemoteIpAddress?.ToString()
                         ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // IAP: 유저당 1분 10회 — 결제 검증은 드물게 일어나야 함. 영수증 리플레이/중복 검증 남용 방지
    options.AddPolicy("iap", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.FindFirst("user_id")?.Value
                         ?? context.Connection.RemoteIpAddress?.ToString()
                         ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// --- Controllers ---
builder.Services.AddControllers();

// --- Swagger/OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MFG API",
        Version = "v1",
        Description = "MFG 서버 API — 인증, 가챠, 재화, 장비, 아레나, 길드, 출석, IAP, 핫딜"
    });
});

var app = builder.Build();

// --- 마이그레이션 자동 적용 ---
// Relational DB(MySQL)만 Migrate. 테스트 환경의 InMemory는 EnsureCreated로 스키마만 생성.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

// --- Middleware ---
app.UseMiddleware<GlobalExceptionHandler>();

// Serilog 요청 로깅 — RequestId / UserId / Path / StatusCode / Elapsed 포함
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("UserId", http.User?.FindFirst("user_id")?.Value
                          ?? http.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? "anon");
        diag.Set("RemoteIP", http.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    };
});

// --- Swagger (Development) ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "MFG API v1"));
}

app.UseCors();

// Development: 인증 바이패스 미들웨어 (JWT 검증 전에 ClaimsIdentity 주입)
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var devUid = context.Request.Headers["X-Dev-Uid"].FirstOrDefault() ?? "dev-player-001";
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, devUid),
            new Claim("user_id", devUid)
        };
        var identity = new ClaimsIdentity(claims, "DevAuth");
        context.User = new ClaimsPrincipal(identity);
        await next();
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

// --- Health Check ---
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
}));

app.Run();
