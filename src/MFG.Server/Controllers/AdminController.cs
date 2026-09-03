using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MFG.Server.Configuration;
using MFG.Server.DTOs;

namespace MFG.Server.Controllers;

/// <summary>
/// 운영자 전용 밸런스 제어. Phase 26 Sprint 26-1 S261-04.
/// reloadOnChange=true 인 appsettings는 파일 변경 시 자동 반영되지만,
/// DB/외부 소스 기반 설정이나 수동 갱신이 필요한 시점에 명시적 reload 트리거용.
/// TODO(Phase 27 LiveOps): admin claim/role 체크 + 감사 로그 + 관리자 대시보드 연동.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<BalanceConfig> _balance;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IConfiguration configuration,
        IOptionsMonitor<BalanceConfig> balance,
        ILogger<AdminController> logger)
    {
        _configuration = configuration;
        _balance = balance;
        _logger = logger;
    }

    /// <summary>
    /// 현재 BalanceConfig 스냅샷 조회. DB/파일 상태 변경 없음.
    /// </summary>
    [HttpGet("balance/snapshot")]
    public ActionResult<ApiResponse<BalanceReloadResponse>> GetBalanceSnapshot()
    {
        var cfg = _balance.CurrentValue;
        return Ok(ApiResponse<BalanceReloadResponse>.Ok(Snapshot(cfg)));
    }

    /// <summary>
    /// 설정 소스(appsettings 또는 환경변수) 재로드 후 BalanceConfig 현재 값을 반환.
    /// IConfigurationRoot.Reload() 실패 시 현재 캐시된 값 그대로 반환 + 경고 로그.
    /// </summary>
    [HttpPost("balance/reload")]
    public ActionResult<ApiResponse<BalanceReloadResponse>> PostBalanceReload()
    {
        // 감사 로그 — 누가/언제 reload를 트리거했는지 추적. LiveOps 관리자 대시보드 연동 전 기록용.
        var actor = User.FindFirst("user_id")?.Value ?? "unknown";
        _logger.LogInformation("[Admin/Audit] BalanceReload actor={Actor} at={Utc:o}",
            actor, DateTime.UtcNow);

        if (_configuration is IConfigurationRoot root)
        {
            try
            {
                root.Reload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Admin] BalanceConfig reload 실패 — 현재 스냅샷 반환");
            }
        }

        var cfg = _balance.CurrentValue;
        return Ok(ApiResponse<BalanceReloadResponse>.Ok(Snapshot(cfg)));
    }

    private static BalanceReloadResponse Snapshot(BalanceConfig cfg) => new()
    {
        ReloadedAt = DateTime.UtcNow,
        OfflineRewardMaxHours = cfg.OfflineRewardMaxHours,
        OfflineRewardGoldPerHourCoef = cfg.OfflineRewardGoldPerHourCoef,
        OfflineRewardTierCount = cfg.OfflineRewardMultiplierTiers?.Count ?? 0,
    };
}
