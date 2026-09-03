namespace MFG.Domain.Entities;

public class GachaHistory : BaseEntity
{
    public long PlayerId { get; set; }
    public string PoolType { get; set; } = string.Empty;   // Equipment, Weapon, Relic
    public string ResultItemId { get; set; } = string.Empty;
    public string ResultGrade { get; set; } = string.Empty; // Normal~Mythic
    public int PityCount { get; set; }

    // Navigation
    public Player Player { get; set; } = null!;
}
