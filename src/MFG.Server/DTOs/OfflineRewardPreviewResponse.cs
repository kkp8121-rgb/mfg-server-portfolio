namespace MFG.Server.DTOs;

/// <summary>
/// GET /api/v1/event/offline-reward/preview 응답.
/// 마지막 로그아웃(또는 로그인) 이후 경과 시간에 기반한 예상 보상을 반환한다.
/// DB 상태 변경 없음 — 순수 미리보기.
/// 관련: server/Docs/Planning/offline-reward-model.md (Phase 26 S261-03)
/// </summary>
public class OfflineRewardPreviewResponse
{
    public long PlayerId { get; init; }
    public long CoinReward { get; init; }
    public long GemReward { get; init; }

    /// <summary>보상 계산에 실제 사용된 경과 시간(초) — 상한 적용 후 값</summary>
    public long DurationSeconds { get; init; }

    public DateTime LastLogoutAt { get; init; }
    public DateTime ServerTime { get; init; }

    /// <summary>상한(12시간) 도달 여부. true면 그 이상 방치해도 추가 보상 없음.</summary>
    public bool Capped { get; init; }
}
