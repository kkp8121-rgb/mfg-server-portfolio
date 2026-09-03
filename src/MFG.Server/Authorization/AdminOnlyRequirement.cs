using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MFG.Server.Configuration;

namespace MFG.Server.Authorization;

/// <summary>
/// "AdminOnly" 정책 요구사항. Phase 26 Sprint 26-1 S261-04 보안 가드.
/// user_id 클레임이 AdminConfig.AllowedUserIds 화이트리스트에 포함되는지 검증.
/// </summary>
public sealed class AdminOnlyRequirement : IAuthorizationRequirement { }

public sealed class AdminOnlyHandler : AuthorizationHandler<AdminOnlyRequirement>
{
    private readonly IOptionsMonitor<AdminConfig> _admin;
    private readonly ILogger<AdminOnlyHandler> _logger;

    public AdminOnlyHandler(IOptionsMonitor<AdminConfig> admin, ILogger<AdminOnlyHandler> logger)
    {
        _admin = admin;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminOnlyRequirement requirement)
    {
        var userId = context.User.FindFirst("user_id")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            // 미인증 상태 — user_id 클레임이 없으면 거부.
            return Task.CompletedTask;
        }

        var allowed = _admin.CurrentValue.AllowedUserIds;
        if (allowed is { Count: > 0 } && allowed.Contains(userId))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 감사 흐름: 접근 거부된 시도를 경고 로그로 기록. 공격 징후 추적용.
        _logger.LogWarning("[Admin] 접근 거부 user_id={UserId} (allowlist count={AllowCount})",
            userId, allowed?.Count ?? 0);
        return Task.CompletedTask;
    }
}
