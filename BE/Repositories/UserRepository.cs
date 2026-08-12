using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _db;

    public UserRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLower();
        return _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
    }

    public Task<User?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLower();
        return _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
    }

    public Task<User?> GetByIdAsync(Guid userId)
    {
        return _db.Users
            .Include(u => u.OwnerProfile)
                .ThenInclude(o => o!.Horses)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public Task<User?> GetByRefreshTokenHashAsync(string hash)
    {
        return _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == hash);
    }

    public Task<List<User>> GetAllAsync()
    {
        return _db.Users
            .Include(u => u.OwnerProfile)
                .ThenInclude(o => o!.Horses)
            .ToListAsync();
    }

    public Task AddAsync(User user)
    {
        _db.Users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user)
    {
        _db.Users.Update(user);
        return Task.CompletedTask;
    }
}
