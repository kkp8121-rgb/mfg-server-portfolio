using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Domain.Entities;
using MFG.Server.DTOs;
using MFG.Server.Services;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/iap")]
[Authorize]
[EnableRateLimiting("iap")]
public class IapController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly GooglePlayReceiptVerifier _googleVerifier;
    private readonly ILogger<IapController> _logger;

    // MFG IAP 상품 카탈로그 — `server-manual-setup.md` Phase 6-1 기준 13종.
    // 값: (재화 타입, 지급량, 구독 여부)
    // 블루다이아 5종은 직접 수량 지급. 스타터팩/배틀패스 8종은 번들이라 후속 상품별 지급 로직 필요 — 현재는 루비 대체 보상.
    // 구독 3종은 별도 players 테이블의 subscription_expires_at 갱신이 필요하나 현재 엔티티 미확장 → 영수증 기록만.
    private static readonly Dictionary<string, ProductReward> ProductCatalog = new()
    {
        // 블루다이아 (소모성)
        ["com.mfg.bluediamond.t1"] = new("BlueDiamond", 60, false),
        ["com.mfg.bluediamond.t2"] = new("BlueDiamond", 220, false),
        ["com.mfg.bluediamond.t3"] = new("BlueDiamond", 480, false),
        ["com.mfg.bluediamond.t4"] = new("BlueDiamond", 1200, false),
        ["com.mfg.bluediamond.t5"] = new("BlueDiamond", 2800, false),

        // 스타터팩 (비소모성) — 임시: 루비 번들로 대체. 향후 Grant 시스템 확장 예정.
        ["com.mfg.starter.basic"]  = new("Ruby", 100, false),
        ["com.mfg.starter.growth"] = new("Ruby", 600, false),
        ["com.mfg.starter.elite"]  = new("Ruby", 1500, false),

        // 배틀패스 (비소모성 시즌팩) — 임시: 루비 번들.
        ["com.mfg.bp.premium"]      = new("Ruby", 1200, false),
        ["com.mfg.bp.premium_plus"] = new("Ruby", 2500, false),

        // 구독 (자동 갱신)
        ["com.mfg.sub.hunter_basic"]  = new("Ruby", 550, true),
        ["com.mfg.sub.hunter_elite"]  = new("Ruby", 1500, true),
        ["com.mfg.sub.hunter_master"] = new("Ruby", 3000, true),
    };

    public IapController(AppDbContext db, GooglePlayReceiptVerifier googleVerifier, ILogger<IapController> logger)
    {
        _db = db;
        _googleVerifier = googleVerifier;
        _logger = logger;
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] IapVerifyRequest request, CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");
        if (string.IsNullOrEmpty(firebaseUid))
            return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);
        if (player is null)
            return NotFound(ApiResponse<object>.Fail("플레이어를 찾을 수 없습니다."));

        if (!ProductCatalog.TryGetValue(request.ProductId, out var reward))
            return BadRequest(ApiResponse<object>.Fail($"존재하지 않는 상품: {request.ProductId}"));

        // 영수증 해시 (중복 방지) — 플랫폼별 토큰/트랜잭션 ID를 모두 포함해 해시
        var receiptHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{request.Platform}|{request.ProductId}|{request.ReceiptData}")));

        var exists = await _db.IapReceipts.AnyAsync(r => r.ReceiptHash == receiptHash, ct);
        if (exists)
            return BadRequest(ApiResponse<object>.Fail("이미 처리된 영수증입니다."));

        // 플랫폼별 실제 검증
        var platform = request.Platform?.ToLowerInvariant() ?? string.Empty;
        ReceiptVerifyResult verification;
        if (platform is "android" or "googleplay" or "google")
        {
            verification = reward.IsSubscription
                ? await _googleVerifier.VerifySubscriptionAsync(request.ProductId, request.ReceiptData, ct)
                : await _googleVerifier.VerifyProductAsync(request.ProductId, request.ReceiptData, ct);
        }
        else if (platform is "ios" or "apple")
        {
            // Apple 검증은 APPLE-* 블로커 해제 후 S-B2에서 구현
            _logger.LogInformation("Apple IAP 검증 스텁 반환 product={Product}", request.ProductId);
            return Ok(ApiResponse<IapVerifyResponse>.Ok(new IapVerifyResponse
            {
                PlayerId = player.Id,
                CurrencyType = reward.CurrencyType,
                AmountGranted = 0,
                TransactionId = "pending_apple_setup",
                ServerTime = DateTime.UtcNow
            }));
        }
        else
        {
            return BadRequest(ApiResponse<object>.Fail($"지원하지 않는 플랫폼: {request.Platform}"));
        }

        if (verification.Status == "unavailable")
        {
            _logger.LogWarning("IAP 검증기 미가용 — play-services.json 미배치. 임시 통과 처리 product={Product} player={PlayerId}",
                request.ProductId, player.Id);
            // 운영 전환 시 이 블록을 제거하고 503 반환하도록 변경해야 함
        }
        else if (!verification.IsValid)
        {
            _logger.LogWarning("IAP 검증 실패 status={Status} order={Order} product={Product}",
                verification.Status, verification.OrderId, request.ProductId);
            return BadRequest(ApiResponse<object>.Fail($"영수증 검증 실패: {verification.Status}"));
        }

        // 영수증 기록
        _db.IapReceipts.Add(new IapReceipt
        {
            PlayerId = player.Id,
            Platform = request.Platform,
            ProductId = request.ProductId,
            ReceiptHash = receiptHash,
            IsValid = verification.IsValid
        });

        // 재화 지급
        var currency = await _db.Currencies
            .FirstOrDefaultAsync(c => c.PlayerId == player.Id && c.Type == reward.CurrencyType, ct);
        if (currency is null)
        {
            currency = new Currency { PlayerId = player.Id, Type = reward.CurrencyType, Amount = 0 };
            _db.Currencies.Add(currency);
        }
        currency.Amount += reward.Amount;

        _db.CurrencyTransactions.Add(new CurrencyTransaction
        {
            PlayerId = player.Id,
            CurrencyType = reward.CurrencyType,
            Amount = reward.Amount,
            BalanceAfter = currency.Amount,
            Reason = reward.IsSubscription ? "IapSubscription" : "IapPurchase",
            ReferenceId = verification.OrderId ?? request.ProductId
        });

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<IapVerifyResponse>.Ok(new IapVerifyResponse
        {
            PlayerId = player.Id,
            CurrencyType = reward.CurrencyType,
            AmountGranted = reward.Amount,
            TransactionId = verification.OrderId ?? receiptHash[..16],
            ServerTime = DateTime.UtcNow
        }));
    }

    private readonly record struct ProductReward(string CurrencyType, long Amount, bool IsSubscription);
}
