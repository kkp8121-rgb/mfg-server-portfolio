namespace MFG.Server.DTOs;

public class EventClaimRequest
{
    public string EventType { get; set; } = string.Empty;  // EventDungeon, BattlePass, OfflineReward
    public string EventId { get; set; } = string.Empty;
    public int Tier { get; set; }                           // 난이도/단계
}

public class EventClaimResponse
{
    public string EventType { get; set; } = string.Empty;
    public List<RewardEntry> Rewards { get; set; } = [];
    public DateTime ServerTime { get; set; }
}

public class RewardEntry
{
    public string Type { get; set; } = string.Empty;  // Gold, Ruby, Equipment, etc.
    public long Amount { get; set; }
}
