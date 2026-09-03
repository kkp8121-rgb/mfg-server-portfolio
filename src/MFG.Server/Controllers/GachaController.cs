using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Server.DTOs;
using MFG.Server.Services;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/gacha")]
[Authorize]
[EnableRateLimiting("gacha")]
public class GachaController : ControllerBase
{
    private readonly GachaService _gachaService;
    private readonly AppDbContext _db;

    public GachaController(GachaService gachaService, AppDbContext db)
    {
        _gachaService = gachaService;
        _db = db;
    }

    /// <summary>
    /// 가챠 뽑기 (1회 또는 10연차)
    /// </summary>
    [HttpPost("pull")]
    public async Task<IActionResult> Pull([FromBody] GachaPullRequest request, CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");

        if (string.IsNullOrEmpty(firebaseUid))
            return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);

        if (player is null)
            return NotFound(ApiResponse<object>.Fail("플레이어를 찾을 수 없습니다."));

        try
        {
            var result = await _gachaService.PullAsync(player.Id, request.PoolType, request.PullCount, ct);
            return Ok(ApiResponse<GachaPullResponse>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
