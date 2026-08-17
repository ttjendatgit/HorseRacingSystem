using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Services;

public class TournamentService : ITournamentService
{
    private readonly ITournamentRepository _tournamentRepo;
    private readonly INotificationService _notificationService;
    private readonly IUserRepository _userRepo;
    private readonly IRaceRepository _raceRepo;
    private readonly IRaceEntryRepository _raceEntryRepo;
    private readonly IRefereeAssignmentRepository _assignmentRepo;
    private readonly IRoundRepository _roundRepo;
    private readonly IHorseRepository _horseRepo;
    private readonly IJockeyRepository _jockeyRepo;
    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;

    public TournamentService(
        ITournamentRepository tournamentRepo,
        INotificationService notificationService,
        IUserRepository userRepo,
        IRaceRepository raceRepo,
        IRaceEntryRepository raceEntryRepo,
        IRefereeAssignmentRepository assignmentRepo,
        IRoundRepository roundRepo,
        IHorseRepository horseRepo,
        IJockeyRepository jockeyRepo,
        ApplicationDbContext db,
        IUnitOfWork unitOfWork)
    {
        _tournamentRepo = tournamentRepo;
        _notificationService = notificationService;
        _userRepo = userRepo;
        _raceRepo = raceRepo;
        _raceEntryRepo = raceEntryRepo;
        _assignmentRepo = assignmentRepo;
        _roundRepo = roundRepo;
        _horseRepo = horseRepo;
        _jockeyRepo = jockeyRepo;
        _db = db;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<TournamentResponse>> CreateTournamentAsync(CreateTournamentRequest request)
    {
        try
        {
            HorseRacing.Models.SurfaceType? surfaceType = null;
            if (!string.IsNullOrWhiteSpace(request.SurfaceType))
            {
                if (!Enum.TryParse<HorseRacing.Models.SurfaceType>(request.SurfaceType, ignoreCase: true, out var parsedSurfaceType))
                {
                    return ServiceResult<TournamentResponse>.Fail(400, $"SurfaceType không hợp lệ: '{request.SurfaceType}'. Giá trị hợp lệ: Dirt, Turf, Synthetic, Sand.");
                }
                surfaceType = parsedSurfaceType;
            }

            var tournament = new Tournament
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                RegistrationDeadline = request.RegistrationDeadline,
                ImageUrl = request.ImageUrl,
                Status = TournamentStatus.Draft,
                IsActive = false, // Will be true when Published
                CreatedAt = DateTime.UtcNow,
                PrizePool = request.PrizePool,
                Venue = request.Venue,
                Country = request.Country,
                Category = request.Category,
                SurfaceType = surfaceType,
                MinParticipants = request.MinParticipants,
                MaxParticipants = request.MaxParticipants,
                MaxRounds = request.MaxRounds
            };

            var draftErrors = ValidateTournamentFields(tournament);
            if (draftErrors.Count > 0)
                return ServiceResult<TournamentResponse>.Fail(400, string.Join("; ", draftErrors));

            await _tournamentRepo.AddAsync(tournament);
            await _unitOfWork.SaveChangesAsync();

            // Notify all spectators about new tournament
            try
            {
                var users = await _userRepo.GetAllAsync();
                var spectators = users.Where(u => u.Role == UserRole.Spectator && u.IsActive).ToList();
                foreach (var s in spectators)
                {
                    try
                    {
                        await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                        {
                            UserId = s.Id,
                            Title = "Giải đấu mới",
                            Message = $"Giải đấu \"{tournament.Name}\" vừa được tạo. Đặt cược ngay!",
                            Type = NotificationType.InApp,
                            Category = NotificationCategory.TournamentCreated,
                            ActionUrl = $"/tournaments/{tournament.Id}",
                            RelatedEntityId = tournament.Id,
                            RelatedEntityType = "Tournament"
                        });
                    }
                    catch { /* skip failed notification for individual user */ }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail tournament creation
                System.Console.WriteLine($"Failed to send tournament notifications: {ex.Message}");
            }

            return new ServiceResult<TournamentResponse>(201, ApiResult<TournamentResponse>.Ok(await MapToResponseAsync(tournament)));
        }
        catch (Exception ex)
        {
            return ServiceResult<TournamentResponse>.Fail(500, $"Lỗi tạo giải đấu: {ex.Message}");
        }
    }

    public async Task<ServiceResult<TournamentResponse>> GetTournamentAsync(Guid id)
    {
        try
        {
            var tournament = await _tournamentRepo.GetByIdAsync(id);
            if (tournament == null)
            {
                return ServiceResult<TournamentResponse>.Fail(404, "Không tìm thấy giải đấu");
            }

            return ServiceResult<TournamentResponse>.Ok(await MapToResponseAsync(tournament));
        }
        catch (Exception ex)
        {
            return ServiceResult<TournamentResponse>.Fail(500, $"Lỗi truy xuất giải đấu: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<TournamentResponse>>> GetAllTournamentsAsync()
    {
        try
        {
            var tournaments = await _tournamentRepo.GetAllAsync();
            var responses = new List<TournamentResponse>();
            foreach (var t in tournaments)
            {
                responses.Add(await MapToResponseAsync(t));
            }
            return ServiceResult<IEnumerable<TournamentResponse>>.Ok(responses);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<TournamentResponse>>.Fail(
                500, $"Lỗi truy xuất danh sách giải đấu: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<TournamentResponse>>> GetActiveTournamentsAsync()
    {
        try
        {
            var tournaments = await _tournamentRepo.GetActiveAsync();
            var responses = new List<TournamentResponse>();
            foreach (var t in tournaments)
            {
                responses.Add(await MapToResponseAsync(t));
            }
            return ServiceResult<IEnumerable<TournamentResponse>>.Ok(responses);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<TournamentResponse>>.Fail(
                500, $"Lỗi truy xuất giải đấu đang hoạt động: {ex.Message}");
        }
    }

    public async Task<ServiceResult<TournamentResponse>> UpdateTournamentAsync(Guid id, UpdateTournamentRequest request)
    {
        try
        {
            var tournament = await _tournamentRepo.GetByIdAsync(id);
            if (tournament == null)
            {
                return ServiceResult<TournamentResponse>.Fail(404, "Không tìm thấy giải đấu");
            }

            // Only allow updates when Draft or Published
            if (tournament.Status != TournamentStatus.Draft && tournament.Status != TournamentStatus.Published)
            {
                return ServiceResult<TournamentResponse>.Fail(400,
                    $"Không thể chỉnh sửa giải đấu ở trạng thái {tournament.Status}. Chỉ có thể chỉnh sửa khi ở trạng thái Bản nháp hoặc Đã công bố.");
            }

            var isDraft = tournament.Status == TournamentStatus.Draft;

            // Phase4B: Immutable fields after Published — the VALUE cannot change, but the client
            // IS allowed to resend the current persisted value (legacy FE always sends all fields).
            // Only reject when the supplied value DIFFERS from the persisted value.
            if (!isDraft)
            {
                var immutableFieldErrors = new List<string>();
                if (request.StartDate.HasValue && request.StartDate.Value != tournament.StartDate)
                    immutableFieldErrors.Add("StartDate không thể thay đổi sau khi công bố giải đấu.");
                if (request.EndDate.HasValue && request.EndDate.Value != tournament.EndDate)
                    immutableFieldErrors.Add("EndDate không thể thay đổi sau khi công bố giải đấu.");
                if (request.RegistrationDeadline.HasValue && request.RegistrationDeadline.Value != tournament.RegistrationDeadline)
                    immutableFieldErrors.Add("RegistrationDeadline không thể thay đổi sau khi công bố giải đấu.");
                if (request.MinParticipants.HasValue && request.MinParticipants.Value != tournament.MinParticipants)
                    immutableFieldErrors.Add("MinParticipants không thể thay đổi sau khi công bố giải đấu.");
                if (request.MaxParticipants.HasValue && request.MaxParticipants.Value != tournament.MaxParticipants)
                    immutableFieldErrors.Add("MaxParticipants không thể thay đổi sau khi công bố giải đấu.");

                if (immutableFieldErrors.Count > 0)
                    return ServiceResult<TournamentResponse>.Fail(400, string.Join("; ", immutableFieldErrors));

                // Phase4B: Name and PrizePool stay mutable after Published, but their own business
                // invariants must still hold. Validate only the supplied values — do NOT run the
                // full Draft validator here, since unrelated immutable legacy data (e.g. an old
                // RegistrationDeadline/StartDate relationship) must not block this edit.
                var publishedMutableFieldErrors = new List<string>();
                if (request.Name != null)
                {
                    if (string.IsNullOrWhiteSpace(request.Name))
                        publishedMutableFieldErrors.Add("Tên giải đấu (Name) không được để trống.");
                    else if (request.Name.Length > 200)
                        publishedMutableFieldErrors.Add("Tên giải đấu (Name) không được vượt quá 200 ký tự.");
                }
                if (request.PrizePool.HasValue && request.PrizePool.Value < 0)
                    publishedMutableFieldErrors.Add("PrizePool không được âm.");

                if (publishedMutableFieldErrors.Count > 0)
                    return ServiceResult<TournamentResponse>.Fail(400, string.Join("; ", publishedMutableFieldErrors));
            }

            // Validate before mutating anything, so an invalid SurfaceType never leaves a partial update in memory.
            HorseRacing.Models.SurfaceType? parsedSurfaceType = tournament.SurfaceType;
            if (!string.IsNullOrWhiteSpace(request.SurfaceType))
            {
                if (!Enum.TryParse<HorseRacing.Models.SurfaceType>(request.SurfaceType, ignoreCase: true, out var newSurfaceType))
                {
                    return ServiceResult<TournamentResponse>.Fail(400, $"SurfaceType không hợp lệ: '{request.SurfaceType}'. Giá trị hợp lệ: Dirt, Turf, Synthetic, Sand.");
                }
                parsedSurfaceType = newSurfaceType;
            }

            // Phase4B: compute candidate state BEFORE mutating the tracked entity. This prevents
            // leaving dirty values in the ChangeTracker if validation rejects the update.
            // Name semantics: null => omitted (preserve existing); non-null => supplied value
            // (including "" and whitespace — validation will catch those for Draft).
            var candidateName = request.Name != null ? request.Name : tournament.Name;
            var candidateDescription = request.Description != null ? request.Description : tournament.Description;
            var candidateStartDate = request.StartDate.HasValue ? request.StartDate.Value : tournament.StartDate;
            var candidateEndDate = request.EndDate.HasValue ? request.EndDate.Value : tournament.EndDate;
            var candidateRegistrationDeadline = request.RegistrationDeadline.HasValue ? request.RegistrationDeadline.Value : tournament.RegistrationDeadline;
            var candidateImageUrl = request.ImageUrl != null ? request.ImageUrl : tournament.ImageUrl;
            var candidatePrizePool = request.PrizePool.HasValue ? request.PrizePool.Value : tournament.PrizePool;
            var candidateVenue = request.Venue != null ? request.Venue : tournament.Venue;
            var candidateCountry = request.Country != null ? request.Country : tournament.Country;
            var candidateCategory = request.Category != null ? request.Category : tournament.Category;
            var candidateSurfaceType = parsedSurfaceType;
            var candidateMinParticipants = request.MinParticipants.HasValue ? request.MinParticipants.Value : tournament.MinParticipants;
            var candidateMaxParticipants = request.MaxParticipants.HasValue ? request.MaxParticipants.Value : tournament.MaxParticipants;
            var candidateMaxRounds = request.MaxRounds.HasValue ? request.MaxRounds.Value : tournament.MaxRounds;

            // Phase4B: re-validate Draft-save rules against the CANDIDATE state, not the tracked entity.
            if (isDraft)
            {
                var candidateTournament = new Tournament
                {
                    Name = candidateName,
                    StartDate = candidateStartDate,
                    EndDate = candidateEndDate,
                    RegistrationDeadline = candidateRegistrationDeadline,
                    PrizePool = candidatePrizePool,
                    MinParticipants = candidateMinParticipants,
                    MaxParticipants = candidateMaxParticipants,
                };
                var draftErrors = ValidateTournamentFields(candidateTournament);
                if (draftErrors.Count > 0)
                    return ServiceResult<TournamentResponse>.Fail(400, string.Join("; ", draftErrors));
            }

            // All validation passed — now apply values to the tracked entity.
            // For Published state, never assign immutable fields (they haven't changed anyway).
            if (request.Name != null)
                tournament.Name = candidateName;
            if (request.Description != null)
                tournament.Description = candidateDescription;
            if (isDraft)
            {
                if (request.StartDate.HasValue)
                    tournament.StartDate = candidateStartDate;
                if (request.EndDate.HasValue)
                    tournament.EndDate = candidateEndDate;
                if (request.RegistrationDeadline.HasValue)
                    tournament.RegistrationDeadline = candidateRegistrationDeadline;
                if (request.MinParticipants.HasValue)
                    tournament.MinParticipants = candidateMinParticipants;
                if (request.MaxParticipants.HasValue)
                    tournament.MaxParticipants = candidateMaxParticipants;
            }
            // IsActive intentionally not settable here (Phase4B) — server-owned, lifecycle-only.
            if (request.ImageUrl != null)
                tournament.ImageUrl = candidateImageUrl;

            // Phase3B additions — null/omitted leaves the existing value untouched (see UpdateTournamentRequest).
            if (request.PrizePool.HasValue)
                tournament.PrizePool = candidatePrizePool;
            if (request.Venue != null)
                tournament.Venue = candidateVenue;
            if (request.Country != null)
                tournament.Country = candidateCountry;
            if (request.Category != null)
                tournament.Category = candidateCategory;
            tournament.SurfaceType = candidateSurfaceType;
            if (request.MaxRounds.HasValue)
                tournament.MaxRounds = candidateMaxRounds;

            tournament.UpdatedAt = DateTime.UtcNow;

            await _tournamentRepo.UpdateAsync(tournament);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<TournamentResponse>.Ok(await MapToResponseAsync(tournament));
        }
        catch (Exception ex)
        {
            return ServiceResult<TournamentResponse>.Fail(500, $"Lỗi cập nhật giải đấu: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> DeleteTournamentAsync(Guid id)
    {
        try
        {
            var tournament = await _tournamentRepo.GetByIdAsync(id);
            if (tournament == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy giải đấu");

            // Get all race IDs in this tournament
            var raceIds = (await _raceRepo.GetByTournamentAsync(id)).Select(r => r.Id).ToList();

            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Delete every race and all of its RESTRICT dependants first.
            foreach (var raceId in raceIds)
            {
                await RaceDeletionHelper.DeleteRaceGraphAsync(_db, raceId);
            }

            await _db.TournamentHorseRegistrations.Where(r => r.TournamentId == id).ExecuteDeleteAsync();
            await _db.Prizes.Where(p => p.TournamentId == id).ExecuteDeleteAsync();
            await _db.Rounds.Where(r => r.TournamentId == id).ExecuteDeleteAsync();
            await _db.Tournaments.Where(t => t.Id == id).ExecuteDeleteAsync();

            await transaction.CommitAsync();

            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi xóa giải đấu: {ex.Message}");
        }
    }

    public async Task<ServiceResult<TournamentResponse>> ChangeStatusAsync(Guid id, ChangeTournamentStatusRequest request, Guid actorId)
    {
        try
        {
            var tournament = await _tournamentRepo.GetByIdAsync(id);
            if (tournament == null)
                return ServiceResult<TournamentResponse>.Fail(404, "Không tìm thấy giải đấu");

            var currentStatus = tournament.Status;
            var newStatus = request.NewStatus;

            // Validate state transition — topology unchanged from Phase3 (whitelist-only).
            var isValidTransition = IsValidStatusTransition(currentStatus, newStatus);
            if (!isValidTransition)
                return ServiceResult<TournamentResponse>.Fail(400,
                    $"Không thể chuyển từ {currentStatus} sang {newStatus}");

            // Phase4B §1: Publish is all-or-nothing per locked V1.1 §4.2. Tournament-level checks
            // are real; the Round/Race structural checks (Sequence, Final Round, AdvanceCount,
            // Track, QualificationSlots) are Phase5's job. Until Phase5 lands, this transition
            // NEVER completes — Tournament.Status/PublishedAt/IsActive are never touched here.
            if (newStatus == TournamentStatus.Published)
            {
                var publishErrors = await ValidatePublishTournamentFieldsAsync(tournament);
                if (publishErrors.Count > 0)
                    return ServiceResult<TournamentResponse>.Fail(400, string.Join("; ", publishErrors));

                // Phase5 internal note: Round/Race structural readiness checks not yet implemented.
                return ServiceResult<TournamentResponse>.Fail(400,
                    "Giải đấu chưa thể công bố vì cấu hình Vòng đấu/Cuộc đua chưa hoàn tất.");
            }

            if (newStatus == TournamentStatus.Cancelled)
            {
                var trimmedReason = request.Reason?.Trim();
                if (string.IsNullOrWhiteSpace(trimmedReason))
                    return ServiceResult<TournamentResponse>.Fail(400, "Lý do hủy giải đấu (Reason) là bắt buộc.");

                // Tournament status flip + child-Race cascade must be atomic: either both persist
                // or neither does. Both operations run against the same DbContext/connection inside
                // one transaction — the Tournament change via SaveChangesAsync (change-tracked), the
                // Race cascade via a single bulk ExecuteUpdateAsync (bypasses the tracker, one UPDATE
                // statement, no N+1) — and only commit once both have succeeded. Same pattern already
                // used by DeleteTournamentAsync/DeleteRoundAsync in this file.
                await using var transaction = await _db.Database.BeginTransactionAsync();

                tournament.Status = newStatus;
                tournament.UpdatedAt = DateTime.UtcNow;
                tournament.CancelledAt = DateTime.UtcNow;
                tournament.CancelledBy = actorId;
                tournament.CancellationReason = trimmedReason;
                tournament.IsActive = false;

                await _tournamentRepo.UpdateAsync(tournament);
                await _unitOfWork.SaveChangesAsync();

                // V1.1 §14.2/§15 cascade: every Race not yet Finished cancels; Finished (and
                // already-Cancelled) Races and all historical data are left untouched. The current
                // transitional RaceStatus set also includes RegistrationOpen/RegistrationClosed,
                // which must cascade too — only Finished/Cancelled are excluded.
                var cascadeFromStatuses = new[]
                {
                    RaceStatus.Scheduled,
                    RaceStatus.InProgress,
                    RaceStatus.RegistrationOpen,
                    RaceStatus.RegistrationClosed
                };
                await _db.Races
                    .Where(r => r.TournamentId == id && cascadeFromStatuses.Contains(r.Status))
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RaceStatus.Cancelled));

                await transaction.CommitAsync();

                return ServiceResult<TournamentResponse>.Ok(await MapToResponseAsync(tournament));
            }

            // Published->Ongoing and Ongoing->Finished: behaviorally unchanged in Phase4B — both
            // depend on registration/RaceEntry-confirmation/qualification/result/prize work that is
            // explicitly out of this phase's scope (see Phase4A §11). Deferred, not implemented here.
            tournament.Status = newStatus;
            tournament.UpdatedAt = DateTime.UtcNow;

            switch (newStatus)
            {
                case TournamentStatus.Ongoing:
                    tournament.StartedAt = DateTime.UtcNow;
                    break;
                case TournamentStatus.Finished:
                    tournament.FinishedAt = DateTime.UtcNow;
                    break;
            }

            tournament.IsActive = newStatus == TournamentStatus.Ongoing;

            await _tournamentRepo.UpdateAsync(tournament);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<TournamentResponse>.Ok(await MapToResponseAsync(tournament));
        }
        catch (Exception ex)
        {
            return ServiceResult<TournamentResponse>.Fail(500, $"Lỗi thay đổi trạng thái: {ex.Message}");
        }
    }

    /// <summary>
    /// Draft-save rules (V1.1 §4.1) — reusable by both CreateTournamentAsync, Draft-state
    /// UpdateTournamentAsync, and as the base layer of ValidatePublishTournamentFieldsAsync.
    /// </summary>
    private static List<string> ValidateTournamentFields(Tournament tournament)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(tournament.Name))
            errors.Add("Tên giải đấu (Name) không được để trống.");
        else if (tournament.Name.Length > 200)
            errors.Add("Tên giải đấu (Name) không được vượt quá 200 ký tự.");

        if (!tournament.MaxParticipants.HasValue)
            errors.Add("Số lượng người tham gia tối đa (MaxParticipants) là bắt buộc.");
        else if (tournament.MaxParticipants.Value <= 0)
            errors.Add("MaxParticipants phải lớn hơn 0.");

        if (!tournament.MinParticipants.HasValue)
        {
            errors.Add("Số lượng người tham gia tối thiểu (MinParticipants) là bắt buộc.");
        }
        else
        {
            if (tournament.MinParticipants.Value < 3)
                errors.Add("MinParticipants phải lớn hơn hoặc bằng 3.");
            if (tournament.MaxParticipants.HasValue && tournament.MinParticipants.Value > tournament.MaxParticipants.Value)
                errors.Add("MinParticipants không được lớn hơn MaxParticipants.");
        }

        if (tournament.StartDate >= tournament.EndDate)
            errors.Add("StartDate phải nhỏ hơn EndDate (không được bằng hoặc lớn hơn).");

        // RegistrationDeadline is REQUIRED at Draft save.
        if (!tournament.RegistrationDeadline.HasValue)
        {
            errors.Add("Hạn đăng ký (RegistrationDeadline) là bắt buộc.");
        }
        else if (tournament.RegistrationDeadline.Value >= tournament.StartDate)
        {
            errors.Add("RegistrationDeadline phải nhỏ hơn StartDate (không được bằng hoặc lớn hơn).");
        }

        if (tournament.PrizePool < 0)
            errors.Add("PrizePool không được âm.");

        return errors;
    }

    /// <summary>
    /// Tournament-level Publish readiness (V1.1 §4.2) — Draft rules plus the now-anchored date
    /// chain and Round existence. Deliberately does NOT check Round Sequence/Final-Round/
    /// AdvanceCount/Race/Track/QualificationSlots invariants — those are Phase5. This method is
    /// designed to be the first building block Phase5's complete ValidatePublishReadinessAsync
    /// composes with its own structural checks, not a throwaway placeholder.
    /// </summary>
    private async Task<List<string>> ValidatePublishTournamentFieldsAsync(Tournament tournament)
    {
        var errors = ValidateTournamentFields(tournament);

        if (!tournament.RegistrationDeadline.HasValue)
        {
            errors.Add("RegistrationDeadline phải được thiết lập trước khi công bố giải đấu.");
        }
        else if (DateTime.UtcNow >= tournament.RegistrationDeadline.Value)
        {
            errors.Add("Hạn đăng ký (RegistrationDeadline) phải ở tương lai so với thời điểm hiện tại để công bố giải đấu.");
        }

        var hasRound = await _db.Rounds.AnyAsync(r => r.TournamentId == tournament.Id);
        if (!hasRound)
            errors.Add("Giải đấu phải có ít nhất 1 Vòng đấu (Round) trước khi công bố.");

        return errors;
    }

    public async Task<ServiceResult<TournamentStatsDto>> GetStatsAsync(Guid id)
    {
        try
        {
            var tournament = await _tournamentRepo.GetByIdAsync(id);
            if (tournament == null)
                return ServiceResult<TournamentStatsDto>.Fail(404, "Không tìm thấy giải đấu");

            var stats = await CalculateStatsAsync(tournament);
            return ServiceResult<TournamentStatsDto>.Ok(stats);
        }
        catch (Exception ex)
        {
            return ServiceResult<TournamentStatsDto>.Fail(500, $"Lỗi lấy thống kê: {ex.Message}");
        }
    }

    public async Task<ServiceResult<List<TournamentTimelineDto>>> GetTimelineAsync(Guid id)
    {
        try
        {
            var tournament = await _tournamentRepo.GetByIdAsync(id);
            if (tournament == null)
                return ServiceResult<List<TournamentTimelineDto>>.Fail(404, "Không tìm thấy giải đấu");

            var timeline = new List<TournamentTimelineDto>();

            // Add creation event
            timeline.Add(new TournamentTimelineDto
            {
                Id = Guid.NewGuid(),
                Timestamp = tournament.CreatedAt,
                Action = "Tạo giải đấu",
                Actor = "Admin",
                Status = TournamentStatus.Draft
            });

            // Add status change events
            if (tournament.PublishedAt.HasValue)
                timeline.Add(new TournamentTimelineDto
                {
                    Id = Guid.NewGuid(),
                    Timestamp = tournament.PublishedAt.Value,
                    Action = "Công bố giải đấu",
                    Actor = "Admin",
                    Status = TournamentStatus.Published
                });

            if (tournament.StartedAt.HasValue)
                timeline.Add(new TournamentTimelineDto
                {
                    Id = Guid.NewGuid(),
                    Timestamp = tournament.StartedAt.Value,
                    Action = "Bắt đầu giải đấu",
                    Actor = "Admin",
                    Status = TournamentStatus.Ongoing
                });

            if (tournament.FinishedAt.HasValue)
                timeline.Add(new TournamentTimelineDto
                {
                    Id = Guid.NewGuid(),
                    Timestamp = tournament.FinishedAt.Value,
                    Action = "Kết thúc giải đấu",
                    Actor = "Admin",
                    Status = TournamentStatus.Finished
                });

            if (tournament.CancelledAt.HasValue)
                timeline.Add(new TournamentTimelineDto
                {
                    Id = Guid.NewGuid(),
                    Timestamp = tournament.CancelledAt.Value,
                    Action = "Hủy giải đấu",
                    Actor = "Admin",
                    Status = TournamentStatus.Cancelled
                });

            // Sort by timestamp descending (newest first)
            timeline = timeline.OrderByDescending(t => t.Timestamp).ToList();

            return ServiceResult<List<TournamentTimelineDto>>.Ok(timeline);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<TournamentTimelineDto>>.Fail(500, $"Lỗi lấy timeline: {ex.Message}");
        }
    }

    private bool IsValidStatusTransition(TournamentStatus current, TournamentStatus next)
    {
        return (current, next) switch
        {
            (TournamentStatus.Draft, TournamentStatus.Published) => true,
            (TournamentStatus.Published, TournamentStatus.Ongoing) => true,
            (TournamentStatus.Ongoing, TournamentStatus.Finished) => true,
            (TournamentStatus.Draft, TournamentStatus.Cancelled) => true,
            (TournamentStatus.Published, TournamentStatus.Cancelled) => true,
            (TournamentStatus.Ongoing, TournamentStatus.Cancelled) => true,
            _ => false
        };
    }

    private List<NextTransitionDto> GetNextTransitions(TournamentStatus current)
    {
        var transitions = new List<NextTransitionDto>();

        switch (current)
        {
            case TournamentStatus.Draft:
                transitions.Add(new NextTransitionDto
                {
                    Status = TournamentStatus.Published,
                    Label = "Công bố giải",
                    IsPrimary = true
                });
                transitions.Add(new NextTransitionDto
                {
                    Status = TournamentStatus.Cancelled,
                    Label = "Hủy giải",
                    IsPrimary = false
                });
                break;

            case TournamentStatus.Published:
                transitions.Add(new NextTransitionDto
                {
                    Status = TournamentStatus.Ongoing,
                    Label = "Bắt đầu giải",
                    IsPrimary = true
                });
                transitions.Add(new NextTransitionDto
                {
                    Status = TournamentStatus.Cancelled,
                    Label = "Hủy giải",
                    IsPrimary = false
                });
                break;

            case TournamentStatus.Ongoing:
                transitions.Add(new NextTransitionDto
                {
                    Status = TournamentStatus.Finished,
                    Label = "Kết thúc giải",
                    IsPrimary = true
                });
                transitions.Add(new NextTransitionDto
                {
                    Status = TournamentStatus.Cancelled,
                    Label = "Hủy giải",
                    IsPrimary = false
                });
                break;
        }

        return transitions;
    }

    private async Task<TournamentStatsDto> CalculateStatsAsync(Tournament tournament)
    {
        var races = await _raceRepo.GetByTournamentAsync(tournament.Id);
        var raceIds = races.Select(r => r.Id).ToList();

        var entries = new List<RaceEntry>();
        foreach (var raceId in raceIds)
        {
            var raceEntries = await _raceEntryRepo.GetByRaceAsync(raceId);
            entries.AddRange(raceEntries);
        }

        var horseIds = entries.Select(e => e.HorseId).Distinct().ToList();
        var jockeyIds = entries.Select(e => e.JockeyId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        int? daysRemaining = null;
        if (tournament.Status == TournamentStatus.Published || tournament.Status == TournamentStatus.Ongoing)
        {
            var daysLeft = (tournament.EndDate - DateTime.UtcNow).Days;
            daysRemaining = daysLeft > 0 ? daysLeft : 0;
        }

        return new TournamentStatsDto
        {
            RaceCount = races.Count,
            EntryCount = entries.Count,
            HorseCount = horseIds.Count,
            JockeyCount = jockeyIds.Count,
            DaysRemaining = daysRemaining
        };
    }

    private async Task<TournamentResponse> MapToResponseAsync(Tournament tournament)
    {
        var stats = await CalculateStatsAsync(tournament);
        var nextTransitions = GetNextTransitions(tournament.Status);

        return new TournamentResponse
        {
            Id = tournament.Id,
            Name = tournament.Name,
            Description = tournament.Description,
            StartDate = tournament.StartDate,
            EndDate = tournament.EndDate,
            IsActive = tournament.IsActive,
            RoundCount = tournament.Rounds?.Count ?? 0,
            RaceCount = stats.RaceCount,
            ImageUrl = tournament.ImageUrl,
            CreatedAt = tournament.CreatedAt,
            UpdatedAt = tournament.UpdatedAt,
            Status = tournament.Status,
            StatusName = tournament.Status.ToString(),
            RegistrationDeadline = tournament.RegistrationDeadline,
            PublishedAt = tournament.PublishedAt,
            StartedAt = tournament.StartedAt,
            FinishedAt = tournament.FinishedAt,
            CancelledAt = tournament.CancelledAt,
            PrizePool = tournament.PrizePool,
            Venue = tournament.Venue,
            Country = tournament.Country,
            Category = tournament.Category,
            SurfaceType = tournament.SurfaceType?.ToString(),
            MinParticipants = tournament.MinParticipants,
            MaxParticipants = tournament.MaxParticipants,
            MaxRounds = tournament.MaxRounds,
            CancelledBy = tournament.CancelledBy,
            CancellationReason = tournament.CancellationReason,
            Stats = stats,
            NextTransitions = nextTransitions
        };
    }

    private TournamentResponse MapToResponse(Tournament tournament)
    {
        return new TournamentResponse
        {
            Id = tournament.Id,
            Name = tournament.Name,
            Description = tournament.Description,
            StartDate = tournament.StartDate,
            EndDate = tournament.EndDate,
            IsActive = tournament.IsActive,
            RoundCount = tournament.Rounds?.Count ?? 0,
            RaceCount = tournament.Races?.Count ?? 0,
            ImageUrl = tournament.ImageUrl,
            CreatedAt = tournament.CreatedAt,
            UpdatedAt = tournament.UpdatedAt,
            Status = tournament.Status,
            StatusName = tournament.Status.ToString(),
            RegistrationDeadline = tournament.RegistrationDeadline,
            PublishedAt = tournament.PublishedAt,
            StartedAt = tournament.StartedAt,
            FinishedAt = tournament.FinishedAt,
            CancelledAt = tournament.CancelledAt,
            PrizePool = tournament.PrizePool,
            Venue = tournament.Venue,
            Country = tournament.Country,
            Category = tournament.Category,
            SurfaceType = tournament.SurfaceType?.ToString(),
            MinParticipants = tournament.MinParticipants,
            MaxParticipants = tournament.MaxParticipants,
            MaxRounds = tournament.MaxRounds,
            CancelledBy = tournament.CancelledBy,
            CancellationReason = tournament.CancellationReason
        };
    }
}

public class RoundService : IRoundService
{
    private readonly IRoundRepository _roundRepo;
    private readonly ITournamentRepository _tournamentRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _db;

    public RoundService(IRoundRepository roundRepo, ITournamentRepository tournamentRepo, IUnitOfWork unitOfWork, ApplicationDbContext db)
    {
        _roundRepo = roundRepo;
        _tournamentRepo = tournamentRepo;
        _unitOfWork = unitOfWork;
        _db = db;
    }

    public async Task<ServiceResult<RoundResponse>> CreateRoundAsync(CreateRoundRequest request)
    {
        try
        {
            var tournament = await _tournamentRepo.GetByIdAsync(request.TournamentId);
            if (tournament == null)
            {
                return ServiceResult<RoundResponse>.Fail(404, "Không tìm thấy giải đấu");
            }

            var round = new Round
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                TournamentId = request.TournamentId,
                RoundNumber = request.RoundNumber,
                ScheduledStartDate = request.ScheduledStartDate,
                ScheduledEndDate = request.ScheduledEndDate,
                Description = request.Description,
                AdvanceCount = request.AdvanceCount
            };

            await _roundRepo.AddAsync(round);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<RoundResponse>(201, ApiResult<RoundResponse>.Ok(await MapToResponseAsync(round)));
        }
        catch (Exception ex)
        {
            return ServiceResult<RoundResponse>.Fail(500, $"Lỗi tạo vòng đấu: {ex.Message}");
        }
    }

    public async Task<ServiceResult<RoundResponse>> GetRoundAsync(Guid id)
    {
        try
        {
            var round = await _roundRepo.GetByIdAsync(id);
            if (round == null)
            {
                return ServiceResult<RoundResponse>.Fail(404, "Không tìm thấy vòng đấu");
            }

            return ServiceResult<RoundResponse>.Ok(await MapToResponseAsync(round));
        }
        catch (Exception ex)
        {
            return ServiceResult<RoundResponse>.Fail(500, $"Lỗi truy xuất vòng đấu: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<RoundResponse>>> GetRoundsByTournamentAsync(Guid tournamentId)
    {
        try
        {
            var rounds = await _roundRepo.GetByTournamentAsync(tournamentId);
            // Sequential awaits: DbContext is not thread-safe for concurrent async operations,
            // so a parallel Task.WhenAll over MapToResponseAsync (which queries _db) is not safe here.
            var responses = new List<RoundResponse>();
            foreach (var r in rounds)
            {
                responses.Add(await MapToResponseAsync(r));
            }
            return ServiceResult<IEnumerable<RoundResponse>>.Ok(responses);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<RoundResponse>>.Fail(
                500, $"Lỗi truy xuất danh sách vòng đấu: {ex.Message}");
        }
    }

    public async Task<ServiceResult<RoundResponse>> UpdateRoundAsync(Guid id, UpdateRoundRequest request)
    {
        try
        {
            var round = await _roundRepo.GetByIdAsync(id);
            if (round == null)
            {
                return ServiceResult<RoundResponse>.Fail(404, "Không tìm thấy vòng đấu");
            }

            if (!string.IsNullOrEmpty(request.Name))
                round.Name = request.Name;
            if (request.RoundNumber.HasValue)
                round.RoundNumber = request.RoundNumber.Value;
            if (request.ScheduledStartDate.HasValue)
                round.ScheduledStartDate = request.ScheduledStartDate.Value;
            if (request.ScheduledEndDate.HasValue)
                round.ScheduledEndDate = request.ScheduledEndDate.Value;
            if (request.ActualStartDate.HasValue)
                round.ActualStartDate = request.ActualStartDate.Value;
            if (request.ActualEndDate.HasValue)
                round.ActualEndDate = request.ActualEndDate.Value;
            if (request.Description != null)
                round.Description = request.Description;
            if (request.AdvanceCount.HasValue)
                round.AdvanceCount = request.AdvanceCount.Value;

            await _roundRepo.UpdateAsync(round);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<RoundResponse>.Ok(await MapToResponseAsync(round));
        }
        catch (Exception ex)
        {
            return ServiceResult<RoundResponse>.Fail(500, $"Lỗi cập nhật vòng đấu: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> DeleteRoundAsync(Guid id)
    {
        try
        {
            var round = await _roundRepo.GetByIdAsync(id);
            if (round == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy vòng đấu");

            await using var transaction = await _db.Database.BeginTransactionAsync();
            var raceIds = await _db.Races
                .Where(r => r.RoundId == id)
                .Select(r => r.Id)
                .ToListAsync();

            foreach (var raceId in raceIds)
            {
                await RaceDeletionHelper.DeleteRaceGraphAsync(_db, raceId);
            }

            await _db.Rounds.Where(r => r.Id == id).ExecuteDeleteAsync();
            await transaction.CommitAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi xóa vòng đấu: {ex.Message}");
        }
    }

    private async Task<RoundResponse> MapToResponseAsync(Round round)
    {
        return new RoundResponse
        {
            Id = round.Id,
            Name = round.Name,
            TournamentId = round.TournamentId,
            RoundNumber = round.RoundNumber,
            ScheduledStartDate = round.ScheduledStartDate,
            ScheduledEndDate = round.ScheduledEndDate,
            ActualStartDate = round.ActualStartDate,
            ActualEndDate = round.ActualEndDate,
            Description = round.Description,
            RaceCount = round.Races?.Count ?? 0,
            AdvanceCount = round.AdvanceCount,
            PlannedParticipants = await ComputePlannedParticipantsAsync(round),
            ActualParticipants = await ComputeActualParticipantsAsync(round.Id)
        };
    }

    /// <summary>
    /// Round 1: Tournament.MaxParticipants (null passes through). Round N&gt;1: the AdvanceCount of the
    /// single Round with RoundNumber == N-1 in the same Tournament. Zero or more-than-one such
    /// predecessor returns null rather than guessing — Round sequence uniqueness is not enforced
    /// until Phase5, so duplicates are a real possibility today.
    /// </summary>
    private async Task<int?> ComputePlannedParticipantsAsync(Round round)
    {
        if (round.RoundNumber == 1)
        {
            return await _db.Tournaments
                .Where(t => t.Id == round.TournamentId)
                .Select(t => t.MaxParticipants)
                .FirstOrDefaultAsync();
        }

        var predecessorAdvanceCounts = await _db.Rounds
            .Where(r => r.TournamentId == round.TournamentId && r.RoundNumber == round.RoundNumber - 1)
            .Select(r => r.AdvanceCount)
            .ToListAsync();

        return predecessorAdvanceCounts.Count == 1 ? predecessorAdvanceCounts[0] : null;
    }

    /// <summary>
    /// COUNT(DISTINCT RaceEntry.HorseId) across RaceEntries whose Race.RoundId == roundId. No
    /// RegistrationStatus filter — matches the existing participant-counting behavior elsewhere
    /// (RaceEntryRepository.GetByRaceAsync, used by the MaxParticipants capacity gate, applies none
    /// either). DISTINCT is required because the one-Horse-per-Round DB constraint doesn't exist yet.
    /// </summary>
    private Task<int> ComputeActualParticipantsAsync(Guid roundId)
    {
        return _db.RaceEntries
            .Where(e => e.Race!.RoundId == roundId)
            .Select(e => e.HorseId)
            .Distinct()
            .CountAsync();
    }
}
