namespace MFG.Domain.Entities;

public class CurrencyTransaction : BaseEntity
{
    public long PlayerId { get; set; }
    public string CurrencyType { get; set; } = string.Empty;
    public long Amount { get; set; }
    public long BalanceAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }

    // Navigation
    public Player Player { get; set; } = null!;
}
