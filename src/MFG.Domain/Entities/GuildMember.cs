namespace MFG.Domain.Entities;

public class GuildMember : BaseEntity
{
    public long GuildId { get; set; }
    public long PlayerId { get; set; }
    public int GoldDonateCount { get; set; }
    public int RubyDonateCount { get; set; }
    public int TicketDonateCount { get; set; }
    public string LastDonateResetDate { get; set; } = string.Empty;
    public int BossAttemptsThisWeek { get; set; }
    public long BossTotalDamage { get; set; }
    public bool RaidUsedThisWeek { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Guild Guild { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
