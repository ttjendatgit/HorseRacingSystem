using System;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class RaceResultRepository : IRaceResultRepository
{
    private readonly ApplicationDbContext _db;

    public RaceResultRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<RaceResult?> GetByRaceIdAsync(Guid raceId)
    {
        return _db.RaceResults
            .Include(r => r.WinningHorse)
            .FirstOrDefaultAsync(r => r.RaceId == raceId);
    }

    public Task AddAsync(RaceResult raceResult)
    {
        _db.RaceResults.Add(raceResult);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RaceResult raceResult)
    {
        _db.RaceResults.Update(raceResult);
        return Task.CompletedTask;
    }
}
