using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HorseRacing.Services;

public class RaceService : IRaceService
{
    private readonly IRaceRepository _races;
    private readonly IRaceResultRepository _results;
    private readonly ITournamentRepository _tournaments;
    private readonly IRaceManagementService _raceManagement;

    public RaceService(IRaceRepository races, IRaceResultRepository results, ITournamentRepository tournaments, IRaceManagementService raceManagement)
    {
        _races = races;
        _results = results;
        _tournaments = tournaments;
        _raceManagement = raceManagement;
    }

    public async Task<ServiceResult<object>> GetRacesAsync()
    {
        var races = await _races.GetAllAsync();
        var summaries = races.Select(r => new RaceSummaryDto
        {
            Id = r.Id,
            Name = r.Name,
            TournamentId = r.TournamentId,
            ScheduledAt = r.ScheduledAt,
            Status = r.Status.ToString()
        }).ToList();

        return ServiceResult<object>.Ok(summaries);
    }

    public async Task<ServiceResult<object>> GetRaceAsync(System.Guid raceId)
    {
        var race = await _races.GetByIdWithEntriesAsync(raceId);
        if (race == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy cuộc đua");
        }

        return ServiceResult<object>.Ok(race);
    }

    public async Task<ServiceResult<object>> GetRaceResultAsync(System.Guid raceId)
    {
        var result = await _results.GetByRaceIdAsync(raceId);
        if (result == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy kết quả");
        }

        return ServiceResult<object>.Ok(new RaceResultResponse
        {
            RaceId = result.RaceId,
            WinningHorseId = result.WinningHorseId,
            TotalParticipants = result.TotalParticipants,
            WinnerFinishTime = result.WinnerFinishTime,
            RecordedAt = result.RecordedAt,
            ApprovedAt = result.ApprovedAt,
            ResultStatus = result.Status.ToString(),
            IsOfficial = result.Status == HorseRacing.Models.RaceResultStatus.Official,
            RejectedReason = result.RejectedReason,
            IsDisputed = result.IsDisputed,
            WinnerPurse = result.WinnerPurse,
            RankingsJson = result.RankingsJson,
            Notes = result.Notes
        });
    }

    public async Task<ServiceResult<object>> GetTournamentsAsync()
    {
        var tournaments = await _tournaments.GetAllWithRacesAsync();
        return ServiceResult<object>.Ok(tournaments);
    }

    public async Task<ServiceResult<bool>> ReleaseHorseAsync(Guid raceId, Guid horseId)
    {
        return await _raceManagement.ReleaseHorseAsync(raceId, horseId);
    }
}
