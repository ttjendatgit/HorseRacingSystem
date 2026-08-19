using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Task C1 §2: Owner-only Withdraw on TournamentHorseRegistration. Pending -> Withdrawn always
/// allowed while the Tournament is Published; Approved -> Withdrawn only when no RaceEntry yet
/// exists for that Horse in that Tournament. Withdrawn never blocks re-registration. Reuses the
/// direct-controller-instantiation pattern from TournamentRegistrationTests.cs.
/// </summary>
public class TournamentRegistrationWithdrawTests
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

    private static async Task<Guid> CreateTournamentAsync(RaceLifecycleTests.LifecycleFixture f, TournamentStatus status, DateTime start)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = $"T-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(5),
            RegistrationDeadline = start.AddDays(-1), MaxParticipants = 10, MinParticipants = 3,
            Status = status, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(tournament);
        await f.Db.SaveChangesAsync();
        return tournament.Id;
    }

    private static async Task<Guid> RegisterAsync(RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid horseId, Guid ownerId, RegistrationStatus status)
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

    /// <summary>Tournament (Draft while the Race is created, per CreateRaceAsync's own gate, then flipped to Published) + Round + Race.</summary>
    private static async Task<(Guid tournamentId, Guid raceId)> CreatePublishedTournamentWithRaceAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start);
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

    [Fact]
    public async Task Withdraw_PendingRegistration_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Withdraw(regId));
        Assert.Equal(200, status);

        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Withdrawn, reloaded.Status);
    }

    [Fact]
    public async Task Withdraw_ApprovedNoRaceEntry_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Withdraw(regId));
        Assert.Equal(200, status);

        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Withdrawn, reloaded.Status);
    }

    [Fact]
    public async Task Withdraw_ApprovedWithRaceEntry_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreatePublishedTournamentWithRaceAsync(f);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Withdraw(regId));
        Assert.Equal(400, status);
        Assert.Contains("phân công", message ?? "");

        // Rejected withdraw must never mutate the registration row.
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Approved, reloaded.Status);
    }

    [Fact]
    public async Task Withdraw_AnotherOwnersRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var (_, otherUserId, _) = await CreateApprovedOwnerHorseAsync(f, "b");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, otherUserId);
        var (status, _) = Unwrap(await controller.Withdraw(regId));
        Assert.Equal(404, status);

        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Pending, reloaded.Status);
    }

    [Fact]
    public void Withdraw_DeclaredHorseOwnerOnly_JockeyForbidden()
    {
        // Same reflection-based declarative check as HorseOwnerAuthorizationTests — no
        // WebApplicationFactory/HTTP pipeline exists in this suite, so [Authorize(Roles=...)] is
        // verified against the method's own attribute metadata.
        var method = typeof(TournamentRegistrationsController).GetMethod(
            nameof(TournamentRegistrationsController.Withdraw), BindingFlags.Public | BindingFlags.Instance);
        var attr = method?.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().FirstOrDefault();
        Assert.NotNull(attr);
        var roles = (attr!.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.DoesNotContain("Jockey", roles);
        Assert.Equal(new[] { "HorseOwner" }, roles);
    }

    [Fact]
    public async Task Withdraw_ThenReregister_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, userId);
        var (withdrawStatus, _) = Unwrap(await controller.Withdraw(regId));
        Assert.Equal(200, withdrawStatus);

        var (registerStatus, registerMessage) = Unwrap(
            await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.True(registerStatus is 200 or 201, registerMessage ?? "expected success");
    }

    [Fact]
    public async Task Withdraw_TournamentNoLongerPublished_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Ongoing, DateTime.UtcNow.AddDays(-1));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Withdraw(regId));
        Assert.Equal(400, status);
        Assert.Contains("Đã công bố", message ?? "");

        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Approved, reloaded.Status);
    }
}
