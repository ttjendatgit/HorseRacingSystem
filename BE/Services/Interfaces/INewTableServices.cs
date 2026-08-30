using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using Microsoft.AspNetCore.Http;

namespace HorseRacing.Services.Interfaces;

public interface IPrizeService
{
    Task<ServiceResult<PrizeResponse>> CreateAsync(CreatePrizeRequest request);
    Task<ServiceResult<PrizeResponse>> UpdateAsync(Guid id, UpdatePrizeRequest request);
    Task<ServiceResult<IEnumerable<PrizeResponse>>> GetByTournamentAsync(Guid tournamentId);
    Task<ServiceResult<IEnumerable<PrizeResponse>>> GetByRaceAsync(Guid raceId);
    Task<ServiceResult<IEnumerable<PrizeResponse>>> GetAllAsync();
    Task<ServiceResult<bool>> DeleteAsync(Guid id);

    // Manual, Admin-triggered payout of already-computed Prize.Amount rows to the Owner standing
    // at each Position in the Tournament's final standings (RaceService.GetFinalStandingsAsync).
    Task<ServiceResult<PrizeDistributionResultDto>> DistributeAsync(Guid tournamentId);

    // Owner-facing "lịch sử nhận thưởng" — reads PrizeDistributionLog (the only successful-payout
    // audit trail; Prize itself carries no OwnerId), newest first.
    Task<ServiceResult<List<PrizeHistoryEntryDto>>> GetMyPrizeHistoryAsync(Guid ownerUserId);

    // Jockey-facing "lịch sử nhận thưởng" — reads PrizeDistributionLog filtered by JockeyUserId.
    Task<ServiceResult<List<JockeyPrizeHistoryEntryDto>>> GetMyJockeyPrizeHistoryAsync(Guid jockeyUserId);
}

public interface IProtestService
{
    Task<ServiceResult<ProtestResponse>> FileAsync(CreateProtestRequest request, Guid filedByUserId);
    Task<ServiceResult<IEnumerable<ProtestResponse>>> GetPendingAsync();
    Task<ServiceResult<IEnumerable<ProtestResponse>>> GetAllAsync();
    Task<ServiceResult<IEnumerable<ProtestResponse>>> GetByFiledByUserAsync(Guid filedByUserId);
    Task<ServiceResult<ProtestResponse>> MarkUnderReviewAsync(Guid id, Guid reviewedByUserId);
    Task<ServiceResult<ProtestResponse>> RuleAsync(Guid id, RuleProtestRequest request, Guid ruledByUserId);
    Task<ServiceResult<ProtestResponse>> WithdrawAsync(Guid id, Guid requestingUserId);
}

public interface IRaceComplaintService
{
    Task<ServiceResult<RaceComplaintResponse>> FileAsync(CreateRaceComplaintRequest request, Guid filedByUserId);
    Task<ServiceResult<IEnumerable<RaceComplaintResponse>>> GetAllAsync(RaceComplaintStatus? status = null);
    Task<ServiceResult<IEnumerable<RaceComplaintResponse>>> GetByFiledByUserAsync(Guid filedByUserId);
    Task<ServiceResult<IEnumerable<RaceComplaintResponse>>> GetForRefereeAsync(Guid refereeUserId);
    Task<ServiceResult<IEnumerable<RaceComplaintEligibleRaceResponse>>> GetEligibleRacesAsync(Guid userId);
    Task<ServiceResult<RaceComplaintResponse>> RouteAsync(Guid id, RouteRaceComplaintRequest request, Guid adminUserId);
    Task<ServiceResult<RaceComplaintResponse>> RespondAsync(Guid id, RespondRaceComplaintRequest request, Guid refereeUserId);
    Task<ServiceResult<RaceComplaintResponse>> RuleAsync(Guid id, RuleRaceComplaintRequest request, Guid ruledByUserId);
    Task<ServiceResult<RaceComplaintResponse>> WithdrawAsync(Guid id, Guid requestingUserId);
    Task<ServiceResult<RaceComplaintEvidenceResponse>> UploadEvidenceAsync(Guid id, IFormFile file, Guid uploaderUserId);
    Task<ServiceResult<bool>> DeleteEvidenceAsync(Guid id, Guid evidenceId, Guid requestingUserId);
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
