namespace MFG.Server.DTOs;

public class LoginRequest
{
    public string FirebaseIdToken { get; set; } = string.Empty;
}

public class LoginResponse
{
    public long PlayerId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public bool IsNewPlayer { get; set; }
    public DateTime ServerTime { get; set; }
}

// ── 로그아웃 ──

public class LogoutResponse
{
    public long PlayerId { get; set; }
    public DateTime LogoutTime { get; set; }
    public DateTime ServerTime { get; set; }
}

// ── 프로필 업데이트 ──

public class PlayerProfileUpdateRequest
{
    public int? Level { get; set; }
    public long? CombatPower { get; set; }
    public string? Nickname { get; set; }
    public string? JobId { get; set; }
}

public class PlayerProfileResponse
{
    public long PlayerId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public int Level { get; set; }
    public long CombatPower { get; set; }
    public string JobId { get; set; } = string.Empty;
    public DateTime ServerTime { get; set; }
}
