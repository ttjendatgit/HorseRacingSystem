using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace HorseRacing.Services;

public class AdminService : IAdminService
{
    private readonly PasswordHasher<User> _passwordHasher = new();
    private readonly IUserRepository _userRepo;
    private readonly IUserRegistrationRepository _registrationRepo;
    private readonly IHorseRepository _horseRepo;
    private readonly IJockeyRepository _jockeyRepo;
    private readonly IRefereeRepository _refereeRepo;
    private readonly IRaceRepository _raceRepo;
    private readonly ITournamentRepository _tournamentRepo;
    private readonly IRefereeService _refereeService;
    private readonly ILiveResultService _liveResultService;
    private readonly IPredictionService _predictionService;
    private readonly IPredictionRepository _predictionRepo;
    private readonly IRaceResultRepository _raceResultRepo;
    private readonly IRaceEntryRepository _entryRepo;
    private readonly IRaceReportRepository _reportRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AdminService(
        IUserRepository userRepo,
        IUserRegistrationRepository registrationRepo,
        IHorseRepository horseRepo,
        IJockeyRepository jockeyRepo,
        IRefereeRepository refereeRepo,
        IRaceRepository raceRepo,
        ITournamentRepository tournamentRepo,
        IRefereeService refereeService,
        ILiveResultService liveResultService,
        IPredictionService predictionService,
        IPredictionRepository predictionRepo,
        IRaceResultRepository raceResultRepo,
        IRaceEntryRepository entryRepo,
        IRaceReportRepository reportRepo,
        IUnitOfWork unitOfWork)
    {
        _userRepo = userRepo;
        _registrationRepo = registrationRepo;
        _horseRepo = horseRepo;
        _jockeyRepo = jockeyRepo;
        _refereeRepo = refereeRepo;
        _raceRepo = raceRepo;
        _tournamentRepo = tournamentRepo;
        _refereeService = refereeService;
        _liveResultService = liveResultService;
        _predictionService = predictionService;
        _predictionRepo = predictionRepo;
        _raceResultRepo = raceResultRepo;
        _entryRepo = entryRepo;
        _reportRepo = reportRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<AdminDashboardResponse>> GetDashboardAsync()
    {
        try
        {
            var users = await _userRepo.GetAllAsync();
            var referees = await _refereeRepo.GetAllAsync();
            var tournaments = await _tournamentRepo.GetActiveAsync();
            var races = await _raceRepo.GetAllAsync();
            var registrations = await _registrationRepo.GetPendingAsync();

            var response = new AdminDashboardResponse
            {
                TotalUsers = users.Count(),
                TotalReferees = referees.Count(),
                ActiveTournaments = tournaments.Count(),
                UpcomingRaces = races.Count(r => r.Status == RaceStatus.Scheduled),
                PendingRegistrations = registrations.Count(),
                OngoingRaces = races.Count(r => r.Status == RaceStatus.InProgress),
                ActiveRaces = races
                    .Where(r => r.Status == RaceStatus.InProgress || r.Status == RaceStatus.Scheduled)
                    .OrderBy(r => r.ScheduledAt)
                    .Take(5)
                    .Select(r => new ActiveRaceItem
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Status = r.Status.ToString(),
                        EntryCount = r.Entries?.Count ?? 0,
                        ScheduledAt = r.ScheduledAt
                    }).ToList(),
                RecentActivity = registrations
                    .OrderByDescending(r => r.SubmittedAt)
                    .Take(5)
                    .Select(r => new RecentActivityItem
                    {
                        Action = "Đăng ký mới",
                        Subject = r.FullName ?? r.Email,
                        CreatedAt = r.SubmittedAt
                    }).ToList()
            };

            return ServiceResult<AdminDashboardResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<AdminDashboardResponse>.Fail(500, $"Lỗi tải dashboard: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<UserManagementResponse>>> GetAllUsersAsync()
    {
        try
        {
            var users = await _userRepo.GetAllAsync();
            var response = users.Select(u => new UserManagementResponse
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                Role = u.Role.ToString(),
                HorseCount = u.OwnerProfile?.Horses?.Count ?? 0,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            });

            return ServiceResult<IEnumerable<UserManagementResponse>>.Ok(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<UserManagementResponse>>.Fail(500, $"Lỗi truy xuất người dùng: {ex.Message}");
        }
    }

    public async Task<ServiceResult<UserManagementResponse>> GetUserAsync(Guid userId)
    {
        try
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<UserManagementResponse>.Fail(404, "Không tìm thấy người dùng");
            }

            var response = new UserManagementResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role.ToString(),
                HorseCount = user.OwnerProfile?.Horses?.Count ?? 0,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };

            return ServiceResult<UserManagementResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<UserManagementResponse>.Fail(500, $"Lỗi truy xuất người dùng: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> DeactivateUserAsync(Guid userId)
    {
        try
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy người dùng");
            }

            user.IsActive = false;
            await _userRepo.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi hủy kích hoạt người dùng: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> ReactivateUserAsync(Guid userId)
    {
        try
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy người dùng");
            }

            user.IsActive = true;
            await _userRepo.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi kích hoạt lại người dùng: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<AdminHorseResponse>>> GetOwnerHorsesAsync(Guid userId)
    {
        try
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<IEnumerable<AdminHorseResponse>>.Fail(404, "Không tìm thấy người dùng");
            }

            if (user.Role != UserRole.HorseOwner && user.Role != UserRole.Jockey)
            {
                return ServiceResult<IEnumerable<AdminHorseResponse>>.Fail(
                    400, "Chỉ người dùng có vai trò Chủ sở hữu mới có ngựa");
            }

            if (user.OwnerProfile == null)
            {
                return ServiceResult<IEnumerable<AdminHorseResponse>>.Fail(404, "Không tìm thấy hồ sơ chủ sở hữu");
            }

            var horses = await _horseRepo.GetByOwnerAsync(user.OwnerProfile.Id);
            return ServiceResult<IEnumerable<AdminHorseResponse>>.Ok(
                horses.Select(horse => MapHorseResponse(horse, user)));
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<AdminHorseResponse>>.Fail(
                500, $"Lỗi truy xuất ngựa của chủ sở hữu: {ex.Message}");
        }
    }

    public async Task<ServiceResult<AdminHorseResponse>> GetOwnerHorseAsync(Guid userId, Guid horseId)
    {
        try
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<AdminHorseResponse>.Fail(404, "Không tìm thấy người dùng");
            }

            if (user.Role != UserRole.HorseOwner && user.Role != UserRole.Jockey)
            {
                return ServiceResult<AdminHorseResponse>.Fail(
                    400, "Chỉ người dùng có vai trò Chủ sở hữu mới có ngựa");
            }

            if (user.OwnerProfile == null)
            {
                return ServiceResult<AdminHorseResponse>.Fail(404, "Không tìm thấy hồ sơ chủ sở hữu");
            }

            var horse = await _horseRepo.GetOwnedHorseAsync(horseId, user.OwnerProfile.Id);
            if (horse == null)
            {
                return ServiceResult<AdminHorseResponse>.Fail(
                    404, "Không tìm thấy ngựa của chủ sở hữu này");
            }

            return ServiceResult<AdminHorseResponse>.Ok(MapHorseResponse(horse, user));
        }
        catch (Exception ex)
        {
            return ServiceResult<AdminHorseResponse>.Fail(
                500, $"Lỗi truy xuất chi tiết ngựa: {ex.Message}");
        }
    }

    public async Task<ServiceResult<AdminHorseResponse>> UpdateOwnerHorseStatusAsync(
        Guid userId,
        Guid horseId,
        UpdateHorseApprovalStatusRequest request)
    {
        try
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<AdminHorseResponse>.Fail(404, "Không tìm thấy người dùng");
            }

            if (user.Role != UserRole.HorseOwner && user.Role != UserRole.Jockey)
            {
                return ServiceResult<AdminHorseResponse>.Fail(
                    400, "Chỉ người dùng có vai trò Chủ sở hữu mới có ngựa");
            }

            if (user.OwnerProfile == null)
            {
                return ServiceResult<AdminHorseResponse>.Fail(404, "Không tìm thấy hồ sơ chủ sở hữu");
            }

            if (!Enum.TryParse<ApprovalStatus>(request.Status, true, out var status) ||
                !Enum.IsDefined(status))
            {
                return ServiceResult<AdminHorseResponse>.Fail(
                    400, "Trạng thái phải là Pending, Approved hoặc Rejected");
            }

            if (status == ApprovalStatus.Rejected && string.IsNullOrWhiteSpace(request.Note))
            {
                return ServiceResult<AdminHorseResponse>.Fail(
                    400, "Cần ghi lý do khi từ chối ngựa");
            }

            var horse = await _horseRepo.GetOwnedHorseAsync(horseId, user.OwnerProfile.Id);
            if (horse == null)
            {
                return ServiceResult<AdminHorseResponse>.Fail(
                    404, "Không tìm thấy ngựa của chủ sở hữu này");
            }

            horse.ApprovalStatus = status;
            horse.ApprovalNote = status == ApprovalStatus.Rejected
                ? request.Note!.Trim()
                : null;

            await _horseRepo.UpdateAsync(horse);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<AdminHorseResponse>.Ok(MapHorseResponse(horse, user));
        }
        catch (Exception ex)
        {
            return ServiceResult<AdminHorseResponse>.Fail(
                500, $"Lỗi cập nhật trạng thái ngựa: {ex.Message}");
        }
    }

    public async Task<ServiceResult<UserRegistrationResponse>> GetRegistrationAsync(Guid id)
    {
        try
        {
            var registration = await _registrationRepo.GetByIdAsync(id);
            if (registration == null)
            {
                return ServiceResult<UserRegistrationResponse>.Fail(404, "Không tìm thấy đăng ký");
            }

            return ServiceResult<UserRegistrationResponse>.Ok(MapToResponse(registration));
        }
        catch (Exception ex)
        {
            return ServiceResult<UserRegistrationResponse>.Fail(500, $"Lỗi truy xuất thông tin đăng ký: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<UserRegistrationResponse>>> GetPendingRegistrationsAsync()
    {
        try
        {
            var registrations = await _registrationRepo.GetPendingAsync();
            return ServiceResult<IEnumerable<UserRegistrationResponse>>.Ok(
                registrations.Select(MapToResponse));
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<UserRegistrationResponse>>.Fail(
                500, $"Lỗi truy xuất đăng ký đang chờ: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<UserRegistrationResponse>>> GetAllRegistrationsAsync()
    {
        try
        {
            var registrations = await _registrationRepo.GetAllAsync();
            return ServiceResult<IEnumerable<UserRegistrationResponse>>.Ok(
                registrations.Select(MapToResponse));
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<UserRegistrationResponse>>.Fail(
                500, $"Lỗi truy xuất danh sách đăng ký: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> ApproveRegistrationAsync(ApproveRegistrationRequest request)
    {
        try
        {
            var registration = await _registrationRepo.GetByIdAsync(request.RegistrationId);
            if (registration == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy đăng ký");
            }

            // Create new user
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = registration.Email,
                FullName = registration.FullName,
                PasswordHash = string.Empty,
                Role = registration.RequestedRole,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            newUser.PasswordHash = _passwordHasher.HashPassword(newUser, request.Password);

            await _userRepo.AddAsync(newUser);

            // Update registration
            registration.Status = RegistrationStatus.Approved;
            registration.ReviewedAt = DateTime.UtcNow;
            registration.ApprovedUserId = newUser.Id;
            registration.AdminNotes = request.AdminNotes;

            await _registrationRepo.UpdateAsync(registration);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi phê duyệt đăng ký: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> RejectRegistrationAsync(RejectRegistrationRequest request)
    {
        try
        {
            var registration = await _registrationRepo.GetByIdAsync(request.RegistrationId);
            if (registration == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy đăng ký");
            }

            registration.Status = RegistrationStatus.Rejected;
            registration.ReviewedAt = DateTime.UtcNow;
            registration.RejectionReason = request.RejectionReason;
            registration.AdminNotes = request.AdminNotes;

            await _registrationRepo.UpdateAsync(registration);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi từ chối đăng ký: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> ApproveJockeyAsync(Guid jockeyId)
    {
        try
        {
            var jockey = await _jockeyRepo.GetByIdAsync(jockeyId);
            if (jockey == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy kỵ sĩ");
            }
            jockey.ApprovalStatus = ApprovalStatus.Approved;
            jockey.ApprovalNote = null;
            await _jockeyRepo.UpdateAsync(jockey);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi phê duyệt kỵ sĩ: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> RejectJockeyAsync(Guid jockeyId, string reason)
    {
        try
        {
            var jockey = await _jockeyRepo.GetByIdAsync(jockeyId);
            if (jockey == null)
            {
                return ServiceResult<bool>.Fail(404, "Không tìm thấy kỵ sĩ");
            }
            jockey.ApprovalStatus = ApprovalStatus.Rejected;
            jockey.ApprovalNote = reason;
            await _jockeyRepo.UpdateAsync(jockey);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi từ chối kỵ sĩ: {ex.Message}");
        }
    }

    public async Task<ServiceResult<RefereeAssignmentResponse>> AssignRefereeToRaceAsync(AssignRefereeRequest request)
    {
        return await _refereeService.AssignRefereeToRaceAsync(request);
    }

    public async Task<ServiceResult<bool>> PublishRaceResultAsync(Guid raceId, RaceResultRequest request)
    {
        return await _liveResultService.UpdateRaceResultAsync(raceId, request);
    }

    /// <summary>
    /// Admin-only retry path for the correction-pass settlement gap: if a
    /// wallet payout threw during ApproveRaceResultAsync, the affected
    /// prediction was reverted to Pending (see PredictionRepository.
    /// RevertWonToPendingAsync) but nothing re-invoked settlement. This
    /// re-derives the winner from the authoritative Official RaceResult
    /// (never from caller input) and calls the same idempotent
    /// SettlePredictionAsync used by ApproveRaceResultAsync, which only
    /// touches predictions still Pending — already-settled winners are
    /// untouched and no wallet call is made for them.
    /// </summary>
    public async Task<ServiceResult<object>> SettlePredictionsAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
                return ServiceResult<object>.Fail(404, "Không tìm thấy cuộc đua");

            if (race.Status != RaceStatus.Finished)
                return ServiceResult<object>.Fail(400, "Cuộc đua chưa kết thúc");

            var raceResult = await _raceResultRepo.GetByRaceIdAsync(raceId);
            if (raceResult == null)
                return ServiceResult<object>.Fail(404, "Chưa có kết quả cuộc đua.");

            if (raceResult.Status != RaceResultStatus.Official)
                return ServiceResult<object>.Fail(400, "Kết quả cuộc đua chưa chính thức (Official).");

            return await _predictionService.SettlePredictionAsync(raceId, raceResult.WinningHorseId);
        }
        catch (Exception ex)
        {
            return ServiceResult<object>.Fail(500, $"Lỗi thanh toán: {ex.Message}");
        }
    }

    /// <summary>
    /// Approves a Provisional result -> Official. Phase2B: Race.Status is
    /// never touched here (it is already Finished and stays Finished).
    /// Prediction settlement is triggered from here — the moment a result
    /// becomes Official — rather than from a later "end race" action.
    /// </summary>
    public async Task<ServiceResult<bool>> ApproveRaceResultAsync(Guid raceId)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");

            if (race.Status != RaceStatus.Finished)
                return ServiceResult<bool>.Fail(400, "Cuộc đua chưa kết thúc");

            var raceResult = await _raceResultRepo.GetByRaceIdAsync(raceId);
            if (raceResult == null)
                return ServiceResult<bool>.Fail(404, "Chưa có kết quả cuộc đua. Trọng tài phải nộp kết quả trước.");

            if (raceResult.Status != RaceResultStatus.Provisional)
                return ServiceResult<bool>.Fail(400, "Kết quả không ở trạng thái chờ duyệt (Provisional)");

            if (!string.IsNullOrWhiteSpace(raceResult.RejectedReason))
                return ServiceResult<bool>.Fail(400, "Kết quả này đã bị từ chối trước đó. Trọng tài phải nộp lại kết quả trước khi có thể duyệt.");

            // Check if there is at least one referee report (V1.1 mandatory-report gate)
            var hasReport = await _reportRepo.GetByRaceAsync(raceId) != null;
            if (!hasReport)
                return ServiceResult<bool>.Fail(400, "Chưa có báo cáo từ trọng tài. Trọng tài phải nộp báo cáo trước khi duyệt kết quả.");

            // Wrap in transaction: if approval/settlement fails, everything rolls back
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            try
            {
                raceResult.Status = RaceResultStatus.Official;
                raceResult.RejectedReason = null;
                raceResult.ApprovedAt = DateTime.UtcNow;
                await _raceResultRepo.UpdateAsync(raceResult);

                // Ghi kết quả vào entry + cập nhật thành tích ngựa/kỵ sĩ
                var entries = await _entryRepo.GetByRaceAsync(raceId);
                foreach (var entry in entries)
                {
                    if (entry.HorseId == raceResult.WinningHorseId)
                    {
                        entry.FinishPosition = 1;
                    }

                    var horse = await _horseRepo.GetByIdAsync(entry.HorseId);
                    if (horse != null)
                    {
                        horse.TotalRaces += 1;
                        if (entry.HorseId == raceResult.WinningHorseId) horse.TotalWins += 1;
                        await _horseRepo.UpdateAsync(horse);
                    }

                    if (entry.JockeyId.HasValue)
                    {
                        var jockey = await _jockeyRepo.GetByIdAsync(entry.JockeyId.Value);
                        if (jockey != null)
                        {
                            jockey.TotalRaces += 1;
                            if (entry.HorseId == raceResult.WinningHorseId) jockey.TotalWins += 1;
                            jockey.WinRate = jockey.TotalRaces > 0
                                ? Math.Round((decimal)jockey.TotalWins / jockey.TotalRaces * 100, 1)
                                : 0;
                            await _jockeyRepo.UpdateAsync(jockey);
                        }
                    }
                }
                await _entryRepo.UpdateRangeAsync(entries);

                await _unitOfWork.SaveChangesAsync();

                // Settlement only after Official — see PredictionService.SettlePredictionAsync
                // for the explicit Race.Status==Finished + RaceResult.Status==Official guard.
                await _predictionService.SettlePredictionAsync(raceId, raceResult.WinningHorseId);

                scope.Complete();
                return ServiceResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail(500, $"Lỗi phê duyệt kết quả: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi phê duyệt kết quả: {ex.Message}");
        }
    }

    public async Task<ServiceResult<object>> GetPredictionsAsync()
    {
        try
        {
            var predictions = await _predictionRepo.GetAllAsync();
            var items = predictions.Select(p => new
            {
                id = p.Id,
                raceId = p.RaceId,
                raceName = p.Race?.Name,
                tournamentName = p.Race?.Tournament?.Name,
                spectatorId = p.SpectatorUserId,
                spectatorName = p.Spectator?.FullName ?? p.Spectator?.Email,
                horseId = p.PredictedHorseId,
                horseName = p.PredictedHorse?.Name ?? p.HorseNameSnapshot,
                betAmount = p.BetAmount,
                odds = p.Odds,
                potentialPayout = p.PotentialPayout,
                payoutAmount = p.PayoutAmount,
                status = p.Status.ToString(),
                createdAt = p.CreatedAt,
                settledAt = p.SettledAt
            }).ToList();

            var summary = new
            {
                totalPredictions = items.Count,
                totalBet = items.Sum(i => i.betAmount),
                totalPaid = items.Sum(i => i.payoutAmount ?? 0),
                won = items.Count(i => i.status == "Won"),
                lost = items.Count(i => i.status == "Lost"),
                pending = items.Count(i => i.status == "Pending")
            };

            return ServiceResult<object>.Ok(new { summary, items });
        }
        catch (Exception ex)
        {
            return ServiceResult<object>.Fail(500, $"Lỗi truy xuất dự đoán: {ex.Message}");
        }
    }

    /// <summary>
    /// Rejects a Provisional result. Phase2B: rejection is review metadata,
    /// not a status transition — RaceResult.Status stays Provisional, and
    /// Race.Status is never touched (already Finished, stays Finished).
    /// </summary>
    public async Task<ServiceResult<bool>> RejectRaceResultAsync(Guid raceId, string reason)
    {
        try
        {
            var race = await _raceRepo.GetByIdAsync(raceId);
            if (race == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy cuộc đua");

            if (race.Status != RaceStatus.Finished)
                return ServiceResult<bool>.Fail(400, "Cuộc đua chưa kết thúc");

            var raceResult = await _raceResultRepo.GetByRaceIdAsync(raceId);
            if (raceResult == null)
                return ServiceResult<bool>.Fail(404, "Không tìm thấy kết quả cuộc đua");

            if (raceResult.Status != RaceResultStatus.Provisional)
                return ServiceResult<bool>.Fail(400, "Kết quả không ở trạng thái chờ duyệt (Provisional)");

            raceResult.RejectedReason = reason;
            await _raceResultRepo.UpdateAsync(raceResult);

            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi từ chối kết quả: {ex.Message}");
        }
    }

    private UserRegistrationResponse MapToResponse(UserRegistration registration)
    {
        return new UserRegistrationResponse
        {
            Id = registration.Id,
            Email = registration.Email,
            FullName = registration.FullName,
            RequestedRole = registration.RequestedRole.ToString(),
            Description = registration.Description,
            DocumentUrl = registration.DocumentUrl,
            Status = registration.Status.ToString(),
            SubmittedAt = registration.SubmittedAt,
            ReviewedAt = registration.ReviewedAt,
            ReviewedByUserName = registration.ReviewedByUser?.FullName,
            RejectionReason = registration.RejectionReason,
            AdminNotes = registration.AdminNotes
        };
    }

    private static AdminHorseResponse MapHorseResponse(Horse horse, User ownerUser)
    {
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
        var assignedJockeyIds = horse.JockeyInvitations
            .Where(invitation =>
                invitation.Status == JockeyInvitationStatus.Pending ||
                invitation.Status == JockeyInvitationStatus.Accepted)
            .Select(invitation => invitation.JockeyId)
            .Concat(
                horse.RaceEntries
                    .Where(entry => entry.JockeyId.HasValue)
                    .Select(entry => entry.JockeyId!.Value))
            .Distinct()
            .ToArray();

        return new AdminHorseResponse
        {
            Id = horse.Id,
            Name = horse.Name,
            Breed = horse.Breed,
            Gender = horse.Gender,
            DateOfBirth = horse.DateOfBirth,
            Age = horse.Age,
            Weight = horse.Weight,
            Height = horse.Height,
            Color = horse.Color,
            ImageUrl = horse.ImageUrl,
            TotalRaces = horse.TotalRaces,
            TotalWins = horse.TotalWins,
            ApprovalStatus = horse.ApprovalStatus.ToString(),
            ApprovalNote = horse.ApprovalNote,
            OwnerId = horse.OwnerId,
            OwnerUserId = ownerUser.Id,
            OwnerName = ownerUser.FullName,
            AssignedJockeyId = assignedJockey?.Id,
            AssignedJockeyName = assignedJockey?.User?.FullName,
            JockeyAssignmentStatus = activeJockeyInvitation?.Status.ToString() ??
                (raceAssignedJockey != null ? "Đã phân công cuộc đua" : null),
            AssignedJockeyIds = assignedJockeyIds
        };
    }
}
