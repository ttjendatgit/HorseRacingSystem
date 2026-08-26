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
    // J-CROSS: lightweight Race schedule-window projection (ScheduledAt/ScheduledEndAt only, no
    // navigation properties) for one or more Tournaments in a single query. Used to compare the
    // full immutable Race schedule set of two Tournaments without loading entities.
    Task<Dictionary<Guid, List<(DateTime Start, DateTime End)>>> GetScheduleWindowsByTournamentsAsync(IEnumerable<Guid> tournamentIds);
    Task AddAsync(Race race);
    Task UpdateAsync(Race race);
    Task DeleteAsync(Guid raceId);
}
