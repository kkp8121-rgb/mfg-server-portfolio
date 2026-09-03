using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Server.DTOs;
using MFG.Server.Services;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/hotdeal")]
[Authorize]
public class HotDealController : ControllerBase
{
    private readonly HotDealService _hotDealService;
    private readonly AppDbContext _db;

    public HotDealController(HotDealService hotDealService, AppDbContext db)
    {
        _hotDealService = hotDealService;
        _db = db;
    }

    /// <summary>
    /// 핫딜 카탈로그 조회 (전체 목록 + 구매 여부)
    /// </summary>
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken ct)
    {
        var playerId = await GetPlayerIdAsync(ct);
        if (playerId is null)
            return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var result = await _hotDealService.GetCatalogAsync(playerId.Value, ct);
        return Ok(ApiResponse<HotDealCatalogResponse>.Ok(result));
    }

    /// <summary>
    /// 핫딜 구매
    /// </summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] HotDealPurchaseRequest request, CancellationToken ct)
    {
        var playerId = await GetPlayerIdAsync(ct);
        if (playerId is null)
            return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        try
        {
            var result = await _hotDealService.PurchaseAsync(playerId.Value, request.DealId, ct);
            return Ok(ApiResponse<HotDealPurchaseResponse>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<long?> GetPlayerIdAsync(CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");

        if (string.IsNullOrEmpty(firebaseUid)) return null;

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);

        return player?.Id;
    }
}
