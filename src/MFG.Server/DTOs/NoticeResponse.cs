namespace MFG.Server.DTOs;

/// <summary>
/// GET /api/v1/system/notice 응답.
/// 점검/이벤트 공지. Active=false면 Title에서 다이얼로그 숨김.
/// Phase 26 Sprint 26-1 S261-05.
/// </summary>
public class NoticeResponse
{
    public bool Active { get; init; }
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public DateTime? StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public DateTime ServerTime { get; init; }

    // Phase 27 Sprint 27-6 강제 업데이트 선행. Notice Active 와 독립적으로 항상 반환된다.
    /// <summary>강제 업데이트 임계값. 비어있으면 강제 업데이트 없음. 클라가 직접 semver 비교.</summary>
    public string MinClientVersion { get; init; } = "";
    /// <summary>강제 업데이트 다이얼로그 override 메시지. 비어있으면 클라 기본 문구 사용.</summary>
    public string ForceUpdateMessage { get; init; } = "";
}
