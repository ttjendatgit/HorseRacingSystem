using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IRaceRepository
{
    Task<bool> ExistsAsync(Guid raceId);
    Task<List<Race>> GetAllAsync();
    Task<Race?> GetByIdAsync(Guid raceId);
    Task<Race?> GetByIdWithEntriesAsync(Guid raceId);
    Task<List<Race>> GetByTournamentAsync(Guid tournamentId);
    Task<List<Race>> GetByRoundAsync(Guid roundId);
    Task AddAsync(Race race);
    Task UpdateAsync(Race race);
    Task DeleteAsync(Guid raceId);
}
