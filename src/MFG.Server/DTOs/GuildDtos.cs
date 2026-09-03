namespace MFG.Server.DTOs;

// ── 길드 생성/가입 ──

public class GuildCreateRequest
{
    public string GuildName { get; set; } = string.Empty;
}

public class GuildJoinRequest
{
    public long GuildId { get; set; }
}

public class GuildInfoResponse
{
    public long GuildId { get; set; }
    public string GuildName { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Exp { get; set; }
    public int NextLevelExp { get; set; }
    public int MemberCount { get; set; }
    public bool IsBossDefeated { get; set; }
}

// ── 기부 ──

public class GuildDonateRequest
{
    public string DonationType { get; set; } = string.Empty; // gold, ruby, ticket
}

public class GuildDonateResponse
{
    public string DonationType { get; set; } = string.Empty;
    public int GuildExpGained { get; set; }
    public int RemainingDonations { get; set; }
    public long CurrencyRemaining { get; set; }
}

// ── 보스 ──

public class GuildBossResultRequest
{
    public long PlayerCp { get; set; }
}

public class GuildBossResultResponse
{
    public long DamageDealt { get; set; }
    public bool IsBossDefeated { get; set; }
    public float ContributionPercent { get; set; }
    public int ContributionRank { get; set; }
    public float RewardMultiplier { get; set; }
    public int RewardRuby { get; set; }
    public long RewardGold { get; set; }
    public int RemainingAttempts { get; set; }
}
