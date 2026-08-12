using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IWithdrawalRepository
{
    Task AddAsync(WithdrawalRequest withdrawal);
    Task<List<WithdrawalRequest>> GetByUserIdAsync(Guid userId);
    Task<WithdrawalRequest?> GetByIdAsync(Guid id);
    Task<List<WithdrawalRequest>> GetPendingAsync();
    Task<List<WithdrawalRequest>> GetAllAsync();
    Task UpdateAsync(WithdrawalRequest withdrawal);
}
