namespace MFG.Domain.Entities;

public class AttendanceRecord : BaseEntity
{
    public long PlayerId { get; set; }
    public DateOnly CheckDate { get; set; }
    public int ConsecutiveDays { get; set; } = 1;
    public string RewardType { get; set; } = string.Empty;
    public int RewardAmount { get; set; }

    // Navigation
    public Player Player { get; set; } = null!;
}
