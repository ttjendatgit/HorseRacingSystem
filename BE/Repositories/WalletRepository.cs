using System;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class WalletRepository : IWalletRepository
{
    private readonly ApplicationDbContext _db;

    public WalletRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<Wallet?> GetByUserIdAsync(Guid userId)
    {
        return _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
    }

    public Task AddAsync(Wallet wallet)
    {
        _db.Wallets.Add(wallet);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Wallet wallet)
    {
        _db.Wallets.Update(wallet);
        return Task.CompletedTask;
    }

    public async Task<bool> AddBalanceAsync(Guid userId, decimal amount)
    {
        var updated = await _db.Wallets
            .Where(w => w.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(w => w.Balance, w => w.Balance + amount)
                .SetProperty(w => w.UpdatedAt, DateTime.UtcNow));
        return updated > 0;
    }

    public async Task<bool> DeductBalanceAsync(Guid userId, decimal amount)
    {
        var updated = await _db.Wallets
            .Where(w => w.UserId == userId && w.Balance >= amount)
            .ExecuteUpdateAsync(s => s
                .SetProperty(w => w.Balance, w => w.Balance - amount)
                .SetProperty(w => w.UpdatedAt, DateTime.UtcNow));
        return updated > 0;
    }
}
