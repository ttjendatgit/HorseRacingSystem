using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services;

namespace HorseRacing.Services.Interfaces;

public interface ITournamentService
{
    Task<ServiceResult<TournamentResponse>> CreateTournamentAsync(CreateTournamentRequest request);
    Task<ServiceResult<TournamentResponse>> GetTournamentAsync(Guid id);
    Task<ServiceResult<IEnumerable<TournamentResponse>>> GetAllTournamentsAsync();
    Task<ServiceResult<IEnumerable<TournamentResponse>>> GetActiveTournamentsAsync();
    Task<ServiceResult<TournamentResponse>> UpdateTournamentAsync(Guid id, UpdateTournamentRequest request);
    Task<ServiceResult<bool>> DeleteTournamentAsync(Guid id);

    // State machine methods
    Task<ServiceResult<TournamentResponse>> ChangeStatusAsync(Guid id, ChangeTournamentStatusRequest request, Guid actorId);
    Task<ServiceResult<TournamentStatsDto>> GetStatsAsync(Guid id);
    Task<ServiceResult<List<TournamentTimelineDto>>> GetTimelineAsync(Guid id);
}

public interface IRoundService
{
    Task<ServiceResult<RoundResponse>> CreateRoundAsync(CreateRoundRequest request);
    Task<ServiceResult<RoundResponse>> GetRoundAsync(Guid id);
    Task<ServiceResult<IEnumerable<RoundResponse>>> GetRoundsByTournamentAsync(Guid tournamentId);
    Task<ServiceResult<RoundResponse>> UpdateRoundAsync(Guid id, UpdateRoundRequest request);
    Task<ServiceResult<bool>> DeleteRoundAsync(Guid id);
}

public interface IRaceManagementService
{
    // Race Management
    Task<ServiceResult<RaceDetailResponse>> CreateRaceAsync(CreateRaceRequest request);
    Task<ServiceResult<RaceDetailResponse>> GetRaceDetailsAsync(Guid raceId);
    Task<ServiceResult<RaceDetailResponse>> UpdateRaceAsync(Guid raceId, UpdateRaceRequest request);
    Task<ServiceResult<IEnumerable<RaceDetailResponse>>> GetRacesByTournamentAsync(Guid tournamentId);
    Task<ServiceResult<IEnumerable<RaceDetailResponse>>> GetRacesByRoundAsync(Guid roundId);
    Task<ServiceResult<bool>> DeleteRaceAsync(Guid raceId);

    // Horse Assignment to Race
    Task<ServiceResult<bool>> AssignHorseToRaceAsync(Guid raceId, AssignHorseToRaceRequest request);
    Task<ServiceResult<bool>> BulkAssignHorsesToRaceAsync(Guid raceId, BulkAssignHorsesToRaceRequest request);
    Task<ServiceResult<bool>> RemoveHorseFromRaceAsync(Guid raceId, Guid horseId);
    Task<ServiceResult<List<Guid>>> GetBusyHorseIdsAsync();
    Task<ServiceResult<bool>> UpdateOddsAsync(Guid raceId, Guid horseId, decimal odds);

    // Race Status
    Task<ServiceResult<bool>> OpenRegistrationAsync(Guid raceId);
    Task<ServiceResult<bool>> CloseRegistrationAsync(Guid raceId);
    Task<ServiceResult<bool>> StartRaceAsync(Guid raceId);
    Task<ServiceResult<bool>> EndRaceAsync(Guid raceId);
    Task<ServiceResult<bool>> CancelRaceAsync(Guid raceId);

    // Horse Release
    Task<ServiceResult<bool>> ReleaseHorseAsync(Guid raceId, Guid horseId);

    // Q1: Qualification — generate Round N+1 RaceEntries from Round N's Official rankings
    Task<ServiceResult<GenerateNextRoundResultDto>> GenerateNextRoundEntriesAsync(Guid roundId);

    // GATE-V1: Referee-only starting gate assignment. Caller (RefereesController) is responsible
    // for identity + Confirmed-assignment-to-this-Race authorization before calling this — this
    // method validates only the Race/Entry/gate business rules themselves.
    Task<ServiceResult<bool>> AssignGateNumberAsync(Guid raceId, Guid entryId, int gateNumber);
}

public interface IRefereeService
{
    // Referee Management
    Task<ServiceResult<RefereeResponse>> CreateRefereeAsync(CreateRefereeRequest request);
    Task<ServiceResult<RefereeResponse>> GetRefereeAsync(Guid id);
    Task<ServiceResult<IEnumerable<RefereeResponse>>> GetAllRefereesAsync();
    Task<ServiceResult<IEnumerable<RefereeResponse>>> GetActiveRefereesAsync();
    Task<ServiceResult<RefereeResponse>> UpdateRefereeAsync(Guid id, UpdateRefereeRequest request);
    Task<ServiceResult<bool>> DeleteRefereeAsync(Guid id);

    // Referee Assignment
    Task<ServiceResult<RefereeAssignmentResponse>> AssignRefereeToRaceAsync(AssignRefereeRequest request);
    Task<ServiceResult<IEnumerable<RefereeAssignmentResponse>>> GetRaceAssignmentsAsync(Guid raceId);
    Task<ServiceResult<IEnumerable<RefereeAssignmentResponse>>> GetRefereeAssignmentsAsync(Guid refereeId);
    Task<ServiceResult<IEnumerable<RefereeAssignmentResponse>>> GetAllAssignmentsAsync();
    Task<ServiceResult<RefereeAssignmentResponse>> ConfirmAssignmentAsync(ConfirmRefereeAssignmentRequest request);
}

public interface IRefereeHealthCheckService
{
    // Health Checks
    Task<ServiceResult<HealthCheckResponse>> CreateHealthCheckAsync(CreateHealthCheckRequest request);
    Task<ServiceResult<HealthCheckResponse>> CompleteHealthCheckAsync(CompleteHealthCheckRequest request);
    Task<ServiceResult<HealthCheckResponse>> GetHealthCheckAsync(Guid id);
    Task<ServiceResult<IEnumerable<HealthCheckResponse>>> GetRaceHealthChecksAsync(Guid raceId);
    Task<ServiceResult<IEnumerable<HealthCheckResponse>>> GetHorseHealthCheckHistoryAsync(Guid horseId);
    Task<ServiceResult<bool>> ApproveHorseForRaceAsync(Guid healthCheckId);
    Task<ServiceResult<bool>> RejectHorseForRaceAsync(Guid healthCheckId, string reason);
}

public interface IViolationRecordService
{
    // Violation Records
    Task<ServiceResult<ViolationResponse>> RecordViolationAsync(CreateViolationRequest request, Guid refereeUserId);
    Task<ServiceResult<ViolationResponse>> GetViolationAsync(Guid id);
    Task<ServiceResult<IEnumerable<ViolationResponse>>> GetRaceViolationsAsync(Guid raceId);
    Task<ServiceResult<IEnumerable<ViolationResponse>>> GetHorseViolationsAsync(Guid horseId);
    Task<ServiceResult<bool>> DeleteViolationAsync(Guid id, Guid refereeUserId);
}

public interface IRaceReportService
{
    // Race Reports
    Task<ServiceResult<RaceReportResponse>> CreateReportAsync(CreateRaceReportRequest request);
    Task<ServiceResult<RaceReportResponse>> GetReportAsync(Guid id);
    Task<ServiceResult<RaceReportResponse>> GetRaceReportAsync(Guid raceId);
    Task<ServiceResult<IEnumerable<RaceReportResponse>>> GetRefereeReportsAsync(Guid refereeId);
    Task<ServiceResult<RaceReportResponse>> UpdateReportAsync(Guid id, CreateRaceReportRequest request);
    Task<ServiceResult<bool>> PublishReportAsync(Guid id);
}

public interface IAdminService
{
    // User Management
    Task<ServiceResult<AdminDashboardResponse>> GetDashboardAsync();
    Task<ServiceResult<IEnumerable<UserManagementResponse>>> GetAllUsersAsync();
    Task<ServiceResult<UserManagementResponse>> GetUserAsync(Guid userId);
    Task<ServiceResult<bool>> DeactivateUserAsync(Guid userId);
    Task<ServiceResult<bool>> ReactivateUserAsync(Guid userId);

    // User Registration Approval
    Task<ServiceResult<UserRegistrationResponse>> GetRegistrationAsync(Guid id);
    Task<ServiceResult<IEnumerable<UserRegistrationResponse>>> GetPendingRegistrationsAsync();
    Task<ServiceResult<IEnumerable<UserRegistrationResponse>>> GetAllRegistrationsAsync();
    Task<ServiceResult<bool>> ApproveRegistrationAsync(ApproveRegistrationRequest request);
    Task<ServiceResult<bool>> RejectRegistrationAsync(RejectRegistrationRequest request);

    // Horse Management
    Task<ServiceResult<IEnumerable<AdminHorseResponse>>> GetOwnerHorsesAsync(Guid userId);
    Task<ServiceResult<AdminHorseResponse>> GetOwnerHorseAsync(Guid userId, Guid horseId);
    Task<ServiceResult<AdminHorseResponse>> UpdateOwnerHorseStatusAsync(
        Guid userId,
        Guid horseId,
        UpdateHorseApprovalStatusRequest request);

    // Jockey Management
    Task<ServiceResult<bool>> ApproveJockeyAsync(Guid jockeyId);
    Task<ServiceResult<bool>> RejectJockeyAsync(Guid jockeyId, string? reason);
    Task<ServiceResult<JockeyAdminDetailResponse>> GetJockeyDetailAsync(Guid jockeyId);

    // Operations
    Task<ServiceResult<RefereeAssignmentResponse>> AssignRefereeToRaceAsync(AssignRefereeRequest request);
    Task<ServiceResult<bool>> ApproveRaceResultAsync(Guid raceId);
    Task<ServiceResult<bool>> RejectRaceResultAsync(Guid raceId, string reason);
    Task<ServiceResult<object>> GetPredictionsAsync();
    Task<ServiceResult<object>> SettlePredictionsAsync(Guid raceId);
    Task<ServiceResult<bool>> ResolveViolationAsync(Guid violationId, string penaltyType, int? penaltyTimeSeconds = null);
}

public interface ILiveResultService
{
    // Live Race Results
    Task<ServiceResult<LiveRaceResultResponse>> GetLiveRaceResultAsync(Guid raceId);
    Task<ServiceResult<RaceRankingResponse>> GetRaceRankingAsync(Guid raceId);
    Task<ServiceResult<IEnumerable<CurrentPositionData>>> GetCurrentPositionsAsync(Guid raceId);

    // Update Results
    Task<ServiceResult<bool>> UpdateRaceResultAsync(Guid raceId, SubmitRaceResultRequest request);
    Task<ServiceResult<bool>> UpdateParticipantStatusAsync(Guid raceId, Guid horseId, string status);
}

public class RaceResultRequest
{
    /// <summary>
    /// R0: optional compatibility field only. The ranking is the single
    /// source of truth for the winner — if supplied, this must equal
    /// Rankings[Position == 1].HorseId or the request is rejected (400).
    /// Never treated as an independently-editable second winner source.
    /// </summary>
    public Guid? WinningHorseId { get; set; }
    public List<RaceResultRankingItemRequest>? Rankings { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// One entry of a submitted finishing order. R0 deliberately carries only
/// HorseId + Position — no time/margin/score; see RaceResult.RankingsJson.
/// </summary>
public class RaceResultRankingItemRequest
{
    public Guid HorseId { get; set; }
    public int Position { get; set; }
}
