using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Services;

public class RaceManagementService : IRaceManagementService
{
    private readonly IRaceRepository _raceRepo;
    private readonly IRaceEntryRepository _entryRepo;
    private readonly IHorseRepository _horseRepo;
    private readonly IJockeyRepository _jockeyRepo;
    private readonly ITournamentRepository _tournamentRepo;
    private readonly IRoundRepository _roundRepo;
    private readonly IPredictionRepository _predictionRepo;
    private readonly IRefereeAssignmentRepository _assignmentRepo;
    private readonly IWalletService _walletService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RaceManagementService> _logger;
    private readonly IRaceEntryService _raceEntryService;
    private readonly ApplicationDbContext _db;
    private readonly ITournamentService _tournamentService;

    public RaceManagementService(
        IRaceRepository raceRepo,
        IRaceEntryRepository entryRepo,
        IHorseRepository horseRepo,
        IJockeyRepository jockeyRepo,
        ITournamentRepository tournamentRepo,
        IRoundRepository roundRepo,
        IPredictionRepository predictionRepo,
        IRefereeAssignmentRepository assignmentRepo,
        IWalletService walletService,
        IUnitOfWork unitOfWork,
        ILogger<RaceManagementService> logger,
        IRaceEntryService raceEntryService,
        ApplicationDbContext db,
        ITournamentService tournamentService)
    {
        _raceRepo = raceRepo;
        _entryRepo = entryRepo;
        _horseRepo = horseRepo;
        _jockeyRepo = jockeyRepo;
        _tournamentRepo = tournamentRepo;
        _roundRepo = roundRepo;
        _predictionRepo = predictionRepo;
        _assignmentRepo = assignmentRepo;
        _walletService = walletService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _raceEntryService = raceEntryService;
        _db = db;
        _tournamentService = tournamentService;
    }

    /// <summary>
    /// Khởi tạo một cuộc đua mới thuộc giải đấu và vòng đua chỉ định.
    /// Kiểm tra thời gian khởi tranh, sức chứa đường đua và trạng thái bản nháp của giải đấu.
    /// </summary>
    /// <param name="request">Thông tin cấu hình cuộc đua mới.</param>
    /// <returns>Chi tiết cuộc đua vừa khởi tạo thành công.</returns>
    public async Task<ServiceResult<RaceDetailResponse>> CreateRaceAsync(CreateRaceRequest request)
    {
        try
        {
            var tournament = await _tournamentRepo.GetByIdAsync(request.TournamentId);
            if (tournament == null)
            {
                return ServiceResult<RaceDetailResponse>.Error("Không tìm thấy giải đấu", 404);
            }

            if (tournament.Status != TournamentStatus.Draft)
            {
                return ServiceResult<RaceDetailResponse>.Error(
                    "Không thể thêm Cuộc đua vì giải đấu không còn ở trạng thái Bản nháp.", 400);
            }

            if (!request.RoundId.HasValue || request.RoundId.Value == Guid.Empty)
            {
                return ServiceResult<RaceDetailResponse>.Error("Vòng đấu (Round) là bắt buộc để tạo cuộc đua.", 400);
            }

            var round = await _roundRepo.GetByIdAsync(request.RoundId.Value);
            if (round == null)
            {
                return ServiceResult<RaceDetailResponse>.Error("Không tìm thấy vòng đấu.", 404);
            }

            if (round.TournamentId != request.TournamentId)
            {
                return ServiceResult<RaceDetailResponse>.Error("Vòng đấu không thuộc giải đấu đã chọn.", 400);
            }

            if (request.MaxParticipants <= 0)
            {
                return ServiceResult<RaceDetailResponse>.Error("Số lượng tối đa (MaxParticipants) phải lớn hơn 0.", 400);
            }

            var scheduleErrors = ValidateRaceScheduleWithinRound(round, request.ScheduledAt, request.ScheduledEndAt);
            if (scheduleErrors.Count > 0)
            {
                return ServiceResult<RaceDetailResponse>.Error(string.Join("; ", scheduleErrors), 400);
            }

            Track? track = null;
            if (request.TrackId.HasValue)
            {
                track = await _db.Tracks.FirstOrDefaultAsync(t => t.Id == request.TrackId.Value);
                if (track == null)
                {
                    return ServiceResult<RaceDetailResponse>.Error("Không tìm thấy đường đua (Track).", 404);
                }

                if (track.Capacity.HasValue && request.MaxParticipants > track.Capacity.Value)
                {
                    return ServiceResult<RaceDetailResponse>.Error(
                        $"Số lượng tối đa ({request.MaxParticipants}) vượt quá sức chứa đường đua \"{track.Name}\" ({track.Capacity.Value}).", 400);
                }

                if (request.ScheduledEndAt.HasValue)
                {
                    var overlap = await TrackScheduleHelper.HasOverlapAsync(
                        _db, request.TournamentId, request.TrackId.Value, request.ScheduledAt, request.ScheduledEndAt.Value, excludeRaceId: null);
                    if (overlap)
                    {
                        return ServiceResult<RaceDetailResponse>.Error(
                            $"Đường đua \"{track.Name}\" đã có Cuộc đua khác trùng khung giờ này.", 400);
                    }
                }
            }

            var qualificationErrors = await ValidateQualificationSlotsForRaceSaveAsync(
                round.Id, null, request.QualificationSlots, request.MaxParticipants);
            if (qualificationErrors.Count > 0)
            {
                return ServiceResult<RaceDetailResponse>.Error(string.Join("; ", qualificationErrors), 400);
            }

            var race = new Race
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                TournamentId = round.TournamentId,
                RoundId = round.Id,
                ScheduledAt = request.ScheduledAt,
                ScheduledEndAt = request.ScheduledEndAt,
                TrackId = request.TrackId,
                Status = RaceStatus.Scheduled,
                Location = request.Location,
                Description = request.Description,
                MaxParticipants = request.MaxParticipants,
                Distance = request.Distance,
                RoundNames = request.RoundNames,
                QualificationSlots = request.QualificationSlots,
                CreatedAt = DateTime.UtcNow
            };

            await _raceRepo.AddAsync(race);

            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<RaceDetailResponse>(201, ApiResult<RaceDetailResponse>.Ok(MapToDetailResponse(race)));
        }
        catch (Exception ex)
        {
            return ServiceResult<RaceDetailResponse>.Fail(500, "Không thể tạo cuộc đua. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<RaceDetailResponse>> GetRaceDetailsAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
            {
                return ServiceResult<RaceDetailResponse>.Fail(404, "Không tìm thấy cuộc đua");
            }

            return ServiceResult<RaceDetailResponse>.Ok(MapToDetailResponse(race));
        }
        catch (Exception ex)
        {
            return ServiceResult<RaceDetailResponse>.Fail(500, "Không thể tải thông tin cuộc đua.");
        }
    }

    public async Task<ServiceResult<RaceDetailResponse>> UpdateRaceAsync(Guid raceId, UpdateRaceRequest request)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
            {
                return ServiceResult<RaceDetailResponse>.Fail(404, "Không tìm thấy cuộc đua");
            }

            if (race.Tournament != null && race.Tournament.Status != TournamentStatus.Draft)
            {
                return ServiceResult<RaceDetailResponse>.Fail(400,
                    "Không thể chỉnh sửa Cuộc đua vì giải đấu không còn ở trạng thái Bản nháp.");
            }

            // Phase5: compute candidate state before mutating the tracked entity, mirroring the
            // Phase4B UpdateTournamentAsync convention — validation runs against the CANDIDATE,
            // never leaving a partial/invalid update in the ChangeTracker if it's rejected.
            var candidateMaxParticipants = request.MaxParticipants ?? race.MaxParticipants;
            var candidateScheduledAt = request.ScheduledAt ?? race.ScheduledAt;
            var candidateScheduledEndAt = request.ScheduledEndAt ?? race.ScheduledEndAt;
            var candidateTrackId = request.TrackId ?? race.TrackId;

            if (candidateMaxParticipants <= 0)
            {
                return ServiceResult<RaceDetailResponse>.Fail(400, "Số lượng tối đa (MaxParticipants) phải lớn hơn 0.");
            }

            // Race.RoundId is immutable after creation (locked Phase5 rule), so the candidate
            // schedule is always validated against the Race's existing, real, persisted Round.
            if (race.Round != null)
            {
                var scheduleErrors = ValidateRaceScheduleWithinRound(race.Round, candidateScheduledAt, candidateScheduledEndAt);
                if (scheduleErrors.Count > 0)
                {
                    return ServiceResult<RaceDetailResponse>.Fail(400, string.Join("; ", scheduleErrors));
                }
            }

            Track? candidateTrack = null;
            if (candidateTrackId.HasValue)
            {
                candidateTrack = await _db.Tracks.FirstOrDefaultAsync(t => t.Id == candidateTrackId.Value);
                if (candidateTrack == null)
                {
                    return ServiceResult<RaceDetailResponse>.Fail(404, "Không tìm thấy đường đua (Track).");
                }

                if (candidateTrack.Capacity.HasValue && candidateMaxParticipants > candidateTrack.Capacity.Value)
                {
                    return ServiceResult<RaceDetailResponse>.Fail(400,
                        $"Số lượng tối đa ({candidateMaxParticipants}) vượt quá sức chứa đường đua \"{candidateTrack.Name}\" ({candidateTrack.Capacity.Value}).");
                }

                if (candidateScheduledEndAt.HasValue)
                {
                    var overlap = await TrackScheduleHelper.HasOverlapAsync(
                        _db, race.TournamentId, candidateTrackId.Value, candidateScheduledAt, candidateScheduledEndAt.Value, excludeRaceId: race.Id);
                    if (overlap)
                    {
                        return ServiceResult<RaceDetailResponse>.Fail(400,
                            $"Đường đua \"{candidateTrack.Name}\" đã có Cuộc đua khác trùng khung giờ này.");
                    }
                }
            }
            var candidateQualificationSlots = request.QualificationSlots.HasValue
                ? request.QualificationSlots.Value
                : race.QualificationSlots;
            var qualificationErrors = await ValidateQualificationSlotsForRaceSaveAsync(
                race.RoundId, race.Id, candidateQualificationSlots, candidateMaxParticipants);
            if (qualificationErrors.Count > 0)
            {
                return ServiceResult<RaceDetailResponse>.Fail(400, string.Join("; ", qualificationErrors));
            }

            if (!string.IsNullOrEmpty(request.Name))
                race.Name = request.Name;
            if (request.ScheduledAt.HasValue)
                race.ScheduledAt = request.ScheduledAt.Value;
            if (!string.IsNullOrEmpty(request.Location))
                race.Location = request.Location;
            if (!string.IsNullOrEmpty(request.Description))
                race.Description = request.Description;
            if (request.MaxParticipants.HasValue)
                race.MaxParticipants = request.MaxParticipants.Value;
            if (request.Distance.HasValue)
                race.Distance = request.Distance.Value;
            if (!string.IsNullOrEmpty(request.RoundNames))
                race.RoundNames = request.RoundNames;
            if (request.ScheduledEndAt.HasValue)
                race.ScheduledEndAt = request.ScheduledEndAt.Value;
            if (request.TrackId.HasValue)
                race.TrackId = request.TrackId;
            if (request.QualificationSlots.HasValue)
                race.QualificationSlots = request.QualificationSlots.Value;

            race.UpdatedAt = DateTime.UtcNow;
            await _raceRepo.UpdateAsync(race);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<RaceDetailResponse>.Ok(MapToDetailResponse(race));
        }
        catch (Exception ex)
        {
            return ServiceResult<RaceDetailResponse>.Fail(500, "Không thể cập nhật cuộc đua. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<IEnumerable<RaceDetailResponse>>> GetRacesByTournamentAsync(Guid tournamentId)
    {
        try
        {
            var races = await _raceRepo.GetByTournamentAsync(tournamentId);
            return ServiceResult<IEnumerable<RaceDetailResponse>>.Ok(
                races.Select(MapToDetailResponse));
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<RaceDetailResponse>>.Fail(
                500, "Không thể tải danh sách cuộc đua.");
        }
    }

    public async Task<ServiceResult<IEnumerable<RaceDetailResponse>>> GetRacesByRoundAsync(Guid roundId)
    {
        try
        {
            var races = await _raceRepo.GetByRoundAsync(roundId);
            return ServiceResult<IEnumerable<RaceDetailResponse>>.Ok(
                races.Select(MapToDetailResponse));
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<RaceDetailResponse>>.Fail(
                500, "Không thể tải danh sách cuộc đua.");
        }
    }

    public async Task<ServiceResult<bool>> DeleteRaceAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");

            if (race.Tournament != null && race.Tournament.Status != TournamentStatus.Draft)
                return ServiceResult<bool>.Fail(400, "Không thể xóa Cuộc đua vì giải đấu không còn ở trạng thái Bản nháp.");

            if (race.Status != RaceStatus.Scheduled && race.Status != RaceStatus.Cancelled)
                return ServiceResult<bool>.Fail(400, $"Không thể xóa cuộc đua với trạng thái '{race.Status}'. Chỉ có thể xóa cuộc đua đã lên lịch hoặc đã hủy.");

            await using var transaction = await _db.Database.BeginTransactionAsync();
            await RaceDeletionHelper.DeleteRaceGraphAsync(_db, raceId);
            await transaction.CommitAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, "Không thể xóa cuộc đua. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<bool>> AssignHorseToRaceAsync(Guid raceId, AssignHorseToRaceRequest request)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");
            }

            if ((race.Round?.RoundNumber ?? 1) > 1)
            {
                return ServiceResult<bool>.Fail(400, "Ngựa ở vòng sau phải được xác định từ kết quả vòng trước.");
            }

            if (race.Status != RaceStatus.Scheduled && race.Status != RaceStatus.RegistrationOpen)
            {
                return ServiceResult<bool>.Fail(400, $"Không thể thêm ngựa vào cuộc đua có trạng thái '{race.Status}'. Cuộc đua phải ở trạng thái Đã lên lịch hoặc Đang mở đăng ký.");
            }

            var currentCount = await _entryRepo.GetByRaceAsync(raceId);
            if (currentCount.Count >= race.MaxParticipants)
            {
                return ServiceResult<bool>.Fail(400, $"Cuộc đua đã đạt số lượng người tham gia tối đa ({race.MaxParticipants}).");
            }

            var horse = await _horseRepo.GetByIdAsync(request.HorseId);
            if (horse == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy ngựa");
            }
            if (horse.ApprovalStatus != ApprovalStatus.Approved)
            {
                return ServiceResult<bool>.Fail(400, "Ngựa chưa được admin phê duyệt");
            }
            if (horse.IsArchived)
            {
                return ServiceResult<bool>.Fail(400, "Ngựa đã được lưu trữ (archive) và không thể phân công vào cuộc đua mới.");
            }

            // Task B §6: RaceEntry gate — Horse must hold an Approved TournamentHorseRegistration
            // for THIS Race's Tournament. Historical Withdrawn/Rejected rows must not hide a
            // newer Approved registration.
            var hasApprovedRegistration = await _db.TournamentHorseRegistrations.AnyAsync(x =>
                x.TournamentId == race.TournamentId &&
                x.HorseId == request.HorseId &&
                x.Status == RegistrationStatus.Approved);
            if (!hasApprovedRegistration)
            {
                return ServiceResult<bool>.Fail(400, "Ngựa chưa được duyệt đăng ký tham gia giải đấu này.");
            }

            var alreadyInActiveRace = await _entryRepo.IsHorseInActiveRaceAsync(request.HorseId);
            if (alreadyInActiveRace)
            {
                var busyEntry = (await _entryRepo.GetByHorseAsync(request.HorseId))
                    .FirstOrDefault(e => e.Race != null && e.Race.Status != RaceStatus.Finished && e.Race.Status != RaceStatus.Cancelled);
                var busyRaceName = busyEntry?.Race?.Name ?? "cuộc đua khác";
                return ServiceResult<bool>.Fail(400, $"Ngựa này đã được đăng ký trong \"{busyRaceName}\". Không thể thêm vào nhiều cuộc đua cùng lúc.");
            }

            var existsInThisRace = await _entryRepo.ExistsAsync(raceId, request.HorseId);
            if (existsInThisRace)
            {
                return ServiceResult<bool>.Fail(400, "Ngựa đã được thêm vào cuộc đua này.");
            }

            // J1: Admin assigns only the horse; jockey selection stays in the owner invitation flow.
            // O1: Owner consent already happened at Tournament registration (Owner registered ->
            // Admin approved the TournamentHorseRegistration) — this Admin assignment is the
            // authoritative RaceEntry creation path, so OwnerConfirmed is true immediately; there is
            // no second manual Owner "Xác nhận tham gia cuộc đua" step. JockeyConfirmed stays false
            // until Owner Final Confirm (J3) establishes the official Jockey.
            var entry = new RaceEntry
            {
                Id = Guid.NewGuid(),
                RaceId = raceId,
                HorseId = request.HorseId,
                JockeyId = null,
                Status = RegistrationStatus.Approved,
                OwnerConfirmed = true,
                JockeyConfirmed = false
            };

            await _entryRepo.AddAsync(entry);
            await _unitOfWork.SaveChangesAsync();

            await RecalculateOddsAsync(raceId);

            return new ServiceResult<bool>(201, ApiResult<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi phân công ngựa vào cuộc đua {RaceId}", raceId);
            return ServiceResult<bool>.Fail(500, "Không thể thêm ngựa vào cuộc đua. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<bool>> BulkAssignHorsesToRaceAsync(Guid raceId, BulkAssignHorsesToRaceRequest request)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");
            }

            if ((race.Round?.RoundNumber ?? 1) > 1)
            {
                return ServiceResult<bool>.Fail(400, "Ngựa ở vòng sau phải được xác định từ kết quả vòng trước.");
            }

            if (race.Status != RaceStatus.Scheduled && race.Status != RaceStatus.RegistrationOpen)
            {
                return ServiceResult<bool>.Fail(400, $"Không thể thêm ngựa vào cuộc đua có trạng thái '{race.Status}'. Cuộc đua phải ở trạng thái Đã lên lịch hoặc Đang mở đăng ký.");
            }

            var currentCount = await _entryRepo.GetByRaceAsync(raceId);
            if (currentCount.Count >= race.MaxParticipants)
            {
                return ServiceResult<bool>.Fail(400, $"Cuộc đua đã đạt số lượng người tham gia tối đa ({race.MaxParticipants}).");
            }

            var errors = new List<string>();
            var added = 0;
            foreach (var horseId in request.HorseIds)
            {
                if (added + currentCount.Count >= race.MaxParticipants)
                {
                    errors.Add($"Đã đạt số lượng tối đa ({race.MaxParticipants}), bỏ qua các ngựa còn lại.");
                    break;
                }
                var horse = await _horseRepo.GetByIdAsync(horseId);
                if (horse == null)
                {
                    errors.Add($"Không tìm thấy ngựa {horseId}");
                    continue;
                }
                if (horse.ApprovalStatus != ApprovalStatus.Approved)
                {
                    errors.Add($"Ngựa \"{horse.Name}\" chưa được phê duyệt");
                    continue;
                }
                if (horse.IsArchived)
                {
                    errors.Add($"Ngựa \"{horse.Name}\" đã được lưu trữ (archive)");
                    continue;
                }

                // Task B §6: same RaceEntry gate as AssignHorseToRaceAsync — Approved
                // TournamentHorseRegistration for this Race's Tournament, required.
                var hasApprovedRegistration = await _db.TournamentHorseRegistrations.AnyAsync(x =>
                    x.TournamentId == race.TournamentId &&
                    x.HorseId == horseId &&
                    x.Status == RegistrationStatus.Approved);
                if (!hasApprovedRegistration)
                {
                    errors.Add($"Ngựa \"{horse.Name}\" chưa được duyệt đăng ký tham gia giải đấu này");
                    continue;
                }

                var alreadyInActiveRace = await _entryRepo.IsHorseInActiveRaceAsync(horseId);
                if (alreadyInActiveRace)
                {
                    errors.Add($"Ngựa \"{horse.Name}\" đã đăng ký trong cuộc đua khác");
                    continue;
                }

                var existsInThisRace = await _entryRepo.ExistsAsync(raceId, horseId);
                if (existsInThisRace)
                {
                    errors.Add($"Ngựa \"{horse.Name}\" đã được thêm vào cuộc đua này");
                    continue;
                }

                // O1: same authoritative-assignment rule as the single-assign path above —
                // OwnerConfirmed = true immediately, no second manual Owner confirmation step.
                var entry = new RaceEntry
                {
                    Id = Guid.NewGuid(),
                    RaceId = raceId,
                    HorseId = horseId,
                    JockeyId = null,
                    Status = RegistrationStatus.Approved,
                    OwnerConfirmed = true,
                    JockeyConfirmed = false
                };
                await _entryRepo.AddAsync(entry);
                added++;
            }

            await _unitOfWork.SaveChangesAsync();
            await RecalculateOddsAsync(raceId);

            if (errors.Count > 0 && errors.Count == request.HorseIds.Length)
                return ServiceResult<bool>.Fail(400, $"Không thể thêm tất cả ngựa: {string.Join("; ", errors)}");

            if (errors.Count > 0)
                return new ServiceResult<bool>(207, ApiResult<bool>.Ok(true, $"Đã thêm với cảnh báo: {string.Join("; ", errors)}"));

            return new ServiceResult<bool>(201, ApiResult<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi thêm hàng loạt ngựa vào cuộc đua {RaceId}", raceId);
            return ServiceResult<bool>.Fail(500, "Không thể thêm hàng loạt ngựa. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<bool>> RemoveHorseFromRaceAsync(Guid raceId, Guid horseId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");

            if (race.Status != RaceStatus.Scheduled && race.Status != RaceStatus.RegistrationOpen)
                return ServiceResult<bool>.Fail(400, "Không thể xóa ngựa sau khi cuộc đua đã đóng đăng ký.");

            var entry = await _entryRepo.GetByRaceAndHorseAsync(raceId, horseId);
            if (entry == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy đăng ký tham gia");
            }

            await _entryRepo.DeleteAsync(entry.Id);
            await _unitOfWork.SaveChangesAsync();
            await RecalculateOddsAsync(raceId);
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, "Không thể gỡ ngựa khỏi cuộc đua. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<List<Guid>>> GetBusyHorseIdsAsync()
    {
        try
        {
            var ids = await _entryRepo.GetHorseIdsInActiveRacesAsync();
            return ServiceResult<List<Guid>>.Ok(ids);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Guid>>.Fail(500, "Không thể tải danh sách ngựa bận.");
        }
    }

    public async Task<ServiceResult<bool>> UpdateOddsAsync(Guid raceId, Guid horseId, decimal odds)
    {
        try
        {
            if (odds <= 0)
                return ServiceResult<bool>.Fail(400, "Tỷ lệ cược phải lớn hơn 0.");

            var entry = await _entryRepo.GetByRaceAndHorseAsync(raceId, horseId);
            if (entry == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy ngựa trong cuộc đua.");

            entry.Odds = odds;
            await _entryRepo.UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, "Không thể cập nhật tỉ lệ cược. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<bool>> OpenRegistrationAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");

            if (race.Status != RaceStatus.Scheduled)
                return ServiceResult<bool>.Fail(400, $"Không thể mở đăng ký cho cuộc đua với trạng thái '{race.Status}'.");

            race.Status = RaceStatus.RegistrationOpen;
            race.UpdatedAt = DateTime.UtcNow;
            await _raceRepo.UpdateAsync(race);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception)
        {
            return ServiceResult<bool>.Fail(500, "Không thể mở đăng ký. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<bool>> CloseRegistrationAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");

            if (race.Status != RaceStatus.RegistrationOpen)
                return ServiceResult<bool>.Fail(400, $"Không thể đóng đăng ký cho cuộc đua với trạng thái '{race.Status}'.");

            race.Status = RaceStatus.RegistrationClosed;
            race.UpdatedAt = DateTime.UtcNow;
            await _raceRepo.UpdateAsync(race);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception)
        {
            return ServiceResult<bool>.Fail(500, "Không thể đóng đăng ký. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<bool>> StartRaceAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");
            }

            var canStartFromPreRaceStatus =
                race.Status == RaceStatus.Scheduled ||
                race.Status == RaceStatus.RegistrationOpen ||
                race.Status == RaceStatus.RegistrationClosed;
            if (!canStartFromPreRaceStatus)
            {
                return ServiceResult<bool>.Fail(400, $"Không thể bắt đầu cuộc đua với trạng thái '{race.Status}'. Cuộc đua phải ở trạng thái chuẩn bị trước khi bắt đầu.");
            }

            if (race.Tournament?.Status != TournamentStatus.Ongoing)
            {
                return ServiceResult<bool>.Fail(
                    409,
                    "Giải đấu phải ở trạng thái đang diễn ra trước khi bắt đầu cuộc đua.");
            }

            var entries = await _entryRepo.GetByRaceAsync(raceId);
            if (entries.Count == 0)
            {
                return ServiceResult<bool>.Fail(400, "Không thể bắt đầu cuộc đua khi chưa có ngựa tham gia.");
            }

            // Check if at least one referee has accepted (Confirmed status)
            var hasAcceptedReferee = race.RefereeAssignments?.Any(ra => ra.Status == RefereeAssignmentStatus.Confirmed) ?? false;
            if (!hasAcceptedReferee)
            {
                return ServiceResult<bool>.Fail(400, "Không thể bắt đầu cuộc đua khi chưa có trọng tài nào chấp nhận lời mời.");
            }

            var entryValidation = await _raceEntryService.ValidateRaceEntriesForStartAsync(raceId);
            if (!entryValidation.Result.Success)
                return ServiceResult<bool>.Fail(entryValidation.StatusCode, entryValidation.Result.Message ?? "Entry chưa hợp lệ");

            var gateValidation = ValidateGateReadinessForStart(entries, race.Track?.Capacity);
            if (!gateValidation.Result.Success)
                return ServiceResult<bool>.Fail(gateValidation.StatusCode, gateValidation.Result.Message ?? "Cổng xuất phát chưa hợp lệ");

            race.Status = RaceStatus.InProgress;
            race.ActualStartTime = DateTime.UtcNow;
            race.UpdatedAt = DateTime.UtcNow;

            await _raceRepo.UpdateAsync(race);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, "Không thể bắt đầu cuộc đua. Vui lòng thử lại.");
        }
    }

    /// <summary>
    /// GATE-V1: StartRace readiness for Referee-assigned starting gates — kept as its own method,
    /// separate from RaceEntryService.ValidateRaceEntriesForStartAsync (R1a), so R1a's existing
    /// per-entry reasons/semantics are never touched. Uses the same participation filter as R1a
    /// (Status != Rejected &amp;&amp; ScratchedAt == null) — Rejected/scratched entries never need a
    /// gate. FINAL CAPACITY CORRECTION: the gate is a physical Track slot, not a Race participant
    /// slot — the upper bound is Track.Capacity (Publish readiness already guarantees
    /// Race.MaxParticipants &lt;= Track.Capacity, see TournamentAndRoundService), never
    /// Race.MaxParticipants. Range/duplicate checks are defensive: the write endpoint and the DB
    /// unique index should make these unreachable via normal use, but legacy data predating
    /// GATE-V1 could still violate them, so StartRace re-checks rather than trusting stored state
    /// blindly. A null/missing Track.Capacity (should be unreachable post-Publish) is treated as
    /// "no valid gate can be proven in range" and rejects defensively rather than silently passing.
    /// </summary>
    private ServiceResult<bool> ValidateGateReadinessForStart(List<RaceEntry> entries, int? trackCapacity)
    {
        var participatingEntries = entries
            .Where(e => e.Status != RegistrationStatus.Rejected && e.ScratchedAt == null)
            .ToList();

        if (participatingEntries.Any(e => e.GateNumber == null))
            return ServiceResult<bool>.Fail(400, "Không thể bắt đầu cuộc đua khi chưa phân đủ cổng xuất phát.");

        if (!trackCapacity.HasValue || participatingEntries.Any(e => e.GateNumber!.Value < 1 || e.GateNumber.Value > trackCapacity.Value))
            return ServiceResult<bool>.Fail(400, "Không thể bắt đầu cuộc đua vì có cổng xuất phát không hợp lệ.");

        var duplicateGate = participatingEntries
            .GroupBy(e => e.GateNumber!.Value)
            .Any(g => g.Count() > 1);
        if (duplicateGate)
            return ServiceResult<bool>.Fail(400, "Không thể bắt đầu cuộc đua vì có cổng xuất phát bị trùng lặp.");

        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// GATE-V1: assigns/updates the starting gate for one participating RaceEntry. Referee
    /// identity and Confirmed-assignment-to-this-Race authorization are the caller's
    /// responsibility (RefereesController) — this method validates only the Race/Entry/gate
    /// business rules: Race exists and is pre-start, entry exists and belongs to this Race, entry
    /// is participating (Status != Rejected &amp;&amp; ScratchedAt == null), gate is within
    /// [1, Track.Capacity], and no other entry in the same Race already holds that gate.
    /// FINAL CAPACITY CORRECTION: GateNumber represents a physical Track starting gate, not a
    /// Race participant slot — Track.Capacity (not Race.MaxParticipants) is the correct upper
    /// bound, since Race.MaxParticipants only caps how many RaceEntries the Race holds, while the
    /// Track physically has Capacity gates available (Publish readiness already guarantees
    /// Race.MaxParticipants &lt;= Track.Capacity, so this is never a smaller pool). Gates need not
    /// be contiguous or match the participant count. Re-submitting an entry's own current gate is
    /// idempotent and always succeeds.
    /// </summary>
    public async Task<ServiceResult<bool>> AssignGateNumberAsync(Guid raceId, Guid entryId, int gateNumber)
    {
        var race = await _raceRepo.GetByIdAsync(raceId);
        if (race == null)
            return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");

        var canEditGate =
            race.Status == RaceStatus.Scheduled ||
            race.Status == RaceStatus.RegistrationOpen ||
            race.Status == RaceStatus.RegistrationClosed;
        if (!canEditGate)
            return ServiceResult<bool>.Fail(400, $"Không thể chỉnh sửa cổng xuất phát khi cuộc đua đang ở trạng thái '{race.Status}'.");

        var entry = await _entryRepo.GetByIdAsync(entryId);
        if (entry == null)
            return ServiceResult<bool>.Fail(404, "Không tìm thấy lượt đăng ký tham gia.");

        if (entry.RaceId != raceId)
            return ServiceResult<bool>.Fail(400, "Lượt đăng ký này không thuộc cuộc đua đã chọn.");

        var isParticipating = entry.Status != RegistrationStatus.Rejected && entry.ScratchedAt == null;
        if (!isParticipating)
            return ServiceResult<bool>.Fail(400, "Không thể gán cổng xuất phát cho lượt đăng ký đã bị từ chối hoặc rút lui.");

        var track = race.Track;
        if (track == null)
            return ServiceResult<bool>.Fail(400, "Cuộc đua chưa được gán đường đua (Track), không thể phân cổng xuất phát.");
        if (!track.Capacity.HasValue || track.Capacity.Value < 1)
            return ServiceResult<bool>.Fail(400, "Sức chứa (Capacity) của đường đua chưa được thiết lập, không thể phân cổng xuất phát.");

        if (gateNumber < 1 || gateNumber > track.Capacity.Value)
            return ServiceResult<bool>.Fail(400, $"Cổng xuất phát phải từ 1 đến {track.Capacity.Value}.");

        if (entry.GateNumber != gateNumber)
        {
            var raceEntries = await _entryRepo.GetByRaceAsync(raceId);
            var conflict = raceEntries.Any(e => e.Id != entryId && e.GateNumber == gateNumber);
            if (conflict)
                return ServiceResult<bool>.Fail(409, "Cổng xuất phát này đã được sử dụng bởi ngựa khác trong cuộc đua.");

            entry.GateNumber = gateNumber;
            await _entryRepo.UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync();
        }

        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// Transitions InProgress -> Finished. Phase2B: this is an event-progress-only
    /// action — it marks that the physical race has concluded. It no longer
    /// requires, reads, or creates a RaceResult, and no longer settles
    /// predictions (settlement now happens when a result becomes Official —
    /// see AdminService.ApproveRaceResultAsync). Method name kept as
    /// EndRaceAsync / endpoint kept as POST .../end for API compatibility.
    /// </summary>
    public async Task<ServiceResult<bool>> EndRaceAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");
            }

            if (race.Status != RaceStatus.InProgress)
            {
                return ServiceResult<bool>.Fail(400, $"Không thể kết thúc cuộc đua với trạng thái '{race.Status}'. Cuộc đua phải đang diễn ra.");
            }

            race.Status = RaceStatus.Finished;
            if (race.ActualEndTime is null) race.ActualEndTime = DateTime.UtcNow;
            race.UpdatedAt = DateTime.UtcNow;

            await _raceRepo.UpdateAsync(race);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, "Không thể kết thúc cuộc đua. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<bool>> CancelRaceAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");
            }

            // Finished is terminal under the locked V1.1 lifecycle regardless of
            // result status (Provisional or Official) — the event already
            // happened. Only pre-Finished races can be cancelled. This preserves
            // the pre-Phase2B intent (Scheduled/InProgress were always
            // cancellable) while dropping AwaitingResult/ResultPendingApproval,
            // which no longer exist as distinct RaceStatus values — both are
            // now Finished, which is non-cancellable.
            if (race.Status != RaceStatus.Scheduled && race.Status != RaceStatus.InProgress)
            {
                return ServiceResult<bool>.Fail(400, $"Không thể hủy cuộc đua với trạng thái '{race.Status}'.");
            }

            // Refund all pending predictions. Shared with the Tournament cancel cascade —
            // see PredictionRefundHelper. swallowIndividualFailures: true preserves this
            // method's original behavior of leaving a failed-refund Prediction Pending for
            // manual resolution instead of blocking the whole Race cancellation.
            await PredictionRefundHelper.RefundPendingPredictionsAsync(
                _db, _walletService, new[] { raceId }, "refund", swallowIndividualFailures: true);

            race.Status = RaceStatus.Cancelled;
            race.UpdatedAt = DateTime.UtcNow;

            await _raceRepo.UpdateAsync(race);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, "Không thể hủy cuộc đua. Vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<bool>> ReleaseHorseAsync(Guid raceId, Guid horseId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");

            // Allow release from any non-finished, non-cancelled race
            if (race.Status == RaceStatus.Finished || race.Status == RaceStatus.Cancelled)
                return ServiceResult<bool>.Fail(400, "Không thể giải phóng ngựa từ cuộc đua đã kết thúc hoặc đã hủy.");

            var entry = await _entryRepo.GetByRaceAndHorseAsync(raceId, horseId);
            if (entry == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy đăng ký tham gia");

            await _entryRepo.DeleteAsync(entry.Id);
            await _unitOfWork.SaveChangesAsync();
            await RecalculateOddsAsync(raceId);
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, "Không thể giải phóng ngựa. Vui lòng thử lại.");
        }
    }

    /// <summary>
    /// Q1: Admin-triggered generation of Round N+1 RaceEntries from Round N's Official rankings.
    /// Round/Race rows are never created here — both must already exist (created before Publish).
    /// Readiness, qualifier selection, round-robin distribution, capacity, and Jockey carry-forward
    /// all happen server-side; the caller supplies only the current Round's identity.
    /// </summary>
    public async Task<ServiceResult<GenerateNextRoundResultDto>> GenerateNextRoundEntriesAsync(Guid roundId, bool confirmShortfall = false, Guid actorId = default)
    {
        try
        {
            var currentRound = await _roundRepo.GetByIdAsync(roundId);
            if (currentRound == null)
                return ServiceResult<GenerateNextRoundResultDto>.Fail(404, "Không tìm thấy vòng đấu");

            var tournament = currentRound.Tournament ?? await _tournamentRepo.GetByIdAsync(currentRound.TournamentId);
            if (tournament == null)
                return ServiceResult<GenerateNextRoundResultDto>.Fail(404, "Không tìm thấy giải đấu");

            // V0 source of truth: Final is RoundNumber == Tournament.MaxRounds — never AdvanceCount==0.
            if (currentRound.RoundNumber >= tournament.MaxRounds)
                return ServiceResult<GenerateNextRoundResultDto>.Fail(400,
                    "Vòng đấu này là Vòng chung kết, không thể tạo vòng tiếp theo.");

            var allRounds = await _roundRepo.GetByTournamentAsync(tournament.Id);
            var nextRound = allRounds.FirstOrDefault(r => r.RoundNumber == currentRound.RoundNumber + 1);
            if (nextRound == null)
                return ServiceResult<GenerateNextRoundResultDto>.Fail(404,
                    "Không tìm thấy vòng đấu tiếp theo. Vòng đấu tiếp theo phải được tạo sẵn trước khi công bố giải đấu.");

            // Deterministic source order: ScheduledAt, then Id.
            var sourceRaces = (currentRound.Races ?? new List<Race>())
                .OrderBy(r => r.ScheduledAt).ThenBy(r => r.Id).ToList();
            if (sourceRaces.Count == 0)
                return ServiceResult<GenerateNextRoundResultDto>.Fail(400, "Vòng đấu hiện tại không có cuộc đua nào.");

            var targetRaces = (nextRound.Races ?? new List<Race>())
                .OrderBy(r => r.ScheduledAt).ThenBy(r => r.Id).ToList();
            if (targetRaces.Count == 0)
                return ServiceResult<GenerateNextRoundResultDto>.Fail(400,
                    "Vòng đấu tiếp theo chưa có cuộc đua nào để nhận ngựa đủ điều kiện.");

            if (!currentRound.AdvanceCount.HasValue)
                return ServiceResult<GenerateNextRoundResultDto>.Fail(409, "Vòng đấu hiện tại chưa thiết lập AdvanceCount.");
            var expectedAdvance = currentRound.AdvanceCount.Value;

            // ── Readiness + qualifier selection ──
            // Every source Race must be Finished with an Official result before ANY qualifier is
            // taken from it — a Cancelled/unfinished/Provisional Race rejects the whole generation
            // rather than guessing qualifiers from it.
            var qualifiers = new List<Guid>(); // deterministic flattened order: per Race (ScheduledAt,Id), then FinishPosition ascending
            foreach (var race in sourceRaces)
            {
                if (race.Status == RaceStatus.Cancelled)
                    return ServiceResult<GenerateNextRoundResultDto>.Fail(409,
                        $"Cuộc đua \"{race.Name}\" đã bị hủy — không thể tạo vòng tiếp theo.");
                if (race.Status != RaceStatus.Finished)
                    return ServiceResult<GenerateNextRoundResultDto>.Fail(409,
                        $"Cuộc đua \"{race.Name}\" chưa kết thúc.");

                var result = await _db.RaceResults.AsNoTracking().FirstOrDefaultAsync(r => r.RaceId == race.Id);
                if (result == null)
                    return ServiceResult<GenerateNextRoundResultDto>.Fail(409,
                        $"Cuộc đua \"{race.Name}\" chưa có kết quả.");
                if (result.Status != RaceResultStatus.Official)
                    return ServiceResult<GenerateNextRoundResultDto>.Fail(409,
                        $"Kết quả cuộc đua \"{race.Name}\" chưa ở trạng thái chính thức (Official).");

                var slots = race.QualificationSlots ?? 0;
                if (slots <= 0)
                    continue; // this Race contributes no qualifiers — still validated above regardless

                // Q1's qualification authority is the SAME canonical Official ranking R0/Admin
                // approval trusts — RaceResult.RankingsJson, re-parsed and re-validated against
                // this Race's CURRENT RaceEntry participants via the shared validator (also used
                // by AdminService.ApproveRaceResultAsync). RaceEntry.FinishPosition remains a
                // durable denormalized display/stat field written by that approval, but it is
                // NEVER the source Q1 selects qualifiers from or writes to.
                var participants = await _db.RaceEntries.AsNoTracking()
                    .Where(e => e.RaceId == race.Id)
                    .ToListAsync();

                List<RaceResultRankingItemRequest> ranking;
                try
                {
                    ranking = RaceResultRankingValidator.ParseAndValidate(result.RankingsJson, result.WinningHorseId, participants);
                }
                catch (InvalidOperationException ex)
                {
                    return ServiceResult<GenerateNextRoundResultDto>.Fail(409,
                        $"Cuộc đua \"{race.Name}\": {ex.Message}");
                }

                var completedInRace = ranking.Where(r => r.Status == "Completed").OrderBy(r => r.Position).ToList();
                qualifiers.AddRange(completedInRace.Take(slots).Select(r => r.HorseId));
            }

            // Số ngựa đủ điều kiện thực tế có thể KHÔNG khớp AdvanceCount của vòng (VD một số ngựa
            // DNF/DSQ khiến 1 race không đủ Completed để lấp đầy slot của nó) — quyết định thật sự
            // diễn ra ở cấp vòng đấu ngay dưới đây, không fail ngay theo từng race.
            var eligible = qualifiers.Count;

            if (eligible > expectedAdvance)
            {
                // Phòng thủ — không nên xảy ra nếu slot mỗi race đã được validate đúng lúc Publish. Nếu
                // xảy ra là có bug ở nơi khác, PHẢI fail loud, không được âm thầm cắt bớt qualifiers.
                return ServiceResult<GenerateNextRoundResultDto>.Fail(409,
                    $"Số ngựa đủ điều kiện được chọn ({eligible}) VƯỢT QUÁ AdvanceCount của vòng ({expectedAdvance}) — có sai lệch dữ liệu cần kiểm tra lại cấu hình suất đi tiếp của từng cuộc đua.");
            }

            if (eligible < expectedAdvance)
            {
                if (eligible == 0)
                {
                    // Không còn ngựa nào đủ điều kiện — toàn bộ ngựa của Round này đã vi phạm/bị loại
                    // (DNF/DSQ). Giải đấu coi như không còn ai để thi đấu tiếp — tự động huỷ giải. Đây
                    // là kết quả HỢP LỆ (200), không phải shortfall cần Admin xác nhận thêm —
                    // confirmShortfall không liên quan tới nhánh này (cả true/false đều dẫn tới cùng
                    // kết quả), giống walkover ở điểm này.
                    //
                    // Khác với walkover (Finished không có sẵn cascade, phải tự viết tay): nhánh
                    // Cancelled của ChangeStatusAsync ĐÃ TỰ cascade-cancel mọi Race chưa diễn ra + tự
                    // hoàn tiền Prediction đang Pending bên trong nó rồi (cùng transaction, cùng
                    // PredictionRefundHelper) — gọi thẳng, không viết lại cascade/refund thủ công.
                    var voidResult = await _tournamentService.ChangeStatusAsync(
                        tournament.Id,
                        new ChangeTournamentStatusRequest
                        {
                            NewStatus = TournamentStatus.Cancelled,
                            Reason = "Toàn bộ ngựa vi phạm/bị loại — không còn ngựa nào đủ điều kiện thi đấu tiếp."
                        },
                        actorId);
                    if (!voidResult.Result.Success)
                    {
                        // Không nên xảy ra (Ongoing -> Cancelled luôn hợp lệ, Reason đã được cung cấp
                        // sẵn ở trên) — nhưng nếu có, trả lỗi thay vì âm thầm coi như thành công.
                        return ServiceResult<GenerateNextRoundResultDto>.Fail(voidResult.StatusCode,
                            $"Không thể tự động huỷ giải đấu: {voidResult.Result.Message}");
                    }

                    return ServiceResult<GenerateNextRoundResultDto>.Ok(new GenerateNextRoundResultDto
                    {
                        SourceRoundNumber = currentRound.RoundNumber,
                        TargetRoundNumber = nextRound.RoundNumber,
                        GeneratedEntries = 0,
                        IsVoided = true
                    });
                }

                if (eligible == 1)
                {
                    // Walkover: đúng 1 ngựa đủ điều kiện — giải đấu coi như đã ngã ngũ, không tổ chức
                    // vòng tiếp theo nữa. Đây là kết quả HỢP LỆ (200), không phải shortfall cần Admin
                    // xác nhận thêm — confirmShortfall không liên quan tới nhánh này.
                    //
                    // Huỷ mọi Race CHƯA diễn ra của giải đấu này — y hệt cascade
                    // TournamentService.ChangeStatusAsync dùng khi chuyển Cancelled (cùng danh sách
                    // trạng thái, cùng cách hoàn tiền Prediction đang Pending), nhưng KHÔNG đi qua
                    // code path Cancelled toàn Tournament vì Tournament ở đây phải thành Finished, không
                    // phải Cancelled. Mọi Race đã Finished (đã diễn ra) được giữ nguyên, không đụng tới.
                    var walkoverWinnerHorseId = qualifiers[0];

                    await using var walkoverTransaction = await _db.Database.BeginTransactionAsync();

                    var walkoverCascadeFromStatuses = new[]
                    {
                        RaceStatus.Scheduled,
                        RaceStatus.InProgress,
                        RaceStatus.RegistrationOpen,
                        RaceStatus.RegistrationClosed
                    };
                    var walkoverRaceIdsToCancel = await _db.Races
                        .Where(r => r.TournamentId == tournament.Id && walkoverCascadeFromStatuses.Contains(r.Status))
                        .Select(r => r.Id)
                        .ToListAsync();
                    await PredictionRefundHelper.RefundPendingPredictionsAsync(
                        _db, _walletService, walkoverRaceIdsToCancel, $"tournament_walkover_{tournament.Id}", swallowIndividualFailures: false);
                    await _db.Races
                        .Where(r => r.TournamentId == tournament.Id && walkoverCascadeFromStatuses.Contains(r.Status))
                        .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RaceStatus.Cancelled));

                    // Đúng transition có sẵn (Ongoing -> Finished, whitelist-only, không có precondition
                    // nào chặn ở nhánh Finished) — không tự set tay tournament.Status để giữ nguyên mọi
                    // audit/validation hiện có của TournamentService.ChangeStatusAsync.
                    var finishResult = await _tournamentService.ChangeStatusAsync(
                        tournament.Id,
                        new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Finished },
                        actorId);
                    if (!finishResult.Result.Success)
                    {
                        // Không nên xảy ra (Ongoing -> Finished luôn hợp lệ, không có precondition nào ở
                        // nhánh Finished) — nhưng nếu có, để `using` rollback toàn bộ transaction thay vì
                        // để Race đã bị huỷ mà Tournament vẫn Ongoing.
                        return ServiceResult<GenerateNextRoundResultDto>.Fail(finishResult.StatusCode,
                            $"Không thể hoàn tất walkover: {finishResult.Result.Message}");
                    }

                    await walkoverTransaction.CommitAsync();

                    return ServiceResult<GenerateNextRoundResultDto>.Ok(new GenerateNextRoundResultDto
                    {
                        SourceRoundNumber = currentRound.RoundNumber,
                        TargetRoundNumber = nextRound.RoundNumber,
                        GeneratedEntries = 0,
                        IsWalkover = true,
                        WalkoverWinnerHorseId = walkoverWinnerHorseId
                    });
                }

                if (!confirmShortfall)
                {
                    // Thiếu một phần, còn đủ để tổ chức 1 cuộc đua (≥2) — cần Admin xác nhận rõ ràng
                    // trước khi tiếp tục với số ít hơn kế hoạch. FE dùng EligibleCount/RequiredAdvanceCount
                    // để dựng popup, rồi gọi lại đúng API này với confirmShortfall=true nếu Admin đồng ý.
                    return new ServiceResult<GenerateNextRoundResultDto>(409, new ApiResult<GenerateNextRoundResultDto>
                    {
                        Success = false,
                        Message = $"Chỉ có {eligible}/{expectedAdvance} ngựa đủ điều kiện đi tiếp. Cần xác nhận để tạo vòng tiếp theo với {eligible} ngựa.",
                        Data = new GenerateNextRoundResultDto
                        {
                            SourceRoundNumber = currentRound.RoundNumber,
                            TargetRoundNumber = nextRound.RoundNumber,
                            RequiresShortfallConfirmation = true,
                            EligibleCount = eligible,
                            RequiredAdvanceCount = expectedAdvance
                        }
                    });
                }
                // confirmShortfall == true và eligible >= 2 → cho đi tiếp, KHÔNG return, chạy tiếp
                // xuống dưới với đúng `eligible` ngựa trong `qualifiers` (không nhồi thêm gì).
            }

            // ── Idempotency: reject cleanly if Round N+1 already has ANY RaceEntries, including a
            // partial/inconsistent prior attempt — never merge/repair, always reject. ──
            var targetRaceIds = targetRaces.Select(r => r.Id).ToList();
            var nextRoundAlreadyPopulated = await _db.RaceEntries.AsNoTracking()
                .AnyAsync(e => targetRaceIds.Contains(e.RaceId));
            if (nextRoundAlreadyPopulated)
                return ServiceResult<GenerateNextRoundResultDto>.Fail(409,
                    "Vòng đấu tiếp theo đã có ngựa được tạo trước đó — không thể tạo lại.");

            // ── Resolve each qualified Horse's official Tournament-wide Jockey (J3/J3.1) ──
            // Missing pairing rejects the ENTIRE generation; Q1 never creates a RaceEntry with
            // JockeyId == null and never re-opens invitation selection.
            var jockeyByHorse = new Dictionary<Guid, Guid>();
            foreach (var horseId in qualifiers.Distinct())
            {
                var officialAssignment = await _entryRepo.GetOfficialAssignmentForHorseInTournamentAsync(horseId, tournament.Id);
                if (officialAssignment?.JockeyId == null)
                    return ServiceResult<GenerateNextRoundResultDto>.Fail(409,
                        $"Ngựa đủ điều kiện (Id={horseId}) chưa có kỵ sĩ chính thức cho giải đấu này.");
                jockeyByHorse[horseId] = officialAssignment.JockeyId.Value;
            }

            // ── Round-robin distribution across target Races, in deterministic target-Race order ──
            var assignments = targetRaces.ToDictionary(r => r.Id, _ => new List<Guid>());
            for (var i = 0; i < qualifiers.Count; i++)
            {
                var targetRace = targetRaces[i % targetRaces.Count];
                assignments[targetRace.Id].Add(qualifiers[i]);
            }

            // ── Round-robin can spread even a sufficient total across too many target Races, leaving
            // one with 0 or 1 horse (e.g. 2 qualifiers into 3 target Races) — that target Race could
            // never hold a meaningful race (needs >= 2 competitors), which round-robin distribution
            // alone cannot fix. This is a tournament-level structural problem, not something a Round-
            // level confirmShortfall can bypass. ──
            var underfilledRaces = targetRaces.Where(r => assignments[r.Id].Count < 2).ToList();
            if (underfilledRaces.Count > 0)
            {
                var names = string.Join(", ", underfilledRaces.Select(r => $"\"{r.Name}\" ({assignments[r.Id].Count} ngựa)"));
                return new ServiceResult<GenerateNextRoundResultDto>(409, new ApiResult<GenerateNextRoundResultDto>
                {
                    Success = false,
                    Message = $"Không thể phân bổ đủ ngựa cho mọi cuộc đua ở vòng tiếp theo — {names} sẽ không đủ 2 ngựa để tổ chức. Cần xử lý ở cấp giải đấu.",
                    Data = new GenerateNextRoundResultDto
                    {
                        SourceRoundNumber = currentRound.RoundNumber,
                        TargetRoundNumber = nextRound.RoundNumber,
                        RequiresTournamentLevelAction = true,
                        EligibleCount = eligible,
                        RequiredAdvanceCount = expectedAdvance
                    }
                });
            }

            // ── Capacity: reject the ENTIRE generation if any target Race can't hold its assigned
            // qualifiers. Race.MaxParticipants is already Publish-validated to fit Track.Capacity,
            // so no separate Track query is needed here. ──
            foreach (var race in targetRaces)
            {
                var assignedCount = assignments[race.Id].Count;
                if (assignedCount > race.MaxParticipants)
                    return ServiceResult<GenerateNextRoundResultDto>.Fail(409,
                        $"Cuộc đua \"{race.Name}\" không đủ chỗ cho {assignedCount} ngựa đủ điều kiện (tối đa {race.MaxParticipants}).");
            }

            // ── Insert — single transaction, all-or-nothing. Every prior step was read-only, so
            // nothing needs rolling back if THIS fails; the transaction's job is to make the insert
            // of (potentially many) RaceEntries atomic against itself and against a concurrent
            // duplicate generation attempt (the unique index on RaceEntries(RaceId,HorseId) makes a
            // racing second attempt fail here with a DbUpdateException instead of double-inserting). ──
            var newEntries = new List<RaceEntry>();
            foreach (var race in targetRaces)
            {
                foreach (var horseId in assignments[race.Id])
                {
                    newEntries.Add(new RaceEntry
                    {
                        Id = Guid.NewGuid(),
                        RaceId = race.Id,
                        HorseId = horseId,
                        JockeyId = jockeyByHorse[horseId],
                        Status = RegistrationStatus.Approved,
                        OwnerConfirmed = true,
                        JockeyConfirmed = true,
                        GateNumber = null,
                        FinishPosition = null
                    });
                }
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.RaceEntries.AddRange(newEntries);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                // Provider-portable classification (works identically under Npgsql production and
                // the Sqlite test provider, unlike inspecting a provider-specific error code):
                // roll back FIRST — querying through an aborted relational transaction would itself
                // fail — then clear the failed Added RaceEntry tracking so the re-query below can
                // only see actually-committed database state, never confused by our own failed
                // insert attempt. Only THEN can we tell whether this was a concurrent duplicate
                // generation (unique (RaceId,HorseId) index hit by a racing second attempt) or some
                // unrelated persistence failure.
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();

                var targetPopulatedNow = await _db.RaceEntries.AsNoTracking()
                    .AnyAsync(e => targetRaceIds.Contains(e.RaceId));
                if (!targetPopulatedNow)
                    throw; // not proven to be a duplicate — rethrow the ORIGINAL exception so the
                            // outer catch reports and logs it as an honest server failure (500)

                return ServiceResult<GenerateNextRoundResultDto>.Fail(409,
                    "Vòng đấu tiếp theo đã có ngựa được tạo trước đó — không thể tạo lại.");
            }

            foreach (var race in targetRaces)
                await RecalculateOddsAsync(race.Id);

            return ServiceResult<GenerateNextRoundResultDto>.Ok(new GenerateNextRoundResultDto
            {
                SourceRoundNumber = currentRound.RoundNumber,
                TargetRoundNumber = nextRound.RoundNumber,
                GeneratedEntries = newEntries.Count,
                Assignments = targetRaces.Select(r => new GenerateNextRoundRaceAssignmentDto
                {
                    RaceId = r.Id,
                    RaceName = r.Name,
                    HorseIds = assignments[r.Id]
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tạo vòng đấu tiếp theo cho vòng {RoundId}", roundId);
            return ServiceResult<GenerateNextRoundResultDto>.Fail(500, "Không thể tạo vòng đấu tiếp theo. Vui lòng thử lại.");
        }
    }

    private async Task<List<string>> ValidateQualificationSlotsForRaceSaveAsync(
        Guid roundId, Guid? currentRaceId, int? qualificationSlots, int maxParticipants)
    {
        var errors = new List<string>();
        var round = await _roundRepo.GetByIdAsync(roundId);

        if (!qualificationSlots.HasValue)
        {
            if (round?.AdvanceCount == 0)
                errors.Add("Vòng chung kết phải có 0 suất đi tiếp.");
            return errors;
        }

        if (qualificationSlots.Value < 0)
            errors.Add("Số suất đi tiếp từ cuộc đua không được âm.");

        if (round?.AdvanceCount is null)
            return errors;

        if (round.AdvanceCount.Value == 0)
        {
            if (qualificationSlots.Value != 0)
                errors.Add("Vòng chung kết phải có 0 suất đi tiếp.");
            return errors;
        }

        if (qualificationSlots.Value >= maxParticipants)
            errors.Add("Số suất đi tiếp từ cuộc đua phải nhỏ hơn số ngựa tối đa của cuộc đua.");

        var siblingSlots = await _db.Races
            .AsNoTracking()
            .Where(r => r.RoundId == roundId && (!currentRaceId.HasValue || r.Id != currentRaceId.Value))
            .SumAsync(r => r.QualificationSlots ?? 0);
        var candidateTotal = siblingSlots + qualificationSlots.Value;
        if (candidateTotal > round.AdvanceCount.Value)
        {
            errors.Add($"Tổng số suất đi tiếp của vòng ({candidateTotal}) không được vượt quá AdvanceCount ({round.AdvanceCount.Value}).");
        }

        return errors;
    }
    /// <summary>
    /// Phase5B Fix3: reject a Race scheduled outside its Round's window at Create/Update time,
    /// instead of only at Publish. Mirrors ValidateRoundScheduleWithinTournament's convention
    /// (inclusive Round boundaries, strict internal Race duration). ScheduledEndAt is optional on
    /// a Race — start containment is always checked, but end containment / self-consistency only
    /// runs once ScheduledEndAt is actually supplied.
    /// </summary>
    private static List<string> ValidateRaceScheduleWithinRound(Round round, DateTime scheduledAt, DateTime? scheduledEndAt)
    {
        var errors = new List<string>();

        if (round.ScheduledStartDate > scheduledAt)
            errors.Add("Thời gian bắt đầu Cuộc đua không được trước thời gian bắt đầu Vòng đấu.");

        if (scheduledEndAt.HasValue)
        {
            if (scheduledAt >= scheduledEndAt.Value)
                errors.Add("Thời gian bắt đầu phải trước thời gian kết thúc.");

            if (scheduledEndAt.Value > round.ScheduledEndDate)
                errors.Add("Thời gian kết thúc Cuộc đua không được sau thời gian kết thúc Vòng đấu.");
        }

        return errors;
    }

    private RaceDetailResponse MapToDetailResponse(Race race) => RaceDetailResponseMapper.ToDetailResponse(race);

    private async Task RecalculateOddsAsync(Guid raceId)
    {
        var entries = await _entryRepo.GetByRaceAsync(raceId);
        if (!entries.Any()) return;
        OddsCalculator.Recalculate(entries);
        await _unitOfWork.SaveChangesAsync();
    }
}
