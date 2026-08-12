using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

public interface IPrizeService
{
    Task<ServiceResult<PrizeResponse>> CreateAsync(CreatePrizeRequest request);
    Task<ServiceResult<IEnumerable<PrizeResponse>>> GetByTournamentAsync(Guid tournamentId);
    Task<ServiceResult<IEnumerable<PrizeResponse>>> GetByRaceAsync(Guid raceId);
    Task<ServiceResult<IEnumerable<PrizeResponse>>> GetAllAsync();
    Task<ServiceResult<bool>> DeleteAsync(Guid id);
}

public interface IProtestService
{
    Task<ServiceResult<ProtestResponse>> FileAsync(CreateProtestRequest request, Guid filedByUserId);
    Task<ServiceResult<IEnumerable<ProtestResponse>>> GetPendingAsync();
    Task<ServiceResult<IEnumerable<ProtestResponse>>> GetAllAsync();
    Task<ServiceResult<ProtestResponse>> RuleAsync(Guid id, RuleProtestRequest request, Guid ruledByUserId);
}

public interface IHorseTransferService
{
    Task<ServiceResult<HorseTransferResponse>> CreateAsync(CreateHorseTransferRequest request, Guid fromOwnerId);
    Task<ServiceResult<IEnumerable<HorseTransferResponse>>> GetPendingAsync();
    Task<ServiceResult<IEnumerable<HorseTransferResponse>>> GetAllAsync();
    Task<ServiceResult<HorseTransferResponse>> ApproveAsync(Guid id, ApproveHorseTransferRequest request, Guid approvedByUserId);
    Task<ServiceResult<HorseTransferResponse>> RejectAsync(Guid id, string reason, Guid approvedByUserId);
}

public interface IContractService
{
    Task<ServiceResult<ContractResponse>> CreateAsync(CreateContractRequest request);
    Task<ServiceResult<ContractResponse>> SignByOwnerAsync(Guid id, Guid ownerId);
    Task<ServiceResult<ContractResponse>> SignByJockeyAsync(Guid id, Guid jockeyId);
    Task<ServiceResult<IEnumerable<ContractResponse>>> GetByOwnerAsync(Guid ownerId);
    Task<ServiceResult<IEnumerable<ContractResponse>>> GetByJockeyAsync(Guid jockeyId);
    Task<ServiceResult<IEnumerable<ContractResponse>>> GetAllAsync();
}

public interface IInjuryRecordService
{
    Task<ServiceResult<InjuryRecordResponse>> CreateAsync(CreateInjuryRecordRequest request, Guid reportedByUserId);
    Task<ServiceResult<IEnumerable<InjuryRecordResponse>>> GetByHorseAsync(Guid horseId);
    Task<ServiceResult<IEnumerable<InjuryRecordResponse>>> GetAllAsync();
    Task<ServiceResult<InjuryRecordResponse>> MarkRecoveredAsync(Guid id);
    Task<ServiceResult<InjuryRecordResponse>> ClearToRaceAsync(Guid id);
}
