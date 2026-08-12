using System;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class OwnerRepository : IOwnerRepository
{
    private readonly ApplicationDbContext _db;

    public OwnerRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(Guid ownerId)
    {
        return _db.Owners.AnyAsync(o => o.Id == ownerId);
    }

    public Task<Owner?> GetByIdAsync(Guid ownerId)
    {
        return _db.Owners
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == ownerId);
    }

    public Task<Owner?> GetByUserIdAsync(Guid userId)
    {
        return _db.Owners
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.UserId == userId);
    }

    public Task AddAsync(Owner owner)
    {
        _db.Owners.Add(owner);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Owner owner)
    {
        _db.Owners.Update(owner);
        return Task.CompletedTask;
    }
}
