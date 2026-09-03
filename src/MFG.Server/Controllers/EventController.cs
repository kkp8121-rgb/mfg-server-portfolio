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
[Route("api/v1/event")]
[Authorize]
public class EventController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ValidationService _validation;
    private readonly OfflineRewardService _offlineReward;

    // 이벤트 던전 보상 테이블 (난이도별)
    private static readonly Dictionary<int, (long Gold, int Ruby)> EventDungeonRewards = new()
    {
        [1] = (50_000, 20),
        [2] = (100_000, 50),
        [3] = (200_000, 100)
    };

    // 배틀패스 보상 (레벨별)
    private static readonly (long Gold, int Ruby)[] BattlePassRewards =
    [
        (10_000, 10),  // Tier 1
        (20_000, 20),
        (30_000, 30),
        (50_000, 50),
        (100_000, 100) // Tier 5
    ];

    public EventController(AppDbContext db, ValidationService validation, OfflineRewardService offlineReward)
    {
        _db = db;
        _validation = validation;
        _offlineReward = offlineReward;
    }

    /// <summary>이벤트 보상 수령</summary>
    [HttpPost("claim")]
    public async Task<IActionResult> Claim([FromBody] EventClaimRequest request, CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");
        if (string.IsNullOrEmpty(firebaseUid))
            return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var player = await _db.Players.FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);
        if (player is null)
            return NotFound(ApiResponse<object>.Fail("플레이어를 찾을 수 없습니다."));

        var rewards = new List<RewardEntry>();

        switch (request.EventType)
        {
            case "EventDungeon":
                // 일일 횟수 제한 검증
                var dailyRuns = await _validation.GetDailyEventDungeonRunsAsync(player.Id, ct);
                // 라이브 BalanceConfig 우선 (S261-04 핫리로드), BalanceTables 폴백.
                var maxRuns = _validation.GetDailyEventDungeonMaxRuns();
                if (dailyRuns >= maxRuns)
                    return BadRequest(ApiResponse<object>.Fail(
                        $"일일 이벤트 던전 최대 횟수 초과 ({maxRuns}회)"));

                if (!EventDungeonRewards.TryGetValue(request.Tier, out var dungeonReward))
                    return BadRequest(ApiResponse<object>.Fail($"잘못된 난이도: {request.Tier}"));
                rewards.Add(new RewardEntry { Type = "Gold", Amount = dungeonReward.Gold });
                rewards.Add(new RewardEntry { Type = "Ruby", Amount = dungeonReward.Ruby });
                break;

            case "BattlePass":
                int bpIndex = Math.Clamp(request.Tier - 1, 0, BattlePassRewards.Length - 1);
                var bpReward = BattlePassRewards[bpIndex];
                rewards.Add(new RewardEntry { Type = "Gold", Amount = bpReward.Gold });
                rewards.Add(new RewardEntry { Type = "Ruby", Amount = bpReward.Ruby });
                break;

            case "OfflineReward":
                // Preview와 동일한 계산 공유 (Phase 26 S261-03)
                var calc = _offlineReward.Calculate(player);
                rewards.AddRange(calc.Rewards);
                break;

            default:
                return BadRequest(ApiResponse<object>.Fail($"알 수 없는 이벤트 타입: {request.EventType}"));
        }

        // 보상 지급
        foreach (var reward in rewards)
        {
            var currency = await _db.Currencies
                .FirstOrDefaultAsync(c => c.PlayerId == player.Id && c.Type == reward.Type, ct);

            if (currency is null)
            {
                currency = new Currency { PlayerId = player.Id, Type = reward.Type, Amount = 0 };
                _db.Currencies.Add(currency);
            }

            currency.Amount += reward.Amount;

            _db.CurrencyTransactions.Add(new CurrencyTransaction
            {
                PlayerId = player.Id,
                CurrencyType = reward.Type,
                Amount = reward.Amount,
                BalanceAfter = currency.Amount,
                Reason = $"Event_{request.EventType}",
                ReferenceId = request.EventId
            });
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<EventClaimResponse>.Ok(new EventClaimResponse
        {
            EventType = request.EventType,
            Rewards = rewards,
            ServerTime = DateTime.UtcNow
        }));
    }

    /// <summary>
    /// 오프라인 보상 미리보기 — 읽기 전용. DB 상태 변경 없음.
    /// LastLogoutAt(또는 LastLoginAt)부터 현재까지 경과 시간 기반 예상 보상 반환.
    /// 지급은 POST /api/v1/event/claim {eventType:"OfflineReward"} 로 분리.
    /// 관련: server/Docs/Planning/offline-reward-model.md (Phase 26 S261-03)
    /// </summary>
    [HttpGet("offline-reward/preview")]
    public async Task<ActionResult<ApiResponse<OfflineRewardPreviewResponse>>> PreviewOfflineReward(CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");
        if (string.IsNullOrEmpty(firebaseUid))
            return Unauthorized(ApiResponse<OfflineRewardPreviewResponse>.Fail("인증 실패"));

        // AsNoTracking: 읽기 전용 보장 (실수로도 DB 변경되지 않도록)
        var player = await _db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);
        if (player is null)
            return NotFound(ApiResponse<OfflineRewardPreviewResponse>.Fail("플레이어를 찾을 수 없습니다."));

        var calc = _offlineReward.Calculate(player);

        return Ok(ApiResponse<OfflineRewardPreviewResponse>.Ok(new OfflineRewardPreviewResponse
        {
            PlayerId = calc.PlayerId,
            CoinReward = calc.CoinReward,
            GemReward = calc.GemReward,
            DurationSeconds = calc.CappedSeconds,
            LastLogoutAt = calc.LastLogoutAt,
            ServerTime = calc.ServerTime,
            Capped = calc.Capped
        }));
    }
}
