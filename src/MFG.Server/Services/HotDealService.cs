using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Domain.Entities;
using MFG.Server.DTOs;

namespace MFG.Server.Services;

public class HotDealService
{
    private readonly AppDbContext _db;
    private readonly CurrencyService _currencyService;

    // 핫딜 카탈로그 정의 (hot-deals-data.json 기반, 인게임 재화 구매 딜만)
    // 메이플 키우기 → MFG 재화 매핑:
    //   BlueDiamonds → Ruby
    //   WeaponSummoningTickets → SummonTicket_Weapon
    //   CompanionSummoningTickets → SummonTicket_Pet
    //   BonusPotentialCube → PotentialCube
    //   RegularPotentialCube → PotentialCube_Regular
    //   MemorialScroll → EnhanceScroll
    //   StarForceEnhancementScroll → StarForceScroll
    //   MedalOfHonor → Medal
    //   HeroToken → HeroToken
    //   Mesos → Gold

    private static readonly Dictionary<string, HotDealDefinition> Catalog;

    static HotDealService()
    {
        var deals = new List<HotDealDefinition>
        {
            // ── Summoning (4개) ──
            new("summoning_weapon_lv8", "Summoning", "무기 소환 레벨 핫딜 1",
                "무기 소환 레벨 8 달성", 12,
                "Ruby", 9000,
                [("SummonTicket_Weapon", 1000)]),

            new("summoning_weapon_lv11", "Summoning", "무기 소환 레벨 핫딜 2",
                "무기 소환 레벨 11 달성", 12,
                "Ruby", 15000,
                [("SummonTicket_Weapon", 1700)]),

            new("summoning_pet_lv3", "Summoning", "동료 소환 레벨 핫딜 1",
                "동료 소환 레벨 3 달성", 12,
                "Ruby", 3000,
                [("SummonTicket_Pet", 140)]),

            new("summoning_pet_lv5", "Summoning", "동료 소환 레벨 핫딜 2",
                "동료 소환 레벨 5 달성", 12,
                "Ruby", 15000,
                [("SummonTicket_Pet", 700)]),

            // ── GrowthHub / HeroPowerLevel (2개) ──
            new("growth_ability_unlock", "GrowthHub", "어빌리티 해금 핫딜",
                "어빌리티 패스 해금 (가이드 퀘스트 114)", 12,
                "Ruby", 9000,
                [("Medal", 5000), ("HeroToken", 10000)]),

            new("growth_ability_lv8", "GrowthHub", "어빌리티 레벨 핫딜 3",
                "어빌리티 재설정 레벨 8 달성", 12,
                "Ruby", 15000,
                [("Medal", 50000)]),

            // ── EquipmentPotential_Regular (4개) ──
            new("potential_epic", "EquipmentPotential", "에픽 잠재 옵션 달성 핫딜",
                "에픽 잠재 옵션 획득", 12,
                "Ruby", 6000,
                [("PotentialCube_Regular", 16)]),

            new("potential_unique", "EquipmentPotential", "유니크 잠재 옵션 달성 핫딜",
                "유니크 잠재 옵션 획득", 12,
                "Ruby", 15000,
                [("PotentialCube_Regular", 30), ("EnhanceScroll", 30)]),

            new("potential_legendary", "EquipmentPotential", "레전더리 잠재 옵션 달성 핫딜",
                "레전더리 잠재 옵션 획득", 12,
                "Ruby", 30000,
                [("PotentialCube_Regular", 60), ("EnhanceScroll", 60)]),

            new("potential_mythic", "EquipmentPotential", "미스틱 잠재 옵션 달성 핫딜",
                "미스틱 잠재 옵션 획득", 12,
                "Ruby", 45000,
                [("PotentialCube_Regular", 90), ("EnhanceScroll", 90)]),

            // ── EquipmentPotential_Bonus (4개) ──
            new("bonus_potential_epic", "EquipmentPotential", "에픽 보너스 잠재 달성 핫딜",
                "에픽 보너스 잠재 옵션 획득", 12,
                "Ruby", 6000,
                [("PotentialCube", 8)]),

            new("bonus_potential_unique", "EquipmentPotential", "유니크 보너스 잠재 달성 핫딜",
                "유니크 보너스 잠재 옵션 획득", 12,
                "Ruby", 15000,
                [("PotentialCube", 18), ("EnhanceScroll", 18)]),

            new("bonus_potential_legendary", "EquipmentPotential", "레전더리 보너스 잠재 달성 핫딜",
                "레전더리 보너스 잠재 옵션 획득", 12,
                "Ruby", 30000,
                [("PotentialCube", 36), ("EnhanceScroll", 36)]),

            new("bonus_potential_mythic", "EquipmentPotential", "미스틱 보너스 잠재 달성 핫딜",
                "미스틱 보너스 잠재 옵션 획득", 12,
                "Ruby", 45000,
                [("PotentialCube", 54), ("EnhanceScroll", 54)]),

            // ── StarForce (4개) ──
            new("starforce_lv8", "StarForce", "스타포스 달성 핫딜 1",
                "스타포스 레벨 8 달성", 12,
                "Ruby", 6000,
                [("StarForceScroll", 30)]),

            new("starforce_lv11", "StarForce", "스타포스 달성 핫딜 2",
                "스타포스 레벨 11 달성", 12,
                "Ruby", 15000,
                [("StarForceScroll", 60), ("Gold", 3000000)]),

            new("starforce_lv15", "StarForce", "스타포스 달성 핫딜 3",
                "스타포스 레벨 15 달성", 12,
                "Ruby", 30000,
                [("StarForceScroll", 120), ("Gold", 6000000)]),

            new("starforce_lv20", "StarForce", "스타포스 달성 핫딜 4",
                "스타포스 레벨 20 달성", 12,
                "Ruby", 45000,
                [("StarForceScroll", 180), ("Gold", 9000000)]),

            // ── General (1개 — 인게임 재화 구매 가능한 것만) ──
            new("general_expert_equip", "General", "전문가 장비 핫딜",
                "가이드 퀘스트 328 완료", 12,
                "Ruby", 6000,
                [("LegendaryEquipBox_Lv70", 1)]),

            // ── BlueDiamond Spending (인게임 재화, 무료 보상 딜 — cost 없이 보상만, 서버에서는 조건 달성 시 무료 지급) ──
            // 비용이 없는 딜이지만 서버에서 조건 달성 확인 후 1회 지급하는 보상형 핫딜
            new("spending_1m", "Spending", "루비 소비 보상 1",
                "루비 1,000,000 이상 소비", 6,
                "Free", 0,
                [("Ruby", 6000), ("SummonTicket_Weapon", 300)]),

            new("spending_2m", "Spending", "루비 소비 보상 2",
                "루비 2,000,000 이상 소비", 6,
                "Free", 0,
                [("Ruby", 15000), ("SummonTicket_Pet", 300)]),

            new("spending_3m", "Spending", "루비 소비 보상 3",
                "루비 3,000,000 이상 소비", 6,
                "Free", 0,
                [("Ruby", 30000)]),

            new("spending_4m", "Spending", "루비 소비 보상 4",
                "루비 4,000,000 이상 소비", 6,
                "Free", 0,
                [("Ruby", 45000), ("PotentialCube_Regular", 55)]),

            new("spending_5m", "Spending", "루비 소비 보상 5",
                "루비 5,000,000 이상 소비", 6,
                "Free", 0,
                [("Ruby", 54000)]),
        };

        Catalog = deals.ToDictionary(d => d.DealId);
    }

    public HotDealService(AppDbContext db, CurrencyService currencyService)
    {
        _db = db;
        _currencyService = currencyService;
    }

    /// <summary>
    /// 핫딜 카탈로그 조회 (구매 여부 포함)
    /// </summary>
    public async Task<HotDealCatalogResponse> GetCatalogAsync(long playerId, CancellationToken ct)
    {
        // 구매 이력 조회 (CurrencyTransaction에서 Reason = "HotDeal"인 것)
        var purchasedDealIds = await _db.CurrencyTransactions
            .Where(t => t.PlayerId == playerId && t.Reason == "HotDeal")
            .Select(t => t.ReferenceId)
            .Distinct()
            .ToListAsync(ct);

        var purchasedSet = new HashSet<string>(purchasedDealIds.Where(id => id != null)!);

        var dealInfos = Catalog.Values
            .Select(d => new HotDealInfo
            {
                DealId = d.DealId,
                Category = d.Category,
                Name = d.Name,
                Condition = d.Condition,
                DurationHours = d.DurationHours,
                Cost = new HotDealCostDto { Type = d.CostType, Amount = d.CostAmount },
                Rewards = d.Rewards.Select(r => new HotDealRewardDto
                {
                    Item = r.Item,
                    Amount = r.Amount
                }).ToList(),
                IsPurchased = purchasedSet.Contains(d.DealId),
                ExpiresAt = null // 클라이언트에서 해금 시점 기준으로 계산
            })
            .OrderBy(d => d.Category)
            .ThenBy(d => d.DealId)
            .ToList();

        return new HotDealCatalogResponse
        {
            Deals = dealInfos,
            ServerTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 핫딜 구매
    /// </summary>
    public async Task<HotDealPurchaseResponse> PurchaseAsync(long playerId, string dealId, CancellationToken ct)
    {
        // 딜 존재 여부 확인
        if (!Catalog.TryGetValue(dealId, out var deal))
            throw new ArgumentException($"존재하지 않는 핫딜: {dealId}");

        // 중복 구매 방지
        var alreadyPurchased = await _db.CurrencyTransactions
            .AnyAsync(t => t.PlayerId == playerId
                        && t.Reason == "HotDeal"
                        && t.ReferenceId == dealId, ct);

        if (alreadyPurchased)
            throw new InvalidOperationException("이미 구매한 핫딜입니다.");

        long currencyRemaining = 0;

        // 비용 차감 (Free가 아닌 경우)
        if (deal.CostType != "Free" && deal.CostAmount > 0)
        {
            var spendResult = await _currencyService.SpendAsync(
                playerId, deal.CostType, deal.CostAmount, "HotDeal", dealId, ct);
            currencyRemaining = spendResult.BalanceAfter;
        }
        else
        {
            // 무료 딜: 구매 기록만 남김 (재화 차감 없음)
            _db.CurrencyTransactions.Add(new CurrencyTransaction
            {
                PlayerId = playerId,
                CurrencyType = "Free",
                Amount = 0,
                BalanceAfter = 0,
                Reason = "HotDeal",
                ReferenceId = dealId
            });
        }

        // 보상 지급
        var rewardsGranted = new List<HotDealRewardDto>();
        foreach (var reward in deal.Rewards)
        {
            await _currencyService.EarnAsync(
                playerId, reward.Item, reward.Amount, "HotDealReward", dealId, ct);

            rewardsGranted.Add(new HotDealRewardDto
            {
                Item = reward.Item,
                Amount = reward.Amount
            });
        }

        await _db.SaveChangesAsync(ct);

        return new HotDealPurchaseResponse
        {
            DealId = dealId,
            RewardsGranted = rewardsGranted,
            CurrencyRemaining = currencyRemaining,
            ServerTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 특정 딜 정보 조회 (내부용)
    /// </summary>
    public static HotDealDefinition? GetDealDefinition(string dealId)
    {
        return Catalog.GetValueOrDefault(dealId);
    }

    // ── 내부 타입 ──

    public record HotDealDefinition(
        string DealId,
        string Category,
        string Name,
        string Condition,
        int DurationHours,
        string CostType,
        long CostAmount,
        List<HotDealRewardItem> Rewards)
    {
        public HotDealDefinition(
            string dealId, string category, string name,
            string condition, int durationHours,
            string costType, long costAmount,
            (string Item, long Amount)[] rewards)
            : this(dealId, category, name, condition, durationHours, costType, costAmount,
                   rewards.Select(r => new HotDealRewardItem(r.Item, r.Amount)).ToList())
        {
        }
    }

    public record HotDealRewardItem(string Item, long Amount);
}
