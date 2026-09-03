namespace MFG.Domain.Entities;

public class IapReceipt : BaseEntity
{
    public long PlayerId { get; set; }
    public string Platform { get; set; } = string.Empty;    // GooglePlay, Apple, Web
    public string ProductId { get; set; } = string.Empty;
    public string ReceiptHash { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;

    // Navigation
    public Player Player { get; set; } = null!;
}
