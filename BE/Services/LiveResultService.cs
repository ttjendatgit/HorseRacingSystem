using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;

namespace HorseRacing.Services;

public class LiveResultService : ILiveResultService
{
    private readonly IRaceRepository _raceRepo;
    private readonly IRaceEntryRepository _entryRepo;
    private readonly IHorseRepository _horseRepo;
    private readonly IJockeyRepository _jockeyRepo;
    private readonly IRaceManagementRepository _raceManagementRepo;
    private readonly IRaceResultRepository _raceResultRepo;
    private readonly IPredictionService _predictionService;
    private readonly IUnitOfWork _unitOfWork;

    public LiveResultService(
        IRaceRepository raceRepo,
        IRaceEntryRepository entryRepo,
        IHorseRepository horseRepo,
        IJockeyRepository jockeyRepo,
        IRaceManagementRepository raceManagementRepo,
        IRaceResultRepository raceResultRepo,
        IPredictionService predictionService,
        IUnitOfWork unitOfWork)
    {
        _raceRepo = raceRepo;
        _entryRepo = entryRepo;
        _horseRepo = horseRepo;
        _jockeyRepo = jockeyRepo;
        _raceManagementRepo = raceManagementRepo;
        _raceResultRepo = raceResultRepo;
        _predictionService = predictionService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<LiveRaceResultResponse>> GetLiveRaceResultAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
            {
                return ServiceResult<LiveRaceResultResponse>.Error("Không tìm thấy cuộc đua", 404);
            }

            var entries = await _raceManagementRepo.GetRaceEntriesAsync(raceId);
            var result = await _raceManagementRepo.GetRaceResultAsync(raceId);

            var response = new LiveRaceResultResponse
            {
                RaceId = race.Id,
                RaceName = race.Name,
                Status = race.Status.ToString(),
                ActualStartTime = race.ActualStartTime,
                TotalParticipants = entries.Count(),
                FinishedCount = entries.Count(e => e != null), // In real scenario, check status
                TimingData = new RaceTimingData
                {
                    StartTime = race.ActualStartTime,
                    EndTime = race.ActualEndTime,
                    Duration = race.ActualEndTime.HasValue && race.ActualStartTime.HasValue
                        ? (race.ActualEndTime.Value - race.ActualStartTime.Value).TotalSeconds
                        : null
                },
                CurrentPositions = entries.Select((e, i) => new CurrentPositionData
                {
                    Position = i + 1,
                    HorseId = e.HorseId,
                    HorseName = e.Horse?.Name ?? "Không xác định",
                    JockeyId = e.JockeyId,
                    JockeyName = e.Jockey?.User?.FullName,
                    Status = e.Status.ToString(),
                    TimeTaken = null // In real scenario, calculate from tracking data
                }).ToArray()
            };

            return ServiceResult<LiveRaceResultResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<LiveRaceResultResponse>.Error($"Lỗi lấy kết quả trực tiếp: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<RaceRankingResponse>> GetRaceRankingAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
            {
                return ServiceResult<RaceRankingResponse>.Error("Không tìm thấy cuộc đua", 404);
            }

            var entries = await _raceManagementRepo.GetRaceEntriesAsync(raceId);
            var result = await _raceManagementRepo.GetRaceResultAsync(raceId);

            var rankings = entries
                .Select((e, i) => new RankingEntry
                {
                    Position = i + 1,
                    HorseId = e.HorseId,
                    HorseName = e.Horse?.Name ?? "Không xác định",
                    JockeyId = e.JockeyId,
                    JockeyName = e.Jockey?.User?.FullName,
                    TimeTaken = null, // Calculate from tracking
                    Won = result?.WinningHorseId == e.HorseId
                })
                .OrderByDescending(r => r.Won)
                .ThenBy(r => r.Position)
                .ToArray();

            var response = new RaceRankingResponse
            {
                RaceId = raceId,
                RaceName = race.Name,
                RaceDate = race.ScheduledAt,
                ResultStatus = result?.Status.ToString(),
                Rankings = rankings
            };

            return ServiceResult<RaceRankingResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<RaceRankingResponse>.Error($"Lỗi lấy bảng xếp hạng: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<IEnumerable<CurrentPositionData>>> GetCurrentPositionsAsync(Guid raceId)
    {
        try
        {
            var entries = await _raceManagementRepo.GetRaceEntriesAsync(raceId);

            var positions = entries
                .Select((e, i) => new CurrentPositionData
                {
                    Position = i + 1,
                    HorseId = e.HorseId,
                    HorseName = e.Horse?.Name ?? "Không xác định",
                    JockeyId = e.JockeyId,
                    JockeyName = e.Jockey?.User?.FullName,
                    Status = e.Status.ToString(),
                    TimeTaken = null
                })
                .ToList();

            return ServiceResult<IEnumerable<CurrentPositionData>>.Success(positions);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<CurrentPositionData>>.Error(
                $"Lỗi lấy vị trí hiện tại: {ex.Message}", 500);
        }
    }

    /// <summary>
    /// Handles both first submission and resubmission of a Provisional result.
    /// Phase2B: gated purely on RaceStatus == Finished (event-progress) and
    /// RaceResultStatus (result-lifecycle). Never mutates Race.Status. Cannot
    /// be used to edit an Official result.
    /// </summary>
    public async Task<ServiceResult<bool>> UpdateRaceResultAsync(Guid raceId, RaceResultRequest request)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
            {
                return ServiceResult<bool>.Error("Không tìm thấy cuộc đua", 404);
            }

            // Validate that the winning horse is actually in this race
            var isHorseInRace = await _entryRepo.ExistsAsync(raceId, request.WinningHorseId);
            if (!isHorseInRace)
            {
                return ServiceResult<bool>.Error("Ngựa thắng không có trong danh sách tham gia cuộc đua này", 400);
            }

            if (race.Status != RaceStatus.Finished)
            {
                return ServiceResult<bool>.Error($"Không thể nộp kết quả cho cuộc đua với trạng thái '{race.Status}'. Cuộc đua phải đã kết thúc.", 400);
            }

            var existingResult = await _raceResultRepo.GetByRaceIdAsync(raceId);

            if (existingResult != null)
            {
                if (existingResult.Status == RaceResultStatus.Official)
                {
                    return ServiceResult<bool>.Error("Kết quả đã chính thức (Official) và không thể nộp lại qua đường thông thường.", 400);
                }

                // Resubmission: remains Provisional, clears rejection metadata.
                existingResult.WinningHorseId = request.WinningHorseId;
                existingResult.Notes = request.Notes;
                existingResult.RecordedAt = DateTime.UtcNow;
                existingResult.Status = RaceResultStatus.Provisional;
                existingResult.RejectedReason = null;
                await _raceResultRepo.UpdateAsync(existingResult);
                await _unitOfWork.SaveChangesAsync();
                return ServiceResult<bool>.Success(true);
            }

            // First submission
            var result = new RaceResult
            {
                Id = Guid.NewGuid(),
                RaceId = raceId,
                WinningHorseId = request.WinningHorseId,
                RecordedAt = DateTime.UtcNow,
                Status = RaceResultStatus.Provisional,
                RejectedReason = null,
                Notes = request.Notes
            };

            await _raceResultRepo.AddAsync(result);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Error($"Lỗi cập nhật kết quả cuộc đua: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<bool>> UpdateParticipantStatusAsync(Guid raceId, Guid horseId, string status)
    {
        try
        {
            var entry = await _entryRepo.GetByRaceAndHorseAsync(raceId, horseId);
            if (entry == null)
            {
                return ServiceResult<bool>.Error("Không tìm thấy đăng ký tham gia", 404);
            }

            entry.Status = Enum.Parse<RegistrationStatus>(status);
            await _entryRepo.UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Error($"Lỗi cập nhật trạng thái người tham gia: {ex.Message}", 500);
        }
    }
}
