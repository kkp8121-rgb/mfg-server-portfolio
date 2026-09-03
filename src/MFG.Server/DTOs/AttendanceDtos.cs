namespace MFG.Server.DTOs;

public class AttendanceCheckResponse
{
    public int ConsecutiveDays { get; set; }
    public AttendanceReward Reward { get; set; } = new();
    public string ServerDate { get; set; } = string.Empty;
    public DateTime NextCheckTime { get; set; }
}

public class AttendanceReward
{
    public string Type { get; set; } = string.Empty;
    public int Amount { get; set; }
}
