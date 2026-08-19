using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

public interface IJockeyService
{
    Task<ServiceResult<object>> GetAvailableJockeysAsync(Guid currentUserId, bool includeUnapproved = false);
    Task<ServiceResult<object>> GetInvitationsAsync(Guid userId);
    Task<ServiceResult<object>> RespondInvitationAsync(Guid userId, Guid invitationId, JockeyInvitationRespondRequest request);
    Task<ServiceResult<object>> WithdrawInvitationAsync(Guid userId, Guid invitationId, JockeyInvitationWithdrawRequest request);
    Task<ServiceResult<object>> GetAssignedRacesAsync(Guid userId);
    Task<ServiceResult<object>> GetPendingRaceEntriesAsync(Guid userId);
    Task<ServiceResult<object>> ConfirmRaceEntryAsync(Guid userId, Guid entryId);
    Task<ServiceResult<object>> DeclineRaceEntryAsync(Guid userId, Guid entryId);
    Task<ServiceResult<object>> GetMyProfileAsync(Guid userId);
}
