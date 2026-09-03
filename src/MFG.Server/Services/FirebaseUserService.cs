using FirebaseAdmin;
using FirebaseAdmin.Auth;

namespace MFG.Server.Services;

// Firebase Admin SDK 래퍼. JwtBearer 미들웨어로는 불가능한 관리 작업(커스텀 클레임, 강제 로그아웃, 유저 조회)을 담당.
// 초기화는 Program.cs에서 FirebaseApp.Create 로 수행. FirebaseApp.DefaultInstance가 없으면 Dev 바이패스 상태이므로 안전 가드.
public sealed class FirebaseUserService
{
    private readonly ILogger<FirebaseUserService> _logger;

    public FirebaseUserService(ILogger<FirebaseUserService> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable => FirebaseApp.DefaultInstance is not null;

    public async Task<UserRecord?> GetUserAsync(string uid, CancellationToken ct = default)
    {
        if (!IsAvailable) return null;
        try
        {
            return await FirebaseAuth.DefaultInstance.GetUserAsync(uid, ct);
        }
        catch (FirebaseAuthException ex)
        {
            _logger.LogWarning(ex, "Firebase GetUser 실패 uid={Uid}", uid);
            return null;
        }
    }

    public async Task SetCustomClaimsAsync(string uid, IReadOnlyDictionary<string, object> claims, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            _logger.LogDebug("Firebase Admin SDK 미초기화 — SetCustomClaims 스킵 uid={Uid}", uid);
            return;
        }
        await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(uid, claims.ToDictionary(k => k.Key, v => v.Value), ct);
        _logger.LogInformation("Firebase custom claims 갱신 uid={Uid} keys={Keys}", uid, string.Join(",", claims.Keys));
    }

    public async Task RevokeTokensAsync(string uid, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("Firebase Admin SDK 미초기화 — RevokeTokens 스킵 uid={Uid}", uid);
            return;
        }
        await FirebaseAuth.DefaultInstance.RevokeRefreshTokensAsync(uid, ct);
        _logger.LogInformation("Firebase refresh token revoked uid={Uid}", uid);
    }
}
