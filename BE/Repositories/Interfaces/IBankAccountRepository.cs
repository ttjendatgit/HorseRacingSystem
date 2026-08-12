using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IBankAccountRepository
{
    Task<List<BankAccount>> GetByUserIdAsync(Guid userId);
    Task<BankAccount?> GetByIdAsync(Guid id);
    Task AddAsync(BankAccount account);
}
