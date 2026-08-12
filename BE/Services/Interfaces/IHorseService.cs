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
    Task<ServiceResult<object>> UpdateHorseAsync(Guid ownerId, Guid horseId, HorseUpdateRequest request);
    Task<ServiceResult<string>> DeleteHorseAsync(Guid ownerId, Guid horseId);
    Task<ServiceResult<object>> InviteJockeyAsync(Guid ownerId, Guid horseId, JockeyInvitationCreateRequest request);
    Task<ServiceResult<string>> RemoveJockeyAsync(Guid ownerId, Guid horseId, Guid raceId, JockeyRemovalRequest request);
    Task<ServiceResult<object>> ConfirmOwnerAsync(Guid ownerId, Guid raceId, Guid entryId);
}
