using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class RaceEntryRepository : IRaceEntryRepository
{
    private readonly ApplicationDbContext _db;

    public RaceEntryRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(Guid raceId, Guid horseId)
    {
        return _db.RaceEntries.AnyAsync(e => e.RaceId == raceId && e.HorseId == horseId);
    }

    public Task<bool> OwnerHasHorseInRaceAsync(Guid raceId, Guid ownerId)
    {
        return _db.RaceEntries
            .Include(e => e.Horse)
            .AnyAsync(e => e.RaceId == raceId && e.Horse!.OwnerId == ownerId);
    }

    public Task<RaceEntry?> GetByIdWithHorseAsync(Guid entryId, Guid raceId)
    {
        return _db.RaceEntries
            .Include(e => e.Horse)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.RaceId == raceId);
    }

    public Task<RaceEntry?> GetByRaceHorseAsync(Guid raceId, Guid horseId)
    {
        return _db.RaceEntries
            .Include(e => e.Horse)
            .Include(e => e.Jockey)
            .Include(e => e.Race)
                .ThenInclude(r => r!.Tournament)
            .FirstOrDefaultAsync(e => e.RaceId == raceId && e.HorseId == horseId);
    }

    public Task<RaceEntry?> GetByRaceAndHorseAsync(Guid raceId, Guid horseId)
    {
        return _db.RaceEntries
            .Include(e => e.Horse)
            .Include(e => e.Jockey)
            .FirstOrDefaultAsync(e => e.RaceId == raceId && e.HorseId == horseId);
    }

    // J3 schedule correctness: a race belongs in the Jockey's OFFICIAL schedule only when
    // RaceEntry.JockeyId == this Jockey — never inferred from an Accepted invitation (J2
    // acceptance is not assignment) or from the caller also owning the Horse. Only Owner Final
    // Confirm (HorseService.FinalConfirmJockeyAsync) sets JockeyId, so that equality check is the
    // single source of truth here. This is a pure display query — it returns EVERY official
    // RaceEntry as-is (including ones in a Finished/Cancelled Tournament, or several within the
    // same Tournament) and applies no per-Tournament dedup; the one-active-Tournament business
    // lock is enforced only at Final Confirm time (HorseService), never here.
    public Task<List<RaceEntry>> GetByJockeyAsync(Guid jockeyId)
    {
        return _db.RaceEntries
            .Include(e => e.Race)
                .ThenInclude(r => r!.Tournament)
            .Include(e => e.Race)
                .ThenInclude(r => r!.Round)
            .Include(e => e.Race)
                .ThenInclude(r => r!.Result)
            .Include(e => e.Horse)
            .Include(e => e.Jockey)
            .Where(e => e.JockeyId == jockeyId && e.Race != null)
            .OrderBy(e => e.Race!.ScheduledAt)
            .ToListAsync();
    }

    public Task<List<RaceEntry>> GetPendingConfirmationsByJockeyAsync(Guid jockeyId)
    {
        return _db.RaceEntries
            .Include(e => e.Race)!.ThenInclude(r => r!.Tournament)
            .Include(e => e.Horse)
            .Where(e => e.JockeyId == jockeyId && !e.JockeyConfirmed)
            .ToListAsync();
    }

    public Task<List<RaceEntry>> GetByHorseAsync(Guid horseId)
    {
        return _db.RaceEntries
            .Include(e => e.Race)
                .ThenInclude(r => r!.Tournament)
            .Include(e => e.Race)
                .ThenInclude(r => r!.Track)
            .Include(e => e.Horse)
            .Include(e => e.Jockey)
            .Where(e => e.HorseId == horseId)
            .ToListAsync();
    }

    public Task<List<RaceEntry>> GetByRaceAsync(Guid raceId)
    {
        return _db.RaceEntries
            .Include(e => e.Horse)!.ThenInclude(h => h!.Owner)!.ThenInclude(o => o!.User)
            .Include(e => e.Jockey)!.ThenInclude(j => j!.User)
            .Where(e => e.RaceId == raceId)
            .ToListAsync();
    }

    public Task<List<Guid>> GetHorseIdsInActiveRacesAsync()
    {
        var finishedOrCancelled = new[] { RaceStatus.Finished, RaceStatus.Cancelled };
        return _db.RaceEntries
            .Where(e => !finishedOrCancelled.Contains(e.Race!.Status))
            .Select(e => e.HorseId)
            .Distinct()
            .ToListAsync();
    }

    public Task<bool> IsHorseInActiveRaceAsync(Guid horseId, Guid? excludeRaceId = null)
    {
        var finishedOrCancelled = new[] { RaceStatus.Finished, RaceStatus.Cancelled };
        var query = _db.RaceEntries
            .Where(e => e.HorseId == horseId && !finishedOrCancelled.Contains(e.Race!.Status));
        if (excludeRaceId.HasValue)
            query = query.Where(e => e.RaceId != excludeRaceId.Value);
        return query.AnyAsync();
    }

    public Task<bool> HasJockeyScheduleConflictAsync(Guid jockeyId, DateTime scheduledAt,
        DateTime scheduledEndAt, Guid? excludeEntryId = null)
    {
        var query = _db.RaceEntries.Where(e =>
            e.JockeyId == jockeyId &&
            e.Status != RegistrationStatus.Rejected &&
            e.Race != null &&
            e.Race.Status != RaceStatus.Cancelled &&
            e.Race.Status != RaceStatus.Finished &&
            e.Race.ScheduledAt < scheduledEndAt &&
            (e.Race.ScheduledEndAt ?? e.Race.ScheduledAt.AddMinutes(30)) > scheduledAt);

        if (excludeEntryId.HasValue)
            query = query.Where(e => e.Id != excludeEntryId.Value);

        return query.AnyAsync();
    }

    // J3: every official assignment (RaceEntry.JockeyId == jockeyId) for this Jockey, used to
    // detect the Tournament-long Horse/Jockey pairing (one Jockey, one Horse per Tournament) and
    // the one-active-Tournament-per-Jockey lock. Only a Rejected RaceEntry is excluded — Race.Status
    // is otherwise irrelevant here: once a pairing is established it stays the source of truth for
    // that Tournament even after the specific Race that created it finishes (a later Round's
    // RaceEntry for the same Horse carries the same Jockey forward automatically — see J3 §7).
    // Race.Tournament is included so the caller can check TournamentId identity + Tournament.Status
    // without a second query.
    public Task<List<RaceEntry>> GetOfficialAssignmentsForJockeyAsync(Guid jockeyId)
    {
        return _db.RaceEntries
            .Include(e => e.Race)
                .ThenInclude(r => r!.Tournament)
            .Where(e =>
                e.JockeyId == jockeyId &&
                e.Status != RegistrationStatus.Rejected &&
                e.Race != null)
            .ToListAsync();
    }

    // J3: does this Horse already have an official Jockey (RaceEntry.JockeyId != null) anywhere in
    // this Tournament? One Horse pairs with at most one Jockey per Tournament, for the Horse's
    // entire Tournament journey — this intentionally is NOT scoped to a single RaceEntry, so it
    // also catches a pairing already established on a different (e.g. earlier-Round) RaceEntry for
    // the same Horse.
    public Task<RaceEntry?> GetOfficialAssignmentForHorseInTournamentAsync(Guid horseId, Guid tournamentId)
    {
        return _db.RaceEntries
            .Include(e => e.Jockey)
                .ThenInclude(j => j!.User)
            .Where(e =>
                e.HorseId == horseId &&
                e.JockeyId != null &&
                e.Status != RegistrationStatus.Rejected &&
                e.Race != null &&
                e.Race.TournamentId == tournamentId)
            .FirstOrDefaultAsync();
    }

    public async Task<RaceEntry?> GetByIdAsync(Guid id)
    {
        return await _db.RaceEntries
            .Include(e => e.Race)
            .Include(e => e.Horse)
            .Include(e => e.Jockey)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public Task AddAsync(RaceEntry entry)
    {
        _db.RaceEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RaceEntry entry)
    {
        _db.RaceEntries.Update(entry);
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<RaceEntry> entries)
    {
        _db.RaceEntries.UpdateRange(entries);
        return Task.CompletedTask;
    }

    public Task<List<RaceEntry>> GetPendingWithDetailsAsync()
    {
        return _db.RaceEntries
            .Include(e => e.Race)!.ThenInclude(r => r!.Tournament)
            .Include(e => e.Horse)!.ThenInclude(h => h!.Owner)!.ThenInclude(o => o!.User)
            .Include(e => e.Jockey)!.ThenInclude(j => j!.User)
            .Where(e => e.Status == RegistrationStatus.Pending)
            .ToListAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entry = await GetByIdAsync(id);
        if (entry != null)
        {
            _db.RaceEntries.Remove(entry);
        }
    }
}
