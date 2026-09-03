using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Domain.Entities;
using MFG.Server.DTOs;
using MFG.Server.Services;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    private static readonly (string Type, long Amount)[] InitialCurrencies =
    [
        ("Gold", 10000),
        ("Ruby", 10000)
    ];

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Firebase ID Token으로 로그인. 신규 유저면 Player + 초기 재화 생성.
    /// </summary>
    [Authorize]
    [HttpPost("login")]
    public async Task<IActionResult> Login(CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");

        if (string.IsNullOrEmpty(firebaseUid))
            return Unauthorized(ApiResponse<object>.Fail("Firebase UID를 추출할 수 없습니다."));

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);

        var isNewPlayer = player is null;

        if (isNewPlayer)
        {
            player = new Player
            {
                FirebaseUid = firebaseUid,
                Nickname = $"용사{Random.Shared.Next(1000, 9999)}",
                LastLoginAt = DateTime.UtcNow
            };
            _db.Players.Add(player);
            await _db.SaveChangesAsync(ct); // Player.Id auto-increment 생성

            // 초기 재화 지급
            foreach (var (type, amount) in InitialCurrencies)
            {
                _db.Currencies.Add(new Currency
                {
                    PlayerId = player.Id,
                    Type = type,
                    Amount = amount
                });

                _db.CurrencyTransactions.Add(new CurrencyTransaction
                {
                    PlayerId = player.Id,
                    CurrencyType = type,
                    Amount = amount,
                    BalanceAfter = amount,
                    Reason = "InitialGrant"
                });
            }
        }
        else
        {
            player!.LastLoginAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            PlayerId = player.Id,
            Nickname = player.Nickname,
            IsNewPlayer = isNewPlayer,
            ServerTime = DateTime.UtcNow
        }));
    }

    /// <summary>
    /// 로그아웃 (LastLogoutAt 기록). 오프라인 보상 계산에 사용.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");

        if (string.IsNullOrEmpty(firebaseUid))
            return Unauthorized(ApiResponse<object>.Fail("Firebase UID를 추출할 수 없습니다."));

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);

        if (player is null)
            return NotFound(ApiResponse<object>.Fail("플레이어를 찾을 수 없습니다."));

        player.LastLogoutAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<LogoutResponse>.Ok(new LogoutResponse
        {
            PlayerId = player.Id,
            LogoutTime = player.LastLogoutAt.Value,
            ServerTime = DateTime.UtcNow
        }));
    }

    /// <summary>
    /// 플레이어 프로필 업데이트 (레벨, 전투력, 닉네임 동기화).
    /// </summary>
    [Authorize]
    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] PlayerProfileUpdateRequest request, CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");

        if (string.IsNullOrEmpty(firebaseUid))
            return Unauthorized(ApiResponse<object>.Fail("Firebase UID를 추출할 수 없습니다."));

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);

        if (player is null)
            return NotFound(ApiResponse<object>.Fail("플레이어를 찾을 수 없습니다."));

        // Level: 0은 JsonUtility 미전송 아티팩트로 간주하고 스킵 (clientDev 2026-04-15 요청)
        if (request.Level.HasValue && request.Level.Value > 0)
        {
            if (request.Level.Value > BalanceTables.MaxLevel)
                return BadRequest(ApiResponse<object>.Fail(
                    $"유효하지 않은 레벨: {request.Level.Value} (1~{BalanceTables.MaxLevel})"));
            player.Level = request.Level.Value;
        }

        // CombatPower: 0도 JsonUtility 미전송 아티팩트로 간주하고 스킵
        if (request.CombatPower.HasValue && request.CombatPower.Value > 0)
        {
            player.CombatPower = request.CombatPower.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Nickname))
        {
            var trimmed = request.Nickname.Trim();
            if (trimmed.Length < 2 || trimmed.Length > 12)
                return BadRequest(ApiResponse<object>.Fail("닉네임은 2~12자여야 합니다."));
            player.Nickname = trimmed;
        }

        if (!string.IsNullOrWhiteSpace(request.JobId))
        {
            var jobId = request.JobId.Trim();
            if (jobId.Length > 32)
                return BadRequest(ApiResponse<object>.Fail("JobId는 32자 이하여야 합니다."));
            player.JobId = jobId;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<PlayerProfileResponse>.Ok(new PlayerProfileResponse
        {
            PlayerId = player.Id,
            Nickname = player.Nickname,
            Level = player.Level,
            CombatPower = player.CombatPower,
            JobId = player.JobId,
            ServerTime = DateTime.UtcNow
        }));
    }
}
