using System.Collections.Generic;
using System.Linq;
using HorseRacing.Models;

namespace HorseRacing.Services;

/// <summary>
/// PRIZE-V1.2: Prize.Amount is DERIVED from Prize.PercentageOfPool and Tournament.PrizePool —
/// Admin never submits Amount directly. All arithmetic is decimal (never float/double), since
/// PrizePool is VND money.
///
/// Rounding strategy (locked, see PRIZE-V1.2 report §4): round each configured row's Amount to
/// the nearest whole VND (MidpointRounding.AwayFromZero) EXCEPT the last row in Position order,
/// which instead absorbs whatever remainder keeps SUM(Amount) exactly consistent with the
/// currently-configured rows' OWN total percentage — not with the full PrizePool. Concretely:
/// totalForConfiguredRows = round(PrizePool * SUM(all configured Percentages) / 100); the last
/// row's Amount = totalForConfiguredRows - SUM(other rows' rounded Amounts). This keeps a
/// partial Draft allocation (e.g. 50%+30%=80%) showing each row's OWN honest amount (1,000,000,000
/// and 600,000,000 of a 2,000,000,000 pool — not "50% + whatever's left of the whole pool"), while
/// still guaranteeing the required invariant once percentages total exactly 100%: SUM(Amount) ==
/// PrizePool exactly, with no rounding remainder lost or invented.
/// </summary>
internal static class PrizeAmountCalculator
{
    /// <summary>Mutates .Amount on every Prize in `prizes` (any order) in place. No-op for an
    /// empty list. Prizes must belong to the same Tournament; caller is responsible for
    /// persisting (SaveChangesAsync) — entities already tracked by the DbContext just need their
    /// property mutated, no explicit Update() call required.</summary>
    public static void RecalculateAmounts(IEnumerable<Prize> prizes, decimal prizePool)
    {
        var ordered = prizes.OrderBy(p => p.Position).ToList();
        if (ordered.Count == 0) return;

        decimal runningSum = 0m;
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var amount = decimal.Round(prizePool * ordered[i].PercentageOfPool / 100m, 0, System.MidpointRounding.AwayFromZero);
            ordered[i].Amount = amount;
            runningSum += amount;
        }

        var totalPercentage = ordered.Sum(p => p.PercentageOfPool);
        var totalForConfiguredRows = decimal.Round(prizePool * totalPercentage / 100m, 0, System.MidpointRounding.AwayFromZero);
        ordered[^1].Amount = totalForConfiguredRows - runningSum;
    }
}
