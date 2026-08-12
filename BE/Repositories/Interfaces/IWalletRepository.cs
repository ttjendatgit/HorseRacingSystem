using System;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByUserIdAsync(Guid userId);
    Task AddAsync(Wallet wallet);
    Task UpdateAsync(Wallet wallet);
    Task<bool> AddBalanceAsync(Guid userId, decimal amount);
    Task<bool> DeductBalanceAsync(Guid userId, decimal amount);
}
