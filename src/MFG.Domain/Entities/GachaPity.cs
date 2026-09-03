namespace MFG.Domain.Entities;

/// <summary>
/// 플레이어별 풀별 누적 뽑기 수 (소환 레벨 + 천장 계산용)
/// </summary>
public class GachaPity : BaseEntity
{
    public long PlayerId { get; set; }
    public string PoolType { get; set; } = string.Empty; // Equipment, Weapon, Relic
    public int TotalPulls { get; set; }

    // Navigation
    public Player Player { get; set; } = null!;
}
