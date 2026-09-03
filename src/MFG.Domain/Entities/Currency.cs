namespace MFG.Domain.Entities;

public class Currency : BaseEntity
{
    public long PlayerId { get; set; }
    public string Type { get; set; } = string.Empty;
    public long Amount { get; set; }

    // Navigation
    public Player Player { get; set; } = null!;
}
