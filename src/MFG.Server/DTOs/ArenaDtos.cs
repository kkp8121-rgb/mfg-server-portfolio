namespace MFG.Server.DTOs;

// ── 매칭 후보 ──

public class ArenaCandidate
{
    public string Name { get; set; } = string.Empty;
    public string Job { get; set; } = string.Empty;
    public long Cp { get; set; }
    public int Tier { get; set; }
}

public class ArenaCandidatesResponse
{
    public List<ArenaCandidate> Candidates { get; set; } = [];
    public int RemainingEntries { get; set; }
}

// ── 전투 결과 ──

public class ArenaBattleRequest
{
    public int CandidateIndex { get; set; }
    public long PlayerCp { get; set; }
}

public class ArenaBattleResponse
{
    public bool IsVictory { get; set; }
    public int RatingChange { get; set; }
    public int NewRating { get; set; }
    public int NewTier { get; set; }
    public int CurrentWinStreak { get; set; }
}

// ── 상태 ──

public class ArenaStatusResponse
{
    public int CurrentTier { get; set; }
    public int Rating { get; set; }
    public int TotalVictories { get; set; }
    public int TotalDefeats { get; set; }
    public int CurrentWinStreak { get; set; }
    public int BestWinStreak { get; set; }
    public int RemainingEntries { get; set; }
    public string SeasonId { get; set; } = string.Empty;
    public List<ArenaRecordDto> RecentRecords { get; set; } = [];
}

public class ArenaRecordDto
{
    public string OpponentName { get; set; } = string.Empty;
    public string OpponentJob { get; set; } = string.Empty;
    public long OpponentCp { get; set; }
    public bool IsVictory { get; set; }
    public int RatingChange { get; set; }
    public string Timestamp { get; set; } = string.Empty;
}

// ── 리더보드 ──

public class ArenaLeaderboardResponse
{
    public List<ArenaLeaderboardEntry> Entries { get; set; } = [];
    public DateTime ServerTime { get; set; }
}

public class ArenaLeaderboardEntry
{
    public int Rank { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public int Rating { get; set; }
    public int Tier { get; set; }
    public int TotalVictories { get; set; }
    public int BestWinStreak { get; set; }
}
