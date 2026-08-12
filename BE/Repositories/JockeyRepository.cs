using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class JockeyRepository : IJockeyRepository
{
    private readonly ApplicationDbContext _db;

    public JockeyRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(Guid jockeyId)
    {
        return _db.Jockeys.AnyAsync(j => j.Id == jockeyId);
    }

    public Task<List<Jockey>> GetAllAsync()
    {
        return _db.Jockeys
            .Include(j => j.User)
            .ToListAsync();
    }

    public Task<List<Jockey>> GetAvailableAsync()
    {
        return _db.Jockeys
            .Include(j => j.User)
            .Where(j => j.User == null || j.User.IsActive)
            .OrderBy(j => j.Rank ?? int.MaxValue)
            .ThenBy(j => j.User!.FullName)
            .ToListAsync();
    }

    public Task<Jockey?> GetByIdAsync(Guid jockeyId)
    {
        return _db.Jockeys
            .Include(j => j.User)
            .FirstOrDefaultAsync(j => j.Id == jockeyId);
    }

    public Task<Jockey?> GetByUserIdAsync(Guid userId)
    {
        return _db.Jockeys
            .Include(j => j.User)
            .FirstOrDefaultAsync(j => j.UserId == userId);
    }

    public Task AddAsync(Jockey jockey)
    {
        _db.Jockeys.Add(jockey);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Jockey jockey)
    {
        _db.Jockeys.Update(jockey);
        return Task.CompletedTask;
    }
}
