using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly ApplicationDbContext _db;

    public TransactionRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Transaction transaction)
    {
        _db.Transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public Task<Transaction?> GetByReferenceAsync(string reference)
    {
        return _db.Transactions.FirstOrDefaultAsync(t => t.Reference == reference);
    }

    public Task<Transaction?> GetLatestByUserAsync(Guid userId)
    {
        return _db.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public Task<List<Transaction>> GetHistoryByUserAsync(Guid userId)
    {
        return _db.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public Task<bool> HasCompletedSinceAsync(Guid userId, DateTime since)
    {
        return _db.Transactions
            .AnyAsync(t => t.UserId == userId
                && t.Status == "completed"
                && t.CompletedAt >= since);
    }

    public Task<Transaction?> GetPendingByRefAsync(string reference)
    {
        return _db.Transactions.FirstOrDefaultAsync(t =>
            t.Reference == reference && t.Status == "pending");
    }

    public Task<bool> ExistsBySepayIdAsync(long sepayTransactionId)
    {
        return _db.Transactions.AnyAsync(t =>
            t.SepayTransactionId == sepayTransactionId);
    }

    public async Task<bool> TryCompleteByRefAsync(string reference, long sepayTransactionId)
    {
        var updated = await _db.Transactions
            .Where(t => t.Reference == reference && t.Status == "pending")
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, "completed")
                .SetProperty(t => t.CompletedAt, DateTime.UtcNow)
                .SetProperty(t => t.SepayTransactionId, sepayTransactionId));
        return updated > 0;
    }

    public Task<List<string>> GetPendingReferencesAsync()
    {
        return _db.Transactions
            .Where(t => t.Status == "pending" && t.Reference != null)
            .Select(t => t.Reference!)
            .ToListAsync();
    }
}
