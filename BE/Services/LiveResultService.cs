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
    /// Lưu bảng xếp hạng dựa trên thời gian thực tế (TimeTaken) và trạng thái (Status).
    /// Hỗ trợ Đồng hạng (Dead Heat) và các ngựa bỏ cuộc/bị loại (DNF/DSQ).
    /// </summary>
    public async Task<ServiceResult<bool>> UpdateRaceResultAsync(Guid raceId, SubmitRaceResultRequest request)
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

            // Tự động tìm ngựa Hạng 1 (Phải là ngựa Completed)
            var winner = canonical.FirstOrDefault(r => r.Position == 1 && r.Status == "Completed");

            // Fallback: Nếu tất cả đều DNF/DSQ (rất hiếm), lấy con đầu tiên trong mảng để không lỗi DB (do WinningHorseId là bắt buộc)
            var canonicalWinnerId = winner != null ? winner.HorseId : canonical[0].HorseId;
            var winnerTime = winner?.TimeTaken;

            var rankingsJson = JsonSerializer.Serialize(canonical);

            if (existingResult != null)
            {
                // Nộp lại (Resubmission)
                existingResult.WinningHorseId = canonicalWinnerId;
                existingResult.WinnerFinishTime = winnerTime.HasValue ? (decimal)winnerTime.Value : null;
                existingResult.RankingsJson = rankingsJson;
                existingResult.RecordedAt = DateTime.UtcNow;
                existingResult.Status = RaceResultStatus.Provisional;
                existingResult.RejectedReason = null;

                await _raceResultRepo.UpdateAsync(existingResult);
                await _unitOfWork.SaveChangesAsync();
                return ServiceResult<bool>.Success(true);
            }

            // Nộp lần đầu (First submission)
            var result = new RaceResult
            {
                Id = Guid.NewGuid(),
                RaceId = raceId,
                WinningHorseId = canonicalWinnerId,
                WinnerFinishTime = winnerTime.HasValue ? (decimal)winnerTime.Value : null,
                RankingsJson = rankingsJson,
                RecordedAt = DateTime.UtcNow,
                Status = RaceResultStatus.Provisional,
                RejectedReason = null
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

    private static readonly HashSet<string> AllowedRankingStatuses = new() { "Completed", "DNF", "DSQ" };

    /// <summary>
    /// Validation cho luồng mới:
    /// - Cho phép đồng hạng (trùng Position).
    /// - Cho phép nhảy cóc thứ hạng và gán hạng 99 (cho DNF/DSQ).
    /// </summary>
    private static string? ValidateAndCanonicalizeRankings(
        List<SubmitRankingEntry>? rankings,
        List<RaceEntry> participants,
        out List<SubmitRankingEntry> canonical)
    {
        canonical = new List<SubmitRankingEntry>();

        if (rankings == null || rankings.Count == 0)
        {
            return "Bảng xếp hạng không được để trống.";
        }

        var participantIds = participants.Select(p => p.HorseId).ToHashSet();
        var seenHorseIds = new HashSet<Guid>();

        foreach (var item in rankings)
        {
            if (!participantIds.Contains(item.HorseId))
            {
                return $"Ngựa {item.HorseId} không thuộc danh sách tham gia cuộc đua này.";
            }
            if (item.Position <= 0)
            {
                return "Vị trí xếp hạng phải là số dương.";
            }
            if (!seenHorseIds.Add(item.HorseId))
            {
                return "Một con ngựa không được xuất hiện nhiều hơn một lần trong kết quả.";
            }
            if (!AllowedRankingStatuses.Contains(item.Status))
            {
                return $"Trạng thái \"{item.Status}\" không hợp lệ. Chỉ chấp nhận Completed, DNF, hoặc DSQ.";
            }
        }

        if (rankings.Count != participants.Count)
        {
            return "Bảng xếp hạng phải bao gồm đầy đủ tất cả các ngựa tham gia.";
        }

        // Sắp xếp: Ưu tiên Position nhỏ xếp trước, nếu cùng Position thì ưu tiên Thời gian nhỏ hơn
        canonical = rankings
            .OrderBy(r => r.Position)
            .ThenBy(r => r.TimeTaken ?? double.MaxValue)
            .ToList();

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
