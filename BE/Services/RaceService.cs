using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HorseRacing.Services;

public class RaceService : IRaceService
{
    private readonly IRaceRepository _races;
    private readonly IRaceEntryRepository _entries;
    private readonly IRaceResultRepository _results;
    private readonly ITournamentRepository _tournaments;
    private readonly IRaceManagementService _raceManagement;

    public RaceService(IRaceRepository races, IRaceEntryRepository entries, IRaceResultRepository results, ITournamentRepository tournaments, IRaceManagementService raceManagement)
    {
        _races = races;
        _entries = entries;
        _results = results;
        _tournaments = tournaments;
        _raceManagement = raceManagement;
    }

    /// <summary>
    /// Lấy danh sách tất cả các cuộc đua trong hệ thống kèm thông tin tóm tắt.
    /// </summary>
    /// <returns>Danh sách tóm tắt tất cả cuộc đua.</returns>
    public async Task<ServiceResult<object>> GetRacesAsync()
    {
        var races = await _races.GetAllAsync();
        var summaries = races.Select(r => new RaceSummaryDto
        {
            Id = r.Id,
            Name = r.Name,
            TournamentId = r.TournamentId,
            ScheduledAt = r.ScheduledAt,
            Status = r.Status.ToString(),
            RoundId = r.RoundId,
            RoundNumber = r.Round?.RoundNumber,
            RoundName = r.Round?.Name,
            TrackId = r.TrackId,
            TrackName = r.Track?.Name,
            QualificationSlots = r.QualificationSlots,
            ResultStatus = r.Result?.Status.ToString()
        }).ToList();

        return ServiceResult<object>.Ok(summaries);
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một cuộc đua theo mã định danh.
    /// </summary>
    /// <param name="raceId">Mã định danh cuộc đua.</param>
    /// <returns>Thông tin chi tiết cuộc đua kèm danh sách ngựa tham gia.</returns>
    public async Task<ServiceResult<object>> GetRaceAsync(System.Guid raceId)
    {
        var race = await _races.GetByIdAsync(raceId);
        if (race == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy cuộc đua");
        }

        return ServiceResult<object>.Ok(RaceDetailResponseMapper.ToDetailResponse(race));
    }

    /// <summary>
    /// Lấy kết quả thi đấu và bảng xếp hạng chính thức của một cuộc đua.
    /// </summary>
    /// <param name="raceId">Mã định danh cuộc đua.</param>
    /// <returns>Kết quả thi đấu kèm thông tin ngựa về nhất và bảng xếp hạng.</returns>
    public async Task<ServiceResult<object>> GetRaceResultAsync(System.Guid raceId)
    {
        var result = await _results.GetByRaceIdAsync(raceId);
        if (result == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy kết quả");
        }

        List<RaceResultRankingItemResponse>? rankings = null;
        if (!string.IsNullOrWhiteSpace(result.RankingsJson))
        {
            try
            {
                // Thêm tuỳ chọn để đọc JSON tự động nhận dạng hoa/thường
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // CẬP NHẬT Ở ĐÂY: Parse thẳng ra danh sách Response thay vì Request cũ
                var stored = JsonSerializer.Deserialize<List<RaceResultRankingItemResponse>>(result.RankingsJson, options);

                if (stored != null)
                {
                    var race = await _races.GetByIdWithEntriesAsync(raceId);
                    var horseNameById = race?.Entries?
                        .Where(e => e.Horse != null)
                        .ToDictionary(e => e.HorseId, e => e.Horse!.Name)
                        ?? new Dictionary<System.Guid, string>();
                    var jockeyNameById = race?.Entries?
                        .Where(e => e.Jockey?.User != null)
                        .ToDictionary(e => e.HorseId, e => e.Jockey!.User!.FullName ?? "")
                        ?? new Dictionary<System.Guid, string>();

                    // Mốc so sánh margin: thời gian của (các) ngựa xếp Hạng 1 đã
                    // hoàn thành đua. Đồng hạng 1 thì lấy thời gian nhỏ nhất trong
                    // nhóm làm mốc, để margin của các ngựa sau luôn >= 0.
                    var leaderTime = stored
                        .Where(r => r.Position == 1 && r.Status == "Completed" && r.TimeTaken.HasValue)
                        .Select(r => r.TimeTaken!.Value)
                        .DefaultIfEmpty(double.NaN)
                        .Min();
                    double? leaderTimeOrNull = double.IsNaN(leaderTime) ? null : leaderTime;

                    rankings = stored
                        .OrderBy(r => r.Position)
                        .Select(r => new RaceResultRankingItemResponse
                        {
                            Position = r.Position,
                            HorseId = r.HorseId,
                            HorseName = horseNameById.TryGetValue(r.HorseId, out var name) ? name : null,
                            // ĐỌC VÀ TRẢ VỀ 2 TRƯỜNG THỜI GIAN VÀ TRẠNG THÁI
                            TimeTaken = r.TimeTaken,
                            Status = r.Status ?? "Completed",
                            JockeyName = jockeyNameById.TryGetValue(r.HorseId, out var jname) ? jname : null,
                            Margin = ComputeMargin(r, leaderTimeOrNull)
                        })
                        .ToList();
                }
            }
            catch (JsonException)
            {
                rankings = null;
            }
        }

        return ServiceResult<object>.Ok(new RaceResultResponse
        {
            RaceId = result.RaceId,
            WinningHorseId = result.WinningHorseId,
            WinningHorseName = result.WinningHorse?.Name,
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
            Rankings = rankings,
            Notes = result.Notes
        });
    }

    /// <summary>
    /// "-" cho ngựa dẫn đầu (Hạng 1, Completed) hoặc khi thiếu dữ liệu để so sánh
    /// (không Completed, không có TimeTaken, hoặc không có leaderTime mốc).
    /// Ngược lại là cách biệt thời gian so với leaderTime, dạng "0.65s".
    /// </summary>
    private static string? ComputeMargin(RaceResultRankingItemResponse r, double? leaderTime)
    {
        if (r.Status != "Completed" || r.TimeTaken == null || leaderTime == null)
        {
            return "-";
        }

        if (r.Position == 1)
        {
            return "-";
        }

        return $"{(r.TimeTaken.Value - leaderTime.Value).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}s";
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

    /// <summary>
    /// Q2 (Phase 2, read-only): "Kết quả chung cuộc" của Tournament — Position→Horse của Round
    /// thực sự quyết định giải (Final, hoặc 1 Round giữa chừng nếu walkover/void kết thúc giải
    /// sớm). Chỉ đọc — không SaveChangesAsync, không đụng Prize/IsDistributed.
    /// </summary>
    public async Task<ServiceResult<FinalStandingsDto>> GetFinalStandingsAsync(Guid tournamentId)
    {
        var tournament = await _tournaments.GetWithFullRoundsAndResultsAsync(tournamentId);

        if (tournament == null)
        {
            return ServiceResult<FinalStandingsDto>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy giải đấu");
        }

        switch (tournament.Status)
        {
            case TournamentStatus.Draft:
            case TournamentStatus.Published:
            case TournamentStatus.Ongoing:
                return ServiceResult<FinalStandingsDto>.Ok(new FinalStandingsDto
                {
                    TournamentId = tournament.Id,
                    IsFinal = false,
                    Message = "Giải đấu chưa kết thúc."
                });

            case TournamentStatus.Cancelled:
                // Void = giải coi như chưa từng diễn ra — kể cả khi có Round đã lỡ có RaceResult
                // Official trước khi bị huỷ, vẫn không trả Standings nào. Quyết định thiết kế đã
                // chốt, không phải bug.
                return ServiceResult<FinalStandingsDto>.Ok(new FinalStandingsDto
                {
                    TournamentId = tournament.Id,
                    IsFinal = true,
                    IsVoid = true,
                    VoidReason = tournament.CancellationReason
                });

            case TournamentStatus.Finished:
                return await BuildFinishedStandingsAsync(tournament);

            default:
                return ServiceResult<FinalStandingsDto>.Fail(StatusCodes.Status500InternalServerError,
                    $"Trạng thái giải đấu không xác định: {tournament.Status}.");
        }
    }

    private async Task<ServiceResult<FinalStandingsDto>> BuildFinishedStandingsAsync(Tournament tournament)
    {
        // Vòng quyết định = Round có RoundNumber lớn nhất có ít nhất 1 Race Finished+Official.
        // Duyệt giảm dần RoundNumber vì walkover/void có thể kết thúc giải sớm ở 1 Round giữa
        // chừng — các Round sau đó (kể cả Final) không có Race nào Finished+Official (bị Cancelled).
        var decidingRound = tournament.Rounds
            .OrderByDescending(r => r.RoundNumber)
            .FirstOrDefault(r => r.Races.Any(race =>
                race.Status == RaceStatus.Finished && race.Result?.Status == RaceResultStatus.Official));

        if (decidingRound == null)
        {
            return ServiceResult<FinalStandingsDto>.Fail(StatusCodes.Status409Conflict,
                "Giải đấu đã kết thúc nhưng không tìm thấy Vòng đấu nào có kết quả chính thức — dữ liệu bất thường, cần kiểm tra lại.");
        }

        var decidingRaces = decidingRound.Races
            .Where(race => race.Status == RaceStatus.Finished && race.Result?.Status == RaceResultStatus.Official)
            .ToList();
        var isWalkover = decidingRound.RoundNumber != tournament.MaxRounds;

        if (decidingRaces.Count > 1)
        {
            // Đã xác nhận đây là tình huống THẬT có thể xảy ra ở 1 Round giữa chừng (không phải
            // Final — Final luôn đúng 1 Race qua Publish thật). Không có quy tắc nghiệp vụ nào định
            // nghĩa cách gộp nhiều Race song song thành 1 bảng xếp hạng — không tự bịa công thức
            // (theo Position hay TimeTaken đều là suy đoán) — trả về yêu cầu review thủ công.
            return ServiceResult<FinalStandingsDto>.Ok(new FinalStandingsDto
            {
                TournamentId = tournament.Id,
                IsFinal = true,
                IsVoid = false,
                IsWalkover = isWalkover,
                DecidingRoundNumber = decidingRound.RoundNumber,
                RequiresManualReview = true,
                Message = $"Vòng quyết định (Round {decidingRound.RoundNumber}) có {decidingRaces.Count} Race song song — hệ thống hiện chưa có quy tắc gộp bảng xếp hạng nhiều Race thành 1 kết quả chung cuộc, cần bổ sung thiết kế riêng trước khi hiển thị/trả thưởng cho trường hợp này."
            });
        }

        var decidingRace = decidingRaces[0];

        // Tái dùng chính xác logic parse RankingsJson + join Horse/Jockey của GetRaceResultAsync —
        // không viết lại logic parse JSON ở đây.
        var rankingResult = await GetRaceResultAsync(decidingRace.Id);
        if (rankingResult.Result.Data is not RaceResultResponse raceResultResponse || raceResultResponse.Rankings == null)
        {
            return ServiceResult<FinalStandingsDto>.Fail(StatusCodes.Status409Conflict,
                $"Không đọc được bảng xếp hạng của Cuộc đua quyết định (RaceId={decidingRace.Id}).");
        }

        // GetRaceResultAsync không trả OwnerId/OwnerName/JockeyId — bổ sung riêng bằng RaceEntry
        // của đúng Race quyết định, không đụng/không viết lại phần parse RankingsJson ở trên.
        var entries = await _entries.GetByRaceAsync(decidingRace.Id);
        var entryByHorse = entries
            .Where(e => e.Horse != null)
            .ToDictionary(e => e.HorseId, e => e);

        var standings = raceResultResponse.Rankings.Select(r =>
        {
            entryByHorse.TryGetValue(r.HorseId, out var entry);
            return new StandingEntryDto
            {
                Position = r.Position,
                HorseId = r.HorseId,
                HorseName = r.HorseName ?? entry?.Horse?.Name ?? string.Empty,
                JockeyId = entry?.JockeyId,
                JockeyName = r.JockeyName,
                OwnerId = entry?.Horse?.OwnerId,
                OwnerName = entry?.Horse?.Owner?.User?.FullName,
                OwnerUserId = entry?.Horse?.Owner?.User?.Id
            };
        }).ToList();

        return ServiceResult<FinalStandingsDto>.Ok(new FinalStandingsDto
        {
            TournamentId = tournament.Id,
            IsFinal = true,
            IsVoid = false,
            IsWalkover = isWalkover,
            DecidingRoundNumber = decidingRound.RoundNumber,
            RequiresManualReview = false,
            Standings = standings
        });
    }
}
