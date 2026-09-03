namespace MFG.Server.DTOs;

public class EquipmentEnhanceRequest
{
    public string EnhanceType { get; set; } = string.Empty; // StarForce, Scroll
    public string EquipmentId { get; set; } = string.Empty;  // 클라이언트 장비 인스턴스 ID
    public int CurrentLevel { get; set; }                     // 현재 강화 단계
}

public class EquipmentEnhanceResponse
{
    public bool IsSuccess { get; set; }
    public string Result { get; set; } = string.Empty;  // Success, Fail, Downgrade, Destroy
    public int NewLevel { get; set; }
    public float SuccessRate { get; set; }
    public long GoldCost { get; set; }
    public long GoldRemaining { get; set; }
}
