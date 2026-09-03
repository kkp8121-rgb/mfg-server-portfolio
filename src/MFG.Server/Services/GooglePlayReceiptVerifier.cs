using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;

namespace MFG.Server.Services;

// Google Play Developer API를 호출해 in-app 구매/구독 영수증을 실제 검증.
// play-services.json 서비스 계정 키로 초기화. 파일이 없으면 IsAvailable=false로 안전 비활성.
// 관리 상품(Consumable/Non-Consumable)은 Purchases.Products.Get, 구독은 Purchases.Subscriptions.Get 호출.
public sealed class GooglePlayReceiptVerifier
{
    private readonly AndroidPublisherService? _service;
    private readonly string _packageName;
    private readonly ILogger<GooglePlayReceiptVerifier> _logger;

    public GooglePlayReceiptVerifier(IConfiguration config, ILogger<GooglePlayReceiptVerifier> logger)
    {
        _logger = logger;
        _packageName = config["GooglePlay:PackageName"] ?? "com.mfg.game";
        var credentialPath = config["GooglePlay:CredentialPath"] ?? "play-services.json";

        if (!File.Exists(credentialPath))
        {
            _logger.LogWarning("[GooglePlay] 서비스 계정 키 파일 없음: {Path} — IAP 실제 검증 비활성", credentialPath);
            _service = null;
            return;
        }

        try
        {
            var credential = GoogleCredential
                .FromFile(credentialPath)
                .CreateScoped(AndroidPublisherService.ScopeConstants.Androidpublisher);

            _service = new AndroidPublisherService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MFG Server"
            });

            _logger.LogInformation("[GooglePlay] AndroidPublisher 초기화 완료 package={Package}", _packageName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GooglePlay] AndroidPublisher 초기화 실패 path={Path}", credentialPath);
            _service = null;
        }
    }

    public bool IsAvailable => _service is not null;

    public async Task<ReceiptVerifyResult> VerifyProductAsync(string productId, string purchaseToken, CancellationToken ct)
    {
        if (_service is null)
            return ReceiptVerifyResult.Unavailable();

        try
        {
            var req = _service.Purchases.Products.Get(_packageName, productId, purchaseToken);
            var purchase = await req.ExecuteAsync(ct);

            // purchaseState: 0=Purchased, 1=Cancelled, 2=Pending
            var isValid = purchase.PurchaseState == 0 && purchase.ConsumptionState != 1;
            return new ReceiptVerifyResult
            {
                IsValid = isValid,
                Status = isValid ? "ok" : "invalid",
                OrderId = purchase.OrderId,
                PurchaseTimeMillis = purchase.PurchaseTimeMillis,
                RawPurchaseState = (int?)purchase.PurchaseState
            };
        }
        catch (Google.GoogleApiException ex)
        {
            _logger.LogWarning(ex, "[GooglePlay] Product 영수증 검증 실패 product={Product}", productId);
            return new ReceiptVerifyResult { IsValid = false, Status = $"google_api_error:{ex.HttpStatusCode}", Error = ex.Message };
        }
    }

    public async Task<ReceiptVerifyResult> VerifySubscriptionAsync(string productId, string purchaseToken, CancellationToken ct)
    {
        if (_service is null)
            return ReceiptVerifyResult.Unavailable();

        try
        {
            var req = _service.Purchases.Subscriptions.Get(_packageName, productId, purchaseToken);
            var sub = await req.ExecuteAsync(ct);

            var isValid = sub.PaymentState is 1 or 2; // 1=Received, 2=FreeTrial
            return new ReceiptVerifyResult
            {
                IsValid = isValid,
                Status = isValid ? "ok" : "invalid",
                OrderId = sub.OrderId,
                ExpiryTimeMillis = sub.ExpiryTimeMillis,
                PurchaseTimeMillis = sub.StartTimeMillis
            };
        }
        catch (Google.GoogleApiException ex)
        {
            _logger.LogWarning(ex, "[GooglePlay] Subscription 영수증 검증 실패 product={Product}", productId);
            return new ReceiptVerifyResult { IsValid = false, Status = $"google_api_error:{ex.HttpStatusCode}", Error = ex.Message };
        }
    }
}

public sealed class ReceiptVerifyResult
{
    public bool IsValid { get; init; }
    public string Status { get; init; } = string.Empty;   // ok / invalid / unavailable / google_api_error:xxx
    public string? OrderId { get; init; }
    public long? PurchaseTimeMillis { get; init; }
    public long? ExpiryTimeMillis { get; init; }
    public int? RawPurchaseState { get; init; }
    public string? Error { get; init; }

    public static ReceiptVerifyResult Unavailable() =>
        new() { IsValid = false, Status = "unavailable", Error = "Google Play credential not configured" };
}
