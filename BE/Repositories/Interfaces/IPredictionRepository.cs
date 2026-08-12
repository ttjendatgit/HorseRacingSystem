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
    Task ExecuteUpdateLosersAsync(Guid raceId, Guid winningHorseId);
    Task ExecuteUpdateWinnersAsync(Guid raceId, Guid winningHorseId);
    Task<List<Prediction>> GetWinnersByRaceAsync(Guid raceId);
    Task DeleteAsync(Guid id);
}
