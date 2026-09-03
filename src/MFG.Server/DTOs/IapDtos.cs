namespace MFG.Server.DTOs;

public class IapVerifyRequest
{
    public string Platform { get; set; } = string.Empty;   // GooglePlay, Apple, Web
    public string ReceiptData { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
}

public class IapVerifyResponse
{
    public long PlayerId { get; set; }
    public string CurrencyType { get; set; } = string.Empty;
    public long AmountGranted { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public DateTime ServerTime { get; set; }
}
