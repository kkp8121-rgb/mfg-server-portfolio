namespace MFG.Server.Configuration;

/// <summary>
/// GET /api/v1/system/notice 응답에 사용되는 점검 공지 설정.
/// appsettings의 "SystemNotice" 섹션에서 주입된다.
/// Phase 26 Sprint 26-1 S261-05.
/// </summary>
public class SystemNoticeConfig
{
    public bool Active { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    // Phase 27 Sprint 27-6 강제 업데이트 선행 필드 — 클라 부팅 시 /system/notice 를 이미 호출하므로
    // 강제 업데이트 정보도 같은 응답에 실어 왕복 수 감소.
    /// <summary>강제 업데이트 임계값. 비어있으면 미적용. semver 형식 권장("1.2.3"). 클라 앱 버전 &lt; 이 값이면 강제 업데이트 유도.</summary>
    public string MinClientVersion { get; set; } = "";
    /// <summary>강제 업데이트 다이얼로그 메시지 override. 비어있으면 클라 i18n 기본 문구 사용.</summary>
    public string ForceUpdateMessage { get; set; } = "";
}
