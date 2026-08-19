using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction);
    Task<Transaction?> GetByReferenceAsync(string reference);
    Task<Transaction?> GetLatestByUserAsync(Guid userId);
    Task<List<Transaction>> GetHistoryByUserAsync(Guid userId);
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<Transaction?> GetPendingByRefAsync(string reference);
    Task<bool> ExistsBySepayIdAsync(long sepayTransactionId);
    Task<bool> TryCompleteByRefAsync(string reference, long sepayTransactionId);
    Task<List<string>> GetPendingReferencesAsync();
}
