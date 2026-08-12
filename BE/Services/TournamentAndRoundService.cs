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
                CreatedAt = DateTime.UtcNow
            };

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

            if (!string.IsNullOrEmpty(request.Name))
                tournament.Name = request.Name;
            if (request.Description != null)
                tournament.Description = request.Description;
            if (request.StartDate.HasValue)
                tournament.StartDate = request.StartDate.Value;
            if (request.EndDate.HasValue)
                tournament.EndDate = request.EndDate.Value;
            if (request.RegistrationDeadline.HasValue)
                tournament.RegistrationDeadline = request.RegistrationDeadline.Value;
            if (request.IsActive.HasValue)
                tournament.IsActive = request.IsActive.Value;
            if (request.ImageUrl != null)
                tournament.ImageUrl = request.ImageUrl;

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

    public async Task<ServiceResult<TournamentResponse>> ChangeStatusAsync(Guid id, ChangeTournamentStatusRequest request)
    {
        try
        {
            var tournament = await _tournamentRepo.GetByIdAsync(id);
            if (tournament == null)
                return ServiceResult<TournamentResponse>.Fail(404, "Không tìm thấy giải đấu");

            var currentStatus = tournament.Status;
            var newStatus = request.NewStatus;

            // Validate state transition
            var isValidTransition = IsValidStatusTransition(currentStatus, newStatus);
            if (!isValidTransition)
                return ServiceResult<TournamentResponse>.Fail(400,
                    $"Không thể chuyển từ {currentStatus} sang {newStatus}");

            // Update status and timestamp
            tournament.Status = newStatus;
            tournament.UpdatedAt = DateTime.UtcNow;

            switch (newStatus)
            {
                case TournamentStatus.Published:
                    tournament.PublishedAt = DateTime.UtcNow;
                    break;
                case TournamentStatus.Ongoing:
                    tournament.StartedAt = DateTime.UtcNow;
                    break;
                case TournamentStatus.Finished:
                    tournament.FinishedAt = DateTime.UtcNow;
                    break;
                case TournamentStatus.Cancelled:
                    tournament.CancelledAt = DateTime.UtcNow;
                    break;
            }

            // Update IsActive based on status
            tournament.IsActive = newStatus == TournamentStatus.Ongoing || newStatus == TournamentStatus.Published;

            await _tournamentRepo.UpdateAsync(tournament);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<TournamentResponse>.Ok(await MapToResponseAsync(tournament));
        }
        catch (Exception ex)
        {
            return ServiceResult<TournamentResponse>.Fail(500, $"Lỗi thay đổi trạng thái: {ex.Message}");
        }
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
            CancelledAt = tournament.CancelledAt
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
                Description = request.Description
            };

            await _roundRepo.AddAsync(round);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<RoundResponse>(201, ApiResult<RoundResponse>.Ok(MapToResponse(round)));
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

            return ServiceResult<RoundResponse>.Ok(MapToResponse(round));
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
            return ServiceResult<IEnumerable<RoundResponse>>.Ok(
                rounds.Select(MapToResponse));
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

            await _roundRepo.UpdateAsync(round);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult<RoundResponse>.Ok(MapToResponse(round));
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

    private RoundResponse MapToResponse(Round round)
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
            RaceCount = round.Races?.Count ?? 0
        };
    }
}
