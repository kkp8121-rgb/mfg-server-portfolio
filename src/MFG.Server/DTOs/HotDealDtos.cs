namespace MFG.Server.DTOs;

public class HotDealCatalogResponse
{
    public List<HotDealInfo> Deals { get; set; } = [];
    public DateTime ServerTime { get; set; }
}

public class HotDealInfo
{
    public string DealId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public int DurationHours { get; set; }
    public HotDealCostDto Cost { get; set; } = new();
    public List<HotDealRewardDto> Rewards { get; set; } = [];
    public bool IsPurchased { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class HotDealCostDto
{
    public string Type { get; set; } = string.Empty;
    public long Amount { get; set; }
}

public class HotDealRewardDto
{
    public string Item { get; set; } = string.Empty;
    public long Amount { get; set; }
}

public class HotDealPurchaseRequest
{
    public string DealId { get; set; } = string.Empty;
}

public class HotDealPurchaseResponse
{
    public string DealId { get; set; } = string.Empty;
    public List<HotDealRewardDto> RewardsGranted { get; set; } = [];
    public long CurrencyRemaining { get; set; }
    public DateTime ServerTime { get; set; }
}
