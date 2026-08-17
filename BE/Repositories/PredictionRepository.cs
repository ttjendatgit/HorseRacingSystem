using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class PredictionRepository : IPredictionRepository
{
    private readonly ApplicationDbContext _db;

    public PredictionRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(Guid raceId, Guid spectatorUserId)
    {
        return _db.Predictions.AnyAsync(p => p.RaceId == raceId && p.SpectatorUserId == spectatorUserId);
    }

    public Task AddAsync(Prediction prediction)
    {
        _db.Predictions.Add(prediction);
        return Task.CompletedTask;
    }

    public Task<List<Prediction>> GetByUserAsync(Guid spectatorUserId)
    {
        return _db.Predictions
            .Include(p => p.Race)
            .Include(p => p.PredictedHorse)
            .Where(p => p.SpectatorUserId == spectatorUserId)
            .ToListAsync();
    }

    public Task<List<Prediction>> GetByRaceAsync(Guid raceId)
    {
        return _db.Predictions
            .Where(p => p.RaceId == raceId)
            .ToListAsync();
    }

    public Task<List<Prediction>> GetAllAsync()
    {
        return _db.Predictions
            .Include(p => p.Race)!.ThenInclude(r => r!.Tournament)
            .Include(p => p.PredictedHorse)
            .Include(p => p.Spectator)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public Task<int> ExecuteUpdateLosersAsync(Guid raceId, Guid winningHorseId)
    {
        return _db.Predictions
            .Where(p => p.RaceId == raceId
                && p.Status == PredictionStatus.Pending
                && p.PredictedHorseId != winningHorseId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PredictionStatus.Lost)
                .SetProperty(p => p.PayoutAmount, 0)
                .SetProperty(p => p.SettledAt, DateTime.UtcNow));
    }

    public Task<int> ExecuteUpdateWinnersAsync(Guid raceId, Guid winningHorseId)
    {
        return _db.Predictions
            .Where(p => p.RaceId == raceId
                && p.Status == PredictionStatus.Pending
                && p.PredictedHorseId == winningHorseId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PredictionStatus.Won)
                .SetProperty(p => p.PayoutAmount, p => p.PotentialPayout > 0 ? p.PotentialPayout : p.BetAmount * 2)
                .SetProperty(p => p.SettledAt, DateTime.UtcNow));
    }

    public Task<List<Guid>> GetPendingWinnerIdsAsync(Guid raceId, Guid winningHorseId)
    {
        return _db.Predictions
            .AsNoTracking()
            .Where(p => p.RaceId == raceId
                && p.Status == PredictionStatus.Pending
                && p.PredictedHorseId == winningHorseId)
            .Select(p => p.Id)
            .ToListAsync();
    }

    public Task<List<Prediction>> GetByIdsAsync(IReadOnlyCollection<Guid> predictionIds)
    {
        return _db.Predictions
            .AsNoTracking()
            .Include(p => p.PredictedHorse)
            .Where(p => predictionIds.Contains(p.Id))
            .ToListAsync();
    }

    public async Task<bool> RevertWonToPendingAsync(Guid predictionId)
    {
        var updated = await _db.Predictions
            .Where(p => p.Id == predictionId && p.Status == PredictionStatus.Won)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PredictionStatus.Pending)
                .SetProperty(p => p.PayoutAmount, (decimal?)null)
                .SetProperty(p => p.SettledAt, (DateTime?)null));
        return updated > 0;
    }

    public async Task DeleteAsync(Guid id)
    {
        var prediction = await _db.Predictions.FindAsync(id);
        if (prediction != null) { _db.Predictions.Remove(prediction); }
    }
}
