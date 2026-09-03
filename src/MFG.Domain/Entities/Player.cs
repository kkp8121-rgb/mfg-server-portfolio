namespace MFG.Domain.Entities;

public class Player : BaseEntity
{
    public string FirebaseUid { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public long Exp { get; set; }
    public string JobId { get; set; } = string.Empty;
    public int JobTier { get; set; }
    public long CombatPower { get; set; }
    public int CurrentFloor { get; set; } = 1;
    public int MaxFloor { get; set; } = 1;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogoutAt { get; set; }

    // Navigation
    public ICollection<Currency> Currencies { get; set; } = [];
    public ICollection<GachaPity> GachaPities { get; set; } = [];
    public ICollection<GachaHistory> GachaHistories { get; set; } = [];
    public ICollection<IapReceipt> IapReceipts { get; set; } = [];
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = [];
    public ProgressData? ProgressData { get; set; }
    public ArenaPlayer? ArenaPlayer { get; set; }
    public GuildMember? GuildMember { get; set; }
}
