using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Services;

/// <summary>
/// Shared Pending-Prediction refund logic, reused by both the direct Race cancel path
/// (RaceManagementService.CancelRaceAsync) and the Tournament cancel cascade
/// (TournamentService.ChangeStatusAsync). Refund convention: wallet credited via
/// IWalletService.AddPointsAsync, Prediction marked Lost (the existing "refunded/cancelled"
/// marker — PredictionStatus has no dedicated Refunded value) with SettledAt stamped.
///
/// Idempotency/concurrency boundary: each Prediction is claimed via a single conditional
/// ExecuteUpdateAsync (Id match AND Status == Pending), inspecting the affected-row count —
/// NOT via a SELECT-then-tracked-SaveChanges pattern, and NOT by relying on any caller-side
/// transaction/row-locking side effect. This makes the claim safe across BOTH callers even
/// though only the Tournament-cancel path runs inside an explicit DB transaction — the direct
/// Race-cancel path does not, so the atomicity has to live in the single UPDATE statement
/// itself.
/// </summary>
public static class PredictionRefundHelper
{
    /// <param name="swallowIndividualFailures">
    /// true (direct Race cancel convention): a failed wallet credit compensates the just-claimed
    /// Prediction back to Pending (SettledAt cleared) for manual resolution; other refunds still
    /// proceed. There is no surrounding transaction on this path, so the claim already committed
    /// the instant it executed — compensation is the only way to undo it.
    /// false (Tournament cancel convention): a failed wallet credit throws and is NOT caught here.
    /// The caller (TournamentService.ChangeStatusAsync) already has this call inside its own DB
    /// transaction, so letting the exception propagate rolls back the claim (and everything else
    /// in that transaction) — no manual compensation needed or attempted on this path.
    /// </param>
    public static async Task<int> RefundPendingPredictionsAsync(
        ApplicationDbContext db,
        IWalletService walletService,
        IReadOnlyCollection<Guid> raceIds,
        string referencePrefix,
        bool swallowIndividualFailures)
    {
        if (raceIds.Count == 0)
            return 0;

        // AsNoTracking + projection: these are read only to drive the atomic claim below. Never
        // let EF's change tracker hold these Prediction entities — a tracked instance mutated in
        // memory and later flushed by an unrelated SaveChangesAsync on the same DbContext could
        // silently overwrite the conditional SQL claim's result.
        // Deterministic order (CreatedAt, then Id as a tiebreaker) — no correctness dependency,
        // but makes processing order reproducible for callers/tests that seed multiple
        // Predictions and need to reason about which one is handled first.
        var candidates = await db.Predictions
            .AsNoTracking()
            .Where(p => raceIds.Contains(p.RaceId) && p.Status == PredictionStatus.Pending)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.SpectatorUserId, p.BetAmount, p.RaceId })
            .ToListAsync();

        var refunded = 0;
        foreach (var candidate in candidates)
        {
            var claimed = await ClaimAsync(db, candidate.Id, PredictionStatus.Pending, PredictionStatus.Lost, DateTime.UtcNow);
            if (claimed == 0)
            {
                // Another caller (direct Race cancel racing a Tournament cancel, a retry, etc.)
                // already claimed this Prediction — do not credit the wallet a second time.
                continue;
            }

            try
            {
                var reference = $"{referencePrefix}_{candidate.RaceId}";
                var walletResult = await walletService.AddPointsAsync(candidate.SpectatorUserId, candidate.BetAmount, reference);
                if (!walletResult.Result.Success)
                {
                    // AddPointsAsync can fail without throwing (e.g. no wallet for this user) —
                    // that must be treated exactly like a thrown failure: never leave a
                    // Prediction marked Lost when the wallet was not actually credited.
                    throw new InvalidOperationException(
                        $"Refund failed for prediction {candidate.Id}: {walletResult.Result.Message}");
                }

                refunded++;
            }
            catch when (swallowIndividualFailures)
            {
                // Undo the claim: narrow conditional update, only reverts the exact row this
                // call itself just claimed (guarded by Status == Lost so it can't clobber a
                // row some other process touched in between).
                await ClaimAsync(db, candidate.Id, PredictionStatus.Lost, PredictionStatus.Pending, settledAt: null);
            }
            // swallowIndividualFailures == false: the exception is NOT caught here — it
            // propagates to the caller's own DB transaction, which rolls back the claim above
            // along with everything else in that transaction.
        }

        return refunded;
    }

    private static Task<int> ClaimAsync(
        ApplicationDbContext db, Guid predictionId, PredictionStatus fromStatus, PredictionStatus toStatus, DateTime? settledAt)
    {
        return db.Predictions
            .Where(p => p.Id == predictionId && p.Status == fromStatus)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, toStatus)
                .SetProperty(p => p.SettledAt, settledAt));
    }
}
