namespace MFG.Server.DTOs;

public class GachaPullRequest
{
    public string PoolType { get; set; } = string.Empty; // Equipment, Weapon, Relic
    public int PullCount { get; set; } = 1;              // 1 or 10
}

public class GachaPullResponse
{
    public List<GachaResultItem> Results { get; set; } = [];
    public CurrencySpent CurrencySpent { get; set; } = new();
    public long CurrencyRemaining { get; set; }
    public int PityCount { get; set; }
    public SummonLevelInfo SummonLevel { get; set; } = new();
}

public class GachaResultItem
{
    public string ItemId { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public bool IsNew { get; set; }
}

public class CurrencySpent
{
    public string Type { get; set; } = string.Empty;
    public long Amount { get; set; }
}

public class SummonLevelInfo
{
    public string Pool { get; set; } = string.Empty;
    public int TotalPulls { get; set; }
    public int Level { get; set; }
}
