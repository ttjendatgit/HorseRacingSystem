using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class PrizeRepository : IPrizeRepository
{
    private readonly ApplicationDbContext _context;
    public PrizeRepository(ApplicationDbContext context) => _context = context;

    public async Task<Prize?> GetByIdAsync(Guid id) =>
        await _context.Prizes.FindAsync(id);

    public async Task<IEnumerable<Prize>> GetByTournamentAsync(Guid tournamentId) =>
        await _context.Prizes.Where(p => p.TournamentId == tournamentId).ToListAsync();

    public async Task<IEnumerable<Prize>> GetByRaceAsync(Guid raceId) =>
        await _context.Prizes.Where(p => p.RaceId == raceId).ToListAsync();

    public async Task<IEnumerable<Prize>> GetAllAsync() =>
        await _context.Prizes.ToListAsync();

    public async Task AddAsync(Prize prize) => await _context.Prizes.AddAsync(prize);
    public Task UpdateAsync(Prize prize) { _context.Prizes.Update(prize); return Task.CompletedTask; }
    public async Task DeleteAsync(Guid id) { var p = await _context.Prizes.FindAsync(id); if (p != null) _context.Prizes.Remove(p); }
}

public class ProtestRepository : IProtestRepository
{
    private readonly ApplicationDbContext _context;
    public ProtestRepository(ApplicationDbContext context) => _context = context;

    public async Task<Protest?> GetByIdAsync(Guid id) =>
        await _context.Protests.Include(p => p.Race).Include(p => p.FiledByUser)
            .Include(p => p.AgainstEntry).ThenInclude(e => e!.Horse).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IEnumerable<Protest>> GetByRaceAsync(Guid raceId) =>
        await _context.Protests.Where(p => p.RaceId == raceId).ToListAsync();

    public async Task<IEnumerable<Protest>> GetPendingAsync() =>
        await _context.Protests.Where(p => p.Status == ProtestStatus.Pending || p.Status == ProtestStatus.UnderReview)
            .Include(p => p.FiledByUser).Include(p => p.Race).ToListAsync();

    public async Task<IEnumerable<Protest>> GetAllAsync() =>
        await _context.Protests.Include(p => p.FiledByUser).Include(p => p.Race).ToListAsync();

    public async Task AddAsync(Protest protest) => await _context.Protests.AddAsync(protest);
    public Task UpdateAsync(Protest protest) { _context.Protests.Update(protest); return Task.CompletedTask; }
}

public class HorseTransferRepository : IHorseTransferRepository
{
    private readonly ApplicationDbContext _context;
    public HorseTransferRepository(ApplicationDbContext context) => _context = context;

    public async Task<HorseTransfer?> GetByIdAsync(Guid id) =>
        await _context.HorseTransfers.Include(t => t.Horse).Include(t => t.FromOwner)
            .Include(t => t.ToOwner).FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IEnumerable<HorseTransfer>> GetByHorseAsync(Guid horseId) =>
        await _context.HorseTransfers.Where(t => t.HorseId == horseId).ToListAsync();

    public async Task<IEnumerable<HorseTransfer>> GetPendingAsync() =>
        await _context.HorseTransfers.Where(t => t.Status == TransferStatus.Pending)
            .Include(t => t.Horse).Include(t => t.FromOwner).Include(t => t.ToOwner).ToListAsync();

    public async Task<IEnumerable<HorseTransfer>> GetAllAsync() =>
        await _context.HorseTransfers.Include(t => t.Horse).Include(t => t.FromOwner)
            .Include(t => t.ToOwner).ToListAsync();

    public async Task AddAsync(HorseTransfer t) => await _context.HorseTransfers.AddAsync(t);
    public Task UpdateAsync(HorseTransfer t) { _context.HorseTransfers.Update(t); return Task.CompletedTask; }
}

public class ContractRepository : IContractRepository
{
    private readonly ApplicationDbContext _context;
    public ContractRepository(ApplicationDbContext context) => _context = context;

    public async Task<Contract?> GetByIdAsync(Guid id) =>
        await _context.Contracts.Include(c => c.Owner).Include(c => c.Jockey)
            .Include(c => c.Horse).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IEnumerable<Contract>> GetByOwnerAsync(Guid ownerId) =>
        await _context.Contracts.Where(c => c.OwnerId == ownerId).Include(c => c.Jockey).ToListAsync();

    public async Task<IEnumerable<Contract>> GetByJockeyAsync(Guid jockeyId) =>
        await _context.Contracts.Where(c => c.JockeyId == jockeyId).Include(c => c.Owner).ToListAsync();

    public async Task<IEnumerable<Contract>> GetAllAsync() =>
        await _context.Contracts.Include(c => c.Owner).Include(c => c.Jockey).Include(c => c.Horse).ToListAsync();

    public async Task AddAsync(Contract c) => await _context.Contracts.AddAsync(c);
    public Task UpdateAsync(Contract c) { _context.Contracts.Update(c); return Task.CompletedTask; }
}

public class InjuryRecordRepository : IInjuryRecordRepository
{
    private readonly ApplicationDbContext _context;
    public InjuryRecordRepository(ApplicationDbContext context) => _context = context;

    public async Task<InjuryRecord?> GetByIdAsync(Guid id) =>
        await _context.InjuryRecords.Include(i => i.Horse).FirstOrDefaultAsync(i => i.Id == id);

    public async Task<IEnumerable<InjuryRecord>> GetByHorseAsync(Guid horseId) =>
        await _context.InjuryRecords.Where(i => i.HorseId == horseId).ToListAsync();

    public async Task<IEnumerable<InjuryRecord>> GetAllAsync() =>
        await _context.InjuryRecords.Include(i => i.Horse).ToListAsync();

    public async Task AddAsync(InjuryRecord r) => await _context.InjuryRecords.AddAsync(r);
    public Task UpdateAsync(InjuryRecord r) { _context.InjuryRecords.Update(r); return Task.CompletedTask; }
}
