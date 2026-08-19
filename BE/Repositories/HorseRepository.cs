using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class HorseRepository : IHorseRepository
{
    private readonly ApplicationDbContext _db;

    public HorseRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<Horse>> GetByOwnerAsync(Guid ownerId)
    {
        var horses = await _db.Horses
            .AsSplitQuery()
            .Include(h => h.JockeyInvitations)
                .ThenInclude(i => i.Jockey)
                    .ThenInclude(j => j!.User)
            .Include(h => h.RaceEntries)
                .ThenInclude(e => e.Jockey)
                    .ThenInclude(j => j!.User)
            .Include(h => h.RaceEntries)
                .ThenInclude(e => e.Race)
                    .ThenInclude(r => r.Tournament)
            .Include(h => h.RaceEntries)
                .ThenInclude(e => e.Race)
                    .ThenInclude(r => r.Track)
            .Include(h => h.RaceEntries)
                .ThenInclude(e => e.Race)
                    .ThenInclude(r => r.Round)
            .Where(h => h.OwnerId == ownerId)
            .ToListAsync();

        return horses;
    }

    public async Task<Horse?> GetByIdAsync(Guid horseId)
    {
        var horse = await _db.Horses
            .AsSplitQuery()
            .Include(h => h.Owner)
                .ThenInclude(o => o!.User)
            .Include(h => h.JockeyInvitations)
                .ThenInclude(i => i.Jockey)
                    .ThenInclude(j => j!.User)
            .Include(h => h.RaceEntries)
                .ThenInclude(e => e.Jockey)
                    .ThenInclude(j => j!.User)
            .Include(h => h.RaceEntries)
                .ThenInclude(e => e.Race)
                    .ThenInclude(r => r.Tournament)
            .FirstOrDefaultAsync(h => h.Id == horseId);

        return horse;
    }

    public async Task<Horse?> GetOwnedHorseAsync(Guid horseId, Guid ownerId)
    {
        var horse = await _db.Horses
            .AsSplitQuery()
            .Include(h => h.JockeyInvitations)
                .ThenInclude(i => i.Jockey)
                    .ThenInclude(j => j!.User)
            .Include(h => h.RaceEntries)
                .ThenInclude(e => e.Jockey)
                    .ThenInclude(j => j!.User)
            .Include(h => h.RaceEntries)
                .ThenInclude(e => e.Race)
                    .ThenInclude(r => r.Tournament)
            .FirstOrDefaultAsync(h => h.Id == horseId && h.OwnerId == ownerId);

        return horse;
    }

    public async Task<List<Horse>> GetAllApprovedAsync()
    {
        return await _db.Horses
            .Where(h => h.ApprovalStatus == ApprovalStatus.Approved)
            .Include(h => h.Owner)
                .ThenInclude(o => o!.User)
            .ToListAsync();
    }

    public Task AddAsync(Horse horse)
    {
        _db.Horses.Add(horse);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Horse horse)
    {
        _db.Horses.Update(horse);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Horse horse)
    {
        _db.Horses.Remove(horse);
        return Task.CompletedTask;
    }
    // Removed NormalizeHorseInvitations to allow multiple invitations (one per race)
}
