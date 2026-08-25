using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// PRIZE-V1.1: Prize.Position must not exceed PlannedFinalParticipants — the Tournament's
/// STRUCTURAL planned Final-round capacity (single-round: Tournament.MaxParticipants; multi-round:
/// AdvanceCount of the pre-Final Round), never actual registrations/RaceEntry counts/Track/Race
/// capacity. Also covers Draft structural-mutation safety (lowering MaxParticipants/MaxRounds/
/// pre-Final AdvanceCount must not strand an existing Prize row) and the Draft/public Prize
/// visibility policy on ManagementController.GetPrizesByTournament. Wired against a real Sqlite
/// in-memory DB and the actual production services, reusing RaceLifecycleTests.LifecycleFixture
/// (same convention as PrizeV1Tests/GateAssignmentTests) — kept in its own file rather than
/// editing PrizeV1Tests.cs, to avoid any risk to the already-passing PRIZE-V1 suite.
/// </summary>
public class PrizeV1_1Tests
{
    private static PrizeService MakePrizeService(RaceLifecycleTests.LifecycleFixture f)
        => new PrizeService(new PrizeRepository(f.Db), f.TournamentRepo, f.UnitOfWork, f.Db);

    private static async Task PublishStatusOnlyAsync(RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, TournamentStatus status)
    {
        var t = await f.Db.Tournaments.FirstAsync(x => x.Id == tournamentId);
        t.Status = status;
        await f.Db.SaveChangesAsync();
    }

    private static async Task<Guid> BuildSingleRoundTournamentAsync(RaceLifecycleTests.LifecycleFixture f, int maxParticipants, decimal prizePool)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "PrizeV1.1-single-" + Guid.NewGuid().ToString("N")[..8],
            StartDate = start, EndDate = start.AddDays(10), RegistrationDeadline = start.AddDays(-1),
            MinParticipants = 3, MaxParticipants = maxParticipants, MaxRounds = 1, PrizePool = prizePool,
        });
        Assert.True(create.Result.Success, create.Result.Message);
        return create.Result.Data!.Id;
    }

    /// <summary>Two-Round Tournament (Round1 = pre-Final, Round2 = Final). PlannedFinalParticipants
    /// = preFinalAdvanceCount (Round1.AdvanceCount).</summary>
    private static async Task<(Guid tournamentId, Guid round1Id, Guid round2Id)> BuildMultiRoundTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, int tournamentMaxParticipants, int preFinalAdvanceCount, decimal prizePool)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "PrizeV1.1-multi-" + Guid.NewGuid().ToString("N")[..8],
            StartDate = start, EndDate = start.AddDays(20), RegistrationDeadline = start.AddDays(-1),
            MinParticipants = 3, MaxParticipants = tournamentMaxParticipants, MaxRounds = 2, PrizePool = prizePool,
        });
        Assert.True(create.Result.Success, create.Result.Message);
        var tournamentId = create.Result.Data!.Id;

        var r1 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(5), AdvanceCount = preFinalAdvanceCount
        });
        Assert.True(r1.Result.Success, r1.Result.Message);
        var r2 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Final", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(5), ScheduledEndDate = start.AddDays(10), AdvanceCount = 0
        });
        Assert.True(r2.Result.Success, r2.Result.Message);

        return (tournamentId, r1.Result.Data!.Id, r2.Result.Data!.Id);
    }

    // ── SINGLE-ROUND ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePrize_PositionWithinTournamentMax_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await BuildSingleRoundTournamentAsync(f, maxParticipants: 3, prizePool: 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 50 });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task CreatePrize_PositionEqualsTournamentMax_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await BuildSingleRoundTournamentAsync(f, maxParticipants: 3, prizePool: 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 3, PercentageOfPool = 50 });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task CreatePrize_PositionAboveTournamentMax_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await BuildSingleRoundTournamentAsync(f, maxParticipants: 3, prizePool: 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 4, PercentageOfPool = 50 });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Vòng chung kết", result.Result.Message);
    }

    // ── MULTI-ROUND ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePrize_PositionWithinFinalPlannedParticipants_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var (tid, _, _) = await BuildMultiRoundTournamentAsync(f, tournamentMaxParticipants: 16, preFinalAdvanceCount: 4, prizePool: 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 50 });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task CreatePrize_PositionEqualsPreFinalAdvanceCount_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var (tid, _, _) = await BuildMultiRoundTournamentAsync(f, tournamentMaxParticipants: 16, preFinalAdvanceCount: 4, prizePool: 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 4, PercentageOfPool = 50 });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task CreatePrize_PositionAbovePreFinalAdvanceCount_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var (tid, _, _) = await BuildMultiRoundTournamentAsync(f, tournamentMaxParticipants: 16, preFinalAdvanceCount: 4, prizePool: 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 5, PercentageOfPool = 50 });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Vòng chung kết", result.Result.Message);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePrize_PositionAboveFinalLimit_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await BuildSingleRoundTournamentAsync(f, maxParticipants: 3, prizePool: 1000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);

        var update = await svc.UpdateAsync(create.Result.Data!.Id, new UpdatePrizeRequest { Position = 4, PercentageOfPool = 50 });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
        Assert.Contains("Vòng chung kết", update.Result.Message);
    }

    // ── PUBLISH LEGACY DEFENSE ───────────────────────────────────────────

    [Fact]
    public async Task Publish_LegacyPrizeAboveFinalRankLimit_Rejected()
    {
        // Seeds a legacy Prize row directly (Position=4 > Tournament.MaxParticipants=3), bypassing
        // PrizeService.CreateAsync which would itself reject it — proves Publish's own defensive
        // re-check catches it, matching "legacy DB rows may exist" (Part 3).
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tid, roundId, roundStart, roundEnd) = await Phase5StructuralTestsHelper.BuildDraftSingleFinalRoundAsync(f, prizePool: 1000, maxParticipants: 3);
        var track = await Phase5StructuralTestsHelper.CreateTrackAsync(f, capacity: 5);
        await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tid, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 3, QualificationSlots = 0
        });
        f.Db.Prizes.Add(new Prize { Id = Guid.NewGuid(), TournamentId = tid, Name = "Legacy", Amount = 1000, Position = 4, CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();

        var publish = await f.TournamentSvc.ChangeStatusAsync(tid, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());
        Assert.False(publish.Result.Success);
        Assert.Equal(400, publish.StatusCode);
        Assert.Contains("vượt quá số người có thể tham gia Vòng chung kết", publish.Result.Message);
    }

    // ── DRAFT STRUCTURAL MUTATION SAFETY ─────────────────────────────────

    [Fact]
    public async Task ReduceSingleRoundMaxParticipants_BelowExistingPrizeRank_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await BuildSingleRoundTournamentAsync(f, maxParticipants: 4, prizePool: 1000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 4, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);

        var update = await f.TournamentSvc.UpdateTournamentAsync(tid, new UpdateTournamentRequest { MaxParticipants = 3 });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task ReducePreFinalAdvanceCount_BelowExistingPrizeRank_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var (tid, round1Id, _) = await BuildMultiRoundTournamentAsync(f, tournamentMaxParticipants: 10, preFinalAdvanceCount: 4, prizePool: 1000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 4, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);

        var update = await f.RoundSvc.UpdateRoundAsync(round1Id, new UpdateRoundRequest { AdvanceCount = 3 });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task StructuralChange_StillSupportsExistingPrizeRanks_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await BuildSingleRoundTournamentAsync(f, maxParticipants: 4, prizePool: 1000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 3, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);

        // Lowering MaxParticipants 4 -> 3 still supports the existing Position=3 row.
        var update = await f.TournamentSvc.UpdateTournamentAsync(tid, new UpdateTournamentRequest { MaxParticipants = 3 });
        Assert.True(update.Result.Success, update.Result.Message);
    }

    // ── VISIBILITY ───────────────────────────────────────────────────────

    private sealed class ThrowingProtestService : IProtestService
    {
        public Task<ServiceResult<ProtestResponse>> FileAsync(CreateProtestRequest request, Guid filedByUserId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<ProtestResponse>>> GetPendingAsync() => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<ProtestResponse>>> GetAllAsync() => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<ProtestResponse>>> GetByFiledByUserAsync(Guid filedByUserId) => throw new NotSupportedException();
        public Task<ServiceResult<ProtestResponse>> MarkUnderReviewAsync(Guid id, Guid reviewedByUserId) => throw new NotSupportedException();
        public Task<ServiceResult<ProtestResponse>> RuleAsync(Guid id, RuleProtestRequest request, Guid ruledByUserId) => throw new NotSupportedException();
        public Task<ServiceResult<ProtestResponse>> WithdrawAsync(Guid id, Guid requestingUserId) => throw new NotSupportedException();
    }

    private sealed class ThrowingRaceComplaintService : IRaceComplaintService
    {
        public Task<ServiceResult<RaceComplaintResponse>> FileAsync(CreateRaceComplaintRequest request, Guid filedByUserId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<RaceComplaintResponse>>> GetAllAsync(RaceComplaintStatus? status) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<RaceComplaintResponse>>> GetByFiledByUserAsync(Guid filedByUserId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<RaceComplaintResponse>>> GetForRefereeAsync(Guid refereeUserId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<RaceComplaintEligibleRaceResponse>>> GetEligibleRacesAsync(Guid userId) => throw new NotSupportedException();
        public Task<ServiceResult<RaceComplaintResponse>> RouteAsync(Guid id, RouteRaceComplaintRequest request, Guid adminUserId) => throw new NotSupportedException();
        public Task<ServiceResult<RaceComplaintResponse>> RespondAsync(Guid id, RespondRaceComplaintRequest request, Guid refereeUserId) => throw new NotSupportedException();
        public Task<ServiceResult<RaceComplaintResponse>> RuleAsync(Guid id, RuleRaceComplaintRequest request, Guid ruledByUserId) => throw new NotSupportedException();
        public Task<ServiceResult<RaceComplaintResponse>> WithdrawAsync(Guid id, Guid requestingUserId) => throw new NotSupportedException();
    }

    private sealed class ThrowingTransferService : IHorseTransferService
    {
        public Task<ServiceResult<HorseTransferResponse>> CreateAsync(CreateHorseTransferRequest request, Guid fromOwnerId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<HorseTransferResponse>>> GetPendingAsync() => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<HorseTransferResponse>>> GetAllAsync() => throw new NotSupportedException();
        public Task<ServiceResult<HorseTransferResponse>> ApproveAsync(Guid id, ApproveHorseTransferRequest request, Guid approvedByUserId) => throw new NotSupportedException();
        public Task<ServiceResult<HorseTransferResponse>> RejectAsync(Guid id, string reason, Guid approvedByUserId) => throw new NotSupportedException();
    }

    private sealed class ThrowingContractService : IContractService
    {
        public Task<ServiceResult<ContractResponse>> CreateAsync(CreateContractRequest request) => throw new NotSupportedException();
        public Task<ServiceResult<ContractResponse>> SignByOwnerAsync(Guid id, Guid ownerId) => throw new NotSupportedException();
        public Task<ServiceResult<ContractResponse>> SignByJockeyAsync(Guid id, Guid jockeyId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<ContractResponse>>> GetByOwnerAsync(Guid ownerId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<ContractResponse>>> GetByJockeyAsync(Guid jockeyId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<ContractResponse>>> GetAllAsync() => throw new NotSupportedException();
    }

    private sealed class ThrowingInjuryService : IInjuryRecordService
    {
        public Task<ServiceResult<InjuryRecordResponse>> CreateAsync(CreateInjuryRecordRequest request, Guid reportedByUserId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<InjuryRecordResponse>>> GetByHorseAsync(Guid horseId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.IEnumerable<InjuryRecordResponse>>> GetAllAsync() => throw new NotSupportedException();
        public Task<ServiceResult<InjuryRecordResponse>> MarkRecoveredAsync(Guid id) => throw new NotSupportedException();
        public Task<ServiceResult<InjuryRecordResponse>> ClearToRaceAsync(Guid id) => throw new NotSupportedException();
    }

    private static ManagementController BuildManagementController(RaceLifecycleTests.LifecycleFixture f, string? role)
    {
        var controller = new ManagementController(
            MakePrizeService(f), new ThrowingProtestService(), new ThrowingRaceComplaintService(), new ThrowingTransferService(),
            new ThrowingContractService(), new ThrowingInjuryService(), f.TournamentSvc);

        var httpContext = new DefaultHttpContext();
        if (role != null)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role),
            }, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static (int status, bool? success) Unwrap(ActionResult result)
    {
        if (result is ObjectResult obj)
        {
            var successProp = obj.Value?.GetType().GetProperty("Success");
            var success = successProp?.GetValue(obj.Value) as bool?;
            return (obj.StatusCode ?? 200, success);
        }
        throw new InvalidOperationException($"Unexpected result type {result.GetType()}");
    }

    [Fact]
    public async Task Anonymous_GetDraftPrizeBreakdown_RejectedOrHidden()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tid = await BuildSingleRoundTournamentAsync(f, maxParticipants: 5, prizePool: 1000); // stays Draft

        var controller = BuildManagementController(f, role: null);
        var (status, _) = Unwrap(await controller.GetPrizesByTournament(tid));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task AuthenticatedNonAdmin_GetDraftPrizeBreakdown_RejectedOrHidden()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tid = await BuildSingleRoundTournamentAsync(f, maxParticipants: 5, prizePool: 1000);

        var controller = BuildManagementController(f, role: "HorseOwner");
        var (status, _) = Unwrap(await controller.GetPrizesByTournament(tid));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Admin_GetDraftPrizeBreakdown_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await BuildSingleRoundTournamentAsync(f, maxParticipants: 5, prizePool: 1000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 100 });
        Assert.True(create.Result.Success, create.Result.Message);

        var controller = BuildManagementController(f, role: "Admin");
        var (status, success) = Unwrap(await controller.GetPrizesByTournament(tid));
        Assert.Equal(200, status);
        Assert.True(success == true);
    }

    [Fact]
    public async Task Anonymous_GetPublishedPrizeBreakdown_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await BuildSingleRoundTournamentAsync(f, maxParticipants: 5, prizePool: 1000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 100 });
        Assert.True(create.Result.Success, create.Result.Message);
        await PublishStatusOnlyAsync(f, tid, TournamentStatus.Published);

        var controller = BuildManagementController(f, role: null);
        var (status, success) = Unwrap(await controller.GetPrizesByTournament(tid));
        Assert.Equal(200, status);
        Assert.True(success == true);
    }
}
