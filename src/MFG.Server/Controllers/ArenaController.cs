using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Domain.Entities;
using MFG.Server.DTOs;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/arena")]
[Authorize]
public class ArenaController : ControllerBase
{
    private readonly AppDbContext _db;

    private const int DAILY_FREE_ENTRIES = 5;
    private const float MATCH_CP_RANGE = 0.2f;
    private const int WIN_RATING = 30;
    private const int LOSE_RATING = 15;
    private const int MAX_RECORDS = 50;

    // 티어 경계: Bronze 0~999, Silver 1000~1999, Gold 2000~2999, Diamond 3000+
    private static readonly int[] TierThresholds = [0, 1000, 2000, 3000];

    private static readonly string[] Jobs = ["warrior", "archer", "mage"];
    private static readonly string[] NamePrefixes = ["용감한", "강철", "그림자", "불꽃", "얼음"];
    private static readonly string[] NameSuffixes = ["전사", "검객", "궁수", "마법사", "기사"];

    public ArenaController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>아레나 리더보드 (상위 50명)</summary>
    [AllowAnonymous]
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(CancellationToken ct)
    {
        var rawEntries = await _db.ArenaPlayers
            .OrderByDescending(a => a.Rating)
            .Take(50)
            .Join(_db.Players,
                arena => arena.PlayerId,
                player => player.Id,
                (arena, player) => new ArenaLeaderboardEntry
                {
                    Nickname = player.Nickname,
                    Rating = arena.Rating,
                    Tier = arena.CurrentTier,
                    TotalVictories = arena.TotalVictories,
                    BestWinStreak = arena.BestWinStreak
                })
            .ToListAsync(ct);

        // Rank는 메모리에서 부여 (SQL 변환 불가)
        for (int i = 0; i < rawEntries.Count; i++)
            rawEntries[i].Rank = i + 1;

        return Ok(ApiResponse<ArenaLeaderboardResponse>.Ok(new ArenaLeaderboardResponse
        {
            Entries = rawEntries,
            ServerTime = DateTime.UtcNow
        }));
    }

    /// <summary>매칭 후보 3명 생성</summary>
    [HttpPost("match-candidates")]
    public async Task<IActionResult> GetCandidates([FromBody] ArenaBattleRequest request, CancellationToken ct)
    {
        var (player, arena) = await GetOrCreateArenaPlayerAsync(ct);
        if (player is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        CheckDailyReset(arena);

        var candidates = new List<ArenaCandidate>();
        var playerCp = request.PlayerCp > 0 ? request.PlayerCp : player.CombatPower;

        for (int i = 0; i < 3; i++)
        {
            float variance = (float)(Random.Shared.NextDouble() * 2 - 1) * MATCH_CP_RANGE;
            long opponentCp = Math.Max(100, (long)(playerCp * (1f + variance)));

            candidates.Add(new ArenaCandidate
            {
                Name = $"{NamePrefixes[Random.Shared.Next(NamePrefixes.Length)]}{NameSuffixes[Random.Shared.Next(NameSuffixes.Length)]}",
                Job = Jobs[Random.Shared.Next(Jobs.Length)],
                Cp = opponentCp,
                Tier = GetTierForRating(arena.Rating)
            });
        }

        return Ok(ApiResponse<ArenaCandidatesResponse>.Ok(new ArenaCandidatesResponse
        {
            Candidates = candidates,
            RemainingEntries = DAILY_FREE_ENTRIES - arena.UsedFreeEntries
        }));
    }

    /// <summary>전투 결과 처리</summary>
    [HttpPost("battle-result")]
    public async Task<IActionResult> ProcessBattle([FromBody] ArenaBattleRequest request, CancellationToken ct)
    {
        var (player, arena) = await GetOrCreateArenaPlayerAsync(ct);
        if (player is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        CheckDailyReset(arena);

        if (arena.UsedFreeEntries >= DAILY_FREE_ENTRIES)
            return BadRequest(ApiResponse<object>.Fail("일일 무료 도전 횟수를 모두 사용했습니다."));

        arena.UsedFreeEntries++;

        // CP 기반 승률 계산
        var playerCp = request.PlayerCp > 0 ? request.PlayerCp : player.CombatPower;
        float cpRatio = (float)playerCp / (playerCp + Math.Max(1, playerCp)); // fallback
        float roll = cpRatio + (float)(Random.Shared.NextDouble() * 0.3 - 0.15);
        bool isVictory = roll >= 0.5f;

        int ratingChange;
        if (isVictory)
        {
            arena.TotalVictories++;
            arena.CurrentWinStreak++;
            if (arena.CurrentWinStreak > arena.BestWinStreak)
                arena.BestWinStreak = arena.CurrentWinStreak;

            float streakBonus = Math.Min(arena.CurrentWinStreak * 0.1f, 0.5f);
            ratingChange = (int)(WIN_RATING * (1f + streakBonus));
        }
        else
        {
            arena.TotalDefeats++;
            arena.CurrentWinStreak = 0;
            ratingChange = -LOSE_RATING;
        }

        arena.Rating = Math.Max(0, arena.Rating + ratingChange);
        arena.CurrentTier = GetTierForRating(arena.Rating);

        // 전적 기록
        _db.ArenaRecords.Add(new ArenaRecord
        {
            PlayerId = player.Id,
            OpponentName = "상대",
            OpponentJob = Jobs[Random.Shared.Next(Jobs.Length)],
            OpponentCp = playerCp,
            IsVictory = isVictory,
            RatingChange = ratingChange
        });

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<ArenaBattleResponse>.Ok(new ArenaBattleResponse
        {
            IsVictory = isVictory,
            RatingChange = ratingChange,
            NewRating = arena.Rating,
            NewTier = arena.CurrentTier,
            CurrentWinStreak = arena.CurrentWinStreak
        }));
    }

    /// <summary>아레나 상태 조회</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var (player, arena) = await GetOrCreateArenaPlayerAsync(ct);
        if (player is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        CheckDailyReset(arena);

        var records = await _db.ArenaRecords
            .Where(r => r.PlayerId == player.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .Select(r => new ArenaRecordDto
            {
                OpponentName = r.OpponentName,
                OpponentJob = r.OpponentJob,
                OpponentCp = r.OpponentCp,
                IsVictory = r.IsVictory,
                RatingChange = r.RatingChange,
                Timestamp = r.CreatedAt.ToString("o")
            })
            .ToListAsync(ct);

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<ArenaStatusResponse>.Ok(new ArenaStatusResponse
        {
            CurrentTier = arena.CurrentTier,
            Rating = arena.Rating,
            TotalVictories = arena.TotalVictories,
            TotalDefeats = arena.TotalDefeats,
            CurrentWinStreak = arena.CurrentWinStreak,
            BestWinStreak = arena.BestWinStreak,
            RemainingEntries = DAILY_FREE_ENTRIES - arena.UsedFreeEntries,
            SeasonId = arena.SeasonId,
            RecentRecords = records
        }));
    }

    private void CheckDailyReset(ArenaPlayer arena)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (arena.LastResetDate < today)
        {
            arena.UsedFreeEntries = 0;
            arena.LastResetDate = today;
        }
    }

    private static int GetTierForRating(int rating)
    {
        for (int i = TierThresholds.Length - 1; i >= 0; i--)
        {
            if (rating >= TierThresholds[i]) return i;
        }
        return 0;
    }

    private async Task<(Player? player, ArenaPlayer arena)> GetOrCreateArenaPlayerAsync(CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");
        if (string.IsNullOrEmpty(firebaseUid)) return (null, null!);

        var player = await _db.Players.FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);
        if (player is null) return (null, null!);

        var arena = await _db.ArenaPlayers.FirstOrDefaultAsync(a => a.PlayerId == player.Id, ct);
        if (arena is null)
        {
            var seasonId = $"S{DateTime.UtcNow:yyyyMMdd}";
            arena = new ArenaPlayer
            {
                PlayerId = player.Id,
                LastResetDate = DateOnly.FromDateTime(DateTime.UtcNow),
                SeasonId = seasonId,
                SeasonStartTime = DateTime.UtcNow
            };
            _db.ArenaPlayers.Add(arena);
        }

        return (player, arena);
    }
}
