using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

    /// <summary>
    /// Lấy dữ liệu diễn biến kết quả thi đấu trực tiếp (Live Race Stream) của cuộc đua đang diễn ra.
    /// </summary>
    /// <param name="raceId">Mã định danh cuộc đua.</param>
    /// <returns>Dữ liệu thời gian trực tiếp và vị trí tạm thời của các ngựa đua.</returns>
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

            var entriesList = entries.ToList();
            var scores = entriesList.Select(e =>
            {
                decimal hRate = e.Horse != null && e.Horse.TotalRaces > 0 ? ((decimal)e.Horse.TotalWins / e.Horse.TotalRaces) * 100m : 10.0m;
                decimal jRate = e.Jockey != null && e.Jockey.WinRate > 0 ? e.Jockey.WinRate : 10.0m;
                decimal sc = (hRate * 0.70m) + (jRate * 0.30m);
                return sc <= 0m ? 0.01m : sc;
            }).ToList();
            decimal totalScore = scores.Sum();

            var response = new LiveRaceResultResponse
            {
                RaceId = race.Id,
                RaceName = race.Name,
                Status = race.Status.ToString(),
                ActualStartTime = race.ActualStartTime,
                TotalParticipants = entriesList.Count,
                FinishedCount = entriesList.Count(e => e != null),
                TimingData = new RaceTimingData
                {
                    StartTime = race.ActualStartTime,
                    EndTime = race.ActualEndTime,
                    Duration = race.ActualEndTime.HasValue && race.ActualStartTime.HasValue
                        ? (race.ActualEndTime.Value - race.ActualStartTime.Value).TotalSeconds
                        : null
                },
                CurrentPositions = entriesList.Select((e, i) => new CurrentPositionData
                {
                    Position = i + 1,
                    HorseId = e.HorseId,
                    HorseName = e.Horse?.Name ?? "Không xác định",
                    JockeyId = e.JockeyId,
                    JockeyName = e.Jockey?.User?.FullName,
                    Status = e.Status.ToString(),
                    TimeTaken = null,
                    Odds = e.Odds,
                    ProbabilityPercent = totalScore > 0 ? Math.Round((scores[i] / totalScore) * 100m, 1) : 0m
                }).ToArray()
            };

            return ServiceResult<LiveRaceResultResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<LiveRaceResultResponse>.Error($"Lỗi lấy kết quả trực tiếp: {ex.Message}", 500);
        }
    }

    /// <summary>
    /// Lấy bảng xếp hạng điểm và thứ hạng về đích của các ngựa tham gia cuộc đua.
    /// </summary>
    /// <param name="raceId">Mã định danh cuộc đua.</param>
    /// <returns>Bảng xếp hạng thứ hạng và thời gian về đích.</returns>
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

            var entriesList = entries.ToList();
            var scores = entriesList.Select(e =>
            {
                decimal hRate = e.Horse != null && e.Horse.TotalRaces > 0 ? ((decimal)e.Horse.TotalWins / e.Horse.TotalRaces) * 100m : 10.0m;
                decimal jRate = e.Jockey != null && e.Jockey.WinRate > 0 ? e.Jockey.WinRate : 10.0m;
                decimal sc = (hRate * 0.70m) + (jRate * 0.30m);
                return sc <= 0m ? 0.01m : sc;
            }).ToList();
            decimal totalScore = scores.Sum();

            var rankings = entriesList
                .Select((e, i) => new RankingEntry
                {
                    Position = i + 1,
                    HorseId = e.HorseId,
                    HorseName = e.Horse?.Name ?? "Không xác định",
                    JockeyId = e.JockeyId,
                    JockeyName = e.Jockey?.User?.FullName,
                    TimeTaken = null,
                    Won = result?.WinningHorseId == e.HorseId,
                    Odds = e.Odds,
                    ProbabilityPercent = totalScore > 0 ? Math.Round((scores[i] / totalScore) * 100m, 1) : 0m
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
            var entriesList = entries.ToList();
            var scores = entriesList.Select(e =>
            {
                decimal hRate = e.Horse != null && e.Horse.TotalRaces > 0 ? ((decimal)e.Horse.TotalWins / e.Horse.TotalRaces) * 100m : 10.0m;
                decimal jRate = e.Jockey != null && e.Jockey.WinRate > 0 ? e.Jockey.WinRate : 10.0m;
                decimal sc = (hRate * 0.70m) + (jRate * 0.30m);
                return sc <= 0m ? 0.01m : sc;
            }).ToList();
            decimal totalScore = scores.Sum();

            var positions = entriesList
                .Select((e, i) => new CurrentPositionData
                {
                    Position = i + 1,
                    HorseId = e.HorseId,
                    HorseName = e.Horse?.Name ?? "Không xác định",
                    JockeyId = e.JockeyId,
                    JockeyName = e.Jockey?.User?.FullName,
                    Status = e.Status.ToString(),
                    TimeTaken = null,
                    Odds = e.Odds,
                    ProbabilityPercent = totalScore > 0 ? Math.Round((scores[i] / totalScore) * 100m, 1) : 0m
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
    /// R0: the submitted Rankings[] is the single source of truth for the
    /// result — WinningHorseId is derived from Rankings[Position == 1], never
    /// an independently-editable second source (see ValidateAndCanonicalize).
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

            if (race.Status != RaceStatus.Finished)
            {
                return ServiceResult<bool>.Error($"Không thể nộp kết quả cho cuộc đua với trạng thái '{race.Status}'. Cuộc đua phải đã kết thúc.", 400);
            }

            var existingResult = await _raceResultRepo.GetByRaceIdAsync(raceId);
            if (existingResult != null && existingResult.Status == RaceResultStatus.Official)
            {
                return ServiceResult<bool>.Error("Kết quả đã chính thức (Official) và không thể nộp lại qua đường thông thường.", 400);
            }

            var participants = await _entryRepo.GetByRaceAsync(raceId);
            var validationError = ValidateAndCanonicalizeRankings(request.Rankings, participants, out var canonical);
            if (validationError != null)
            {
                return ServiceResult<bool>.Error(validationError, 400);
            }

            // Positions are validated continuous 1..N above, so the first
            // item after ascending sort is always exactly Position 1.
            var canonicalWinnerId = canonical[0].HorseId;
            if (request.WinningHorseId.HasValue && request.WinningHorseId.Value != canonicalWinnerId)
            {
                return ServiceResult<bool>.Error(
                    "WinningHorseId không khớp với ngựa xếp vị trí 1 trong Rankings. Bảng xếp hạng là nguồn xác định người thắng cuộc duy nhất.",
                    400);
            }

            var rankingsJson = JsonSerializer.Serialize(canonical);

            if (existingResult != null)
            {
                // Resubmission: remains Provisional, clears rejection metadata.
                existingResult.WinningHorseId = canonicalWinnerId;
                existingResult.RankingsJson = rankingsJson;
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
                WinningHorseId = canonicalWinnerId,
                RankingsJson = rankingsJson,
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

    /// <summary>
    /// R0 full-ranking validation. A submitted result must cover every
    /// current RaceEntry for this Race exactly once, with continuous
    /// 1..N positions — there is no DNS/DNF/DQ classification implemented
    /// yet, so partial rankings are rejected rather than given invented
    /// semantics. Returns null (canonical populated, sorted ascending by
    /// Position) on success, or a Vietnamese business-error message on
    /// failure — never lets a DB exception stand in for validation.
    /// </summary>
    private static string? ValidateAndCanonicalizeRankings(
        List<RaceResultRankingItemRequest>? rankings,
        List<RaceEntry> participants,
        out List<RaceResultRankingItemRequest> canonical)
    {
        canonical = new List<RaceResultRankingItemRequest>();

        if (rankings == null || rankings.Count == 0)
        {
            return "Bảng xếp hạng không được để trống.";
        }

        var participantIds = participants.Select(p => p.HorseId).ToHashSet();
        var seenHorseIds = new HashSet<Guid>();
        var seenPositions = new HashSet<int>();

        foreach (var item in rankings)
        {
            if (!participantIds.Contains(item.HorseId))
            {
                return $"Ngựa {item.HorseId} không thuộc danh sách tham gia cuộc đua này.";
            }
            if (item.Position <= 0)
            {
                return "Vị trí xếp hạng phải là số nguyên dương.";
            }
            if (!seenHorseIds.Add(item.HorseId))
            {
                return "Một ngựa không được xuất hiện nhiều hơn một lần trong bảng xếp hạng.";
            }
            if (!seenPositions.Add(item.Position))
            {
                return "Một vị trí không được gán cho nhiều hơn một ngựa.";
            }
        }

        if (rankings.Count != participants.Count)
        {
            return "Bảng xếp hạng phải bao gồm đầy đủ và chỉ những ngựa tham gia cuộc đua này.";
        }

        if (!seenPositions.SetEquals(Enumerable.Range(1, rankings.Count)))
        {
            return "Vị trí xếp hạng phải liên tục từ 1 đến hết, không được có khoảng trống.";
        }

        canonical = rankings.OrderBy(r => r.Position).ToList();
        return null;
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
