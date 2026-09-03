namespace MFG.Domain.Entities;

/// <summary>
/// 플레이어별 아레나 상태 (티어, 레이팅, 시즌)
/// </summary>
public class ArenaPlayer : BaseEntity
{
    public long PlayerId { get; set; }
    public int CurrentTier { get; set; }        // 0=Bronze, 1=Silver, 2=Gold, 3=Diamond
    public int Rating { get; set; }
    public int TotalVictories { get; set; }
    public int TotalDefeats { get; set; }
    public int CurrentWinStreak { get; set; }
    public int BestWinStreak { get; set; }
    public int UsedFreeEntries { get; set; }
    public DateOnly LastResetDate { get; set; }
    public string SeasonId { get; set; } = string.Empty;
    public DateTime SeasonStartTime { get; set; } = DateTime.UtcNow;

    // Navigation
    public Player Player { get; set; } = null!;
    public ICollection<ArenaRecord> Records { get; set; } = [];
}
