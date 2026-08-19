using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Tournament capacity registration gate: once Approved TournamentHorseRegistration count reaches
/// Tournament.MaxParticipants, no NEW submission is accepted (TournamentRegistrationsController.
/// Register), while existing Pending registrations created before capacity filled are left
/// untouched — no auto-reject, no waiting-list promotion. The Admin Approve capacity re-check and
/// the Withdraw rule (Approved may withdraw only while Published and no RaceEntry exists) are both
/// pre-existing and unchanged; these tests only prove capacity's own effect around them.
/// </summary>
public class TournamentCapacityTests
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
        RaceLifecycleTests.LifecycleFixture f, TournamentStatus status, DateTime start, int? maxParticipants)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = $"T-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(5),
            RegistrationDeadline = start.AddDays(-1), MaxParticipants = maxParticipants, MinParticipants = 1,
            Status = status, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(tournament);
        await f.Db.SaveChangesAsync();
        return tournament.Id;
    }

    private static async Task<Guid> DirectRegisterAsync(
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

    private static async Task<Guid> CreateTrackAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var track = new Track { Id = Guid.NewGuid(), Name = $"Track-{Guid.NewGuid():N}", Capacity = 10, CreatedAt = DateTime.UtcNow };
        f.Db.Add(track);
        await f.Db.SaveChangesAsync();
        return track.Id;
    }

    /// <summary>Tournament (Draft while the Race is created, then flipped to Published) + Round + Race, for the RaceEntry-blocks-withdraw scenario.</summary>
    private static async Task<(Guid tournamentId, Guid raceId)> CreatePublishedTournamentWithRaceAsync(
        RaceLifecycleTests.LifecycleFixture f, int? maxParticipants)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, maxParticipants);
        var round = new Round { Id = Guid.NewGuid(), Name = "R1", TournamentId = tournamentId, RoundNumber = 1, ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1) };
        f.Db.Add(round);
        await f.Db.SaveChangesAsync();
        var track = await CreateTrackAsync(f);
        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = round.Id,
            ScheduledAt = start, ScheduledEndAt = start.AddHours(1), TrackId = track, MaxParticipants = 10
        });
        Assert.True(race.Result.Success, race.Result.Message);

        var tournament = await f.Db.Tournaments.FirstAsync(t => t.Id == tournamentId);
        tournament.Status = TournamentStatus.Published;
        await f.Db.SaveChangesAsync();

        return (tournamentId, race.Result.Data!.Id);
    }

    private static TournamentRegistrationsController BuildController(RaceLifecycleTests.LifecycleFixture f, Guid userId)
    {
        var controller = new TournamentRegistrationsController(f.Db, f.UnitOfWork);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static (int status, string? message) Unwrap(ActionResult result)
    {
        if (result is ObjectResult obj)
            return (obj.StatusCode ?? 200, obj.Value?.GetType().GetProperty("message")?.GetValue(obj.Value) as string);
        throw new InvalidOperationException($"Unexpected result type {result.GetType()}");
    }

    /// <summary>Seeds `approvedCount` distinct Approved registrations (each its own Owner/Horse) for the given Tournament.</summary>
    private static async Task SeedApprovedRegistrationsAsync(RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, int approvedCount, string tagPrefix)
    {
        for (var i = 0; i < approvedCount; i++)
        {
            var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, $"{tagPrefix}{i}");
            await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);
        }
    }

    [Fact]
    public async Task Register_ApprovedCountBelowMax_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10), maxParticipants: 4);
        await SeedApprovedRegistrationsAsync(f, tournamentId, 3, "below");
        var (_, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "below-new");

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Register_ApprovedCountAtMax_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10), maxParticipants: 4);
        await SeedApprovedRegistrationsAsync(f, tournamentId, 4, "atmax");
        var (_, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "atmax-new");

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.Equal(400, status);
        Assert.Contains("đủ số lượng ngựa tham gia", message ?? "");
    }

    [Fact]
    public async Task Register_ApprovedCountAtMax_CreatesNoRegistrationRow()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10), maxParticipants: 4);
        await SeedApprovedRegistrationsAsync(f, tournamentId, 4, "norow");
        var (_, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "norow-new");

        var controller = BuildController(f, userId);
        await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId });

        Assert.False(await f.Db.TournamentHorseRegistrations.AnyAsync(r => r.HorseId == horseId));
    }

    [Fact]
    public async Task ApprovingIntoFullCapacity_LeavesOtherPendingUntouched()
    {
        // MaxParticipants=4, Approved=3, PendingA, PendingB — Admin approves A -> 4/4.
        // B must remain Pending: never auto-rejected, never silently promoted.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10), maxParticipants: 4);
        await SeedApprovedRegistrationsAsync(f, tournamentId, 3, "fill");
        var (ownerAId, _, horseAId) = await CreateApprovedOwnerHorseAsync(f, "pendingA");
        var (ownerBId, _, horseBId) = await CreateApprovedOwnerHorseAsync(f, "pendingB");
        var regAId = await DirectRegisterAsync(f, tournamentId, horseAId, ownerAId, RegistrationStatus.Pending);
        var regBId = await DirectRegisterAsync(f, tournamentId, horseBId, ownerBId, RegistrationStatus.Pending);

        var controller = BuildController(f, Guid.NewGuid());
        var (approveStatus, approveMessage) = Unwrap(await controller.Approve(regAId));
        Assert.Equal(200, approveStatus);

        var approvedCount = await f.Db.TournamentHorseRegistrations.CountAsync(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.Approved);
        Assert.Equal(4, approvedCount);

        var regB = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(r => r.Id == regBId);
        Assert.Equal(RegistrationStatus.Pending, regB.Status);
    }

    [Fact]
    public async Task Approve_FullTournament_CannotApproveAnotherPending()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10), maxParticipants: 4);
        await SeedApprovedRegistrationsAsync(f, tournamentId, 4, "full");
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "full-pending");
        var pendingId = await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, message) = Unwrap(await controller.Approve(pendingId));
        Assert.Equal(400, status);
        Assert.Contains("đủ số lượng", message ?? "");

        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(r => r.Id == pendingId);
        Assert.Equal(RegistrationStatus.Pending, reloaded.Status);
    }

    [Fact]
    public async Task Withdraw_ApprovedAtFullCapacity_ReducesApprovedCount()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10), maxParticipants: 4);
        await SeedApprovedRegistrationsAsync(f, tournamentId, 3, "wd");
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "wd-fourth");
        var regId = await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var approvedBefore = await f.Db.TournamentHorseRegistrations.CountAsync(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.Approved);
        Assert.Equal(4, approvedBefore);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Withdraw(regId));
        Assert.Equal(200, status);

        var approvedAfter = await f.Db.TournamentHorseRegistrations.CountAsync(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.Approved);
        Assert.Equal(3, approvedAfter);
    }

    [Fact]
    public async Task Register_AfterLegalWithdrawalReopensCapacity_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10), maxParticipants: 4);
        await SeedApprovedRegistrationsAsync(f, tournamentId, 3, "reopen");
        var (ownerId, withdrawerUserId, withdrawerHorseId) = await CreateApprovedOwnerHorseAsync(f, "reopen-fourth");
        var regId = await DirectRegisterAsync(f, tournamentId, withdrawerHorseId, ownerId, RegistrationStatus.Approved);

        var withdrawController = BuildController(f, withdrawerUserId);
        var (withdrawStatus, _) = Unwrap(await withdrawController.Withdraw(regId));
        Assert.Equal(200, withdrawStatus);

        var (_, newUserId, newHorseId) = await CreateApprovedOwnerHorseAsync(f, "reopen-new");
        var registerController = BuildController(f, newUserId);
        var (registerStatus, registerMessage) = Unwrap(await registerController.Register(
            new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = newHorseId }));
        Assert.True(registerStatus is 200 or 201, registerMessage ?? "expected success");
    }

    [Fact]
    public async Task Withdraw_ApprovedWithRaceEntry_CannotReopenCapacity()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreatePublishedTournamentWithRaceAsync(f, maxParticipants: 4);
        await SeedApprovedRegistrationsAsync(f, tournamentId, 3, "entry");
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "entry-fourth");
        var regId = await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Withdraw(regId));
        Assert.Equal(400, status);
        Assert.Contains("phân công", message ?? "");

        var approvedCount = await f.Db.TournamentHorseRegistrations.CountAsync(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.Approved);
        Assert.Equal(4, approvedCount);
    }
}
