using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IRaceEntryRepository
{
    Task<bool> ExistsAsync(Guid raceId, Guid horseId);
    Task<bool> OwnerHasHorseInRaceAsync(Guid raceId, Guid ownerId);
    Task<RaceEntry?> GetByIdWithHorseAsync(Guid entryId, Guid raceId);
    Task<RaceEntry?> GetByRaceHorseAsync(Guid raceId, Guid horseId);
    Task<RaceEntry?> GetByRaceAndHorseAsync(Guid raceId, Guid horseId);
    Task<List<RaceEntry>> GetByJockeyAsync(Guid jockeyId);
    Task<List<RaceEntry>> GetPendingConfirmationsByJockeyAsync(Guid jockeyId);
    Task<List<RaceEntry>> GetByHorseAsync(Guid horseId);
    Task<List<RaceEntry>> GetByRaceAsync(Guid raceId);
    Task<List<Guid>> GetHorseIdsInActiveRacesAsync();
    Task<bool> IsHorseInActiveRaceAsync(Guid horseId, Guid? excludeRaceId = null);
    Task<bool> HasJockeyScheduleConflictAsync(Guid jockeyId, DateTime scheduledAt, DateTime scheduledEndAt, Guid? excludeEntryId = null);
    // J3: Tournament-long Horse/Jockey pairing checks — see RaceEntryRepository for details.
    Task<List<RaceEntry>> GetOfficialAssignmentsForJockeyAsync(Guid jockeyId);
    Task<RaceEntry?> GetOfficialAssignmentForHorseInTournamentAsync(Guid horseId, Guid tournamentId);
    Task AddAsync(RaceEntry entry);
    Task UpdateAsync(RaceEntry entry);
    Task UpdateRangeAsync(IEnumerable<RaceEntry> entries);
    Task<List<RaceEntry>> GetPendingWithDetailsAsync();
    Task DeleteAsync(Guid id);
    Task<RaceEntry?> GetByIdAsync(Guid id);
}
