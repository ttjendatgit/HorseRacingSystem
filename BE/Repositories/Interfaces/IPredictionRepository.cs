using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IPredictionRepository
{
    Task<bool> ExistsAsync(Guid raceId, Guid spectatorUserId);
    Task AddAsync(Prediction prediction);
    Task<List<Prediction>> GetByUserAsync(Guid spectatorUserId);
    Task<List<Prediction>> GetByRaceAsync(Guid raceId);
    Task<List<Prediction>> GetAllAsync();
    /// <summary>Returns the number of Pending predictions transitioned to Lost.</summary>
    Task<int> ExecuteUpdateLosersAsync(Guid raceId, Guid winningHorseId);
    /// <summary>Returns the number of Pending predictions transitioned to Won.</summary>
    Task<int> ExecuteUpdateWinnersAsync(Guid raceId, Guid winningHorseId);
    /// <summary>
    /// IDs of Pending predictions on the winning horse, captured BEFORE the
    /// Pending-&gt;Won bulk transition. The payout loop must pay exactly this
    /// set (via GetByIdsAsync) — never a fresh "all Won for this race" read —
    /// so a winner already paid by an earlier settlement invocation can never
    /// be re-selected by a later retry.
    /// </summary>
    Task<List<Guid>> GetPendingWinnerIdsAsync(Guid raceId, Guid winningHorseId);
    Task<List<Prediction>> GetByIdsAsync(IReadOnlyCollection<Guid> predictionIds);
    /// <summary>
    /// Reverts a single Won prediction (whose payout failed) back to Pending
    /// via a direct bulk update — bypasses the change tracker entirely so it
    /// cannot be masked by a stale already-tracked entity, and is scoped to
    /// exactly this one row regardless of what else this settlement call
    /// touched. Returns true only if the row was still Won when reverted.
    /// </summary>
    Task<bool> RevertWonToPendingAsync(Guid predictionId);
    Task DeleteAsync(Guid id);
}
