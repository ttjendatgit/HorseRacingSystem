using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

public interface IHorseService
{
    Task<ServiceResult<object>> GetHorseAsync(Guid ownerId, Guid horseId);
    Task<ServiceResult<object>> GetMyHorsesAsync(Guid ownerId);
    Task<ServiceResult<object>> GetAllApprovedHorsesAsync();
    Task<ServiceResult<object>> CreateHorseAsync(Guid ownerId, HorseCreateRequest request);
    // Task C1 correction: Admin retains the pre-existing management capability the
    // [Authorize(Roles="HorseOwner,Admin")] attribute already declares — an Admin caller has no
    // Owner row of their own, so isAdmin=true bypasses ownership resolution/scoping entirely
    // (targets the Horse by id only) instead of requiring one.
    Task<ServiceResult<object>> UpdateHorseAsync(Guid ownerId, Guid horseId, HorseUpdateRequest request, bool isAdmin = false);
    Task<ServiceResult<string>> DeleteHorseAsync(Guid ownerId, Guid horseId, bool isAdmin = false);
    Task<ServiceResult<object>> InviteJockeyAsync(Guid ownerId, Guid horseId, JockeyInvitationCreateRequest request);
    Task<ServiceResult<string>> RemoveJockeyAsync(Guid ownerId, Guid horseId, Guid raceId, JockeyRemovalRequest request);
    Task<ServiceResult<object>> ConfirmOwnerAsync(Guid ownerId, Guid raceId, Guid entryId);
    // J3: Owner selects exactly one Accepted invitation as the official Jockey for a RaceEntry.
    Task<ServiceResult<object>> FinalConfirmJockeyAsync(Guid ownerId, Guid horseId, Guid raceId, OwnerFinalConfirmJockeyRequest request);
}
