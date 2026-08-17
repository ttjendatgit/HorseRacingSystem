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
