using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Domain.Entities;
using MFG.Server.DTOs;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/equipment")]
[Authorize]
public class EquipmentController : ControllerBase
{
    private readonly AppDbContext _db;

    // 스타포스 확률 테이블 (0~24성)
    private static readonly float[] StarForceRates =
    [
        0.95f, 0.90f, 0.85f, 0.85f, 0.80f,  // 0~4
        0.75f, 0.70f, 0.65f, 0.60f, 0.55f,  // 5~9
        0.50f, 0.45f, 0.40f, 0.35f, 0.30f,  // 10~14
        0.30f, 0.30f, 0.30f, 0.30f, 0.30f,  // 15~19 (하락 구간)
        0.30f, 0.30f, 0.03f, 0.02f, 0.01f   // 20~24 (파괴 구간)
    ];

    // 하락 시작 단계 (15성 이상)
    private const int DOWNGRADE_START = 15;
    // 파괴 시작 단계 (20성 이상)
    private const int DESTROY_START = 20;

    // 골드 비용: (현재 단계 + 1) x 10,000
    private static long GetGoldCost(int level) => (level + 1) * 10_000L;

    public EquipmentController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>장비 강화 (서버 확률 판정)</summary>
    [HttpPost("enhance")]
    public async Task<IActionResult> Enhance([FromBody] EquipmentEnhanceRequest request, CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");
        if (string.IsNullOrEmpty(firebaseUid))
            return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var player = await _db.Players.FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);
        if (player is null)
            return NotFound(ApiResponse<object>.Fail("플레이어를 찾을 수 없습니다."));

        int level = Math.Clamp(request.CurrentLevel, 0, StarForceRates.Length - 1);
        float successRate = StarForceRates[level];
        long goldCost = GetGoldCost(level);

        // 골드 차감
        var gold = await _db.Currencies
            .FirstOrDefaultAsync(c => c.PlayerId == player.Id && c.Type == "Gold", ct);

        if (gold is null || gold.Amount < goldCost)
            return BadRequest(ApiResponse<object>.Fail($"골드 부족: 보유 {gold?.Amount ?? 0}, 필요 {goldCost}"));

        gold.Amount -= goldCost;

        _db.CurrencyTransactions.Add(new CurrencyTransaction
        {
            PlayerId = player.Id,
            CurrencyType = "Gold",
            Amount = -goldCost,
            BalanceAfter = gold.Amount,
            Reason = $"Enhance_{request.EnhanceType}",
            ReferenceId = request.EquipmentId
        });

        // 확률 판정
        float roll = (float)Random.Shared.NextDouble();
        bool isSuccess = roll < successRate;

        string result;
        int newLevel;

        if (isSuccess)
        {
            result = "Success";
            newLevel = level + 1;
        }
        else if (level >= DESTROY_START && Random.Shared.NextDouble() < 0.1) // 파괴 10% 확률
        {
            result = "Destroy";
            newLevel = 12; // 12성으로 리셋
        }
        else if (level >= DOWNGRADE_START)
        {
            result = "Downgrade";
            newLevel = Math.Max(0, level - 1);
        }
        else
        {
            result = "Fail";
            newLevel = level; // 유지
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<EquipmentEnhanceResponse>.Ok(new EquipmentEnhanceResponse
        {
            IsSuccess = isSuccess,
            Result = result,
            NewLevel = newLevel,
            SuccessRate = successRate,
            GoldCost = goldCost,
            GoldRemaining = gold.Amount
        }));
    }
}
