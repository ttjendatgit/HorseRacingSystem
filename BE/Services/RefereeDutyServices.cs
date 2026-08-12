using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;

namespace HorseRacing.Services;

public class RefereeHealthCheckService : IRefereeHealthCheckService
{
    private readonly IHealthCheckRepository _healthCheckRepo;
    private readonly IRaceRepository _raceRepo;
    private readonly IHorseRepository _horseRepo;
    private readonly IRefereeRepository _refereeRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RefereeHealthCheckService(
        IHealthCheckRepository healthCheckRepo,
        IRaceRepository raceRepo,
        IHorseRepository horseRepo,
        IRefereeRepository refereeRepo,
        IUnitOfWork unitOfWork)
    {
        _healthCheckRepo = healthCheckRepo;
        _raceRepo = raceRepo;
        _horseRepo = horseRepo;
        _refereeRepo = refereeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<HealthCheckResponse>> CreateHealthCheckAsync(CreateHealthCheckRequest request)
    {
        try
        {
            if (!Enum.TryParse<HealthCheckStatus>(request.HealthCheckStatus, out var status))
            {
                return ServiceResult<HealthCheckResponse>.Error(
                    $"Trạng thái không hợp lệ: {request.HealthCheckStatus}", 400);
            }

            var healthCheck = new HorseHealthCheck
            {
                Id = Guid.NewGuid(),
                HorseId = request.HorseId,
                RaceId = request.RaceId,
                RefereeId = request.RefereeId,
                Status = status,
                CheckedAt = DateTime.UtcNow,
                Observations = request.Observations,
                ApprovedToRace = status == HealthCheckStatus.Passed
            };

            await _healthCheckRepo.AddAsync(healthCheck);
            await _unitOfWork.SaveChangesAsync();

            var horse = await _horseRepo.GetByIdAsync(request.HorseId);
            var race = await _raceRepo.GetByIdAsync(request.RaceId);
            var referee = await _refereeRepo.GetByIdAsync(request.RefereeId);

            return ServiceResult<HealthCheckResponse>.Success(
                MapToResponse(healthCheck, horse, race, referee), 201);
        }
        catch (Exception ex)
        {
            return ServiceResult<HealthCheckResponse>.Error($"Lỗi tạo kiểm tra sức khỏe: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<HealthCheckResponse>> CompleteHealthCheckAsync(CompleteHealthCheckRequest request)
    {
        try
        {
            var healthCheck = await _healthCheckRepo.GetByIdAsync(request.HealthCheckId);
            if (healthCheck == null)
            {
                return ServiceResult<HealthCheckResponse>.Error("Không tìm thấy kiểm tra sức khỏe", 404);
            }

            healthCheck.Status = Enum.Parse<HealthCheckStatus>(request.Status);
            healthCheck.Verdict = request.Verdict;
            healthCheck.ApprovedToRace = request.ApprovedToRace;

            await _healthCheckRepo.UpdateAsync(healthCheck);
            await _unitOfWork.SaveChangesAsync();

            var horse = await _horseRepo.GetByIdAsync(healthCheck.HorseId);
            var race = await _raceRepo.GetByIdAsync(healthCheck.RaceId);
            var referee = await _refereeRepo.GetByIdAsync(healthCheck.RefereeId);

            return ServiceResult<HealthCheckResponse>.Success(
                MapToResponse(healthCheck, horse, race, referee));
        }
        catch (Exception ex)
        {
            return ServiceResult<HealthCheckResponse>.Error($"Lỗi hoàn thành kiểm tra sức khỏe: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<HealthCheckResponse>> GetHealthCheckAsync(Guid id)
    {
        try
        {
            var healthCheck = await _healthCheckRepo.GetByIdAsync(id);
            if (healthCheck == null)
            {
                return ServiceResult<HealthCheckResponse>.Error("Không tìm thấy kiểm tra sức khỏe", 404);
            }

            var horse = await _horseRepo.GetByIdAsync(healthCheck.HorseId);
            var race = await _raceRepo.GetByIdAsync(healthCheck.RaceId);
            var referee = await _refereeRepo.GetByIdAsync(healthCheck.RefereeId);

            return ServiceResult<HealthCheckResponse>.Success(
                MapToResponse(healthCheck, horse, race, referee));
        }
        catch (Exception ex)
        {
            return ServiceResult<HealthCheckResponse>.Error($"Lỗi truy xuất kiểm tra sức khỏe: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<IEnumerable<HealthCheckResponse>>> GetRaceHealthChecksAsync(Guid raceId)
    {
        try
        {
            var healthChecks = await _healthCheckRepo.GetByRaceAsync(raceId);
            var race = await _raceRepo.GetByIdAsync(raceId);

            var responses = new List<HealthCheckResponse>();
            foreach (var hc in healthChecks)
            {
                var horse = await _horseRepo.GetByIdAsync(hc.HorseId);
                var referee = await _refereeRepo.GetByIdAsync(hc.RefereeId);
                responses.Add(MapToResponse(hc, horse, race, referee));
            }

            return ServiceResult<IEnumerable<HealthCheckResponse>>.Success(responses);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<HealthCheckResponse>>.Error(
                $"Lỗi truy xuất danh sách kiểm tra: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<IEnumerable<HealthCheckResponse>>> GetHorseHealthCheckHistoryAsync(Guid horseId)
    {
        try
        {
            var healthChecks = await _healthCheckRepo.GetByHorseAsync(horseId);
            var horse = await _horseRepo.GetByIdAsync(horseId);

            var responses = new List<HealthCheckResponse>();
            foreach (var hc in healthChecks)
            {
                var race = await _raceRepo.GetByIdAsync(hc.RaceId);
                var referee = await _refereeRepo.GetByIdAsync(hc.RefereeId);
                responses.Add(MapToResponse(hc, horse, race, referee));
            }

            return ServiceResult<IEnumerable<HealthCheckResponse>>.Success(responses);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<HealthCheckResponse>>.Error(
                $"Lỗi truy xuất lịch sử kiểm tra: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<bool>> ApproveHorseForRaceAsync(Guid healthCheckId)
    {
        try
        {
            var healthCheck = await _healthCheckRepo.GetByIdAsync(healthCheckId);
            if (healthCheck == null)
            {
                return ServiceResult<bool>.Error("Không tìm thấy kiểm tra sức khỏe", 404);
            }

            healthCheck.ApprovedToRace = true;
            healthCheck.Status = HealthCheckStatus.Passed;

            await _healthCheckRepo.UpdateAsync(healthCheck);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Error($"Lỗi phê duyệt ngựa: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<bool>> RejectHorseForRaceAsync(Guid healthCheckId, string reason)
    {
        try
        {
            var healthCheck = await _healthCheckRepo.GetByIdAsync(healthCheckId);
            if (healthCheck == null)
            {
                return ServiceResult<bool>.Error("Không tìm thấy kiểm tra sức khỏe", 404);
            }

            healthCheck.ApprovedToRace = false;
            healthCheck.Status = HealthCheckStatus.Failed;
            healthCheck.Verdict = reason;

            await _healthCheckRepo.UpdateAsync(healthCheck);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Error($"Lỗi từ chối ngựa: {ex.Message}", 500);
        }
    }

    private HealthCheckResponse MapToResponse(HorseHealthCheck hc, Horse? horse, Race? race, Referee? referee)
    {
        return new HealthCheckResponse
        {
            Id = hc.Id,
            HorseId = hc.HorseId,
            HorseName = horse?.Name,
            RaceId = hc.RaceId,
            RaceName = race?.Name,
            RefereeId = hc.RefereeId,
            RefereeName = referee?.User?.FullName,
            Status = hc.Status.ToString(),
            CheckedAt = hc.CheckedAt,
            Observations = hc.Observations,
            Verdict = hc.Verdict,
            ApprovedToRace = hc.ApprovedToRace
        };
    }
}

public class ViolationRecordService : IViolationRecordService
{
    private readonly IViolationRecordRepository _violationRepo;
    private readonly IRaceRepository _raceRepo;
    private readonly IRaceEntryRepository _entryRepo;
    private readonly IRefereeRepository _refereeRepo;
    private readonly INotificationService _notificationService;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ViolationRecordService(
        IViolationRecordRepository violationRepo,
        IRaceRepository raceRepo,
        IRaceEntryRepository entryRepo,
        IRefereeRepository refereeRepo,
        INotificationService notificationService,
        IUserRepository userRepo,
        IUnitOfWork unitOfWork)
    {
        _violationRepo = violationRepo;
        _raceRepo = raceRepo;
        _entryRepo = entryRepo;
        _refereeRepo = refereeRepo;
        _notificationService = notificationService;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<ViolationResponse>> RecordViolationAsync(CreateViolationRequest request, Guid refereeUserId)
    {
        try
        {
            // Resolve RaceEntryId from HorseId + RaceId
            var entry = await _entryRepo.GetByRaceAndHorseAsync(request.RaceId, request.HorseId);
            if (entry == null)
            {
                return ServiceResult<ViolationResponse>.Error("Không tìm thấy ngựa trong cuộc đua này", 404);
            }

            // Resolve RefereeId from the authenticated user
            var referee = await _refereeRepo.GetByUserIdAsync(refereeUserId);
            if (referee == null)
            {
                return ServiceResult<ViolationResponse>.Error("Không tìm thấy hồ sơ trọng tài", 404);
            }

            var violation = new ViolationRecord
            {
                Id = Guid.NewGuid(),
                RaceId = request.RaceId,
                RaceEntryId = entry.Id,
                RefereeId = referee.Id,
                ViolationType = (ViolationType)request.ViolationType,
                Description = request.Description,
                RecordedAt = DateTime.UtcNow,
                Evidence = request.Evidence,
                Penalty = request.Penalty
            };

            await _violationRepo.AddAsync(violation);
            await _unitOfWork.SaveChangesAsync();

            var race = await _raceRepo.GetByIdAsync(request.RaceId);

            // Notify all admins about the violation
            try
            {
                var users = await _userRepo.GetAllAsync();
                var admins = users.Where(u => u.Role == UserRole.Admin && u.IsActive).ToList();
                foreach (var admin in admins)
                {
                    try
                    {
                        await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                        {
                            UserId = admin.Id,
                            Title = "Vi phạm mới được ghi nhận",
                            Message = $"Trọng tài {referee.User?.FullName ?? referee.Id.ToString()} đã ghi nhận vi phạm trong cuộc đua \"{race?.Name ?? request.RaceId.ToString()}\": {request.Description[..Math.Min(request.Description.Length, 100)]}",
                            Type = NotificationType.InApp,
                            Category = NotificationCategory.ViolationRecord,
                            RelatedEntityId = race?.Id,
                            RelatedEntityType = "Race"
                        });
                    }
                    catch { /* skip failed notification */ }
                }
            }
            catch { /* non-critical */ }

            return ServiceResult<ViolationResponse>.Success(
                MapToResponse(violation, race, entry, referee), 201);
        }
        catch (Exception ex)
        {
            return ServiceResult<ViolationResponse>.Error($"Lỗi ghi nhận vi phạm: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<ViolationResponse>> GetViolationAsync(Guid id)
    {
        try
        {
            var violation = await _violationRepo.GetByIdAsync(id);
            if (violation == null)
            {
                return ServiceResult<ViolationResponse>.Error("Không tìm thấy vi phạm", 404);
            }

            var race = await _raceRepo.GetByIdAsync(violation.RaceId);
            var entry = await _entryRepo.GetByIdAsync(violation.RaceEntryId);
            var referee = await _refereeRepo.GetByIdAsync(violation.RefereeId);

            return ServiceResult<ViolationResponse>.Success(
                MapToResponse(violation, race, entry, referee));
        }
        catch (Exception ex)
        {
            return ServiceResult<ViolationResponse>.Error($"Lỗi truy xuất vi phạm: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<IEnumerable<ViolationResponse>>> GetRaceViolationsAsync(Guid raceId)
    {
        try
        {
            var violations = await _violationRepo.GetByRaceAsync(raceId);
            var race = await _raceRepo.GetByIdAsync(raceId);

            var responses = new List<ViolationResponse>();
            foreach (var v in violations)
            {
                var entry = await _entryRepo.GetByIdAsync(v.RaceEntryId);
                var referee = await _refereeRepo.GetByIdAsync(v.RefereeId);
                responses.Add(MapToResponse(v, race, entry, referee));
            }

            return ServiceResult<IEnumerable<ViolationResponse>>.Success(responses);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<ViolationResponse>>.Error(
                $"Lỗi truy xuất danh sách vi phạm: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<IEnumerable<ViolationResponse>>> GetHorseViolationsAsync(Guid horseId)
    {
        try
        {
            var raceEntries = await _entryRepo.GetByHorseAsync(horseId);
            var allViolations = new List<ViolationRecord>();

            foreach (var entry in raceEntries)
            {
                var violations = await _violationRepo.GetByRaceEntryAsync(entry.Id);
                allViolations.AddRange(violations);
            }

            var responses = new List<ViolationResponse>();
            foreach (var v in allViolations)
            {
                var race = await _raceRepo.GetByIdAsync(v.RaceId);
                var entry = await _entryRepo.GetByIdAsync(v.RaceEntryId);
                var referee = await _refereeRepo.GetByIdAsync(v.RefereeId);
                responses.Add(MapToResponse(v, race, entry, referee));
            }

            return ServiceResult<IEnumerable<ViolationResponse>>.Success(responses);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<ViolationResponse>>.Error(
                $"Lỗi truy xuất vi phạm của ngựa: {ex.Message}", 500);
        }
    }

    private ViolationResponse MapToResponse(ViolationRecord v, Race? race, RaceEntry? entry, Referee? referee)
    {
        return new ViolationResponse
        {
            Id = v.Id,
            RaceId = v.RaceId,
            RaceName = race?.Name,
            RaceEntryId = v.RaceEntryId,
            HorseName = entry?.Horse?.Name,
            RefereeId = v.RefereeId,
            RefereeName = referee?.User?.FullName,
            ViolationType = v.ViolationType.ToString(),
            Description = v.Description,
            RecordedAt = v.RecordedAt,
            Evidence = v.Evidence,
            Penalty = v.Penalty
        };
    }
}

public class RaceReportService : IRaceReportService
{
    private readonly IRaceReportRepository _reportRepo;
    private readonly IRaceRepository _raceRepo;
    private readonly IRefereeRepository _refereeRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RaceReportService(
        IRaceReportRepository reportRepo,
        IRaceRepository raceRepo,
        IRefereeRepository refereeRepo,
        IUnitOfWork unitOfWork)
    {
        _reportRepo = reportRepo;
        _raceRepo = raceRepo;
        _refereeRepo = refereeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<RaceReportResponse>> CreateReportAsync(CreateRaceReportRequest request)
    {
        try
        {
            var report = new RaceReport
            {
                Id = Guid.NewGuid(),
                RaceId = request.RaceId,
                RefereeId = request.RefereeId,
                CompletedAt = DateTime.UtcNow,
                Details = request.Details,
                Incidents = request.Incidents,
                RecommendedActions = request.RecommendedActions,
                IsOfficialReport = false,
                CreatedAt = DateTime.UtcNow
            };

            await _reportRepo.AddAsync(report);
            await _unitOfWork.SaveChangesAsync();

            var race = await _raceRepo.GetByIdAsync(request.RaceId);
            var referee = await _refereeRepo.GetByIdAsync(request.RefereeId);

            return ServiceResult<RaceReportResponse>.Success(
                MapToResponse(report, race, referee), 201);
        }
        catch (Exception ex)
        {
            return ServiceResult<RaceReportResponse>.Error($"Lỗi tạo báo cáo: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<RaceReportResponse>> GetReportAsync(Guid id)
    {
        try
        {
            var report = await _reportRepo.GetByIdAsync(id);
            if (report == null)
            {
                return ServiceResult<RaceReportResponse>.Error("Không tìm thấy báo cáo", 404);
            }

            var race = await _raceRepo.GetByIdAsync(report.RaceId);
            var referee = await _refereeRepo.GetByIdAsync(report.RefereeId);

            return ServiceResult<RaceReportResponse>.Success(
                MapToResponse(report, race, referee));
        }
        catch (Exception ex)
        {
            return ServiceResult<RaceReportResponse>.Error($"Lỗi truy xuất báo cáo: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<RaceReportResponse>> GetRaceReportAsync(Guid raceId)
    {
        try
        {
            var report = await _reportRepo.GetByRaceAsync(raceId);
            if (report == null)
            {
                return ServiceResult<RaceReportResponse>.Error("Không tìm thấy báo cáo", 404);
            }

            var race = await _raceRepo.GetByIdAsync(raceId);
            var referee = await _refereeRepo.GetByIdAsync(report.RefereeId);

            return ServiceResult<RaceReportResponse>.Success(
                MapToResponse(report, race, referee));
        }
        catch (Exception ex)
        {
            return ServiceResult<RaceReportResponse>.Error($"Lỗi truy xuất báo cáo cuộc đua: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<IEnumerable<RaceReportResponse>>> GetRefereeReportsAsync(Guid refereeId)
    {
        try
        {
            var reports = await _reportRepo.GetByRefereeAsync(refereeId);
            var referee = await _refereeRepo.GetByIdAsync(refereeId);

            var responses = new List<RaceReportResponse>();
            foreach (var r in reports)
            {
                var race = await _raceRepo.GetByIdAsync(r.RaceId);
                responses.Add(MapToResponse(r, race, referee));
            }

            return ServiceResult<IEnumerable<RaceReportResponse>>.Success(responses);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<RaceReportResponse>>.Error(
                $"Lỗi truy xuất danh sách báo cáo: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<RaceReportResponse>> UpdateReportAsync(Guid id, CreateRaceReportRequest request)
    {
        try
        {
            var report = await _reportRepo.GetByIdAsync(id);
            if (report == null)
            {
                return ServiceResult<RaceReportResponse>.Error("Không tìm thấy báo cáo", 404);
            }

            report.Details = request.Details;
            report.Incidents = request.Incidents;
            report.RecommendedActions = request.RecommendedActions;
            report.UpdatedAt = DateTime.UtcNow;

            await _reportRepo.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();

            var race = await _raceRepo.GetByIdAsync(report.RaceId);
            var referee = await _refereeRepo.GetByIdAsync(report.RefereeId);

            return ServiceResult<RaceReportResponse>.Success(
                MapToResponse(report, race, referee));
        }
        catch (Exception ex)
        {
            return ServiceResult<RaceReportResponse>.Error($"Lỗi cập nhật báo cáo: {ex.Message}", 500);
        }
    }

    public async Task<ServiceResult<bool>> PublishReportAsync(Guid id)
    {
        try
        {
            var report = await _reportRepo.GetByIdAsync(id);
            if (report == null)
            {
                return ServiceResult<bool>.Error("Không tìm thấy báo cáo", 404);
            }

            report.IsOfficialReport = true;
            report.UpdatedAt = DateTime.UtcNow;

            await _reportRepo.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Error($"Lỗi xuất bản báo cáo: {ex.Message}", 500);
        }
    }

    private RaceReportResponse MapToResponse(RaceReport report, Race? race, Referee? referee)
    {
        return new RaceReportResponse
        {
            Id = report.Id,
            RaceId = report.RaceId,
            RaceName = race?.Name,
            RefereeId = report.RefereeId,
            RefereeName = referee?.User?.FullName,
            CompletedAt = report.CompletedAt,
            Details = report.Details,
            Incidents = report.Incidents,
            RecommendedActions = report.RecommendedActions,
            IsOfficialReport = report.IsOfficialReport,
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt
        };
    }
}
