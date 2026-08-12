using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class WithdrawalRepository : IWithdrawalRepository
{
    private readonly ApplicationDbContext _db;

    public WithdrawalRepository(ApplicationDbContext db) => _db = db;

    public Task AddAsync(WithdrawalRequest withdrawal)
    {
        _db.WithdrawalRequests.Add(withdrawal);
        return Task.CompletedTask;
    }

    public Task<List<WithdrawalRequest>> GetByUserIdAsync(Guid userId)
        => _db.WithdrawalRequests
            .Include(w => w.BankAccount)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

    public Task<WithdrawalRequest?> GetByIdAsync(Guid id)
        => _db.WithdrawalRequests
            .Include(w => w.BankAccount)
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.Id == id);

    public Task<List<WithdrawalRequest>> GetPendingAsync()
        => _db.WithdrawalRequests
            .Include(w => w.BankAccount)
            .Include(w => w.User)
            .Where(w => w.Status == "pending")
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

    public Task<List<WithdrawalRequest>> GetAllAsync()
        => _db.WithdrawalRequests
            .Include(w => w.BankAccount)
            .Include(w => w.User)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

    public Task UpdateAsync(WithdrawalRequest withdrawal)
    {
        _db.WithdrawalRequests.Update(withdrawal);
        return Task.CompletedTask;
    }
}
