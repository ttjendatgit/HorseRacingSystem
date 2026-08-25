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

    // PRIZE-V1: ordered by Position ascending — the Admin allocation table and public Tournament-
    // detail breakdown both render Hạng 1/2/3... in order and rely on this, not a client-side sort.
    public async Task<IEnumerable<Prize>> GetByTournamentAsync(Guid tournamentId) =>
        await _context.Prizes.Where(p => p.TournamentId == tournamentId).OrderBy(p => p.Position).ToListAsync();

    public async Task<IEnumerable<Prize>> GetByRaceAsync(Guid raceId) =>
        await _context.Prizes.Where(p => p.RaceId == raceId).ToListAsync();

    public async Task<IEnumerable<Prize>> GetAllAsync() =>
        await _context.Prizes.ToListAsync();

    public async Task<bool> ExistsPositionAsync(Guid tournamentId, int position, Guid? excludePrizeId)
    {
        var query = _context.Prizes.Where(p => p.TournamentId == tournamentId && p.Position == position);
        if (excludePrizeId.HasValue)
            query = query.Where(p => p.Id != excludePrizeId.Value);
        return await query.AnyAsync();
    }

    public async Task<decimal> GetAllocatedAmountAsync(Guid tournamentId, Guid? excludePrizeId)
    {
        var query = _context.Prizes.Where(p => p.TournamentId == tournamentId);
        if (excludePrizeId.HasValue)
            query = query.Where(p => p.Id != excludePrizeId.Value);
        return await query.SumAsync(p => (decimal?)p.Amount) ?? 0m;
    }

    public async Task<decimal> GetAllocatedPercentageAsync(Guid tournamentId, Guid? excludePrizeId)
    {
        var query = _context.Prizes.Where(p => p.TournamentId == tournamentId);
        if (excludePrizeId.HasValue)
            query = query.Where(p => p.Id != excludePrizeId.Value);
        return await query.SumAsync(p => (decimal?)p.PercentageOfPool) ?? 0m;
    }

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
        await BaseQuery().Where(p => p.RaceId == raceId).ToListAsync();

    public async Task<IEnumerable<Protest>> GetByFiledByUserAsync(Guid filedByUserId) =>
        await BaseQuery()
            .Where(p => p.FiledByUserId == filedByUserId)
            .OrderByDescending(p => p.FiledAt)
            .ToListAsync();

    public async Task<IEnumerable<Protest>> GetPendingAsync() =>
        await BaseQuery()
            .Where(p => p.Status == ProtestStatus.Pending || p.Status == ProtestStatus.UnderReview)
            .OrderByDescending(p => p.FiledAt)
            .ToListAsync();

    public async Task<IEnumerable<Protest>> GetAllAsync() =>
        await BaseQuery().OrderByDescending(p => p.FiledAt).ToListAsync();

    public async Task<bool> HasActiveByFilerRaceEntryAsync(Guid filedByUserId, Guid raceId, Guid againstEntryId) =>
        await _context.Protests.AnyAsync(p =>
            p.FiledByUserId == filedByUserId &&
            p.RaceId == raceId &&
            p.AgainstEntryId == againstEntryId &&
            (p.Status == ProtestStatus.Pending || p.Status == ProtestStatus.UnderReview));

    public async Task AddAsync(Protest protest) => await _context.Protests.AddAsync(protest);
    public Task UpdateAsync(Protest protest) { _context.Protests.Update(protest); return Task.CompletedTask; }

    private IQueryable<Protest> BaseQuery() =>
        _context.Protests
            .Include(p => p.FiledByUser)
            .Include(p => p.Race)
            .Include(p => p.AgainstEntry)
                .ThenInclude(e => e!.Horse);
}

public class RaceComplaintRepository : IRaceComplaintRepository
{
    private readonly ApplicationDbContext _context;
    public RaceComplaintRepository(ApplicationDbContext context) => _context = context;

    public async Task<RaceComplaint?> GetByIdAsync(Guid id) =>
        await BaseQuery().FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IEnumerable<RaceComplaint>> GetByRaceAsync(Guid raceId) =>
        await BaseQuery().Where(c => c.RaceId == raceId).ToListAsync();

    public async Task<IEnumerable<RaceComplaint>> GetByFiledByUserAsync(Guid filedByUserId) =>
        await BaseQuery()
            .Where(c => c.FiledByUserId == filedByUserId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<RaceComplaint>> GetByAssignedRefereeUserAsync(Guid refereeUserId) =>
        await BaseQuery()
            .Where(c =>
                c.AssignedRefereeAssignment != null &&
                c.AssignedRefereeAssignment.Referee != null &&
                c.AssignedRefereeAssignment.Referee.UserId == refereeUserId)
            .OrderByDescending(c => c.ResponseRequestedAt ?? c.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<RaceComplaint>> GetAllAsync() =>
        await BaseQuery().OrderByDescending(c => c.CreatedAt).ToListAsync();

    public async Task<bool> HasActiveByFilerRaceTypeAsync(Guid filedByUserId, Guid raceId, RaceComplaintType type) =>
        await _context.RaceComplaints.AnyAsync(c =>
            c.FiledByUserId == filedByUserId &&
            c.RaceId == raceId &&
            c.Type == type &&
            (c.Status == RaceComplaintStatus.Pending ||
             c.Status == RaceComplaintStatus.AwaitingRefereeResponse ||
             c.Status == RaceComplaintStatus.UnderReview));

    public async Task AddAsync(RaceComplaint complaint) => await _context.RaceComplaints.AddAsync(complaint);
    public Task UpdateAsync(RaceComplaint complaint) { _context.RaceComplaints.Update(complaint); return Task.CompletedTask; }

    private IQueryable<RaceComplaint> BaseQuery() =>
        _context.RaceComplaints
            .Include(c => c.FiledByUser)
            .Include(c => c.RuledByUser)
            .Include(c => c.Race)
                .ThenInclude(r => r!.Tournament)
            .Include(c => c.Race)
                .ThenInclude(r => r!.Result)
                    .ThenInclude(rr => rr!.WinningHorse)
            .Include(c => c.Race)
                .ThenInclude(r => r!.RefereeAssignments)
                    .ThenInclude(a => a.Referee)
                        .ThenInclude(r => r!.User)
            .Include(c => c.AssignedRefereeAssignment)
                .ThenInclude(a => a!.Referee)
                    .ThenInclude(r => r!.User);
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
