using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using HorseRacing.Data;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Services;

public class RaceComplaintService : IRaceComplaintService
{
    private static readonly RaceComplaintStatus[] ActiveStatuses =
    {
        RaceComplaintStatus.Pending,
        RaceComplaintStatus.AwaitingRefereeResponse,
        RaceComplaintStatus.UnderReview,
    };

    private readonly IRaceComplaintRepository _repo;
    private readonly IRaceComplaintEvidenceRepository _evidenceRepo;
    private readonly IRaceRepository _raceRepo;
    private readonly IRaceResultRepository _raceResultRepo;
    private readonly IRaceEntryRepository _entryRepo;
    private readonly IOwnerRepository _ownerRepo;
    private readonly IJockeyRepository _jockeyRepo;
    private readonly IUserRepository _userRepo;
    private readonly IRefereeAssignmentRepository _assignmentRepo;
    private readonly IUnitOfWork _uow;
    private readonly ApplicationDbContext _db;
    private readonly ICloudStorageService _cloudStorage;
    private readonly INotificationService? _notifications;
    private readonly IAuditLogService? _auditLogs;

    public RaceComplaintService(
        IRaceComplaintRepository repo,
        IRaceComplaintEvidenceRepository evidenceRepo,
        IRaceRepository raceRepo,
        IRaceResultRepository raceResultRepo,
        IRaceEntryRepository entryRepo,
        IOwnerRepository ownerRepo,
        IJockeyRepository jockeyRepo,
        IUserRepository userRepo,
        IRefereeAssignmentRepository assignmentRepo,
        IUnitOfWork uow,
        ApplicationDbContext db,
        ICloudStorageService cloudStorage,
        INotificationService? notifications = null,
        IAuditLogService? auditLogs = null)
    {
        _repo = repo;
        _evidenceRepo = evidenceRepo;
        _raceRepo = raceRepo;
        _raceResultRepo = raceResultRepo;
        _entryRepo = entryRepo;
        _ownerRepo = ownerRepo;
        _jockeyRepo = jockeyRepo;
        _userRepo = userRepo;
        _assignmentRepo = assignmentRepo;
        _uow = uow;
        _db = db;
        _cloudStorage = cloudStorage;
        _notifications = notifications;
        _auditLogs = auditLogs;
    }

    public async Task<ServiceResult<RaceComplaintResponse>> FileAsync(CreateRaceComplaintRequest request, Guid filedByUserId)
    {
        if (!Enum.IsDefined(typeof(RaceComplaintType), request.Type))
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Loai khieu nai khong hop le.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Reason is required.");
        if (request.Reason.Length > 2000)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Reason must be 2000 characters or fewer.");
        if (request.EvidenceDescription?.Length > 2000)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "EvidenceDescription must be 2000 characters or fewer.");

        var race = await _raceRepo.GetByIdAsync(request.RaceId);
        if (race == null)
            return ServiceResult<RaceComplaintResponse>.Fail(404, "Khong tim thay cuoc dua.");
        if (race.Status == RaceStatus.Cancelled)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Khong the khieu nai cuoc dua da bi huy.");
        if (race.Status != RaceStatus.Finished)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Chi co the khieu nai cuoc dua da ket thuc.");
        var deadline = race.ScheduledAt.AddHours(48);
        if (DateTime.UtcNow > deadline)
        {
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Đã quá thời hạn 48 giờ để nộp khiếu nại cho cuộc đua này.");
        }

        var raceResult = await _raceResultRepo.GetByRaceIdAsync(request.RaceId);
        if (raceResult == null)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Cuoc dua chua co ket qua tam thoi.");
        if (raceResult.Status == RaceResultStatus.Official)
            return ServiceResult<RaceComplaintResponse>.Fail(409, "Ket qua cuoc dua da chinh thuc va khong the tao khieu nai moi.");
        if (raceResult.Status != RaceResultStatus.Provisional)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Ket qua cuoc dua khong o trang thai tam thoi.");

        var user = await _userRepo.GetByIdAsync(filedByUserId);
        if (user == null || user.Role is not (UserRole.HorseOwner or UserRole.Jockey))
            return ServiceResult<RaceComplaintResponse>.Fail(403, "Only HorseOwner or Jockey can file a race complaint.");

        if (!await HasFilingStandingAsync(user, request.RaceId))
            return ServiceResult<RaceComplaintResponse>.Fail(403, "You do not have standing to file a complaint for this race.");

        if (await _repo.HasActiveByFilerRaceTypeAsync(filedByUserId, request.RaceId, request.Type))
            return ServiceResult<RaceComplaintResponse>.Fail(409, "An active complaint already exists for this race and type.");

        var now = DateTime.UtcNow;
        var complaint = new RaceComplaint
        {
            Id = Guid.NewGuid(),
            RaceId = request.RaceId,
            FiledByUserId = filedByUserId,
            Type = request.Type,
            Reason = request.Reason.Trim(),
            EvidenceDescription = string.IsNullOrWhiteSpace(request.EvidenceDescription)
                ? null
                : request.EvidenceDescription.Trim(),
            Status = RaceComplaintStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            Race = race,
            FiledByUser = user,
        };

        await _repo.AddAsync(complaint);
        await _uow.SaveChangesAsync();
        await NotifyAdminsAsync("Race complaint filed", "A new race complaint is waiting for intake.", complaint.Id);

        var created = await _repo.GetByIdAsync(complaint.Id);
        return ServiceResult<RaceComplaintResponse>.Success(Map(created ?? complaint), 201);
    }

    public async Task<ServiceResult<IEnumerable<RaceComplaintResponse>>> GetAllAsync(RaceComplaintStatus? status = null)
    {
        var complaints = await _repo.GetAllAsync();
        if (status.HasValue)
            complaints = complaints.Where(c => c.Status == status.Value);
        return ServiceResult<IEnumerable<RaceComplaintResponse>>.Ok(complaints.Select(c => Map(c)));
    }

    public async Task<ServiceResult<IEnumerable<RaceComplaintResponse>>> GetByFiledByUserAsync(Guid filedByUserId) =>
        ServiceResult<IEnumerable<RaceComplaintResponse>>.Ok((await _repo.GetByFiledByUserAsync(filedByUserId)).Select(c => Map(c)));

    public async Task<ServiceResult<IEnumerable<RaceComplaintResponse>>> GetForRefereeAsync(Guid refereeUserId) =>
        ServiceResult<IEnumerable<RaceComplaintResponse>>.Ok((await _repo.GetByAssignedRefereeUserAsync(refereeUserId)).Select(c => Map(c, includeAssignmentOptions: false)));

    public async Task<ServiceResult<IEnumerable<RaceComplaintEligibleRaceResponse>>> GetEligibleRacesAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null || user.Role is not (UserRole.HorseOwner or UserRole.Jockey))
            return ServiceResult<IEnumerable<RaceComplaintEligibleRaceResponse>>.Fail(403, "Only HorseOwner or Jockey can view eligible complaint races.");

        IQueryable<RaceEntry> query = _db.RaceEntries
            .AsNoTracking()
            .Include(e => e.Horse)
            .Include(e => e.Race)!.ThenInclude(r => r!.Tournament)
            .Include(e => e.Race)!.ThenInclude(r => r!.Result)
            .Where(e =>
                e.Status == RegistrationStatus.Approved &&
                e.Race != null &&
                e.Race.Status == RaceStatus.Finished &&
                e.Race.Result != null &&
                e.Race.Result.Status == RaceResultStatus.Provisional);

        if (user.Role == UserRole.HorseOwner)
        {
            var owner = await _ownerRepo.GetByUserIdAsync(user.Id);
            if (owner == null)
                return ServiceResult<IEnumerable<RaceComplaintEligibleRaceResponse>>.Ok(Array.Empty<RaceComplaintEligibleRaceResponse>());
            query = query.Where(e => e.Horse != null && e.Horse.OwnerId == owner.Id);
        }
        else
        {
            var jockey = await _jockeyRepo.GetByUserIdAsync(user.Id);
            if (jockey == null)
                return ServiceResult<IEnumerable<RaceComplaintEligibleRaceResponse>>.Ok(Array.Empty<RaceComplaintEligibleRaceResponse>());
            query = query.Where(e => e.JockeyId == jockey.Id);
        }

        var races = await query
            .OrderByDescending(e => e.Race!.ScheduledAt)
            .Select(e => new RaceComplaintEligibleRaceResponse
            {
                RaceId = e.RaceId,
                RaceName = e.Race!.Name,
                EntryId = e.Id,
                HorseId = e.HorseId,
                HorseName = e.Horse != null ? e.Horse.Name : null,
                TournamentName = e.Race.Tournament != null ? e.Race.Tournament.Name : null,
                ScheduledAt = e.Race.ScheduledAt,
                RaceStatus = e.Race.Status.ToString(),
                ResultStatus = e.Race.Result != null ? e.Race.Result.Status.ToString() : null,
            })
            .ToListAsync();

        return ServiceResult<IEnumerable<RaceComplaintEligibleRaceResponse>>.Ok(races);
    }

    public async Task<ServiceResult<RaceComplaintResponse>> RouteAsync(Guid id, RouteRaceComplaintRequest request, Guid adminUserId)
    {
        var complaint = await _repo.GetByIdAsync(id);
        if (complaint == null)
            return ServiceResult<RaceComplaintResponse>.Fail(404, "Race complaint not found.");
        if (complaint.Status != RaceComplaintStatus.Pending)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Only pending complaints can be routed.");

        var assignment = await _assignmentRepo.GetByIdAsync(request.RefereeAssignmentId);
        if (assignment == null)
            return ServiceResult<RaceComplaintResponse>.Fail(404, "Referee assignment not found.");
        if (assignment.RaceId != complaint.RaceId)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Referee assignment must belong to the complaint race.");
        if (assignment.Status != RefereeAssignmentStatus.Confirmed)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Only a confirmed referee assignment can receive a complaint.");

        var oldStatus = complaint.Status;
        var now = DateTime.UtcNow;
        complaint.AssignedRefereeAssignmentId = assignment.Id;
        complaint.ResponseRequestedAt = now;
        complaint.Status = RaceComplaintStatus.AwaitingRefereeResponse;
        complaint.UpdatedAt = now;

        await _repo.UpdateAsync(complaint);
        await _uow.SaveChangesAsync();
        await AuditAdminActionAsync(complaint, adminUserId, AuditAction.Assign, oldStatus, complaint.Status, "Race complaint routed to referee assignment.");

        var routed = await _repo.GetByIdAsync(id);
        if (routed?.AssignedRefereeAssignment?.Referee?.UserId is Guid refereeUserId)
            await NotifyUserAsync(refereeUserId, "Race complaint needs explanation", "A race complaint has been routed to your assignment.", complaint.Id, "/referee/complaints");

        return ServiceResult<RaceComplaintResponse>.Ok(Map(routed ?? complaint));
    }

    public async Task<ServiceResult<RaceComplaintResponse>> RespondAsync(Guid id, RespondRaceComplaintRequest request, Guid refereeUserId)
    {
        var complaint = await _repo.GetByIdAsync(id);
        if (complaint == null)
            return ServiceResult<RaceComplaintResponse>.Fail(404, "Race complaint not found.");
        if (complaint.Status != RaceComplaintStatus.AwaitingRefereeResponse)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Only complaints awaiting referee response can be answered.");
        if (complaint.AssignedRefereeAssignment?.Referee?.UserId != refereeUserId)
            return ServiceResult<RaceComplaintResponse>.Fail(403, "Only the assigned referee can respond to this complaint.");
        if (string.IsNullOrWhiteSpace(request.Response))
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Response is required.");
        if (request.Response.Length > 2000)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Response must be 2000 characters or fewer.");

        var now = DateTime.UtcNow;
        complaint.RefereeResponse = request.Response.Trim();
        complaint.RefereeRespondedAt = now;
        complaint.Status = RaceComplaintStatus.UnderReview;
        complaint.UpdatedAt = now;

        await _repo.UpdateAsync(complaint);
        await _uow.SaveChangesAsync();
        await NotifyAdminsAsync("Race complaint response submitted", "A referee submitted an explanation for a race complaint.", complaint.Id);

        return ServiceResult<RaceComplaintResponse>.Ok(Map(await _repo.GetByIdAsync(id) ?? complaint));
    }

    public async Task<ServiceResult<RaceComplaintResponse>> RuleAsync(Guid id, RuleRaceComplaintRequest request, Guid ruledByUserId)
    {
        var complaint = await _repo.GetByIdAsync(id);
        if (complaint == null)
            return ServiceResult<RaceComplaintResponse>.Fail(404, "Race complaint not found.");
        if (request.Outcome is not RaceComplaintStatus.Upheld and not RaceComplaintStatus.Rejected)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Outcome must be Upheld or Rejected.");
        if (string.IsNullOrWhiteSpace(request.Ruling))
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Ruling is required.");
        if (request.Ruling.Length > 2000)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Ruling must be 2000 characters or fewer.");

        if (complaint.Status == RaceComplaintStatus.Pending)
        {
            if (request.Outcome != RaceComplaintStatus.Rejected)
                return ServiceResult<RaceComplaintResponse>.Fail(400, "Pending complaints can only be rejected at intake or routed for referee response.");
        }
        else if (complaint.Status != RaceComplaintStatus.UnderReview)
        {
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Only pending intake or under-review complaints can receive an admin ruling.");
        }

        if (request.Outcome == RaceComplaintStatus.Upheld && !request.AffectsResult.HasValue)
            return ServiceResult<RaceComplaintResponse>.Fail(400, "AffectsResult must be explicitly set when a complaint is upheld.");

        var oldStatus = complaint.Status;
        var now = DateTime.UtcNow;
        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            complaint.Status = request.Outcome.Value;
            complaint.Ruling = request.Ruling.Trim();
            complaint.RuledByUserId = ruledByUserId;
            complaint.AffectsResult = request.Outcome == RaceComplaintStatus.Upheld ? request.AffectsResult : null;
            complaint.ResolvedAt = now;
            complaint.UpdatedAt = now;
            await _repo.UpdateAsync(complaint);

            if (complaint.Status == RaceComplaintStatus.Upheld && complaint.AffectsResult == true)
            {
                var raceResult = await _raceResultRepo.GetByRaceIdAsync(complaint.RaceId);
                if (raceResult == null || raceResult.Status != RaceResultStatus.Provisional)
                    return ServiceResult<RaceComplaintResponse>.Fail(409, "Current race result must be provisional before a material upheld complaint can require correction.");

                raceResult.RejectedReason = RaceResultCorrectionMessages.UpheldRaceComplaintRequiresCorrection;
                await _raceResultRepo.UpdateAsync(raceResult);
            }

            await _uow.SaveChangesAsync();
            scope.Complete();
        }

        await AuditAdminActionAsync(
            complaint,
            ruledByUserId,
            complaint.Status == RaceComplaintStatus.Upheld ? AuditAction.Approve : AuditAction.Reject,
            oldStatus,
            complaint.Status,
            $"Race complaint ruled {complaint.Status}.");
        await NotifyUserAsync(complaint.FiledByUserId, $"Race complaint {complaint.Status}", "An admin has issued a final race complaint decision.", complaint.Id, "/profile");

        return ServiceResult<RaceComplaintResponse>.Ok(Map(await _repo.GetByIdAsync(id) ?? complaint));
    }

    public async Task<ServiceResult<RaceComplaintResponse>> WithdrawAsync(Guid id, Guid requestingUserId)
    {
        var complaint = await _repo.GetByIdAsync(id);
        if (complaint == null)
            return ServiceResult<RaceComplaintResponse>.Fail(404, "Race complaint not found.");
        if (complaint.FiledByUserId != requestingUserId)
            return ServiceResult<RaceComplaintResponse>.Fail(403, "Only the original filer can withdraw this complaint.");
        if (!ActiveStatuses.Contains(complaint.Status))
            return ServiceResult<RaceComplaintResponse>.Fail(400, "Terminal complaints cannot be withdrawn.");

        var now = DateTime.UtcNow;
        complaint.Status = RaceComplaintStatus.Withdrawn;
        complaint.ResolvedAt = now;
        complaint.UpdatedAt = now;

        await _repo.UpdateAsync(complaint);
        await _uow.SaveChangesAsync();
        return ServiceResult<RaceComplaintResponse>.Ok(Map(await _repo.GetByIdAsync(id) ?? complaint));
    }

    // COMPLAINT-EVIDENCE-V1.1: max 5 files per side, counted from persisted EvidenceSource rows —
    // backend-authoritative, independent of whatever the client's own gallery count shows.
    private const int MaxEvidencePerSource = 5;

    // COMPLAINT-EVIDENCE-V1: the original filer (Owner/Jockey) may attach evidence while their
    // complaint is still active; the assigned Referee may attach supplementary evidence only up to
    // submitting their response (COMPLAINT-EVIDENCE-V1.1: not during UnderReview — once the referee
    // has responded, admin review must operate on a stable evidence set). Neither role can attach
    // evidence to someone else's complaint, and uploading is never itself a ruling action.
    public async Task<ServiceResult<RaceComplaintEvidenceResponse>> UploadEvidenceAsync(Guid id, IFormFile file, Guid uploaderUserId)
    {
        if (file == null || file.Length == 0)
            return ServiceResult<RaceComplaintEvidenceResponse>.Fail(400, "Không có file nào được tải lên.");

        var complaint = await _repo.GetByIdAsync(id);
        if (complaint == null)
            return ServiceResult<RaceComplaintEvidenceResponse>.Fail(404, "Race complaint not found.");

        var isFiler = complaint.FiledByUserId == uploaderUserId;
        var isAssignedReferee = complaint.AssignedRefereeAssignment?.Referee?.UserId == uploaderUserId;

        if (!isFiler && !isAssignedReferee)
            return ServiceResult<RaceComplaintEvidenceResponse>.Fail(403, "You are not permitted to upload evidence for this complaint.");

        if (isFiler && !ActiveStatuses.Contains(complaint.Status))
            return ServiceResult<RaceComplaintEvidenceResponse>.Fail(400, "This complaint is no longer active.");

        if (isAssignedReferee && complaint.Status != RaceComplaintStatus.AwaitingRefereeResponse)
            return ServiceResult<RaceComplaintEvidenceResponse>.Fail(400, "You can only add evidence before submitting your response.");

        // Source is derived from the caller's verified relationship to the complaint above, never
        // from anything the client sends — the filer branch is checked first so a user who happens
        // to be both the filer and the assigned referee (different races) is recorded as Filer here.
        var source = isFiler ? EvidenceSource.Filer : EvidenceSource.Referee;

        var existingCount = await _evidenceRepo.CountBySourceAsync(id, source);
        if (existingCount >= MaxEvidencePerSource)
            return ServiceResult<RaceComplaintEvidenceResponse>.Fail(400, $"Đã đạt giới hạn tối đa {MaxEvidencePerSource} tệp minh chứng.");

        MediaUploadResult upload;
        try
        {
            upload = await _cloudStorage.UploadMediaAsync(file, "race-complaint-evidence");
        }
        catch (Exception ex)
        {
            return ServiceResult<RaceComplaintEvidenceResponse>.Fail(400, ex.Message);
        }

        var mediaType = string.Equals(upload.ResourceType, "video", StringComparison.OrdinalIgnoreCase)
            ? ComplaintEvidenceMediaType.Video
            : ComplaintEvidenceMediaType.Image;

        var evidence = new RaceComplaintEvidence
        {
            Id = Guid.NewGuid(),
            RaceComplaintId = id,
            UploadedByUserId = uploaderUserId,
            FileUrl = upload.Url,
            MediaType = mediaType,
            EvidenceSource = source,
            FileName = file.FileName,
            PublicId = upload.PublicId,
            FileSizeBytes = upload.FileSizeBytes,
            UploadedAt = DateTime.UtcNow,
        };

        await _evidenceRepo.AddAsync(evidence);
        await _uow.SaveChangesAsync();

        var uploader = await _userRepo.GetByIdAsync(uploaderUserId);
        return ServiceResult<RaceComplaintEvidenceResponse>.Success(new RaceComplaintEvidenceResponse
        {
            Id = evidence.Id,
            RaceComplaintId = id,
            FileUrl = evidence.FileUrl,
            MediaType = evidence.MediaType.ToString(),
            EvidenceSource = evidence.EvidenceSource.ToString(),
            FileName = evidence.FileName,
            UploadedByUserId = uploaderUserId,
            UploadedByName = uploader?.FullName ?? uploader?.Email,
            UploadedByRole = uploader?.Role.ToString(),
            FileSizeBytes = evidence.FileSizeBytes,
            UploadedAt = evidence.UploadedAt,
        }, 201);
    }

    // COMPLAINT-EVIDENCE-V1.1: mirrors UploadEvidenceAsync's ownership/lifecycle rules exactly, plus
    // the extra constraint that a side may only ever delete its own evidence. Referee evidence
    // becomes read-only the moment a response is submitted (Status leaves AwaitingRefereeResponse),
    // matching the narrower upload window above, so admin review always sees a stable evidence set.
    public async Task<ServiceResult<bool>> DeleteEvidenceAsync(Guid id, Guid evidenceId, Guid requestingUserId)
    {
        var complaint = await _repo.GetByIdAsync(id);
        if (complaint == null)
            return ServiceResult<bool>.Fail(404, "Race complaint not found.");

        var evidence = await _evidenceRepo.GetByIdAsync(evidenceId);
        if (evidence == null || evidence.RaceComplaintId != id)
            return ServiceResult<bool>.Fail(404, "Evidence not found.");

        // Only the original uploader may ever delete a row: since a row can only ever have been
        // created by the verified filer or the verified assigned referee (see UploadEvidenceAsync),
        // this single check also covers "filer cannot delete referee evidence", "referee cannot
        // delete filer evidence", and "unrelated user forbidden" without needing separate checks.
        if (evidence.UploadedByUserId != requestingUserId)
            return ServiceResult<bool>.Fail(403, "You can only delete your own evidence.");

        if (evidence.EvidenceSource == EvidenceSource.Filer)
        {
            if (!ActiveStatuses.Contains(complaint.Status))
                return ServiceResult<bool>.Fail(400, "This complaint is no longer active.");
        }
        else
        {
            if (complaint.Status != RaceComplaintStatus.AwaitingRefereeResponse)
                return ServiceResult<bool>.Fail(400, "Your evidence is read-only after your response has been submitted.");
        }

        if (!string.IsNullOrWhiteSpace(evidence.PublicId))
        {
            var resourceType = evidence.MediaType == ComplaintEvidenceMediaType.Video ? "video" : "image";
            await _cloudStorage.DeleteAsync(evidence.PublicId, resourceType);
        }

        await _evidenceRepo.RemoveAsync(evidence);
        await _uow.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
    }

    private async Task<bool> HasFilingStandingAsync(User user, Guid raceId)
    {
        if (user.Role == UserRole.HorseOwner)
        {
            var owner = await _ownerRepo.GetByUserIdAsync(user.Id);
            return owner != null && await _entryRepo.OwnerHasHorseInRaceAsync(raceId, owner.Id);
        }

        if (user.Role == UserRole.Jockey)
        {
            var jockey = await _jockeyRepo.GetByUserIdAsync(user.Id);
            if (jockey == null) return false;
            var entries = await _entryRepo.GetByRaceAsync(raceId);
            return entries.Any(e => e.JockeyId == jockey.Id && e.Status == RegistrationStatus.Approved);
        }

        return false;
    }

    private async Task NotifyAdminsAsync(string title, string message, Guid complaintId)
    {
        if (_notifications == null) return;
        var admins = await _db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var adminId in admins)
            await NotifyUserAsync(adminId, title, message, complaintId, "/admin/race-complaints");
    }

    private async Task NotifyUserAsync(Guid userId, string title, string message, Guid complaintId, string actionUrl)
    {
        if (_notifications == null || userId == Guid.Empty) return;
        await _notifications.CreateNotificationAsync(new CreateNotificationDto
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = NotificationType.InApp,
            Category = NotificationCategory.Other,
            RelatedEntityId = complaintId,
            RelatedEntityType = nameof(RaceComplaint),
            ActionUrl = actionUrl,
        });
    }

    private async Task AuditAdminActionAsync(
        RaceComplaint complaint,
        Guid adminUserId,
        AuditAction action,
        RaceComplaintStatus oldStatus,
        RaceComplaintStatus newStatus,
        string description)
    {
        if (_auditLogs == null) return;
        await _auditLogs.LogActionAsync(new CreateAuditLogDto
        {
            AdminId = adminUserId,
            EntityType = nameof(RaceComplaint),
            EntityId = complaint.Id,
            Action = action,
            OldValues = JsonSerializer.Serialize(new { Status = oldStatus.ToString() }),
            NewValues = JsonSerializer.Serialize(new { Status = newStatus.ToString(), complaint.AssignedRefereeAssignmentId, complaint.AffectsResult }),
            Description = description,
            UserId = complaint.FiledByUserId,
        });
    }

    private static RaceComplaintResponse Map(RaceComplaint c, bool includeAssignmentOptions = true)
    {
        var currentResult = c.Race?.Result;
        return new RaceComplaintResponse
        {
            Id = c.Id,
            RaceId = c.RaceId,
            RaceName = c.Race?.Name,
            TournamentName = c.Race?.Tournament?.Name,
            ScheduledAt = c.Race?.ScheduledAt,
            FiledByUserId = c.FiledByUserId,
            FiledByName = c.FiledByUser?.FullName ?? c.FiledByUser?.Email,
            Type = c.Type.ToString(),
            Reason = c.Reason,
            EvidenceDescription = c.EvidenceDescription,
            Status = c.Status.ToString(),
            AssignedRefereeAssignmentId = c.AssignedRefereeAssignmentId,
            AssignedRefereeId = c.AssignedRefereeAssignment?.RefereeId,
            AssignedRefereeName = c.AssignedRefereeAssignment?.Referee?.User?.FullName ?? c.AssignedRefereeAssignment?.Referee?.User?.Email,
            AssignedRefereeRole = c.AssignedRefereeAssignment?.Role,
            ResponseRequestedAt = c.ResponseRequestedAt,
            RefereeResponse = c.RefereeResponse,
            RefereeRespondedAt = c.RefereeRespondedAt,
            RuledByUserId = c.RuledByUserId,
            RuledByName = c.RuledByUser?.FullName ?? c.RuledByUser?.Email,
            Ruling = c.Ruling,
            AffectsResult = c.AffectsResult,
            ResolvedAt = c.ResolvedAt,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            CurrentResult = currentResult == null ? null : new RaceComplaintResultSummary
            {
                RaceId = currentResult.RaceId,
                ResultStatus = currentResult.Status.ToString(),
                WinningHorseId = currentResult.WinningHorseId,
                WinningHorseName = currentResult.WinningHorse?.Name,
                RankingsJson = currentResult.RankingsJson,
                RejectedReason = currentResult.RejectedReason,
            },
            ConfirmedRefereeAssignments = includeAssignmentOptions
                ? c.Race?.RefereeAssignments
                    .Where(a => a.Status == RefereeAssignmentStatus.Confirmed)
                    .OrderBy(a => a.Role)
                    .Select(a => new RaceComplaintAssignmentOption
                    {
                        Id = a.Id,
                        RefereeId = a.RefereeId,
                        RefereeName = a.Referee?.User?.FullName ?? a.Referee?.User?.Email,
                        Role = a.Role,
                        Status = a.Status.ToString(),
                        AssignedAt = a.AssignedAt,
                        ConfirmedAt = a.ConfirmedAt,
                    })
                    .ToList() ?? new List<RaceComplaintAssignmentOption>()
                : new List<RaceComplaintAssignmentOption>(),
            Evidence = c.Evidence
                .OrderBy(e => e.UploadedAt)
                .Select(e => new RaceComplaintEvidenceResponse
                {
                    Id = e.Id,
                    RaceComplaintId = e.RaceComplaintId,
                    FileUrl = e.FileUrl,
                    MediaType = e.MediaType.ToString(),
                    EvidenceSource = e.EvidenceSource.ToString(),
                    FileName = e.FileName,
                    UploadedByUserId = e.UploadedByUserId,
                    UploadedByName = e.UploadedByUser?.FullName ?? e.UploadedByUser?.Email,
                    UploadedByRole = e.UploadedByUser?.Role.ToString(),
                    FileSizeBytes = e.FileSizeBytes,
                    UploadedAt = e.UploadedAt,
                })
                .ToList(),
        };
    }
}
