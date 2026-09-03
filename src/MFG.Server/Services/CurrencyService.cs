using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Domain.Entities;
using MFG.Server.DTOs;

namespace MFG.Server.Services;

public class CurrencyService
{
    private readonly AppDbContext _db;
    private readonly ValidationService _validation;

    public CurrencyService(AppDbContext db, ValidationService validation)
    {
        _db = db;
        _validation = validation;
    }

    public async Task<CurrencyBalanceResponse> GetBalanceAsync(long playerId, CancellationToken ct)
    {
        var currencies = await _db.Currencies
            .Where(c => c.PlayerId == playerId)
            .Select(c => new CurrencyEntry { Type = c.Type, Amount = c.Amount })
            .ToListAsync(ct);

        return new CurrencyBalanceResponse { Currencies = currencies };
    }

    public async Task<CurrencyTransactionResponse> SpendAsync(
        long playerId, string currencyType, long amount, string reason, string? referenceId, CancellationToken ct)
    {
        if (!BalanceTables.ValidCurrencyTypes.Contains(currencyType))
            throw new ArgumentException($"유효하지 않은 재화 타입: {currencyType}");

        if (amount <= 0)
            throw new ArgumentException("차감 금액은 양수여야 합니다.");

        var currency = await _db.Currencies
            .FirstOrDefaultAsync(c => c.PlayerId == playerId && c.Type == currencyType, ct)
            ?? throw new InvalidOperationException($"재화 '{currencyType}'이 없습니다.");

        if (currency.Amount < amount)
            throw new InvalidOperationException($"재화 부족: 보유 {currency.Amount}, 필요 {amount}");

        currency.Amount -= amount;

        _db.CurrencyTransactions.Add(new CurrencyTransaction
        {
            PlayerId = playerId,
            CurrencyType = currencyType,
            Amount = -amount,
            BalanceAfter = currency.Amount,
            Reason = reason,
            ReferenceId = referenceId
        });

        await _db.SaveChangesAsync(ct);

        return new CurrencyTransactionResponse
        {
            CurrencyType = currencyType,
            Amount = -amount,
            BalanceAfter = currency.Amount
        };
    }

    public async Task<CurrencyTransactionResponse> EarnAsync(
        long playerId, string currencyType, long amount, string reason, string? referenceId, CancellationToken ct)
    {
        if (!BalanceTables.ValidCurrencyTypes.Contains(currencyType))
            throw new ArgumentException($"유효하지 않은 재화 타입: {currencyType}");

        if (amount <= 0)
            throw new ArgumentException("지급 금액은 양수여야 합니다.");

        // 일일 재화 획득 상한 검사. 상한값은 BalanceConfig 핫리로드 우선 (S261-04), BalanceTables 폴백.
        if (!await _validation.CheckDailyEarnCapAsync(playerId, currencyType, amount, ct))
            throw new InvalidOperationException(
                $"일일 {currencyType} 획득 상한 초과. 상한: {_validation.GetCurrentDailyCap(currencyType)}");

        var currency = await _db.Currencies
            .FirstOrDefaultAsync(c => c.PlayerId == playerId && c.Type == currencyType, ct);

        if (currency is null)
        {
            currency = new Currency
            {
                PlayerId = playerId,
                Type = currencyType,
                Amount = 0
            };
            _db.Currencies.Add(currency);
        }

        currency.Amount += amount;

        _db.CurrencyTransactions.Add(new CurrencyTransaction
        {
            PlayerId = playerId,
            CurrencyType = currencyType,
            Amount = amount,
            BalanceAfter = currency.Amount,
            Reason = reason,
            ReferenceId = referenceId
        });

        await _db.SaveChangesAsync(ct);

        return new CurrencyTransactionResponse
        {
            CurrencyType = currencyType,
            Amount = amount,
            BalanceAfter = currency.Amount
        };
    }
}
