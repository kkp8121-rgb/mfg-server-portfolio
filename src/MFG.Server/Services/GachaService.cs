using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Domain.Entities;
using MFG.Server.DTOs;

namespace MFG.Server.Services;

public class GachaService
{
    private readonly AppDbContext _db;

    // 등급 가중치 (Normal, Rare, Epic, Unique, Legendary, Mythic)
    private static readonly string[] Grades = ["Normal", "Rare", "Epic", "Unique", "Legendary", "Mythic"];
    private static readonly float[] DefaultWeights = [50f, 30f, 15f, 4f, 0.9f, 0.1f];

    // 소환 레벨 테이블 (누적 뽑기 수 -> 레벨)
    private static readonly (int RequiredPulls, float[] Weights)[] SummonLevels =
    [
        (0,   [50f, 30f, 15f, 4f, 0.9f, 0.1f]),
        (50,  [45f, 30f, 17f, 6f, 1.5f, 0.5f]),
        (150, [40f, 28f, 20f, 8f, 3f, 1f]),
        (300, [35f, 25f, 22f, 12f, 4f, 2f]),
        (500, [30f, 22f, 24f, 15f, 6f, 3f]),
    ];

    private const int PITY_THRESHOLD = 60;
    private const string PITY_GUARANTEE_GRADE = "Legendary";
    private const string TEN_PULL_GUARANTEE_GRADE = "Rare";

    private const long RUBY_COST_SINGLE = 300;
    private const long RUBY_COST_TEN = 2700;
    private const string CURRENCY_RUBY = "Ruby";

    // 풀별 아이템 테이블 (서버 권위)
    private static readonly Dictionary<string, GachaItemDef[]> PoolItems = new()
    {
        ["Equipment"] = GeneratePoolItems("eq", 30),
        ["Weapon"] = GeneratePoolItems("wp", 20),
        ["Relic"] = GeneratePoolItems("rl", 15),
    };

    public GachaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GachaPullResponse> PullAsync(long playerId, string poolType, int pullCount, CancellationToken ct)
    {
        if (pullCount is not (1 or 10))
            throw new ArgumentException("pullCount는 1 또는 10만 허용됩니다.");

        if (!PoolItems.ContainsKey(poolType))
            throw new ArgumentException($"존재하지 않는 풀: {poolType}");

        var cost = pullCount == 1 ? RUBY_COST_SINGLE : RUBY_COST_TEN;

        // 재화 확인 + 차감
        var currency = await _db.Currencies
            .FirstOrDefaultAsync(c => c.PlayerId == playerId && c.Type == CURRENCY_RUBY, ct)
            ?? throw new InvalidOperationException("루비 재화가 없습니다.");

        if (currency.Amount < cost)
            throw new InvalidOperationException($"루비 부족: 보유 {currency.Amount}, 필요 {cost}");

        currency.Amount -= cost;

        // 트랜잭션 기록
        _db.CurrencyTransactions.Add(new CurrencyTransaction
        {
            PlayerId = playerId,
            CurrencyType = CURRENCY_RUBY,
            Amount = -cost,
            BalanceAfter = currency.Amount,
            Reason = "GachaPull",
            ReferenceId = $"{poolType}_{pullCount}"
        });

        // Pity 조회/생성
        var pity = await _db.GachaPities
            .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.PoolType == poolType, ct);

        if (pity is null)
        {
            pity = new GachaPity { PlayerId = playerId, PoolType = poolType, TotalPulls = 0 };
            _db.GachaPities.Add(pity);
        }

        // 뽑기 실행
        var results = new List<GachaResultItem>();
        var items = PoolItems[poolType];
        var hasGuarantee = false;

        for (int i = 0; i < pullCount; i++)
        {
            pity.TotalPulls++;
            var sincePity = pity.TotalPulls % PITY_THRESHOLD;

            string grade;

            // 천장 체크 (60회마다 Legendary 보장)
            if (sincePity == 0)
            {
                grade = PITY_GUARANTEE_GRADE;
            }
            // 10연차 마지막: Rare 이상 보장 (아직 미충족 시)
            else if (pullCount == 10 && i == 9 && !hasGuarantee)
            {
                grade = RollGradeWithMinimum(pity.TotalPulls, TEN_PULL_GUARANTEE_GRADE);
            }
            else
            {
                grade = RollGrade(pity.TotalPulls);
            }

            var gradeIndex = Array.IndexOf(Grades, grade);
            if (gradeIndex >= Array.IndexOf(Grades, TEN_PULL_GUARANTEE_GRADE))
                hasGuarantee = true;

            var item = RollItem(items, grade);

            results.Add(new GachaResultItem
            {
                ItemId = item.ItemId,
                Grade = grade,
                IsNew = false // 서버에서는 인벤토리 조회 후 판단 (향후 구현)
            });

            _db.GachaHistories.Add(new GachaHistory
            {
                PlayerId = playerId,
                PoolType = poolType,
                ResultItemId = item.ItemId,
                ResultGrade = grade,
                PityCount = pity.TotalPulls
            });
        }

        await _db.SaveChangesAsync(ct);

        var summonLevel = GetSummonLevel(pity.TotalPulls);

        return new GachaPullResponse
        {
            Results = results,
            CurrencySpent = new CurrencySpent { Type = CURRENCY_RUBY, Amount = cost },
            CurrencyRemaining = currency.Amount,
            PityCount = pity.TotalPulls,
            SummonLevel = new SummonLevelInfo
            {
                Pool = poolType,
                TotalPulls = pity.TotalPulls,
                Level = summonLevel
            }
        };
    }

    private string RollGrade(int totalPulls)
    {
        var weights = GetWeightsForPulls(totalPulls);
        return WeightedRandom(Grades, weights);
    }

    private string RollGradeWithMinimum(int totalPulls, string minGrade)
    {
        var minIndex = Array.IndexOf(Grades, minGrade);
        var weights = GetWeightsForPulls(totalPulls);

        // minGrade 미만 가중치를 0으로, 나머지 재분배
        for (int i = 0; i < minIndex; i++)
            weights[i] = 0;

        return WeightedRandom(Grades, weights);
    }

    private static GachaItemDef RollItem(GachaItemDef[] items, string grade)
    {
        var candidates = items.Where(i => i.Grade == grade).ToArray();
        if (candidates.Length == 0)
        {
            // fallback: 가장 가까운 등급
            candidates = items;
        }
        return candidates[Random.Shared.Next(candidates.Length)];
    }

    private float[] GetWeightsForPulls(int totalPulls)
    {
        float[] weights = (float[])DefaultWeights.Clone();
        for (int i = SummonLevels.Length - 1; i >= 0; i--)
        {
            if (totalPulls >= SummonLevels[i].RequiredPulls)
            {
                weights = (float[])SummonLevels[i].Weights.Clone();
                break;
            }
        }
        return weights;
    }

    private static int GetSummonLevel(int totalPulls)
    {
        int level = 0;
        for (int i = SummonLevels.Length - 1; i >= 0; i--)
        {
            if (totalPulls >= SummonLevels[i].RequiredPulls)
            {
                level = i;
                break;
            }
        }
        return level;
    }

    private static string WeightedRandom(string[] items, float[] weights)
    {
        var total = weights.Sum();
        var roll = Random.Shared.NextDouble() * total;
        float cumulative = 0;
        for (int i = 0; i < items.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return items[i];
        }
        return items[^1];
    }

    private static GachaItemDef[] GeneratePoolItems(string prefix, int count)
    {
        var items = new List<GachaItemDef>();
        foreach (var grade in Grades)
        {
            for (int i = 1; i <= count; i++)
            {
                items.Add(new GachaItemDef
                {
                    ItemId = $"{prefix}_{grade.ToLower()}_{i:D3}",
                    Grade = grade
                });
            }
        }
        return items.ToArray();
    }

    private record GachaItemDef
    {
        public string ItemId { get; init; } = string.Empty;
        public string Grade { get; init; } = string.Empty;
    }
}
