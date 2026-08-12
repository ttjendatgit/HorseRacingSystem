using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class BankAccountRepository : IBankAccountRepository
{
    private readonly ApplicationDbContext _db;

    public BankAccountRepository(ApplicationDbContext db) => _db = db;

    public Task<List<BankAccount>> GetByUserIdAsync(Guid userId)
        => _db.Set<BankAccount>().Where(b => b.UserId == userId).ToListAsync();

    public Task<BankAccount?> GetByIdAsync(Guid id)
        => _db.Set<BankAccount>().FirstOrDefaultAsync(b => b.Id == id);

    public Task AddAsync(BankAccount account)
    {
        _db.Set<BankAccount>().Add(account);
        return Task.CompletedTask;
    }
}
