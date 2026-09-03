namespace MFG.Server.Configuration;

/// <summary>
/// 운영자 계정 화이트리스트. appsettings "Admin" 섹션.
/// 기본값은 빈 리스트 → deny-by-default. 프로덕션에서는 최소 권한 원칙으로 관리.
/// Phase 26 Sprint 26-1 S261-04 보안 가드.
/// TODO(Phase 27 LiveOps): DB 기반 role/audit 테이블로 승격.
/// </summary>
public class AdminConfig
{
    /// <summary>
    /// 관리자로 인정할 user_id(또는 Firebase UID) 목록.
    /// 빈 리스트는 모든 접근을 차단한다.
    /// </summary>
    public List<string> AllowedUserIds { get; set; } = new();
}
