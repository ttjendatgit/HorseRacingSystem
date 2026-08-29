using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

public interface IRaceService
{
    Task<ServiceResult<object>> GetRacesAsync();
    Task<ServiceResult<object>> GetRaceAsync(Guid raceId);
    Task<ServiceResult<object>> GetRaceResultAsync(Guid raceId);
    Task<ServiceResult<object>> GetTournamentsAsync();

    // Race Registration Management
    Task<ServiceResult<bool>> ReleaseHorseAsync(Guid raceId, Guid horseId);

    // Q2 (Phase 2, read-only): Tournament-level final standings, derived from whichever Round
    // actually decided the Tournament (Final, or an earlier walkover/void Round).
    Task<ServiceResult<FinalStandingsDto>> GetFinalStandingsAsync(Guid tournamentId);
}
