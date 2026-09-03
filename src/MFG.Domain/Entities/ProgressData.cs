namespace MFG.Domain.Entities;

public class ProgressData : BaseEntity
{
    public long PlayerId { get; set; }
    public string SaveJson { get; set; } = "{}";  // JSONB
    public int Version { get; set; } = 1;

    // Navigation
    public Player Player { get; set; } = null!;
}
