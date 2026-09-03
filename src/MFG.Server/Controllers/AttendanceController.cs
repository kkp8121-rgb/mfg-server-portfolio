using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Domain.Entities;
using MFG.Server.DTOs;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _db;

    // 연속 출석 보상 테이블 (7일 사이클)
    private static readonly (string Type, int Amount)[] DailyRewards =
    [
        ("Ruby", 50),   // Day 1
        ("Gold", 5000), // Day 2
        ("Ruby", 50),   // Day 3
        ("Gold", 5000), // Day 4
        ("Ruby", 100),  // Day 5
        ("Gold", 10000),// Day 6
        ("Ruby", 200),  // Day 7
    ];

    public AttendanceController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check(CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");
        if (string.IsNullOrEmpty(firebaseUid))
            return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);
        if (player is null)
            return NotFound(ApiResponse<object>.Fail("플레이어를 찾을 수 없습니다."));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 오늘 이미 출석했는지 확인
        var todayRecord = await _db.AttendanceRecords
            .FirstOrDefaultAsync(a => a.PlayerId == player.Id && a.CheckDate == today, ct);
        if (todayRecord is not null)
            return BadRequest(ApiResponse<object>.Fail("오늘 이미 출석했습니다."));

        // 어제 출석 기록으로 연속 일수 계산
        var yesterday = today.AddDays(-1);
        var yesterdayRecord = await _db.AttendanceRecords
            .FirstOrDefaultAsync(a => a.PlayerId == player.Id && a.CheckDate == yesterday, ct);

        var consecutiveDays = yesterdayRecord is not null
            ? yesterdayRecord.ConsecutiveDays + 1
            : 1;

        // 보상 결정 (7일 사이클)
        var rewardIndex = (consecutiveDays - 1) % DailyRewards.Length;
        var (rewardType, rewardAmount) = DailyRewards[rewardIndex];

        // 출석 기록
        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            PlayerId = player.Id,
            CheckDate = today,
            ConsecutiveDays = consecutiveDays,
            RewardType = rewardType,
            RewardAmount = rewardAmount
        });

        // 보상 지급
        var currency = await _db.Currencies
            .FirstOrDefaultAsync(c => c.PlayerId == player.Id && c.Type == rewardType, ct);

        if (currency is null)
        {
            currency = new Currency { PlayerId = player.Id, Type = rewardType, Amount = 0 };
            _db.Currencies.Add(currency);
        }

        currency.Amount += rewardAmount;

        _db.CurrencyTransactions.Add(new CurrencyTransaction
        {
            PlayerId = player.Id,
            CurrencyType = rewardType,
            Amount = rewardAmount,
            BalanceAfter = currency.Amount,
            Reason = "Attendance",
            ReferenceId = $"day_{consecutiveDays}"
        });

        await _db.SaveChangesAsync(ct);

        var nextDay = today.AddDays(1);

        return Ok(ApiResponse<AttendanceCheckResponse>.Ok(new AttendanceCheckResponse
        {
            ConsecutiveDays = consecutiveDays,
            Reward = new AttendanceReward { Type = rewardType, Amount = rewardAmount },
            ServerDate = today.ToString("yyyy-MM-dd"),
            NextCheckTime = nextDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        }));
    }
}
