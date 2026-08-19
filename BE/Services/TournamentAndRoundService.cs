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
                IsActive = false, // IsActive means Ongoing in this codebase (Phase5B) — Published stays false
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

                // Phase5B fix: a Draft StartDate/EndDate edit must not strand an existing Round
                // outside the candidate window. Reject before mutating the entity.
                if (request.StartDate.HasValue || request.EndDate.HasValue)
                {
                    var existingRounds = await _db.Rounds
                        .Where(r => r.TournamentId == tournament.Id)
                        .OrderBy(r => r.RoundNumber)
                        .ToListAsync();

                    var offendingRound = existingRounds.FirstOrDefault(r =>
                        r.ScheduledStartDate < candidateStartDate || r.ScheduledEndDate > candidateEndDate);
                    if (offendingRound != null)
                    {
                        return ServiceResult<TournamentResponse>.Fail(400,
                            $"Không thể thay đổi thời gian giải đấu vì Vòng {offendingRound.RoundNumber} (\"{offendingRound.Name}\") sẽ nằm ngoài khoảng thời gian mới của giải đấu.");
                    }
                }
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

            // Publish is all-or-nothing per locked V1.1 §4.2/§6/§7/§8: Phase4's Tournament-level
            // checks plus Phase5's Round/Race structural readiness must ALL pass before the
            // transition completes.
            if (newStatus == TournamentStatus.Published)
            {
                var publishErrors = await ValidatePublishReadinessAsync(tournament);
                if (publishErrors.Count > 0)
                    return ServiceResult<TournamentResponse>.Fail(400, string.Join("; ", publishErrors));

                tournament.Status = newStatus;
                tournament.UpdatedAt = DateTime.UtcNow;
                tournament.PublishedAt = DateTime.UtcNow;
                // IsActive means Ongoing in this codebase, not Published — stays false until the
                // later Published -> Ongoing transition (§17).
                tournament.IsActive = false;

                await _tournamentRepo.UpdateAsync(tournament);
                await _unitOfWork.SaveChangesAsync();

                return ServiceResult<TournamentResponse>.Ok(await MapToResponseAsync(tournament));
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

    /// <summary>
    /// Phase5: complete Publish readiness = Phase4's Tournament-level checks + Phase5's Round/Race
    /// structural checks (sequence, Final Round, AdvanceCount, Race presence, schedule hierarchy,
    /// Track existence/capacity/overlap, QualificationSlots, Round capacity coverage). All-or-nothing:
    /// every meaningful failure is collected and joined via the existing ApiResult.Message convention.
    /// </summary>
    private async Task<List<string>> ValidatePublishReadinessAsync(Tournament tournament)
    {
        var errors = await ValidatePublishTournamentFieldsAsync(tournament);
        errors.AddRange(await ValidateStructuralReadinessAsync(tournament));
        return errors;
    }

    /// <summary>
    /// Phase5 structural readiness (V1.1 §6/§7/§8). Final-Round-dependent checks (race count vs
    /// final, QualificationSlots exact-zero-vs-required-sum) only run once the sequence AND the
    /// Final-Round invariant (exactly one AdvanceCount=0, highest RoundNumber) are BOTH independently
    /// valid — never derived merely from MAX(RoundNumber), per the locked Phase5 decision. Facts that
    /// don't depend on which Round is Final (RoundNumber sequence, per-Race MaxParticipants, schedule
    /// containment, Track existence/capacity/overlap) are still validated even when the sequence or
    /// Final Round is ambiguous, so those errors surface immediately rather than being masked.
    /// </summary>
    private async Task<List<string>> ValidateStructuralReadinessAsync(Tournament tournament)
    {
        var errors = new List<string>();

        var rounds = await _db.Rounds
            .Where(r => r.TournamentId == tournament.Id)
            .Include(r => r.Races)
            .OrderBy(r => r.RoundNumber)
            .ToListAsync();

        if (rounds.Count == 0)
            return errors; // already reported by ValidatePublishTournamentFieldsAsync

        // ── Sequence: unique, gapless, starts at 1 ──
        var sequenceValid = rounds.Select(r => r.RoundNumber).OrderBy(n => n)
            .SequenceEqual(Enumerable.Range(1, rounds.Count));
        if (!sequenceValid)
            errors.Add("Thứ tự Vòng đấu (RoundNumber) phải liên tục, không trùng, và bắt đầu từ 1.");

        // ── Final Round: exactly one AdvanceCount=0, and it must be the highest RoundNumber ──
        Round? finalRound = null;
        if (sequenceValid)
        {
            var zeroAdvanceRounds = rounds.Where(r => r.AdvanceCount == 0).ToList();
            if (zeroAdvanceRounds.Count == 0)
                errors.Add("Giải đấu phải có đúng 1 Vòng chung kết (AdvanceCount = 0).");
            else if (zeroAdvanceRounds.Count > 1)
                errors.Add("Chỉ được có đúng 1 Vòng chung kết (AdvanceCount = 0).");
            else
            {
                var candidate = zeroAdvanceRounds[0];
                if (candidate.RoundNumber != rounds.Max(r => r.RoundNumber))
                    errors.Add("Vòng chung kết (AdvanceCount = 0) phải là Vòng đấu cuối cùng (RoundNumber lớn nhất).");
                else
                    finalRound = candidate;
            }
        }

        // ── AdvanceCount: universal bounds (non-null, >= 0), for every Round regardless of Final ──
        foreach (var round in rounds)
        {
            if (!round.AdvanceCount.HasValue)
                errors.Add($"Vòng {round.RoundNumber}: AdvanceCount là bắt buộc trước khi công bố.");
            else if (round.AdvanceCount.Value < 0)
                errors.Add($"Vòng {round.RoundNumber}: AdvanceCount không được âm.");
        }

        // ── AdvanceCount: Final-dependent bounds (non-final > 0, final = 0, < PlannedParticipants) ──
        if (sequenceValid && finalRound != null)
        {
            foreach (var round in rounds)
            {
                if (!round.AdvanceCount.HasValue) continue; // already reported above

                var isFinal = round.Id == finalRound.Id;
                if (isFinal && round.AdvanceCount.Value != 0)
                    errors.Add($"Vòng chung kết (Vòng {round.RoundNumber}) phải có AdvanceCount = 0.");
                if (!isFinal && round.AdvanceCount.Value <= 0)
                    errors.Add($"Vòng {round.RoundNumber} (không phải chung kết) phải có AdvanceCount > 0.");

                var planned = PlannedParticipantsFor(round, rounds, tournament);
                if (planned.HasValue && round.AdvanceCount.Value >= planned.Value)
                    errors.Add($"Vòng {round.RoundNumber}: AdvanceCount phải nhỏ hơn số lượng dự kiến tham gia (PlannedParticipants = {planned.Value}).");
            }
        }

        // ── Per-Round Race structure: presence, schedule, Track, QualificationSlots, capacity ──
        foreach (var round in rounds)
        {
            var isFinalRound = sequenceValid && finalRound != null && round.Id == finalRound.Id;
            var races = round.Races?.ToList() ?? new List<Race>();

            if (sequenceValid && finalRound != null)
            {
                if (isFinalRound && races.Count != 1)
                    errors.Add($"Vòng chung kết (Vòng {round.RoundNumber}) phải có đúng 1 Cuộc đua.");
                else if (!isFinalRound && races.Count == 0)
                    errors.Add($"Vòng {round.RoundNumber} phải có ít nhất 1 Cuộc đua.");
            }

            // Round <-> Tournament schedule: inclusive containment, strict internal duration.
            if (tournament.StartDate > round.ScheduledStartDate)
                errors.Add($"Vòng {round.RoundNumber}: Thời gian bắt đầu không được trước ngày bắt đầu giải đấu.");
            if (round.ScheduledStartDate >= round.ScheduledEndDate)
                errors.Add($"Vòng {round.RoundNumber}: Thời gian bắt đầu phải trước thời gian kết thúc.");

            // Round ordering: Round(N+1).Start >= Round(N).End (non-strict — back-to-back allowed).
            if (sequenceValid && round.RoundNumber > 1)
            {
                var previousRound = rounds.First(r => r.RoundNumber == round.RoundNumber - 1);
                if (round.ScheduledStartDate < previousRound.ScheduledEndDate)
                    errors.Add($"Vòng {round.RoundNumber}: Thời gian bắt đầu không được trước thời gian kết thúc Vòng {previousRound.RoundNumber}.");
            }

            if (round.ScheduledEndDate > tournament.EndDate)
                errors.Add($"Vòng {round.RoundNumber}: Thời gian kết thúc không được sau ngày kết thúc giải đấu.");

            var raceCapacitySum = 0;
            var qualificationSum = 0;
            var qualificationIncomplete = false;

            foreach (var race in races)
            {
                if (race.MaxParticipants <= 0)
                    errors.Add($"Cuộc đua \"{race.Name}\" (Vòng {round.RoundNumber}): MaxParticipants phải lớn hơn 0.");
                raceCapacitySum += race.MaxParticipants;

                // Race <-> Round schedule: inclusive containment, strict internal duration.
                if (round.ScheduledStartDate > race.ScheduledAt)
                    errors.Add($"Cuộc đua \"{race.Name}\": Thời gian bắt đầu không được trước thời gian bắt đầu Vòng đấu.");
                if (!race.ScheduledEndAt.HasValue)
                {
                    errors.Add($"Cuộc đua \"{race.Name}\": Thời gian kết thúc (ScheduledEndAt) là bắt buộc trước khi công bố.");
                }
                else
                {
                    if (race.ScheduledAt >= race.ScheduledEndAt.Value)
                        errors.Add($"Cuộc đua \"{race.Name}\": Thời gian bắt đầu phải trước thời gian kết thúc.");
                    if (race.ScheduledEndAt.Value > round.ScheduledEndDate)
                        errors.Add($"Cuộc đua \"{race.Name}\": Thời gian kết thúc không được sau thời gian kết thúc Vòng đấu.");
                }

                // Track existence + capacity.
                Track? track = null;
                if (!race.TrackId.HasValue)
                {
                    errors.Add($"Cuộc đua \"{race.Name}\": Đường đua (Track) là bắt buộc trước khi công bố.");
                }
                else
                {
                    track = await _db.Tracks.FirstOrDefaultAsync(t => t.Id == race.TrackId.Value);
                    if (track == null)
                        errors.Add($"Cuộc đua \"{race.Name}\": Đường đua đã chọn không tồn tại.");
                    else if (!track.Capacity.HasValue)
                        errors.Add($"Cuộc đua \"{race.Name}\": Sức chứa (Capacity) của đường đua \"{track.Name}\" chưa được thiết lập.");
                    else if (race.MaxParticipants > track.Capacity.Value)
                        errors.Add($"Cuộc đua \"{race.Name}\": MaxParticipants ({race.MaxParticipants}) vượt quá sức chứa đường đua ({track.Capacity.Value}).");
                }

                // Track overlap — tournament-wide (§L convention: own Tournament always checked;
                // other Tournaments only reserve the Track while Published/Ongoing).
                if (race.TrackId.HasValue && race.ScheduledEndAt.HasValue)
                {
                    var overlap = await TrackScheduleHelper.HasOverlapAsync(
                        _db, tournament.Id, race.TrackId.Value, race.ScheduledAt, race.ScheduledEndAt.Value, race.Id);
                    if (overlap)
                        errors.Add($"Cuộc đua \"{race.Name}\": Trùng lịch đường đua với một Cuộc đua khác.");
                }

                // QualificationSlots — Final-dependent, so gated on a resolved Final Round.
                if (isFinalRound)
                {
                    if (!race.QualificationSlots.HasValue || race.QualificationSlots.Value != 0)
                        errors.Add($"Cuộc đua \"{race.Name}\" (Vòng chung kết): QualificationSlots phải bằng 0.");
                }
                else if (sequenceValid && finalRound != null)
                {
                    if (!race.QualificationSlots.HasValue)
                    {
                        errors.Add($"Cuộc đua \"{race.Name}\": QualificationSlots là bắt buộc trước khi công bố.");
                        qualificationIncomplete = true;
                    }
                    else
                    {
                        if (race.QualificationSlots.Value < 0)
                            errors.Add($"Cuộc đua \"{race.Name}\": QualificationSlots không được âm.");
                        if (race.QualificationSlots.Value >= race.MaxParticipants)
                            errors.Add($"Cuộc đua \"{race.Name}\": QualificationSlots phải nhỏ hơn MaxParticipants.");
                        qualificationSum += race.QualificationSlots.Value;
                    }
                }
            }

            // Round capacity coverage: PlannedParticipants(Round) <= SUM(Race.MaxParticipants).
            // Applies to the Final Round too. Skipped when there are no Races — that gap is
            // already reported by the race-presence check above, avoid a redundant message.
            if (sequenceValid && races.Count > 0)
            {
                var planned = PlannedParticipantsFor(round, rounds, tournament);
                if (planned.HasValue && planned.Value > raceCapacitySum)
                    errors.Add($"Vòng {round.RoundNumber}: Tổng MaxParticipants của các Cuộc đua ({raceCapacitySum}) không đủ cho số lượng dự kiến tham gia ({planned.Value}).");
            }

            // Qualification slot sum == AdvanceCount, non-final Rounds only.
            if (!isFinalRound && sequenceValid && finalRound != null && !qualificationIncomplete
                && races.Count > 0 && round.AdvanceCount.HasValue)
            {
                if (qualificationSum != round.AdvanceCount.Value)
                    errors.Add($"Vòng {round.RoundNumber}: Tổng QualificationSlots ({qualificationSum}) phải bằng AdvanceCount ({round.AdvanceCount.Value}).");
            }
        }

        return errors;
    }

    /// <summary>
    /// PlannedParticipants(Round 1) = Tournament.MaxParticipants; PlannedParticipants(Round N&gt;1) =
    /// Round(N-1).AdvanceCount. Mirrors RoundService.ComputePlannedParticipantsAsync's semantics but
    /// operates over an already-loaded, sequence-valid Round list (caller guarantees exactly one Round
    /// per RoundNumber) instead of re-querying — safe to call only once `sequenceValid` is true.
    /// </summary>
    private static int? PlannedParticipantsFor(Round round, List<Round> rounds, Tournament tournament)
    {
        if (round.RoundNumber == 1)
            return tournament.MaxParticipants;

        return rounds.FirstOrDefault(r => r.RoundNumber == round.RoundNumber - 1)?.AdvanceCount;
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
        // Task C1 capacity gate: distinct from Stats.HorseCount, which counts distinct Horses
        // across actual RaceEntries — this counts Approved TournamentHorseRegistration rows,
        // the figure the Owner registration gate and UI need.
        var approvedRegistrationCount = await _db.TournamentHorseRegistrations
            .CountAsync(x => x.TournamentId == tournament.Id && x.Status == RegistrationStatus.Approved);

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
            ApprovedRegistrationCount = approvedRegistrationCount,
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

            if (tournament.Status != TournamentStatus.Draft)
            {
                return ServiceResult<RoundResponse>.Fail(400,
                    "Không thể thêm Vòng đấu vì giải đấu không còn ở trạng thái Bản nháp.");
            }

            if (request.RoundNumber < 1)
            {
                return ServiceResult<RoundResponse>.Fail(400, "RoundNumber phải lớn hơn hoặc bằng 1.");
            }

            var scheduleErrors = ValidateRoundScheduleWithinTournament(tournament, request.ScheduledStartDate, request.ScheduledEndDate);
            if (scheduleErrors.Count > 0)
            {
                return ServiceResult<RoundResponse>.Fail(400, string.Join("; ", scheduleErrors));
            }

            var duplicateRoundNumber = await _db.Rounds.AnyAsync(r =>
                r.TournamentId == request.TournamentId && r.RoundNumber == request.RoundNumber);
            if (duplicateRoundNumber)
            {
                return ServiceResult<RoundResponse>.Fail(400,
                    $"RoundNumber {request.RoundNumber} đã tồn tại trong giải đấu này.");
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

            if (round.Tournament != null && round.Tournament.Status != TournamentStatus.Draft)
            {
                return ServiceResult<RoundResponse>.Fail(400,
                    "Không thể chỉnh sửa Vòng đấu vì giải đấu không còn ở trạng thái Bản nháp.");
            }

            if (request.RoundNumber.HasValue)
            {
                if (request.RoundNumber.Value < 1)
                {
                    return ServiceResult<RoundResponse>.Fail(400, "RoundNumber phải lớn hơn hoặc bằng 1.");
                }

                var duplicateRoundNumber = await _db.Rounds.AnyAsync(r =>
                    r.TournamentId == round.TournamentId && r.RoundNumber == request.RoundNumber.Value && r.Id != round.Id);
                if (duplicateRoundNumber)
                {
                    return ServiceResult<RoundResponse>.Fail(400,
                        $"RoundNumber {request.RoundNumber.Value} đã tồn tại trong giải đấu này.");
                }
            }

            if (request.ScheduledStartDate.HasValue || request.ScheduledEndDate.HasValue)
            {
                var candidateStart = request.ScheduledStartDate ?? round.ScheduledStartDate;
                var candidateEnd = request.ScheduledEndDate ?? round.ScheduledEndDate;
                var tournamentForSchedule = round.Tournament ?? await _tournamentRepo.GetByIdAsync(round.TournamentId);
                if (tournamentForSchedule != null)
                {
                    var scheduleErrors = ValidateRoundScheduleWithinTournament(tournamentForSchedule, candidateStart, candidateEnd);
                    if (scheduleErrors.Count > 0)
                    {
                        return ServiceResult<RoundResponse>.Fail(400, string.Join("; ", scheduleErrors));
                    }
                }
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

            if (round.Tournament != null && round.Tournament.Status != TournamentStatus.Draft)
            {
                return ServiceResult<bool>.Fail(400,
                    "Không thể xóa Vòng đấu vì giải đấu không còn ở trạng thái Bản nháp.");
            }

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

    /// <summary>
    /// Phase5B fix: reject an obviously-out-of-window Round at Create/Update time instead of
    /// waiting for Publish. Same containment rules as the Publish-time schedule check in
    /// TournamentService.ValidateStructuralReadinessAsync (inclusive Tournament boundaries,
    /// strict internal Round duration) — kept separate since this runs against RoundService's
    /// own Tournament reference rather than a pre-loaded Round list.
    /// </summary>
    private static List<string> ValidateRoundScheduleWithinTournament(Tournament tournament, DateTime start, DateTime end)
    {
        var errors = new List<string>();

        if (tournament.StartDate > start)
            errors.Add("Thời gian bắt đầu Vòng đấu không được trước ngày bắt đầu giải đấu.");

        if (start >= end)
            errors.Add("Thời gian bắt đầu Vòng đấu phải trước thời gian kết thúc.");

        if (end > tournament.EndDate)
            errors.Add("Thời gian kết thúc Vòng đấu không được sau ngày kết thúc giải đấu.");

        return errors;
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
