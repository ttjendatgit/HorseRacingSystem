using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class RaceRepository : IRaceRepository
{
    private readonly ApplicationDbContext _db;

    public RaceRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(Guid raceId)
    {
        return _db.Races.AnyAsync(r => r.Id == raceId);
    }

    public Task<List<Race>> GetAllAsync()
    {
        return _db.Races
            .Include(r => r.Entries)
            .Include(r => r.Round)
            .Include(r => r.Track)
            .Include(r => r.Result)
            .ToListAsync();
    }

    public Task<Race?> GetByIdAsync(Guid raceId)
    {
        return _db.Races
            .Include(r => r.Entries)
            .Include(r => r.RefereeAssignments)
            .Include(r => r.Tournament)
            .Include(r => r.Result)
            .Include(r => r.Round)
            .Include(r => r.Track)
            .FirstOrDefaultAsync(r => r.Id == raceId);
    }

    public Task<Race?> GetByIdWithEntriesAsync(Guid raceId)
    {
        return _db.Races
            .Include(r => r.Entries)
            .ThenInclude(e => e.Horse)
            .Include(r => r.Entries).ThenInclude(e => e.Jockey!).ThenInclude(j => j.User)
            .Include(r => r.Result)
            .FirstOrDefaultAsync(r => r.Id == raceId);
    }

    public Task<List<Race>> GetByTournamentAsync(Guid tournamentId)
    {
        return _db.Races
            .Include(r => r.Entries)
            .Include(r => r.RefereeAssignments)
            .Include(r => r.Result)
            .Include(r => r.Round)
            .Include(r => r.Track)
            .Where(r => r.TournamentId == tournamentId)
            .OrderBy(r => r.ScheduledAt)
            .ToListAsync();
    }

    public Task<List<Race>> GetByRoundAsync(Guid roundId)
    {
        return _db.Races
            .Include(r => r.Entries)
            .Include(r => r.RefereeAssignments)
            .Include(r => r.Result)
            .Include(r => r.Round)
            .Include(r => r.Track)
            .Where(r => r.RoundId == roundId)
            .OrderBy(r => r.ScheduledAt)
            .ToListAsync();
    }

    // J-CROSS: single query, no navigation properties — used to compare two Tournaments' full
    // immutable Race schedule sets (including Round2+/Final races that may not have RaceEntries
    // yet) without an N+1 per-Tournament round trip. Finished/Cancelled races are excluded — a
    // race that has already concluded or was cancelled no longer represents a live/future jockey
    // schedule obligation, regardless of the parent Tournament's own status. Tournament status
    // itself is NOT filtered here — the caller (HorseService) already restricts to Published/
    // Ongoing other-Tournament IDs before calling this.
    public async Task<Dictionary<Guid, List<(DateTime Start, DateTime End)>>> GetScheduleWindowsByTournamentsAsync(IEnumerable<Guid> tournamentIds)
    {
        var ids = tournamentIds.Distinct().ToList();
        var races = await _db.Races
            .Where(r => ids.Contains(r.TournamentId) &&
                        r.Status != RaceStatus.Finished &&
                        r.Status != RaceStatus.Cancelled)
            .Select(r => new { r.TournamentId, r.ScheduledAt, r.ScheduledEndAt })
            .ToListAsync();

        return races
            .GroupBy(r => r.TournamentId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => (r.ScheduledAt, r.ScheduledEndAt ?? r.ScheduledAt.AddMinutes(30))).ToList());
    }

    public Task AddAsync(Race race)
    {
        _db.Races.Add(race);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Race race)
    {
        _db.Races.Update(race);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid raceId)
    {
        var race = await _db.Races.FirstOrDefaultAsync(r => r.Id == raceId);
        if (race != null)
        {
            _db.Races.Remove(race);
        }
    }
}
