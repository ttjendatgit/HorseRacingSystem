using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IPrizeRepository
{
    Task<Prize?> GetByIdAsync(Guid id);
    Task<IEnumerable<Prize>> GetByTournamentAsync(Guid tournamentId);
    Task<IEnumerable<Prize>> GetByRaceAsync(Guid raceId);
    Task<IEnumerable<Prize>> GetAllAsync();
    // PRIZE-V1: single projected existence/SUM queries for validation — avoids loading full Prize
    // rows just to check a duplicate Position or compute the allocated total.
    Task<bool> ExistsPositionAsync(Guid tournamentId, int position, Guid? excludePrizeId);
    Task<decimal> GetAllocatedAmountAsync(Guid tournamentId, Guid? excludePrizeId);
    // PRIZE-V1.2: percentage is now the source-of-truth allocation figure (Amount is derived) —
    // mirrors GetAllocatedAmountAsync's shape exactly.
    Task<decimal> GetAllocatedPercentageAsync(Guid tournamentId, Guid? excludePrizeId);
    Task AddAsync(Prize prize);
    Task UpdateAsync(Prize prize);
    Task DeleteAsync(Guid id);
}

public interface IProtestRepository
{
    Task<Protest?> GetByIdAsync(Guid id);
    Task<IEnumerable<Protest>> GetByRaceAsync(Guid raceId);
    Task<IEnumerable<Protest>> GetByFiledByUserAsync(Guid filedByUserId);
    Task<IEnumerable<Protest>> GetPendingAsync();
    Task<IEnumerable<Protest>> GetAllAsync();
    Task<bool> HasActiveByFilerRaceEntryAsync(Guid filedByUserId, Guid raceId, Guid againstEntryId);
    Task AddAsync(Protest protest);
    Task UpdateAsync(Protest protest);
}

public interface IRaceComplaintRepository
{
    Task<RaceComplaint?> GetByIdAsync(Guid id);
    Task<IEnumerable<RaceComplaint>> GetByRaceAsync(Guid raceId);
    Task<IEnumerable<RaceComplaint>> GetByFiledByUserAsync(Guid filedByUserId);
    Task<IEnumerable<RaceComplaint>> GetByAssignedRefereeUserAsync(Guid refereeUserId);
    Task<IEnumerable<RaceComplaint>> GetAllAsync();
    Task<bool> HasActiveByFilerRaceTypeAsync(Guid filedByUserId, Guid raceId, RaceComplaintType type);
    Task AddAsync(RaceComplaint complaint);
    Task UpdateAsync(RaceComplaint complaint);
}

public interface IHorseTransferRepository
{
    Task<HorseTransfer?> GetByIdAsync(Guid id);
    Task<IEnumerable<HorseTransfer>> GetByHorseAsync(Guid horseId);
    Task<IEnumerable<HorseTransfer>> GetPendingAsync();
    Task<IEnumerable<HorseTransfer>> GetAllAsync();
    Task AddAsync(HorseTransfer transfer);
    Task UpdateAsync(HorseTransfer transfer);
}

public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid id);
    Task<IEnumerable<Contract>> GetByOwnerAsync(Guid ownerId);
    Task<IEnumerable<Contract>> GetByJockeyAsync(Guid jockeyId);
    Task<IEnumerable<Contract>> GetAllAsync();
    Task AddAsync(Contract contract);
    Task UpdateAsync(Contract contract);
}

public interface IInjuryRecordRepository
{
    Task<InjuryRecord?> GetByIdAsync(Guid id);
    Task<IEnumerable<InjuryRecord>> GetByHorseAsync(Guid horseId);
    Task<IEnumerable<InjuryRecord>> GetAllAsync();
    Task AddAsync(InjuryRecord record);
    Task UpdateAsync(InjuryRecord record);
}
