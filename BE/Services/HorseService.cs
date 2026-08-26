using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Services;

public class HorseService : IHorseService
{
    private readonly IHorseRepository _horses;
    private readonly IOwnerRepository _owners;
    private readonly IJockeyRepository _jockeys;
    private readonly IRaceRepository _races;
    private readonly IRaceEntryRepository _raceEntries;
    private readonly IJockeyInvitationRepository _invitations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notifications;
    private readonly ApplicationDbContext _db;

    public HorseService(
        IHorseRepository horses,
        IOwnerRepository owners,
        IJockeyRepository jockeys,
        IRaceRepository races,
        IRaceEntryRepository raceEntries,
        IJockeyInvitationRepository invitations,
        IUnitOfWork unitOfWork,
        INotificationService notifications,
        ApplicationDbContext db)
    {
        _horses = horses;
        _owners = owners;
        _jockeys = jockeys;
        _races = races;
        _raceEntries = raceEntries;
        _invitations = invitations;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _db = db;
    }

    /// <summary>
    /// Lấy danh sách tất cả các con ngựa thuộc quyền sở hữu của chủ sở hữu hiện tại.
    /// </summary>
    /// <param name="userId">Mã định danh người dùng chủ sở hữu.</param>
    /// <returns>Danh sách ngựa của chủ sở hữu.</returns>
    public async Task<ServiceResult<object>> GetMyHorsesAsync(Guid userId)
    {
        var owner = await GetOwnerProfileAsync(userId);
        if (owner == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ chủ sở hữu");
        }

        var horses = await _horses.GetByOwnerAsync(owner.Id);
        return ServiceResult<object>.Ok(horses);
    }

    /// <summary>
    /// Lấy danh sách tất cả các con ngựa đã được Quản trị viên phê duyệt (Approved) trong hệ thống.
    /// </summary>
    /// <returns>Danh sách ngựa đủ điều kiện tham gia thi đấu.</returns>
    public async Task<ServiceResult<object>> GetAllApprovedHorsesAsync()
    {
        var horses = await _horses.GetAllApprovedAsync();
        return ServiceResult<object>.Ok(horses);
    }

    public async Task<ServiceResult<object>> GetHorseAsync(Guid userId, Guid horseId)
    {
        var owner = await GetOwnerProfileAsync(userId);
        if (owner == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ chủ sở hữu");
        }

        var horse = await _horses.GetOwnedHorseAsync(horseId, owner.Id);
        if (horse == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy ngựa");
        }

        return ServiceResult<object>.Ok(horse);
    }

    public async Task<ServiceResult<object>> CreateHorseAsync(Guid userId, HorseCreateRequest request)
    {
        var owner = await GetOwnerProfileAsync(userId);
        if (owner == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ chủ sở hữu");
        }

        var validationError = ValidateHorseStats(
            request.DateOfBirth,
            request.Age,
            request.Weight,
            request.Height,
            request.TotalRaces,
            request.TotalWins);
        if (validationError != null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, validationError);
        }

        var horse = new Horse
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Breed = request.Breed,
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            Age = request.Age,
            Weight = request.Weight,
            Height = request.Height,
            Color = request.Color,
            TotalRaces = request.TotalRaces,
            TotalWins = request.TotalWins,
            ImageUrl = request.ImageUrl,
            OwnerId = owner.Id,
            ApprovalStatus = ApprovalStatus.Pending
        };

        await _horses.AddAsync(horse);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(horse);
    }

    public async Task<ServiceResult<object>> UpdateHorseAsync(Guid userId, Guid horseId, HorseUpdateRequest request, bool isAdmin = false)
    {
        Horse? horse;
        if (isAdmin)
        {
            // Admin management: no Owner row required, targets the Horse directly by id.
            horse = await _horses.GetByIdAsync(horseId);
        }
        else
        {
            var owner = await GetOwnerProfileAsync(userId);
            if (owner == null)
            {
                return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ chủ sở hữu");
            }
            horse = await _horses.GetOwnedHorseAsync(horseId, owner.Id);
        }

        if (horse == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy ngựa");
        }

        // Task C1 correction §2: an archived Horse is retired from editing — smallest consistent
        // behavior is a single backend gate (applies to both Owner and Admin), matching how every
        // other new-participation gate for IsArchived is enforced server-side.
        if (horse.IsArchived)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Ngựa đã được lưu trữ (archive) và không thể chỉnh sửa.");
        }

        var wasRejectedByOwner = !isAdmin && horse.ApprovalStatus == ApprovalStatus.Rejected;

        var validationError = ValidateHorseStats(
            request.DateOfBirth,
            request.Age,
            request.Weight,
            request.Height,
            request.TotalRaces,
            request.TotalWins);
        if (validationError != null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, validationError);
        }

        horse.Name = request.Name;
        horse.Breed = request.Breed;
        horse.Gender = request.Gender;
        horse.DateOfBirth = request.DateOfBirth;
        horse.Age = request.Age;
        horse.Weight = request.Weight;
        horse.Height = request.Height;
        horse.Color = request.Color;
        horse.TotalRaces = request.TotalRaces;
        horse.TotalWins = request.TotalWins;
        horse.ImageUrl = request.ImageUrl;
        if (wasRejectedByOwner)
        {
            horse.ApprovalStatus = ApprovalStatus.Pending;
            horse.ApprovalNote = null;
        }
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(horse);
    }

    /// <summary>
    /// Task C1 §1: true when the Horse has ANY row in ANY of these tables — the exact "has
    /// history" definition, checked directly rather than inferred. A Horse with history must
    /// never be hard-deleted (that would destroy Tournament/Race history, or — for a
    /// RaceResult.WinningHorseId reference — outright fail with an unhandled FK violation, since
    /// that FK is Restrict on a non-nullable column).
    /// </summary>
    private async Task<bool> HasParticipationHistoryAsync(Guid horseId)
    {
        if (await _db.TournamentHorseRegistrations.AnyAsync(x => x.HorseId == horseId)) return true;
        if (await _db.RaceEntries.AnyAsync(e => e.HorseId == horseId)) return true;
        if (await _db.RaceResults.AnyAsync(rr => rr.WinningHorseId == horseId)) return true;
        if (await _db.Predictions.AnyAsync(p => p.PredictedHorseId == horseId)) return true;
        if (await _db.JockeyInvitations.AnyAsync(i => i.HorseId == horseId)) return true;
        if (await _db.HorseHealthChecks.AnyAsync(h => h.HorseId == horseId)) return true;
        if (await _db.InjuryRecords.AnyAsync(i => i.HorseId == horseId)) return true;
        if (await _db.Contracts.AnyAsync(c => c.HorseId == horseId)) return true;
        if (await _db.HorseTransfers.AnyAsync(t => t.HorseId == horseId)) return true;
        return false;
    }

    public async Task<ServiceResult<string>> DeleteHorseAsync(Guid userId, Guid horseId, bool isAdmin = false)
    {
        Horse? horse;
        if (isAdmin)
        {
            // Admin management: no Owner row required, targets the Horse directly by id — the
            // same delete-or-archive rule below applies identically regardless of who triggers it.
            horse = await _horses.GetByIdAsync(horseId);
        }
        else
        {
            var owner = await GetOwnerProfileAsync(userId);
            if (owner == null)
            {
                return ServiceResult<string>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ chủ sở hữu");
            }
            horse = await _horses.GetOwnedHorseAsync(horseId, owner.Id);
        }

        if (horse == null)
        {
            return ServiceResult<string>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy ngựa");
        }

        // Task C1 §1: a Horse with ANY participation history is archived (soft-deactivated), not
        // hard-deleted — preserves TournamentHorseRegistration/RaceEntry/RaceResult/Prediction/
        // JockeyInvitation/HealthCheck/Injury/Contract/Transfer history exactly as-is. Only a
        // Horse with zero history is still genuinely hard-deleted.
        if (await HasParticipationHistoryAsync(horse.Id))
        {
            horse.IsArchived = true;
            await _horses.UpdateAsync(horse);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<string>.Ok("Ngựa có lịch sử tham gia giải đấu/cuộc đua nên đã được lưu trữ thay vì xóa vĩnh viễn.");
        }

        await RemoveHorseRelatedDataAsync(horse);
        await _horses.RemoveAsync(horse);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<string>.Ok("Đã xóa");
    }

    public async Task<ServiceResult<object>> InviteJockeyAsync(Guid userId, Guid horseId, JockeyInvitationCreateRequest request)
    {
        var owner = await GetOwnerProfileAsync(userId);
        if (owner == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ chủ sở hữu");
        }

        var horse = await _horses.GetOwnedHorseAsync(horseId, owner.Id);
        if (horse == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy ngựa");
        }

        // Task C1 correction §2: JockeyInvitation is itself one of the 9 "has history" tables and
        // a new-participation path — an archived Horse must not accumulate new invitations.
        if (horse.IsArchived)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Ngựa đã được lưu trữ (archive) và không thể mời kỵ sĩ mới.");
        }

        var invitedJockey = await _jockeys.GetByIdAsync(request.JockeyId);
        if (invitedJockey == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy kỵ sĩ");
        }

        if (invitedJockey.ApprovalStatus != ApprovalStatus.Approved)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Kỵ sĩ chưa được admin phê duyệt");
        }

        if (invitedJockey.User != null && !invitedJockey.User.IsActive)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Tài khoản kỵ sĩ đã bị vô hiệu hóa");
        }

        if (invitedJockey.UserId == userId)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Bạn không thể gửi lời mời cho chính mình");
        }

        if (request.RaceId == Guid.Empty)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Vui lòng chọn giải đua");
        }
        var invitationRaceId = request.RaceId;

        // J2: invitation acceptance is not final assignment, so a jockey may hold
        // multiple Pending/Accepted invitations across different owners/horses/races
        // even when their schedules overlap. Exclusivity/schedule-conflict enforcement
        // belongs to Owner Final Confirm (J3), not invite creation.
        var targetRace = await _db.Races.Include(r => r.Tournament).FirstOrDefaultAsync(r => r.Id == invitationRaceId);
        if (targetRace == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy cuộc đua để mời kỵ sĩ");
        }

        // J2 lifecycle guard: StartDate/EndDate/ScheduledAt are planned dates, not authoritative
        // status — only the actual Tournament/Race Status decides whether inviting is still valid.
        // Round 1 setup happens while the Tournament is Published; later rounds while Ongoing.
        var tournamentStatus = targetRace.Tournament?.Status;
        if (tournamentStatus != TournamentStatus.Published && tournamentStatus != TournamentStatus.Ongoing)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Chỉ có thể mời kỵ sĩ khi giải đấu đang ở trạng thái Đã công bố hoặc Đang diễn ra");
        }

        // RegistrationOpen/RegistrationClosed are legacy pre-start-compatible statuses.
        if (targetRace.Status != RaceStatus.Scheduled &&
            targetRace.Status != RaceStatus.RegistrationOpen &&
            targetRace.Status != RaceStatus.RegistrationClosed)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Không thể mời kỵ sĩ vì cuộc đua đã bắt đầu, đã kết thúc hoặc đã bị hủy");
        }

        var raceEntry = await _raceEntries.GetByRaceHorseAsync(invitationRaceId, horseId);
        if (raceEntry == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Ngựa chưa được đăng ký cho cuộc đua này");
        }

        // J2: multiple different jockeys may be invited for the same Horse+Race, so this
        // duplicate check is scoped to the same jockey, not the whole race.
        var existingInvitationForJockey = horse.JockeyInvitations.FirstOrDefault(i =>
            i.RaceId == invitationRaceId && i.JockeyId == request.JockeyId);
        if (existingInvitationForJockey != null &&
            existingInvitationForJockey.Status != JockeyInvitationStatus.Declined &&
            existingInvitationForJockey.Status != JockeyInvitationStatus.Withdrawn)
        {
            var status = existingInvitationForJockey.Status.ToString().ToLowerInvariant();
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                $"Kỵ sĩ này đã có lời mời với trạng thái {status} cho giải đua này");
        }

        // Also we should create a new invitation rather than reusing an old declined one for a different race
        var existingDeclined = horse.JockeyInvitations.FirstOrDefault(i =>
            i.JockeyId == request.JockeyId &&
            i.RaceId == invitationRaceId &&
            (i.Status == JockeyInvitationStatus.Declined || i.Status == JockeyInvitationStatus.Withdrawn));
        if (existingDeclined != null)
        {
            existingDeclined.Status = JockeyInvitationStatus.Pending;
            existingDeclined.CreatedAt = DateTime.UtcNow;
            existingDeclined.RespondedAt = null;
            existingDeclined.ResponseNote = null;
            existingDeclined.Message = request.Message?.Trim();
        }

        var jockeyExists = await _jockeys.ExistsAsync(request.JockeyId);
        if (!jockeyExists)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy kỵ sĩ");
        }

        var invitation = existingDeclined ?? new JockeyInvitation
        {
            Id = Guid.NewGuid(),
            HorseId = horseId,
            JockeyId = request.JockeyId,
            RaceId = invitationRaceId,
            Status = JockeyInvitationStatus.Pending,
            Message = request.Message?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        if (existingDeclined == null)
        {
            await _invitations.AddAsync(invitation);
        }
        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is Npgsql.PostgresException
            {
                SqlState: Npgsql.PostgresErrorCodes.UniqueViolation
            })
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                "Lời mời cho ngựa và kỵ sĩ này đã tồn tại");
        }

        await _notifications.CreateNotificationAsync(new CreateNotificationDto
        {
            UserId = invitedJockey.UserId,
            Title = "Bạn có lời mời đua mới",
            Message = $"{owner.User?.FullName ?? "Chủ ngựa"} đã mời bạn làm kỵ sĩ cho ngựa {horse.Name}.",
            Type = NotificationType.InApp,
            Category = NotificationCategory.JockeyInvitation,
            ActionUrl = $"/jockey/invitations/{invitation.Id}",
            RelatedEntityId = invitation.Id,
            RelatedEntityType = nameof(JockeyInvitation)
        });

        return ServiceResult<object>.Ok(invitation);
    }

    public async Task<ServiceResult<string>> RemoveJockeyAsync(Guid userId, Guid horseId, Guid raceId, JockeyRemovalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<string>.Fail(
                StatusCodes.Status400BadRequest,
                "Vui lòng nhập lý do hủy kỵ sĩ");
        }

        if (request.InvitationId == Guid.Empty)
        {
            return ServiceResult<string>.Fail(
                StatusCodes.Status400BadRequest,
                "Vui lòng chọn lời mời cần hủy");
        }

        var owner = await GetOwnerProfileAsync(userId);
        if (owner == null)
        {
            return ServiceResult<string>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ chủ sở hữu");
        }

        var horse = await _horses.GetOwnedHorseAsync(horseId, owner.Id);
        if (horse == null)
        {
            return ServiceResult<string>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy ngựa");
        }

        // J2 follow-up: multiple Pending/Accepted invitations can now exist for the same
        // Horse+Race (different jockeys), so the invitation must be pinned by Id — never
        // picked arbitrarily — to avoid cancelling the wrong jockey's invitation.
        var invitation = horse.JockeyInvitations.FirstOrDefault(i =>
            i.Id == request.InvitationId &&
            i.RaceId == raceId &&
            (i.Status == JockeyInvitationStatus.Pending || i.Status == JockeyInvitationStatus.Accepted));

        if (invitation == null)
        {
            return ServiceResult<string>.Fail(
                StatusCodes.Status404NotFound,
                "Không tìm thấy lời mời cần hủy cho ngựa và cuộc đua này");
        }

        // J3.1: official-ness is Tournament-wide and determined from RaceEntry.JockeyId,
        // never from invitation.Status — an Accepted invitation stays Accepted forever
        // even after it becomes official (Final Confirm never touches invitation.Status),
        // so checking Status here would not catch it. Once Owner Final Confirm has
        // established RaceEntry.JockeyId for this Horse anywhere in the Tournament, this
        // action must not be able to undo it — regardless of which Race/invitation the
        // Owner operates on for the same Horse+Tournament.
        var raceForGuard = await _races.GetByIdAsync(raceId);
        if (raceForGuard != null)
        {
            var officialAssignment = await _raceEntries.GetOfficialAssignmentForHorseInTournamentAsync(horseId, raceForGuard.TournamentId);
            if (officialAssignment != null && officialAssignment.JockeyId == invitation.JockeyId)
            {
                return ServiceResult<string>.Fail(
                    StatusCodes.Status409Conflict,
                    "Kỵ sĩ này đã là kỵ sĩ chính thức của ngựa trong giải đấu này và không thể bị hủy qua thao tác này.");
            }
        }

        // J3.1: the official-pairing guard above already rejected this call if the invitation
        // belonged to the official Jockey — so by this point the invitation is necessarily
        // non-official, and only the invitation itself needs to change. RaceEntry is never
        // touched here.
        invitation.Status = JockeyInvitationStatus.Declined;
        invitation.ResponseNote = request.Reason.Trim();
        invitation.RespondedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        if (invitation?.Jockey?.UserId is Guid jockeyUserId)
        {
            await _notifications.CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = jockeyUserId,
                Title = "Chủ ngựa đã hủy phân công",
                Message = $"Bạn đã được hủy khỏi ngựa {horse.Name}. Lý do: {request.Reason.Trim()}",
                Type = NotificationType.InApp,
                Category = NotificationCategory.JockeyInvitation,
                ActionUrl = $"/jockey/invitations/{invitation.Id}",
                RelatedEntityId = invitation.Id,
                RelatedEntityType = nameof(JockeyInvitation)
            });
        }

        return ServiceResult<string>.Ok("Đã hủy kỵ sĩ thành công");
    }

    public async Task<ServiceResult<object>> RegisterHorseAsync(Guid userId, Guid horseId, Guid raceId, RaceRegistrationRequest request)
    {
        var owner = await GetOwnerProfileAsync(userId);
        if (owner == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ chủ sở hữu");
        }

        var horse = await _horses.GetOwnedHorseAsync(horseId, owner.Id);
        if (horse == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy ngựa");
        }

        if (horse.ApprovalStatus != ApprovalStatus.Approved)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Chỉ ngựa đã được phê duyệt mới có thể đăng ký tham gia");
        }

        var raceExists = await _races.ExistsAsync(raceId);
        if (!raceExists)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy cuộc đua");
        }

        // Check race is still open for registration
        var race = await _races.GetByIdAsync(raceId);
        if (race == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy cuộc đua");
        }
        if (race.Status != RaceStatus.RegistrationOpen)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, $"Không thể đăng ký vào cuộc đua với trạng thái '{race.Status}'. Cuộc đua phải ở trạng thái Đang mở đăng ký.");
        }

        if (race.Tournament?.RegistrationDeadline is DateTime registrationDeadline &&
            DateTime.UtcNow > registrationDeadline.ToUniversalTime())
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Đã quá hạn đăng ký của giải đấu");
        }

        var activeEntryCount = race.Entries.Count(entry =>
            entry.Status != RegistrationStatus.Rejected &&
            entry.ScratchedAt == null);
        if (activeEntryCount >= race.MaxParticipants)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                $"Cuộc đua đã đủ số lượng tham gia tối đa ({race.MaxParticipants})");
        }

        // Check horse is not already registered for this specific race
        var exists = await _raceEntries.ExistsAsync(raceId, horseId);
        if (exists)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status409Conflict, "Ngựa đã được đăng ký");
        }

        // Check: owner chỉ được đăng ký 1 con ngựa vào 1 cuộc đua
        var ownerAlreadyInRace = await _raceEntries.OwnerHasHorseInRaceAsync(raceId, owner.Id);
        if (ownerAlreadyInRace)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Bạn chỉ có thể đăng ký 1 con ngựa vào 1 cuộc đua. Ngựa khác của bạn đã được đăng ký hoặc đang chờ duyệt cho cuộc đua này.");
        }

        var acceptedInvitation = horse.JockeyInvitations
            .Where(invitation => invitation.Status == JockeyInvitationStatus.Accepted)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .FirstOrDefault();
        var registeringJockey = acceptedInvitation == null
            ? await _jockeys.GetByUserIdAsync(userId)
            : null;
        var assignedJockeyId = acceptedInvitation?.JockeyId ?? registeringJockey?.Id;
        if (assignedJockeyId.HasValue &&
            await _raceEntries.HasJockeyScheduleConflictAsync(assignedJockeyId.Value,
                race.ScheduledAt, race.ScheduledEndAt ?? race.ScheduledAt.AddMinutes(30)))
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                "Kỵ sĩ này đã có cuộc đua trùng thời gian");
        }

        var entry = new RaceEntry
        {
            Id = Guid.NewGuid(),
            RaceId = raceId,
            HorseId = horseId,
            JockeyId = assignedJockeyId,
            Status = RegistrationStatus.Pending,
            OwnerConfirmed = true,
            JockeyConfirmed = assignedJockeyId.HasValue
        };

        // An invitation may have been sent before the owner selected a race. Bind
        // it now so the jockey sees complete race/tournament information and a
        // later acceptance updates this exact entry.
        if (acceptedInvitation != null || horse.JockeyInvitations.Any(invitation =>
                invitation.Status == JockeyInvitationStatus.Pending))
        {
            var activeInvitation = acceptedInvitation ?? horse.JockeyInvitations
                .Where(invitation => invitation.Status == JockeyInvitationStatus.Pending)
                .OrderByDescending(invitation => invitation.CreatedAt)
                .First();
            activeInvitation.RaceId = raceId;
        }

        await _raceEntries.AddAsync(entry);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(entry);
    }

    public async Task<ServiceResult<object>> ConfirmOwnerAsync(Guid userId, Guid raceId, Guid entryId)
    {
        var owner = await GetOwnerProfileAsync(userId);
        if (owner == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ chủ sở hữu");
        }

        var entry = await _raceEntries.GetByIdWithHorseAsync(entryId, raceId);
        if (entry?.Horse == null || entry.Horse.OwnerId != owner.Id)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy đăng ký tham gia");
        }

        entry.OwnerConfirmed = true;
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(entry);
    }

    // J3: Owner picks exactly one Accepted invitation as the official Jockey for a RaceEntry.
    // RaceEntry.JockeyId is set ONLY here — J2 Accept never touches it. Wrapped in a Serializable
    // transaction (same convention as TournamentRegistrationsController.Approve) so two Owners
    // racing to confirm the same Jockey into conflicting RaceEntries can't both succeed.
    public async Task<ServiceResult<object>> FinalConfirmJockeyAsync(Guid userId, Guid horseId, Guid raceId, OwnerFinalConfirmJockeyRequest request)
    {
        var owner = await GetOwnerProfileAsync(userId);
        if (owner == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ chủ sở hữu");
        }

        // A: Horse must belong to the authenticated Owner.
        var horse = await _horses.GetOwnedHorseAsync(horseId, owner.Id);
        if (horse == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy ngựa");
        }

        if (request.InvitationId == Guid.Empty)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Vui lòng chọn lời mời cần xác nhận");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // B: exact RaceEntry for HorseId + RaceId.
        var entry = await _raceEntries.GetByRaceHorseAsync(raceId, horseId);
        if (entry == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy đăng ký tham gia cho ngựa và cuộc đua này");
        }

        // C: the invitation must exist and belong to this exact Horse + Race.
        var invitation = await _db.JockeyInvitations
            .Include(i => i.Jockey)
                .ThenInclude(j => j!.User)
            .FirstOrDefaultAsync(i => i.Id == request.InvitationId && i.HorseId == horseId && i.RaceId == raceId);
        if (invitation == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy lời mời cho ngựa và cuộc đua này");
        }

        // D: only an Accepted invitation may become the official Jockey.
        if (invitation.Status != JockeyInvitationStatus.Accepted)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Chỉ có thể chọn kỵ sĩ từ lời mời đã được chấp nhận");
        }

        // E: revalidate Jockey eligibility now — accepted earlier does not guarantee still eligible.
        var jockey = invitation.Jockey;
        if (jockey == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy kỵ sĩ");
        }
        if (jockey.ApprovalStatus != ApprovalStatus.Approved)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Kỵ sĩ chưa được admin phê duyệt");
        }
        if (jockey.User != null && !jockey.User.IsActive)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Tài khoản kỵ sĩ đã bị vô hiệu hóa");
        }

        // 4: lifecycle guards — Status is authoritative, never ScheduledAt/StartDate/EndDate.
        if (entry.Race == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy cuộc đua");
        }
        var tournamentStatus = entry.Race.Tournament?.Status;
        if (tournamentStatus != TournamentStatus.Published && tournamentStatus != TournamentStatus.Ongoing)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Chỉ có thể chọn kỵ sĩ chính thức khi giải đấu đang ở trạng thái Đã công bố hoặc Đang diễn ra");
        }
        var raceStatus = entry.Race.Status;
        if (raceStatus != RaceStatus.Scheduled &&
            raceStatus != RaceStatus.RegistrationOpen &&
            raceStatus != RaceStatus.RegistrationClosed)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Không thể chọn kỵ sĩ chính thức vì cuộc đua đã bắt đầu, đã kết thúc hoặc đã bị hủy");
        }

        // BUSINESS PIVOT: one Jockey is paired with one Horse for that Horse's ENTIRE Tournament
        // journey (not just one Race) — Owner Final Confirm establishes this pairing once, and it
        // is then immutable for the Tournament. Within the SAME Tournament this stays a pure
        // identity rule (never Race-timing based, see checks 5/6 below). ACROSS Tournaments
        // (J-CROSS, check 7) the Jockey may now hold official pairings in multiple Published/
        // Ongoing Tournaments at once, gated by whether their Race schedules actually overlap.
        // Q1 carries HorseId+JockeyId forward into each new-round RaceEntry automatically — Owner
        // never re-picks a Jockey for a Horse that already has one in this Tournament.
        var targetTournamentId = entry.Race.TournamentId;

        // 5: does this Horse already have an official Jockey somewhere in this Tournament? Covers
        // both "this exact RaceEntry already has one" (no replacement) and "a different RaceEntry
        // for this Horse in the same Tournament already established the pairing" (e.g. a prior
        // Round) — deliberately not excluded from the query, since either case means the pairing
        // already exists and Owner must not pick again.
        var existingHorsePairing = await _raceEntries.GetOfficialAssignmentForHorseInTournamentAsync(horseId, targetTournamentId);
        if (existingHorsePairing != null)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                "Ngựa này đã có kỵ sĩ chính thức cho giải đấu.");
        }

        // 6/7: load every other existing official assignment for this Jockey once — shared by the
        // same-Tournament different-Horse check and the cross-Tournament lock below.
        var jockeyOfficialAssignments = await _raceEntries.GetOfficialAssignmentsForJockeyAsync(jockey.Id);

        // 6: is the Jockey already officially paired with a DIFFERENT Horse in this Tournament?
        // One Jockey, one Horse per Tournament — never allowed even when Races don't overlap.
        var pairedWithDifferentHorseInTournament = jockeyOfficialAssignments.Any(other =>
            other.Race != null &&
            other.Race.TournamentId == targetTournamentId &&
            other.HorseId != horseId);
        if (pairedWithDifferentHorseInTournament)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                "Kỵ sĩ này đã được ghép chính thức với một ngựa khác trong giải đấu.");
        }

        // 7 (J-CROSS): cross-Tournament SCHEDULE-OVERLAP lock, replacing the old global
        // one-active-Tournament-per-Jockey lock. A Jockey may now be officially paired in several
        // Published/Ongoing Tournaments at once, as long as none of their Race schedules overlap.
        // A Finished/Cancelled Tournament never locks — its historical RaceEntry.JockeyId is left
        // untouched by design (unchanged from before).
        //
        // Do NOT compare only the current Race: official pairing carries forward for the Horse's
        // entire Tournament journey, and Q1 propagates the same JockeyId into Round2+/Final
        // RaceEntries automatically without re-running Final Confirm. Round/Race structure for
        // every round (including Final) already exists before Publish and is backend-immutable
        // once the Tournament leaves Draft, so comparing the two Tournaments' FULL immutable Race
        // schedule sets here safely catches a future-round collision even when neither Tournament
        // has RaceEntries yet for the colliding Race (e.g. two Finals landing on the same day while
        // each Tournament's Round1 does not overlap).
        var otherLockedTournamentIds = jockeyOfficialAssignments
            .Where(other =>
                other.Race != null &&
                other.Race.TournamentId != targetTournamentId &&
                other.Race.Tournament != null &&
                (other.Race.Tournament.Status == TournamentStatus.Published ||
                 other.Race.Tournament.Status == TournamentStatus.Ongoing))
            .Select(other => other.Race!.TournamentId)
            .Distinct()
            .ToList();

        if (otherLockedTournamentIds.Count > 0)
        {
            var scheduleWindows = await _races.GetScheduleWindowsByTournamentsAsync(
                otherLockedTournamentIds.Append(targetTournamentId));

            var targetSchedule = scheduleWindows.TryGetValue(targetTournamentId, out var targetWindows)
                ? targetWindows
                : new List<(DateTime Start, DateTime End)>();

            var hasCrossTournamentScheduleOverlap = otherLockedTournamentIds.Any(otherTournamentId =>
            {
                var otherSchedule = scheduleWindows.TryGetValue(otherTournamentId, out var otherWindows)
                    ? otherWindows
                    : new List<(DateTime Start, DateTime End)>();

                // Standard overlap formula: existing.Start < candidate.End AND candidate.Start < existing.End.
                // Back-to-back windows (A ends exactly when B starts) are NOT an overlap.
                return targetSchedule.Any(t => otherSchedule.Any(o => t.Start < o.End && o.Start < t.End));
            });

            if (hasCrossTournamentScheduleOverlap)
            {
                return ServiceResult<object>.Fail(
                    StatusCodes.Status409Conflict,
                    "Kỵ sĩ này đã được phân công ở một giải đấu khác có lịch thi đấu trùng.");
            }
        }

        // 8: only RaceEntry.JockeyId/JockeyConfirmed are mutated — OwnerConfirmed and every other
        // Accepted/Pending invitation are left exactly as they were (no replacement/history rewrite).
        entry.JockeyId = jockey.Id;
        entry.JockeyConfirmed = true;

        try
        {
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                "Một thao tác khác vừa thay đổi phân công kỵ sĩ cho cuộc đua này. Vui lòng thử lại.");
        }

        return ServiceResult<object>.Ok(entry);
    }

    private async Task RemoveHorseRelatedDataAsync(Horse horse)
    {
        var invitations = await _db.JockeyInvitations.Where(i => i.HorseId == horse.Id).ToListAsync();
        if (invitations.Count > 0)
        {
            _db.JockeyInvitations.RemoveRange(invitations);
        }

        var raceEntries = await _db.RaceEntries.Where(e => e.HorseId == horse.Id).ToListAsync();
        if (raceEntries.Count > 0)
        {
            _db.RaceEntries.RemoveRange(raceEntries);
        }

        var predictions = await _db.Predictions.Where(p => p.PredictedHorseId == horse.Id).ToListAsync();
        if (predictions.Count > 0)
        {
            _db.Predictions.RemoveRange(predictions);
        }

        var healthChecks = await _db.HorseHealthChecks.Where(h => h.HorseId == horse.Id).ToListAsync();
        if (healthChecks.Count > 0)
        {
            _db.HorseHealthChecks.RemoveRange(healthChecks);
        }

        var injuryRecords = await _db.InjuryRecords.Where(i => i.HorseId == horse.Id).ToListAsync();
        if (injuryRecords.Count > 0)
        {
            _db.InjuryRecords.RemoveRange(injuryRecords);
        }

        var contracts = await _db.Contracts.Where(c => c.HorseId == horse.Id).ToListAsync();
        if (contracts.Count > 0)
        {
            _db.Contracts.RemoveRange(contracts);
        }

        var horseTransfers = await _db.HorseTransfers.Where(t => t.HorseId == horse.Id).ToListAsync();
        if (horseTransfers.Count > 0)
        {
            _db.HorseTransfers.RemoveRange(horseTransfers);
        }

        // Keep RaceResults for historical integrity — WinningHorseId will become orphaned but preserves history
    }

    private Task<Owner?> GetOwnerProfileAsync(Guid userId) => _owners.GetByUserIdAsync(userId);

    private static string? ValidateHorseStats(DateTime? dateOfBirth, int age, decimal? weight, decimal? height, int totalRaces, int totalWins)
    {
        if (weight is null || weight <= 0)
        {
            return "Cân nặng phải lớn hơn 0";
        }

        if (decimal.Truncate(weight.Value) != weight.Value)
        {
            return "Cân nặng chỉ được nhập chữ số";
        }

        if (height is null || height <= 0)
        {
            return "Chiều cao phải lớn hơn 0";
        }

        if (decimal.Truncate(height.Value) != height.Value)
        {
            return "Chiều cao chỉ được nhập chữ số";
        }

        if (dateOfBirth.HasValue)
        {
            var birthDate = dateOfBirth.Value.Date;
            var today = DateTime.Today;
            if (birthDate > today)
            {
                return "Ngày sinh không thể ở tương lai";
            }

            var expectedAge = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-expectedAge))
            {
                expectedAge--;
            }

            if (age != expectedAge)
            {
                return $"Tuổi phải là {expectedAge} dựa trên ngày sinh";
            }
        }

        if (totalWins > totalRaces)
        {
            return "Tổng số trận thắng không thể lớn hơn tổng số trận đua";
        }

        return null;
    }
}
