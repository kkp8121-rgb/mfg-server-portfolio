using System.Text.Json;
using Google.Api.Gax.ResourceNames;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Grpc.Auth;

namespace MFG.Server.Services;

// Google Play RTDN (Real-time Developer Notifications) 수신용 BackgroundService.
// Play Console이 GCP Pub/Sub 토픽 `mfg-iap-notifications`에 구독 갱신/취소/환불 이벤트를 게시.
// 서버는 동일 토픽의 pull subscription에서 메시지를 받아 IAP/subscription 상태 반영.
//
// 선행 조건:
//   1. GCP-04 완료: 토픽 `mfg-iap-notifications` 생성 + google-play-developer-notifications@ 에 Publisher 권한
//   2. GCP-05 (신규): pull subscription `mfg-iap-notifications-sub` 생성 + play-iap-verifier@ 에 Subscriber 권한
//   3. PLAY-04: Play Console 수익 창출 설정에 Pub/Sub 토픽 연결 (Play 계정 확보 후)
//
// 메시지 스키마 (Google 공식):
//   {
//     "version": "1.0",
//     "packageName": "com.mfg.game",
//     "eventTimeMillis": "...",
//     "subscriptionNotification": { ... }  // 또는 oneTimeProductNotification / testNotification
//   }
//
// 현재 구현: 메시지 수신 → 구조화 로그 + notificationType 매핑 + Ack. 실제 players/IapReceipt DB 반영은 PLAY-04 연동 + 스키마 확장(SubscriptionTier/ExpiresAt 컬럼) 후 단계.

// Google Play RTDN subscription notificationType (공식 spec)
// https://developer.android.com/google/play/billing/rtdn-reference#sub
public enum SubscriptionNotificationType
{
    Unknown = 0,
    Recovered = 1,
    Renewed = 2,
    Canceled = 3,
    Purchased = 4,
    OnHold = 5,
    InGracePeriod = 6,
    Restarted = 7,
    PriceChangeConfirmed = 8,
    Deferred = 9,
    Paused = 10,
    PauseScheduleChanged = 11,
    Revoked = 12,
    Expired = 13
}

public sealed class PubSubSubscriberService : BackgroundService
{
    private readonly ILogger<PubSubSubscriberService> _logger;
    private readonly string? _projectId;
    private readonly string _subscriptionId;
    private readonly string _credentialPath;

    public PubSubSubscriberService(IConfiguration config, ILogger<PubSubSubscriberService> logger)
    {
        _logger = logger;
        _projectId = config["Firebase:ProjectId"]; // GCP project는 Firebase와 동일
        _subscriptionId = config["PubSub:SubscriptionId"] ?? "mfg-iap-notifications-sub";
        // 재사용: Play IAP 검증용과 동일 서비스 계정 (Subscriber 권한 별도 부여 필요)
        _credentialPath = config["GooglePlay:CredentialPath"] ?? "play-services.json";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_projectId))
        {
            _logger.LogWarning("[PubSub] Firebase:ProjectId 미설정 — RTDN 구독 스킵");
            return;
        }

        if (!File.Exists(_credentialPath))
        {
            _logger.LogWarning("[PubSub] 서비스 계정 키 없음: {Path} — RTDN 구독 스킵", _credentialPath);
            return;
        }

        var subscriptionName = SubscriptionName.FromProjectSubscription(_projectId, _subscriptionId);
        SubscriberClient subscriber;
        try
        {
            // Subscriber 전용 OAuth scope 필요
            var credential = GoogleCredential.FromFile(_credentialPath)
                .CreateScoped("https://www.googleapis.com/auth/pubsub");

            subscriber = await new SubscriberClientBuilder
            {
                SubscriptionName = subscriptionName,
                ChannelCredentials = credential.ToChannelCredentials()
            }.BuildAsync(stoppingToken);

            _logger.LogInformation("[PubSub] RTDN 구독 시작 subscription={Subscription}", subscriptionName);
        }
        catch (Exception ex)
        {
            // 구독이 아직 존재하지 않거나 권한 부족 시 — 서버 기동은 계속
            _logger.LogWarning(ex, "[PubSub] 구독 클라이언트 생성 실패 — GCP-05 미완료 가능성. RTDN 스킵");
            return;
        }

        var startTask = subscriber.StartAsync(async (msg, ct) =>
        {
            try
            {
                var payload = msg.Data.ToStringUtf8();
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                var packageName = root.TryGetProperty("packageName", out var p) ? p.GetString() : null;
                var eventType = DetectEventType(root);

                // 구독 알림이면 세부 필드 파싱 (purchaseToken / subscriptionId / notificationType)
                if (eventType == "subscription" && root.TryGetProperty("subscriptionNotification", out var sub))
                {
                    var notifTypeRaw = sub.TryGetProperty("notificationType", out var nt) && nt.ValueKind == JsonValueKind.Number
                        ? nt.GetInt32() : 0;
                    var notifType = (SubscriptionNotificationType)notifTypeRaw;
                    var purchaseToken = sub.TryGetProperty("purchaseToken", out var pt) ? pt.GetString() : null;
                    var subscriptionId = sub.TryGetProperty("subscriptionId", out var si) ? si.GetString() : null;

                    _logger.LogInformation(
                        "[PubSub][RTDN][Subscription] package={Package} notifType={NotifType}({NotifRaw}) subId={SubId} tokenPrefix={TokenPrefix} msgId={MsgId}",
                        packageName, notifType, notifTypeRaw, subscriptionId,
                        purchaseToken is { Length: > 8 } ? purchaseToken[..8] : purchaseToken,
                        msg.MessageId);

                    // TODO (S-B3 DB 반영 단계): purchaseToken으로 IapReceipt 조회 → PlayerId 획득 → notifType에 따라
                    //   Player.SubscriptionStatus / SubscriptionExpiresAt 업데이트. 선행 작업:
                    //   (1) Player 엔티티에 SubscriptionTier/Status/ExpiresAt 컬럼 추가 + Migration
                    //   (2) IapReceipt에 PurchaseToken 컬럼 추가 + Migration
                    //   (3) Play Developer API purchases.subscriptionsv2.get 호출로 실제 만료 시간 확인 (PLAY-04)
                    //   매핑: PURCHASED/RENEWED/RECOVERED/RESTARTED → Active, IN_GRACE → Grace, ON_HOLD/PAUSED → Hold,
                    //         CANCELED → Cancelled (기간 만료까지 Active 유지), EXPIRED/REVOKED → Expired
                }
                else
                {
                    _logger.LogInformation("[PubSub][RTDN] package={Package} type={Type} msgId={MsgId} payloadLen={Len}",
                        packageName, eventType, msg.MessageId, payload.Length);
                }
                // Apple ASSN V2도 별도 webhook 엔드포인트에서 수신 후 동일 흐름으로 반영 예정.

                return SubscriberClient.Reply.Ack;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PubSub] 메시지 처리 실패 msgId={MsgId} — Nack 후 재전송 대기", msg.MessageId);
                return SubscriberClient.Reply.Nack;
            }
        });

        // 종료 신호 대기
        await Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
            }
        }, stoppingToken);

        _logger.LogInformation("[PubSub] 구독 종료 요청 — pending 메시지 처리 후 정지");
        await subscriber.StopAsync(CancellationToken.None);
        await startTask;
    }

    private static string DetectEventType(JsonElement root)
    {
        if (root.TryGetProperty("subscriptionNotification", out _)) return "subscription";
        if (root.TryGetProperty("oneTimeProductNotification", out _)) return "product";
        if (root.TryGetProperty("voidedPurchaseNotification", out _)) return "voided";
        if (root.TryGetProperty("testNotification", out _)) return "test";
        return "unknown";
    }
}
