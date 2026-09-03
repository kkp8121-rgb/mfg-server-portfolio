using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Server.DTOs;
using MFG.Server.Services;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/currency")]
[Authorize]
public class CurrencyController : ControllerBase
{
    private readonly CurrencyService _currencyService;
    private readonly AppDbContext _db;

    public CurrencyController(CurrencyService currencyService, AppDbContext db)
    {
        _currencyService = currencyService;
        _db = db;
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
    {
        var playerId = await GetPlayerIdAsync(ct);
        if (playerId is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var result = await _currencyService.GetBalanceAsync(playerId.Value, ct);
        return Ok(ApiResponse<CurrencyBalanceResponse>.Ok(result));
    }

    [HttpPost("spend")]
    public async Task<IActionResult> Spend([FromBody] CurrencySpendRequest request, CancellationToken ct)
    {
        var playerId = await GetPlayerIdAsync(ct);
        if (playerId is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        try
        {
            var result = await _currencyService.SpendAsync(
                playerId.Value, request.CurrencyType, request.Amount, request.Reason, request.ReferenceId, ct);
            return Ok(ApiResponse<CurrencyTransactionResponse>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("earn")]
    public async Task<IActionResult> Earn([FromBody] CurrencyEarnRequest request, CancellationToken ct)
    {
        var playerId = await GetPlayerIdAsync(ct);
        if (playerId is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        try
        {
            var result = await _currencyService.EarnAsync(
                playerId.Value, request.CurrencyType, request.Amount, request.Reason, request.ReferenceId, ct);
            return Ok(ApiResponse<CurrencyTransactionResponse>.Ok(result));
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
