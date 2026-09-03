using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MFG.Server.Configuration;
using MFG.Server.DTOs;

namespace MFG.Server.Controllers;

/// <summary>
/// 시스템 공지 제공. Phase 26 Sprint 26-1 S261-05.
/// Active=true + 시간 윈도우 내일 때만 공지 활성.
/// 공지 비활성 시에도 200 OK로 `{active:false}` 반환 (404 X) — 클라 분기 단순화.
/// </summary>
[ApiController]
[Route("api/v1/system")]
public class SystemController : ControllerBase
{
    private readonly IOptionsSnapshot<SystemNoticeConfig> _notice;

    public SystemController(IOptionsSnapshot<SystemNoticeConfig> notice)
    {
        _notice = notice;
    }

    [HttpGet("notice")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<NoticeResponse>> GetNotice()
    {
        var cfg = _notice.Value;
        var now = DateTime.UtcNow;

        // ConfigurationBinder 가 ISO 문자열을 Local/Unspecified Kind로 파싱할 수 있어
        // 비교 전에 UTC로 정규화. appsettings 에 타임존이 없는 경우 UTC로 간주.
        DateTime? starts = NormalizeToUtc(cfg.StartsAt);
        DateTime? ends   = NormalizeToUtc(cfg.EndsAt);

        // 시간 윈도우 검사: StartsAt / EndsAt 중 하나라도 벗어나면 비활성.
        // 명시적 Active=false면 그대로 비활성.
        bool inWindow = (starts is null || now >= starts.Value)
                     && (ends   is null || now <= ends.Value);
        bool active = cfg.Active && inWindow;

        var response = new NoticeResponse
        {
            Active = active,
            // 비활성 시 내용 노출 안 함 — 디버그 로그에도 남지 않도록
            Title = active ? cfg.Title : "",
            Body = active ? cfg.Body : "",
            StartsAt = cfg.StartsAt,
            EndsAt = cfg.EndsAt,
            ServerTime = now,
            // MinClientVersion/ForceUpdateMessage 는 Active 게이트 독립적으로 항상 반환.
            // 클라는 부팅 시 응답의 MinClientVersion 과 자기 버전을 비교해 강제 업데이트 여부 판단.
            MinClientVersion = cfg.MinClientVersion ?? "",
            ForceUpdateMessage = cfg.ForceUpdateMessage ?? ""
        };

        return Ok(ApiResponse<NoticeResponse>.Ok(response));
    }

    private static DateTime? NormalizeToUtc(DateTime? dt)
    {
        if (dt is null) return null;
        return dt.Value.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc)
        };
    }
}
