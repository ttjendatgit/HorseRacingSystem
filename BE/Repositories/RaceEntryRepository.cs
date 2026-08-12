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

    public async Task<List<RaceEntry>> GetByJockeyAsync(Guid jockeyId)
    {
        var jockeyUserId = await _db.Jockeys
            .Where(jockey => jockey.Id == jockeyId)
            .Select(jockey => (Guid?)jockey.UserId)
            .FirstOrDefaultAsync();

        var entries = await _db.RaceEntries
            .Include(e => e.Race)
                .ThenInclude(r => r!.Tournament)
            .Include(e => e.Horse)
            .Include(e => e.Jockey)
            .Where(e =>
                e.JockeyId == jockeyId ||
                (e.Horse != null && e.Horse.JockeyInvitations.Any(invitation =>
                    invitation.JockeyId == jockeyId &&
                    invitation.Status == JockeyInvitationStatus.Accepted)) ||
                (jockeyUserId.HasValue &&
                 e.Horse != null &&
                 e.Horse.Owner != null &&
                 e.Horse.Owner.UserId == jockeyUserId.Value))
            .ToListAsync();

        return entries
            .Where(entry => entry.Race != null)
            .GroupBy(entry => entry.Race!.TournamentId)
            .Select(group => group
                .OrderByDescending(entry => entry.JockeyId == jockeyId)
                .ThenByDescending(entry => entry.JockeyConfirmed)
                .ThenByDescending(entry => entry.Status == RegistrationStatus.Approved)
                .First())
            .ToList();
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
