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
    Task AddAsync(Prize prize);
    Task UpdateAsync(Prize prize);
    Task DeleteAsync(Guid id);
}

public interface IProtestRepository
{
    Task<Protest?> GetByIdAsync(Guid id);
    Task<IEnumerable<Protest>> GetByRaceAsync(Guid raceId);
    Task<IEnumerable<Protest>> GetPendingAsync();
    Task<IEnumerable<Protest>> GetAllAsync();
    Task AddAsync(Protest protest);
    Task UpdateAsync(Protest protest);
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
