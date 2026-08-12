using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HorseRacing.Services;

public class JockeyService : IJockeyService
{
    private readonly IUserRepository _users;
    private readonly IJockeyRepository _jockeys;
    private readonly IJockeyInvitationRepository _invitations;
    private readonly IRaceEntryRepository _raceEntries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notifications;

    public JockeyService(
        IUserRepository users,
        IJockeyRepository jockeys,
        IJockeyInvitationRepository invitations,
        IRaceEntryRepository raceEntries,
        IUnitOfWork unitOfWork,
        INotificationService notifications)
    {
        _users = users;
        _jockeys = jockeys;
        _invitations = invitations;
        _raceEntries = raceEntries;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<ServiceResult<object>> GetAvailableJockeysAsync(Guid currentUserId)
    {
        await EnsureJockeyProfilesAsync();
        var jockeys = await _jockeys.GetAvailableAsync();
        var response = jockeys
            .Where(jockey => jockey.UserId != currentUserId)
            .Select(jockey => new JockeyListResponse
        {
            Id = jockey.Id,
            UserId = jockey.UserId,
            FullName = jockey.User?.FullName ?? "Ky sĩ chưa đặt tên",
            Email = jockey.User?.Email ?? string.Empty,
            LicenseNumber = jockey.LicenseNumber,
            Nationality = jockey.Nationality,
            ExperienceYears = jockey.ExperienceYears,
            TotalRaces = jockey.TotalRaces,
            TotalWins = jockey.TotalWins,
            WinRate = jockey.WinRate,
            Rank = jockey.Rank,
            Status = jockey.Status,
            ApprovalStatus = (int)jockey.ApprovalStatus,
            ApprovalStatusName = jockey.ApprovalStatus.ToString()
        });

        return ServiceResult<object>.Ok(response);
    }

    private async Task EnsureJockeyProfilesAsync()
    {
        var users = await _users.GetAllAsync();
        var jockeyUsers = users.Where(user => user.IsActive && user.Role == UserRole.Jockey).ToList();
        if (jockeyUsers.Count == 0)
        {
            return;
        }

        var existingJockeys = await _jockeys.GetAllAsync();
        var existingUserIds = existingJockeys.Select(jockey => jockey.UserId).ToHashSet();
        var now = DateTime.UtcNow;

        foreach (var user in jockeyUsers.Where(user => !existingUserIds.Contains(user.Id)))
        {
            await _jockeys.AddAsync(new Jockey
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Status = "Đang hoạt động",
                ApprovalStatus = ApprovalStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ServiceResult<object>> GetInvitationsAsync(Guid userId)
    {
        var jockey = await _jockeys.GetByUserIdAsync(userId);
        if (jockey == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ kỵ sĩ");
        }

        var invitations = await _invitations.GetByJockeyAsync(jockey.Id);

        // Repair invitations created by older clients without RaceId and keep
        // accepted invitations reflected on the race participant row.
        var changedEntries = new List<RaceEntry>();
        var repairedInvitation = false;
        foreach (var invitation in invitations.Where(item => !item.RaceId.HasValue))
        {
            var entry = (await _raceEntries.GetByHorseAsync(invitation.HorseId))
                .Where(item =>
                    item.Status != RegistrationStatus.Rejected &&
                    item.ScratchedAt == null &&
                    item.Race != null &&
                    item.Race.Status != RaceStatus.Finished &&
                    item.Race.Status != RaceStatus.Cancelled)
                .OrderByDescending(item => item.Race!.ScheduledAt)
                .FirstOrDefault();
            if (entry == null)
            {
                continue;
            }

            invitation.RaceId = entry.RaceId;
            invitation.Race = entry.Race;
            repairedInvitation = true;
            if (invitation.Status == JockeyInvitationStatus.Accepted && entry.JockeyId != jockey.Id)
            {
                entry.JockeyId = jockey.Id;
                entry.JockeyConfirmed = true;
                changedEntries.Add(entry);
            }
        }

        if (changedEntries.Count > 0)
        {
            await _raceEntries.UpdateRangeAsync(changedEntries);
        }
        if (repairedInvitation)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return ServiceResult<object>.Ok(invitations);
    }

    public async Task<ServiceResult<object>> RespondInvitationAsync(Guid userId, Guid invitationId, JockeyInvitationRespondRequest request)
    {
        var jockey = await _jockeys.GetByUserIdAsync(userId);
        if (jockey == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ kỵ sĩ");
        }

        var invitation = await _invitations.GetByIdAsync(invitationId, jockey.Id);
        if (invitation == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy lời mời");
        }

        if (invitation.Status != JockeyInvitationStatus.Pending)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                "Lời mời này đã được phản hồi");
        }

        var horseEntries = await _raceEntries.GetByHorseAsync(invitation.HorseId);

        // Resolve invitations sent before the owner registered the horse for a race.
        if (!invitation.RaceId.HasValue)
        {
            invitation.RaceId = horseEntries
                .Where(entry =>
                    entry.Status != RegistrationStatus.Rejected &&
                    entry.ScratchedAt == null &&
                    entry.Race != null &&
                    entry.Race.Status != RaceStatus.Finished &&
                    entry.Race.Status != RaceStatus.Cancelled)
                .OrderByDescending(entry => entry.Race!.ScheduledAt)
                .Select(entry => (Guid?)entry.RaceId)
                .FirstOrDefault();
        }

        if (!request.Accept)
        {
            // Từ chối lời mời → gỡ kỵ sĩ khỏi các cuộc đua đã phân công cho ngựa này
            var affected = horseEntries.Where(e => e.JockeyId == jockey.Id).ToList();
            if (affected.Count > 0)
            {
                foreach (var e in affected)
                {
                    e.JockeyId = null;
                    e.JockeyConfirmed = false;
                }
                await _raceEntries.UpdateRangeAsync(affected);
            }
        }

        if (request.Accept && invitation.RaceId.HasValue)
        {
            var entry = await _raceEntries.GetByRaceHorseAsync(invitation.RaceId.Value, invitation.HorseId);
            if (entry == null)
            {
                return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy đăng ký tham gia cho ngựa này");
            }

            if (entry.Race == null)
            {
                return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy cuộc đua");
            }

            var hasScheduleConflict = await _raceEntries.HasJockeyScheduleConflictAsync(
                jockey.Id, entry.Race.ScheduledAt,
                entry.Race.ScheduledEndAt ?? entry.Race.ScheduledAt.AddMinutes(30), entry.Id);
            if (hasScheduleConflict)
            {
                return ServiceResult<object>.Fail(
                    StatusCodes.Status409Conflict,
                    "Kỵ sĩ này đã có cuộc đua trùng thời gian");
            }

            entry.JockeyId = jockey.Id;
            entry.JockeyConfirmed = true;
            await _raceEntries.UpdateAsync(entry);
        }

        invitation.Status = request.Accept ? JockeyInvitationStatus.Accepted : JockeyInvitationStatus.Declined;
        invitation.RespondedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        if (invitation.Horse?.Owner?.User != null)
        {
            var responseText = request.Accept ? "đã chấp nhận" : "đã từ chối";
            var nextStep = request.Accept
                ? "Kỵ sĩ đã được phân công cho ngựa."
                : "Bạn có thể chọn một kỵ sĩ khác cho ngựa.";

            await _notifications.CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = invitation.Horse.Owner.UserId,
                Title = request.Accept ? "Kỵ sĩ đã chấp nhận lời mời" : "Kỵ sĩ đã từ chối lời mời",
                Message = $"{jockey.User?.FullName ?? "Kỵ sĩ"} {responseText} lời mời cho ngựa {invitation.Horse.Name}. {nextStep}",
                Type = NotificationType.InApp,
                Category = NotificationCategory.JockeyInvitation,
                ActionUrl = request.Accept
                    ? $"/owner/horses/{invitation.HorseId}"
                    : "/owner/horses",
                RelatedEntityId = invitation.Id,
                RelatedEntityType = nameof(JockeyInvitation)
            });
        }

        return ServiceResult<object>.Ok(invitation);
    }

    public async Task<ServiceResult<object>> WithdrawInvitationAsync(
        Guid userId,
        Guid invitationId,
        JockeyInvitationWithdrawRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Vui lòng nhập lý do xin rút (ít nhất 3 ký tự)");
        }

        var jockey = await _jockeys.GetByUserIdAsync(userId);
        if (jockey == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ kỵ sĩ");
        }

        var invitation = await _invitations.GetByIdAsync(invitationId, jockey.Id);
        if (invitation == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy lời mời");
        }

        if (invitation.Status != JockeyInvitationStatus.Accepted)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                "Chỉ có thể xin rút khỏi lời mời đã chấp nhận");
        }

        if (!invitation.RaceId.HasValue || invitation.Race == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status409Conflict, "Lời mời chưa được gắn với cuộc đua");
        }

        if (invitation.Race.Status != RaceStatus.Scheduled &&
            invitation.Race.Status != RaceStatus.RegistrationOpen &&
            invitation.Race.Status != RaceStatus.RegistrationClosed)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                "Không thể xin rút khi cuộc đua đã bắt đầu hoặc đã kết thúc");
        }

        if (invitation.Race.ScheduledAt <= DateTime.UtcNow)
        {
            return ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                "Không thể xin rút sau thời gian bắt đầu cuộc đua");
        }

        var entry = await _raceEntries.GetByRaceHorseAsync(invitation.RaceId.Value, invitation.HorseId);
        if (entry != null && entry.JockeyId == jockey.Id)
        {
            entry.JockeyId = null;
            entry.JockeyConfirmed = false;
            await _raceEntries.UpdateAsync(entry);
        }

        var reason = request.Reason.Trim();
        invitation.Status = JockeyInvitationStatus.Withdrawn;
        invitation.ResponseNote = reason;
        invitation.RespondedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        if (invitation.Horse?.Owner != null)
        {
            await _notifications.CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = invitation.Horse.Owner.UserId,
                Title = "Kỵ sĩ đã xin rút khỏi cuộc đua",
                Message = $"{jockey.User?.FullName ?? "Kỵ sĩ"} đã xin rút khỏi ngựa {invitation.Horse.Name}. Lý do: {reason}",
                Type = NotificationType.InApp,
                Category = NotificationCategory.JockeyInvitation,
                ActionUrl = $"/owner/horses/{invitation.HorseId}",
                RelatedEntityId = invitation.Id,
                RelatedEntityType = nameof(JockeyInvitation)
            });
        }

        return ServiceResult<object>.Ok(invitation);
    }

    public async Task<ServiceResult<object>> GetAssignedRacesAsync(Guid userId)
    {
        var jockey = await _jockeys.GetByUserIdAsync(userId);
        if (jockey == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ kỵ sĩ");
        }

        var entries = await _raceEntries.GetByJockeyAsync(jockey.Id);
        var response = entries
            .Where(entry => entry.Race != null && entry.Horse != null)
            .Select(entry => new JockeyAssignedRaceResponse
            {
                Id = entry.Id,
                RaceId = entry.RaceId,
                Status = entry.Status.ToString(),
                OwnerConfirmed = entry.OwnerConfirmed,
                JockeyConfirmed = entry.JockeyConfirmed,
                Race = new JockeyAssignedRaceDetailResponse
                {
                    Id = entry.Race!.Id,
                    Name = entry.Race.Name,
                    ScheduledAt = entry.Race.ScheduledAt,
                    Status = entry.Race.Status.ToString(),
                    Location = entry.Race.Location,
                    Description = entry.Race.Description,
                    MaxParticipants = entry.Race.MaxParticipants,
                    Distance = entry.Race.Distance,
                    Tournament = entry.Race.Tournament == null
                        ? null
                        : new JockeyAssignedTournamentResponse
                        {
                            Id = entry.Race.Tournament.Id,
                            Name = entry.Race.Tournament.Name
                        }
                },
                Horse = new JockeyAssignedHorseResponse
                {
                    Id = entry.Horse!.Id,
                    Name = entry.Horse.Name,
                    Breed = entry.Horse.Breed,
                    Gender = entry.Horse.Gender,
                    Age = entry.Horse.Age,
                    Weight = entry.Horse.Weight,
                    Height = entry.Horse.Height,
                    Color = entry.Horse.Color,
                    TotalRaces = entry.Horse.TotalRaces,
                    TotalWins = entry.Horse.TotalWins
                }
            })
            .ToList();

        return ServiceResult<object>.Ok(response);
    }

    public async Task<ServiceResult<object>> GetPendingRaceEntriesAsync(Guid userId)
    {
        var jockey = await _jockeys.GetByUserIdAsync(userId);
        if (jockey == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ kỵ sĩ");
        }

        var entries = await _raceEntries.GetPendingConfirmationsByJockeyAsync(jockey.Id);
        var response = entries
            .Where(entry => entry.Race != null)
            .Select(entry => new
            {
                entryId = entry.Id,
                raceId = entry.RaceId,
                raceName = entry.Race!.Name,
                tournamentName = entry.Race.Tournament?.Name,
                scheduledAt = entry.Race.ScheduledAt,
                status = entry.Race.Status.ToString(),
                horseId = entry.HorseId,
                horseName = entry.Horse?.Name ?? "Ngựa"
            })
            .ToList();

        return ServiceResult<object>.Ok(response);
    }

    public async Task<ServiceResult<object>> ConfirmRaceEntryAsync(Guid userId, Guid entryId)
    {
        var jockey = await _jockeys.GetByUserIdAsync(userId);
        if (jockey == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ kỵ sĩ");
        }

        var entry = await _raceEntries.GetByIdAsync(entryId);
        if (entry == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy đăng ký cuộc đua");
        }
        if (entry.JockeyId != jockey.Id)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status403Forbidden, "Bạn không phải kỵ sĩ của ngựa này");
        }

        entry.JockeyConfirmed = true;
        await _raceEntries.UpdateAsync(entry);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(entry);
    }

    public async Task<ServiceResult<object>> DeclineRaceEntryAsync(Guid userId, Guid entryId)
    {
        var jockey = await _jockeys.GetByUserIdAsync(userId);
        if (jockey == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ kỵ sĩ");
        }

        var entry = await _raceEntries.GetByIdAsync(entryId);
        if (entry == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy đăng ký cuộc đua");
        }
        if (entry.JockeyId != jockey.Id)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status403Forbidden, "Bạn không phải kỵ sĩ của ngựa này");
        }

        entry.JockeyId = null;
        entry.JockeyConfirmed = false;
        await _raceEntries.UpdateAsync(entry);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(entry);
    }

    public async Task<ServiceResult<object>> GetMyProfileAsync(Guid userId)
    {
        var jockey = await _jockeys.GetByUserIdAsync(userId);
        if (jockey == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ kỵ sĩ");
        }

        return ServiceResult<object>.Ok(new
        {
            id = jockey.Id,
            fullName = jockey.User?.FullName,
            email = jockey.User?.Email,
            licenseNumber = jockey.LicenseNumber,
            totalRaces = jockey.TotalRaces,
            totalWins = jockey.TotalWins,
            winRate = jockey.WinRate,
            rank = jockey.Rank,
            approvalStatus = jockey.ApprovalStatus.ToString()
        });
    }
}
