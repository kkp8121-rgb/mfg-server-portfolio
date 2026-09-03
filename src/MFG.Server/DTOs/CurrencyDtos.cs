namespace MFG.Server.DTOs;

public class CurrencySpendRequest
{
    public string CurrencyType { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
}

public class CurrencyEarnRequest
{
    public string CurrencyType { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
}

public class CurrencyBalanceResponse
{
    public List<CurrencyEntry> Currencies { get; set; } = [];
}

public class CurrencyEntry
{
    public string Type { get; set; } = string.Empty;
    public long Amount { get; set; }
}

public class CurrencyTransactionResponse
{
    public string CurrencyType { get; set; } = string.Empty;
    public long Amount { get; set; }
    public long BalanceAfter { get; set; }
}
