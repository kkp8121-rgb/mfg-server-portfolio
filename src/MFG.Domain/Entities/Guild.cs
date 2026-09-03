namespace MFG.Domain.Entities;

public class Guild : BaseEntity
{
    public string GuildName { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int Exp { get; set; }
    public long LeaderId { get; set; }
    public long BossHpRemaining { get; set; }
    public bool IsBossDefeated { get; set; }
    public string LastBossResetDate { get; set; } = string.Empty;  // yyyy-MM-dd

    // Navigation
    public ICollection<GuildMember> Members { get; set; } = [];
}
