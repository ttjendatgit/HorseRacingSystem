using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// START-TOURNAMENT-HORSE-READINESS-V1: Published -> Ongoing must be blocked when the Tournament
/// has zero Approved TournamentHorseRegistration rows. Deliberately NOT ApprovedCount ==
/// MaxParticipants, NOT a new MinParticipants field, NOT a StartDate/UtcNow gate — only "at least
/// one Approved registration exists". Reuses the direct-DbContext-seeding pattern already
/// established in TournamentRegistrationWithdrawTests.cs and the real TournamentAndRoundService
/// (via RaceLifecycleTests.LifecycleFixture) rather than mocks.
/// </summary>
public class StartTournamentHorseReadinessTests
{
    private static async Task<(Guid ownerId, Guid userId, Guid horseId)> CreateApprovedOwnerHorseAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = $"owner-{tag}@test.com", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = userId, OwnerCode = $"OWN-{tag}" };
        var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse-{tag}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.AddRange(user, owner, horse);
        await f.Db.SaveChangesAsync();
        return (owner.Id, userId, horse.Id);
    }

    private static async Task<Guid> CreateTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, TournamentStatus status, DateTime start, int? maxParticipants = 10)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = $"T-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(5),
            RegistrationDeadline = start.AddDays(-1), MaxParticipants = maxParticipants, MinParticipants = 3,
            Status = status, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(tournament);
        await f.Db.SaveChangesAsync();
        return tournament.Id;
    }

    private static async Task<Guid> RegisterAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid horseId, Guid ownerId, RegistrationStatus status)
    {
        var registration = new TournamentHorseRegistration
        {
            Id = Guid.NewGuid(), TournamentId = tournamentId, HorseId = horseId, OwnerId = ownerId,
            Status = status, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(registration);
        await f.Db.SaveChangesAsync();
        return registration.Id;
    }

    private static Task<ServiceResult<TournamentResponse>> StartAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid? actorId = null) =>
        f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Ongoing }, actorId ?? Guid.NewGuid());

    // Q1-QUALIFICATION-SHORTFALL: Start now also requires ApprovedCount to meet either the
    // planned Final-round capacity or the configured Prize count — seed exactly 1 Prize
    // (Position=1) so the single-Approved-horse scenarios in this file satisfy the
    // Prize-count path. Start also now requires at least one Confirmed RefereeAssignment
    // somewhere in the Tournament — this file's CreateTournamentAsync seeds no Round/Race at
    // all, so seed a minimal Track/Round/Race/Referee/Confirmed-assignment for that gate too.
    // Both are prerequisites for Start to succeed, unrelated to what each test actually
    // asserts (pending-registration handling / registration-record immutability / etc).
    private static async Task SeedStartReadinessAsync(RaceLifecycleTests.LifecycleFixture f, Guid tournamentId)
    {
        var tournament = await f.Db.Tournaments.AsNoTracking().FirstAsync(t => t.Id == tournamentId);
        var prize = new Prize { Id = Guid.NewGuid(), TournamentId = tournamentId, Name = "Champion", Amount = 100, Position = 1, CreatedAt = DateTime.UtcNow };

        var track = new Track { Id = Guid.NewGuid(), Name = $"Track-{Guid.NewGuid():N}", Capacity = 10, CreatedAt = DateTime.UtcNow };
        var round = new Round
        {
            Id = Guid.NewGuid(), Name = "Final", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = tournament.StartDate, ScheduledEndDate = tournament.EndDate, AdvanceCount = 0
        };
        var race = new Race
        {
            Id = Guid.NewGuid(), Name = "Race", TournamentId = tournamentId, RoundId = round.Id, TrackId = track.Id,
            ScheduledAt = tournament.StartDate, ScheduledEndAt = tournament.StartDate.AddHours(1),
            MaxParticipants = 10, Status = RaceStatus.Scheduled, CreatedAt = DateTime.UtcNow
        };

        var refereeUserId = Guid.NewGuid();
        var refereeUser = new User { Id = refereeUserId, Email = $"referee-{Guid.NewGuid():N}@test.com", PasswordHash = "x", FullName = "Referee", Role = UserRole.Referee };
        var referee = new Referee { Id = Guid.NewGuid(), UserId = refereeUserId, LicenseNumber = $"LIC-{Guid.NewGuid():N}", IsActive = true };
        var assignment = new RefereeAssignment
        {
            Id = Guid.NewGuid(), RaceId = race.Id, RefereeId = referee.Id, Role = "Chief Referee",
            Status = RefereeAssignmentStatus.Confirmed, AssignedAt = DateTime.UtcNow, ConfirmedAt = DateTime.UtcNow
        };

        f.Db.AddRange(prize, track, round, race, refereeUser, referee, assignment);
        await f.Db.SaveChangesAsync();
    }

    private const string ExpectedNoApprovedHorseMessage =
        "Giải đấu phải có ít nhất một ngựa được duyệt tham gia trước khi bắt đầu.";

    private const string ExpectedPendingUnresolvedMessage =
        "Vui lòng xử lý tất cả đăng ký đang chờ duyệt trước khi bắt đầu giải đấu.";

    // ── 1. Zero registrations at all ──────────────────────────────────────
    [Fact]
    public async Task Start_PublishedWithZeroRegistrations_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));

        var start = await StartAsync(f, tournamentId);

        Assert.False(start.Result.Success);
        Assert.Equal(400, start.StatusCode);
        Assert.Equal(ExpectedNoApprovedHorseMessage, start.Result.Message);

        var reloaded = await f.Db.Tournaments.AsNoTracking().FirstAsync(t => t.Id == tournamentId);
        Assert.Equal(TournamentStatus.Published, reloaded.Status);
        Assert.Null(reloaded.StartedAt);
    }

    // ── 2. Pending-only registrations ─────────────────────────────────────
    [Fact]
    public async Task Start_PublishedWithOnlyPendingRegistrations_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var start = await StartAsync(f, tournamentId);

        Assert.False(start.Result.Success);
        Assert.Equal(400, start.StatusCode);
        Assert.Equal(ExpectedNoApprovedHorseMessage, start.Result.Message);
    }

    // ── 3. Rejected/Withdrawn-only registrations ──────────────────────────
    [Fact]
    public async Task Start_PublishedWithOnlyRejectedOrWithdrawnRegistrations_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerA, _, horseA) = await CreateApprovedOwnerHorseAsync(f, "a");
        var (ownerB, _, horseB) = await CreateApprovedOwnerHorseAsync(f, "b");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        await RegisterAsync(f, tournamentId, horseA, ownerA, RegistrationStatus.Rejected);
        await RegisterAsync(f, tournamentId, horseB, ownerB, RegistrationStatus.Withdrawn);

        var start = await StartAsync(f, tournamentId);

        Assert.False(start.Result.Success);
        Assert.Equal(400, start.StatusCode);
        Assert.Equal(ExpectedNoApprovedHorseMessage, start.Result.Message);
    }

    // ── 4. At least one Approved registration ─────────────────────────────
    [Fact]
    public async Task Start_PublishedWithAtLeastOneApprovedRegistration_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);
        await SeedStartReadinessAsync(f, tournamentId);

        var start = await StartAsync(f, tournamentId);

        Assert.True(start.Result.Success, start.Result.Message);
        Assert.Equal(TournamentStatus.Ongoing, start.Result.Data!.Status);
        Assert.True(start.Result.Data.IsActive);
        Assert.NotNull(start.Result.Data.StartedAt);
    }

    // ── 6. Never requires ApprovedCount == MaxParticipants ────────────────
    [Fact]
    public async Task Start_ApprovedCountBelowMaxParticipants_StillSucceeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        // MaxParticipants=10, only 1 Approved — must not be treated as "not full enough".
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10), maxParticipants: 10);
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);
        await SeedStartReadinessAsync(f, tournamentId);

        var start = await StartAsync(f, tournamentId);

        Assert.True(start.Result.Success, start.Result.Message);
        Assert.Equal(TournamentStatus.Ongoing, start.Result.Data!.Status);
    }

    // ── 7. Existing invalid lifecycle transitions remain rejected ────────
    [Theory]
    [InlineData(TournamentStatus.Draft, TournamentStatus.Ongoing)]
    [InlineData(TournamentStatus.Finished, TournamentStatus.Ongoing)]
    [InlineData(TournamentStatus.Cancelled, TournamentStatus.Ongoing)]
    public async Task Start_FromNonPublishedStatus_StillRejected_RegardlessOfApprovedRegistrations(
        TournamentStatus from, TournamentStatus to)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, from, DateTime.UtcNow.AddDays(10));
        // Even WITH an Approved registration present, an out-of-whitelist transition must still
        // fail on the transition check itself, before this task's readiness check ever runs.
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = to }, Guid.NewGuid());

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotEqual(ExpectedNoApprovedHorseMessage, result.Result.Message);
    }

    // ── 22 (regression). Starting the Tournament does not mutate registration records ──
    [Fact]
    public async Task Start_Success_DoesNotChangeRegistrationRecords()
    {
        // TOURNAMENT-REGISTRATION-LOCK-AT-START-V1: Start now also requires zero Pending
        // registrations, so this "does Start mutate rows" check uses an Approved + a Rejected
        // registration (no Pending) — the Pending-blocks-Start behavior has its own dedicated
        // test below (Start_ApprovedWithRemainingPending_Rejected).
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (approvedOwner, _, approvedHorse) = await CreateApprovedOwnerHorseAsync(f, "a");
        var (rejectedOwner, _, rejectedHorse) = await CreateApprovedOwnerHorseAsync(f, "b");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var approvedRegId = await RegisterAsync(f, tournamentId, approvedHorse, approvedOwner, RegistrationStatus.Approved);
        var rejectedRegId = await RegisterAsync(f, tournamentId, rejectedHorse, rejectedOwner, RegistrationStatus.Rejected);
        await SeedStartReadinessAsync(f, tournamentId);

        var approvedBefore = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == approvedRegId);
        var rejectedBefore = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == rejectedRegId);

        var start = await StartAsync(f, tournamentId);
        Assert.True(start.Result.Success, start.Result.Message);

        var approvedAfter = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == approvedRegId);
        var rejectedAfter = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == rejectedRegId);

        Assert.Equal(approvedBefore.Status, approvedAfter.Status);
        Assert.Equal(approvedBefore.ApprovedAt, approvedAfter.ApprovedAt);
        Assert.Equal(rejectedBefore.Status, rejectedAfter.Status);
        Assert.Equal(rejectedBefore.CreatedAt, rejectedAfter.CreatedAt);
    }

    // ── 5. Approved>=1 + Pending>0 -> Start rejected ──────────────────────
    [Fact]
    public async Task Start_ApprovedWithRemainingPending_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (approvedOwner, _, approvedHorse) = await CreateApprovedOwnerHorseAsync(f, "a");
        var (pendingOwner, _, pendingHorse) = await CreateApprovedOwnerHorseAsync(f, "b");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var approvedRegId = await RegisterAsync(f, tournamentId, approvedHorse, approvedOwner, RegistrationStatus.Approved);
        var pendingRegId = await RegisterAsync(f, tournamentId, pendingHorse, pendingOwner, RegistrationStatus.Pending);
        await SeedStartReadinessAsync(f, tournamentId);

        var start = await StartAsync(f, tournamentId);

        Assert.False(start.Result.Success);
        Assert.Equal(400, start.StatusCode);
        Assert.Equal(ExpectedPendingUnresolvedMessage, start.Result.Message);

        // Start must NOT auto-reject the Pending row — Admin must resolve it explicitly.
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == pendingRegId);
        Assert.Equal(RegistrationStatus.Pending, reloaded.Status);
        var tournament = await f.Db.Tournaments.AsNoTracking().FirstAsync(t => t.Id == tournamentId);
        Assert.Equal(TournamentStatus.Published, tournament.Status);

        // Once the Pending row is resolved (Approved here), Start succeeds.
        var resolve = await f.Db.TournamentHorseRegistrations.FirstAsync(x => x.Id == pendingRegId);
        resolve.Status = RegistrationStatus.Approved;
        resolve.ApprovedAt = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();

        var startAfterResolution = await StartAsync(f, tournamentId);
        Assert.True(startAfterResolution.Result.Success, startAfterResolution.Result.Message);
    }

    // ── 20 (regression). New Owner registration against an Ongoing Tournament remains rejected ──
    [Fact]
    public async Task Register_AgainstOngoingTournament_RemainsRejectedByExistingRules()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (approvedOwner, _, approvedHorse) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        await RegisterAsync(f, tournamentId, approvedHorse, approvedOwner, RegistrationStatus.Approved);
        await SeedStartReadinessAsync(f, tournamentId);

        var start = await StartAsync(f, tournamentId);
        Assert.True(start.Result.Success, start.Result.Message);

        var (_, newUserId, newHorseId) = await CreateApprovedOwnerHorseAsync(f, "c");
        var controller = new TournamentRegistrationsController(f.Db, f.UnitOfWork);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, newUserId.ToString()) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var register = await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = newHorseId });
        var obj = Assert.IsAssignableFrom<ObjectResult>(register);
        Assert.Equal(400, obj.StatusCode);

        var registrationsForTournament = await f.Db.TournamentHorseRegistrations
            .Where(x => x.TournamentId == tournamentId).ToListAsync();
        Assert.Single(registrationsForTournament); // still only the original Approved one
    }
}
