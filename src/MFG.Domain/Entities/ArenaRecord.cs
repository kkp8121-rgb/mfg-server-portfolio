namespace MFG.Domain.Entities;

public class ArenaRecord : BaseEntity
{
    public long PlayerId { get; set; }
    public string OpponentName { get; set; } = string.Empty;
    public string OpponentJob { get; set; } = string.Empty;
    public long OpponentCp { get; set; }
    public bool IsVictory { get; set; }
    public int RatingChange { get; set; }

    // Navigation
    public ArenaPlayer ArenaPlayer { get; set; } = null!;
}
