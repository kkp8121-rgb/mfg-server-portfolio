using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Domain.Entities;
using MFG.Server.DTOs;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/guild")]
[Authorize]
public class GuildController : ControllerBase
{
    private readonly AppDbContext _db;

    private const int CREATE_COST_RUBY = 500;
    private const int WEEKLY_BOSS_ATTEMPTS = 3;
    private const long BOSS_HP_PER_LEVEL = 1_000_000;
    private const int BOSS_REWARD_RUBY = 200;
    private const long BOSS_REWARD_GOLD = 500_000;

    private static readonly (string Type, string Currency, long Cost, int DailyLimit, int GuildExp)[] Donations =
    [
        ("gold", "Gold", 100_000, 3, 100),
        ("ruby", "Ruby", 50, 1, 300),
        ("ticket", "QuickHuntTicket", 5, 2, 150)
    ];

    private static readonly int[] LevelExpThresholds = [0, 5000, 15000, 30000, 60000, 100000, 150000, 220000, 300000, 400000];

    public GuildController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>길드 생성 (루비 500)</summary>
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] GuildCreateRequest request, CancellationToken ct)
    {
        var player = await GetPlayerAsync(ct);
        if (player is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        // 이미 길드에 가입되어 있는지
        var existingMember = await _db.GuildMembers.FirstOrDefaultAsync(m => m.PlayerId == player.Id, ct);
        if (existingMember is not null)
            return BadRequest(ApiResponse<object>.Fail("이미 길드에 가입되어 있습니다."));

        // 루비 차감
        var ruby = await _db.Currencies.FirstOrDefaultAsync(c => c.PlayerId == player.Id && c.Type == "Ruby", ct);
        if (ruby is null || ruby.Amount < CREATE_COST_RUBY)
            return BadRequest(ApiResponse<object>.Fail($"루비 부족: 필요 {CREATE_COST_RUBY}"));

        // 길드명 중복 체크
        var nameExists = await _db.Guilds.AnyAsync(g => g.GuildName == request.GuildName, ct);
        if (nameExists)
            return BadRequest(ApiResponse<object>.Fail("이미 존재하는 길드 이름입니다."));

        ruby.Amount -= CREATE_COST_RUBY;

        var guild = new Guild
        {
            GuildName = request.GuildName,
            LeaderId = player.Id,
            BossHpRemaining = BOSS_HP_PER_LEVEL,
            LastBossResetDate = GetMondayDate()
        };
        _db.Guilds.Add(guild);
        await _db.SaveChangesAsync(ct); // Guild ID 생성

        _db.GuildMembers.Add(new GuildMember
        {
            GuildId = guild.Id,
            PlayerId = player.Id,
            LastDonateResetDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        });

        _db.CurrencyTransactions.Add(new CurrencyTransaction
        {
            PlayerId = player.Id,
            CurrencyType = "Ruby",
            Amount = -CREATE_COST_RUBY,
            BalanceAfter = ruby.Amount,
            Reason = "GuildCreate"
        });

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<GuildInfoResponse>.Ok(BuildGuildInfo(guild, 1)));
    }

    /// <summary>길드 가입</summary>
    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] GuildJoinRequest request, CancellationToken ct)
    {
        var player = await GetPlayerAsync(ct);
        if (player is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var existingMember = await _db.GuildMembers.FirstOrDefaultAsync(m => m.PlayerId == player.Id, ct);
        if (existingMember is not null)
            return BadRequest(ApiResponse<object>.Fail("이미 길드에 가입되어 있습니다."));

        var guild = await _db.Guilds.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == request.GuildId, ct);
        if (guild is null)
            return NotFound(ApiResponse<object>.Fail("길드를 찾을 수 없습니다."));

        if (guild.Members.Count >= 30)
            return BadRequest(ApiResponse<object>.Fail("길드 정원(30명) 초과"));

        _db.GuildMembers.Add(new GuildMember
        {
            GuildId = guild.Id,
            PlayerId = player.Id,
            LastDonateResetDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        });

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<GuildInfoResponse>.Ok(BuildGuildInfo(guild, guild.Members.Count + 1)));
    }

    /// <summary>기부</summary>
    [HttpPost("donate")]
    public async Task<IActionResult> Donate([FromBody] GuildDonateRequest request, CancellationToken ct)
    {
        var player = await GetPlayerAsync(ct);
        if (player is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var member = await _db.GuildMembers.Include(m => m.Guild).FirstOrDefaultAsync(m => m.PlayerId == player.Id, ct);
        if (member is null)
            return BadRequest(ApiResponse<object>.Fail("길드에 가입되어 있지 않습니다."));

        var donation = Donations.FirstOrDefault(d => d.Type == request.DonationType);
        if (donation.Type is null)
            return BadRequest(ApiResponse<object>.Fail($"존재하지 않는 기부 타입: {request.DonationType}"));

        // 일일 리셋
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        if (member.LastDonateResetDate != today)
        {
            member.GoldDonateCount = 0;
            member.RubyDonateCount = 0;
            member.TicketDonateCount = 0;
            member.LastDonateResetDate = today;
        }

        int used = donation.Type switch
        {
            "gold" => member.GoldDonateCount,
            "ruby" => member.RubyDonateCount,
            "ticket" => member.TicketDonateCount,
            _ => 0
        };

        if (used >= donation.DailyLimit)
            return BadRequest(ApiResponse<object>.Fail("일일 기부 한도 초과"));

        // 재화 차감
        var currency = await _db.Currencies.FirstOrDefaultAsync(c => c.PlayerId == player.Id && c.Type == donation.Currency, ct);
        if (currency is null || currency.Amount < donation.Cost)
            return BadRequest(ApiResponse<object>.Fail($"{donation.Currency} 부족"));

        currency.Amount -= donation.Cost;

        switch (donation.Type)
        {
            case "gold": member.GoldDonateCount++; break;
            case "ruby": member.RubyDonateCount++; break;
            case "ticket": member.TicketDonateCount++; break;
        }

        member.Guild.Exp += donation.GuildExp;
        UpdateGuildLevel(member.Guild);

        await _db.SaveChangesAsync(ct);

        int remaining = donation.DailyLimit - (used + 1);

        return Ok(ApiResponse<GuildDonateResponse>.Ok(new GuildDonateResponse
        {
            DonationType = donation.Type,
            GuildExpGained = donation.GuildExp,
            RemainingDonations = remaining,
            CurrencyRemaining = currency.Amount
        }));
    }

    /// <summary>보스 전투 결과</summary>
    [HttpPost("boss-result")]
    public async Task<IActionResult> BossResult([FromBody] GuildBossResultRequest request, CancellationToken ct)
    {
        var player = await GetPlayerAsync(ct);
        if (player is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var member = await _db.GuildMembers.Include(m => m.Guild).FirstOrDefaultAsync(m => m.PlayerId == player.Id, ct);
        if (member is null)
            return BadRequest(ApiResponse<object>.Fail("길드에 가입되어 있지 않습니다."));

        // 주간 리셋
        var monday = GetMondayDate();
        if (member.Guild.LastBossResetDate != monday)
        {
            member.Guild.LastBossResetDate = monday;
            member.Guild.IsBossDefeated = false;
            member.Guild.BossHpRemaining = member.Guild.Level * BOSS_HP_PER_LEVEL;
            member.BossAttemptsThisWeek = 0;
            member.BossTotalDamage = 0;
        }

        if (member.BossAttemptsThisWeek >= WEEKLY_BOSS_ATTEMPTS)
            return BadRequest(ApiResponse<object>.Fail("주간 보스 도전 횟수 초과"));

        if (member.Guild.IsBossDefeated)
            return BadRequest(ApiResponse<object>.Fail("이번 주 보스가 이미 처치되었습니다."));

        member.BossAttemptsThisWeek++;

        // 데미지 = CP x 랜덤(5~15)
        long damage = (long)(request.PlayerCp * (5f + Random.Shared.NextDouble() * 10f));
        member.BossTotalDamage += damage;
        member.Guild.BossHpRemaining -= damage;

        bool isDefeated = member.Guild.BossHpRemaining <= 0;
        if (isDefeated) member.Guild.IsBossDefeated = true;

        long totalBossHp = member.Guild.Level * BOSS_HP_PER_LEVEL;
        float contribution = Math.Min(0.4f, (float)member.BossTotalDamage / totalBossHp);

        // 순위 시뮬 (NPC 9명 랜덤 기여도)
        int rank = 1;
        for (int i = 0; i < 9; i++)
        {
            if (Random.Shared.NextDouble() * 0.4 > contribution) rank++;
        }

        float rewardMultiplier = rank switch
        {
            1 => 1.5f,
            2 or 3 => 1.2f,
            <= 10 => 1.0f,
            _ => 0.8f
        };

        int rewardRuby = (int)(BOSS_REWARD_RUBY * rewardMultiplier);
        long rewardGold = (long)(BOSS_REWARD_GOLD * rewardMultiplier);

        // 보상 지급
        await GrantCurrencyAsync(player.Id, "Ruby", rewardRuby, "GuildBoss", ct);
        await GrantCurrencyAsync(player.Id, "Gold", rewardGold, "GuildBoss", ct);

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<GuildBossResultResponse>.Ok(new GuildBossResultResponse
        {
            DamageDealt = damage,
            IsBossDefeated = isDefeated,
            ContributionPercent = contribution,
            ContributionRank = rank,
            RewardMultiplier = rewardMultiplier,
            RewardRuby = rewardRuby,
            RewardGold = rewardGold,
            RemainingAttempts = WEEKLY_BOSS_ATTEMPTS - member.BossAttemptsThisWeek
        }));
    }

    /// <summary>길드 정보 조회</summary>
    [HttpGet("info")]
    public async Task<IActionResult> GetInfo(CancellationToken ct)
    {
        var player = await GetPlayerAsync(ct);
        if (player is null) return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var member = await _db.GuildMembers.Include(m => m.Guild).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(m => m.PlayerId == player.Id, ct);

        if (member is null)
            return Ok(ApiResponse<GuildInfoResponse>.Ok(new GuildInfoResponse()));

        return Ok(ApiResponse<GuildInfoResponse>.Ok(BuildGuildInfo(member.Guild, member.Guild.Members.Count)));
    }

    // -- 헬퍼 --

    private async Task<Player?> GetPlayerAsync(CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");
        if (string.IsNullOrEmpty(firebaseUid)) return null;
        return await _db.Players.FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);
    }

    private async Task GrantCurrencyAsync(long playerId, string type, long amount, string reason, CancellationToken ct)
    {
        var currency = await _db.Currencies.FirstOrDefaultAsync(c => c.PlayerId == playerId && c.Type == type, ct);
        if (currency is null)
        {
            currency = new Currency { PlayerId = playerId, Type = type, Amount = 0 };
            _db.Currencies.Add(currency);
        }
        currency.Amount += amount;

        _db.CurrencyTransactions.Add(new CurrencyTransaction
        {
            PlayerId = playerId,
            CurrencyType = type,
            Amount = amount,
            BalanceAfter = currency.Amount,
            Reason = reason
        });
    }

    private static void UpdateGuildLevel(Guild guild)
    {
        for (int i = LevelExpThresholds.Length - 1; i >= 0; i--)
        {
            if (guild.Exp >= LevelExpThresholds[i])
            {
                guild.Level = i + 1;
                return;
            }
        }
    }

    private static GuildInfoResponse BuildGuildInfo(Guild guild, int memberCount)
    {
        int nextLevelExp = guild.Level < LevelExpThresholds.Length
            ? LevelExpThresholds[guild.Level]
            : LevelExpThresholds[^1];

        return new GuildInfoResponse
        {
            GuildId = guild.Id,
            GuildName = guild.GuildName,
            Level = guild.Level,
            Exp = guild.Exp,
            NextLevelExp = nextLevelExp,
            MemberCount = memberCount,
            IsBossDefeated = guild.IsBossDefeated
        };
    }

    private static string GetMondayDate()
    {
        var today = DateTime.UtcNow;
        int daysToMonday = ((int)today.DayOfWeek - 1 + 7) % 7;
        return today.AddDays(-daysToMonday).ToString("yyyy-MM-dd");
    }
}
