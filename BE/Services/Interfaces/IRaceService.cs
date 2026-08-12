using System;
using System.Threading.Tasks;

namespace HorseRacing.Services.Interfaces;

public interface IRaceService
{
    Task<ServiceResult<object>> GetRacesAsync();
    Task<ServiceResult<object>> GetRaceAsync(Guid raceId);
    Task<ServiceResult<object>> GetRaceResultAsync(Guid raceId);
    Task<ServiceResult<object>> GetTournamentsAsync();

    // Race Registration Management
    Task<ServiceResult<bool>> ReleaseHorseAsync(Guid raceId, Guid horseId);
}
