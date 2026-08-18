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
        ApplicationDbContext db)
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
    }

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

            var activeJockeyInvitation = horse.JockeyInvitations
                .Where(invitation =>
                    invitation.Status == JockeyInvitationStatus.Pending ||
                    invitation.Status == JockeyInvitationStatus.Accepted)
                .OrderByDescending(invitation => invitation.CreatedAt)
                .FirstOrDefault();
            var raceAssignedJockey = horse.RaceEntries
                .Where(entry => entry.Jockey != null)
                .OrderByDescending(entry => entry.Race?.ScheduledAt ?? DateTime.MinValue)
                .Select(entry => entry.Jockey)
                .FirstOrDefault();
            var assignedJockey = activeJockeyInvitation?.Jockey ?? raceAssignedJockey;

            if (assignedJockey != null &&
                request.JockeyId.HasValue &&
                request.JockeyId.Value != assignedJockey.Id)
            {
                return ServiceResult<bool>.Fail(
                    400,
                    $"Ngựa đã được phân công cho kỵ sĩ {assignedJockey.User?.FullName ?? assignedJockey.Id.ToString()}");
            }

            if (assignedJockey != null)
            {
                request.JockeyId = assignedJockey.Id;
            }

            if (request.JockeyId.HasValue)
            {
                var jockey = await _jockeyRepo.GetByIdAsync(request.JockeyId.Value);
                if (jockey == null)
                {
                    return ServiceResult<bool>.Fail(404, "Không tìm thấy kỵ sĩ");
                }
                if (jockey.ApprovalStatus != ApprovalStatus.Approved)
                {
                    return ServiceResult<bool>.Fail(400, "Kỵ sĩ chưa được admin phê duyệt");
                }

                var hasScheduleConflict = await _entryRepo.HasJockeyScheduleConflictAsync(
                    jockey.Id, race.ScheduledAt, race.ScheduledEndAt ?? race.ScheduledAt.AddMinutes(30));
                if (hasScheduleConflict)
                {
                    return ServiceResult<bool>.Fail(
                        409,
                        "Kỵ sĩ này đã có cuộc đua trùng thời gian");
                }
            }

            var entry = new RaceEntry
            {
                Id = Guid.NewGuid(),
                RaceId = raceId,
                HorseId = request.HorseId,
                JockeyId = request.JockeyId,
                // Admin trực tiếp gán ngựa → tự duyệt đăng ký và tự động xác nhận
                Status = RegistrationStatus.Approved,
                OwnerConfirmed = true,
                JockeyConfirmed = true
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

                var entry = new RaceEntry
                {
                    Id = Guid.NewGuid(),
                    RaceId = raceId,
                    HorseId = horseId,
                    // Admin trực tiếp gán ngựa → tự duyệt đăng ký và tự động xác nhận
                    Status = RegistrationStatus.Approved,
                    OwnerConfirmed = true,
                    JockeyConfirmed = true
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

            if (race.Status != RaceStatus.RegistrationClosed)
            {
                return ServiceResult<bool>.Fail(400, $"Không thể bắt đầu cuộc đua với trạng thái '{race.Status}'. Phải đóng đăng ký trước khi bắt đầu.");
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

            // Refund all pending predictions
            var pendingPredictions = await _predictionRepo.GetByRaceAsync(raceId);
            var refunded = 0;
            foreach (var p in pendingPredictions.Where(p => p.Status == PredictionStatus.Pending))
            {
                try
                {
                    await _walletService.AddPointsAsync(p.SpectatorUserId, p.BetAmount, $"refund_{raceId}");
                    p.Status = PredictionStatus.Lost; // Use Lost as "refunded/cancelled"
                    p.SettledAt = DateTime.UtcNow;
                    refunded++;
                }
                catch
                {
                    // If refund fails, keep prediction as Pending for manual resolution
                }
            }

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
