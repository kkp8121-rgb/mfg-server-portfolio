namespace MFG.Server.DTOs;

public class SaveSyncRequest
{
    public string SaveData { get; set; } = "{}";  // JSON string
    public DateTime ClientTimestamp { get; set; }
}

public class SaveSyncResponse
{
    public long PlayerId { get; set; }
    public int VersionUpdated { get; set; }
    public DateTime ServerTime { get; set; }
    public int NextSyncWindow { get; set; } = 300; // 초
}

public class SaveLoadResponse
{
    public long PlayerId { get; set; }
    public string SaveData { get; set; } = "{}";
    public int Version { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public DateTime ServerTime { get; set; }
}

// 최초 로그인 시 로컬 SaveData.json → 서버 1회 업로드 (S251-01).
// 기존 ProgressData가 없을 때만 수락. /save/sync의 30초 쓰로틀 우회.
public class SaveMigrateRequest
{
    public string SaveData { get; set; } = "{}";
    public DateTime ClientTimestamp { get; set; }
}

public class SaveMigrateResponse
{
    public long PlayerId { get; set; }
    public bool Migrated { get; set; }
    public int Version { get; set; }
    public DateTime ServerTime { get; set; }
    public string? Reason { get; set; } // "already_exists" 등
}
