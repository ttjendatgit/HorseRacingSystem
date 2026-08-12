using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

public interface IRaceEntryService
{
    Task<ServiceResult<object>> RegisterAsync(Guid userId, Guid horseId, Guid raceId, RaceRegistrationRequest request);
    Task<ServiceResult<bool>> ApproveAsync(Guid entryId);
    Task<ServiceResult<bool>> RejectAsync(Guid entryId, string? reason);
    Task<ServiceResult<bool>> ValidateRaceEntriesForStartAsync(Guid raceId);
}
