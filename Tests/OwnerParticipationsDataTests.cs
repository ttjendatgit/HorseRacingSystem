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

namespace Tests;

/// <summary>
/// Task C1 §3: "My Participations" is composed on the FE from two existing endpoints —
/// TournamentRegistrationsController.MyRegistrations (extended with tournamentStatus/dates) and
/// HorsesController.GetMyRaceEntries (extended with tournamentId/roundNumber/roundName). These
/// tests lock the data shape those endpoints must provide: every registration status stays
/// visible regardless of Tournament stage, and FinishPosition is never fabricated — it is null
/// until a real result is recorded.
/// </summary>
public class OwnerParticipationsDataTests
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

    private static async Task<(Guid tournamentId, Guid raceId)> CreateTournamentWithRaceAsync(
        RaceLifecycleTests.LifecycleFixture f, TournamentStatus status)
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

        var tournament = await f.Db.Tournaments.FirstAsync(t => t.Id == tournamentId);
        tournament.Status = status;
        await f.Db.SaveChangesAsync();

        return (tournamentId, race.Result.Data!.Id);
    }

    private static TournamentRegistrationsController BuildRegistrationController(RaceLifecycleTests.LifecycleFixture f, Guid userId)
    {
        var controller = new TournamentRegistrationsController(f.Db, f.UnitOfWork);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static HorsesController BuildHorsesController(RaceLifecycleTests.LifecycleFixture f, Guid userId)
    {
        var horseService = new HorseService(
            new HorseRepository(f.Db), new OwnerRepository(f.Db), new JockeyRepository(f.Db),
            f.RaceRepo, f.EntryRepo, new JockeyInvitationRepository(f.Db), f.UnitOfWork,
            null!, f.Db);
        // GetMyRaceEntries only touches IHorseService — the remaining dependencies are unused by
        // that action and are never invoked here.
        var controller = new HorsesController(horseService, null!, null!, null!);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    // The controller actions return anonymous-type projections declared in the BE assembly —
    // `dynamic` member binding fails on those from Tests.dll because anonymous types are
    // effectively assembly-internal, so property access here goes through reflection instead.
    private static object[] UnwrapList(ActionResult result)
    {
        var value = ((OkObjectResult)result).Value;
        return ((System.Collections.IEnumerable)value!).Cast<object>().ToArray();
    }

    private static T? GetProp<T>(object row, string name) =>
        (T?)row.GetType().GetProperty(name)!.GetValue(row);

    [Fact]
    public async Task MyRegistrations_AllStatusesRemainVisible()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var pendingT = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var rejectedT = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(20));
        var withdrawnT = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(30));
        await RegisterAsync(f, pendingT, horseId, ownerId, RegistrationStatus.Pending);
        await RegisterAsync(f, rejectedT, horseId, ownerId, RegistrationStatus.Rejected);
        await RegisterAsync(f, withdrawnT, horseId, ownerId, RegistrationStatus.Withdrawn);

        var controller = BuildRegistrationController(f, userId);
        var list = UnwrapList(await controller.MyRegistrations());

        Assert.Equal(3, list.Length);
        Assert.Contains(list, r => GetProp<string>(r, "status") == "Pending");
        Assert.Contains(list, r => GetProp<string>(r, "status") == "Rejected");
        Assert.Contains(list, r => GetProp<string>(r, "status") == "Withdrawn");
        // Every row must carry the Tournament's own status so the FE can bucket Upcoming/Ongoing/Finished.
        Assert.All(list, r => Assert.Equal("Published", GetProp<string>(r, "tournamentStatus")));
    }

    [Fact]
    public async Task MyRegistrations_ApprovedTournamentOngoing_ExposesOngoingStatus()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var (tournamentId, _) = await CreateTournamentWithRaceAsync(f, TournamentStatus.Ongoing);
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var controller = BuildRegistrationController(f, userId);
        var list = UnwrapList(await controller.MyRegistrations());

        Assert.Single(list);
        Assert.Equal("Approved", GetProp<string>(list[0], "status"));
        Assert.Equal("Ongoing", GetProp<string>(list[0], "tournamentStatus"));
    }

    [Fact]
    public async Task MyRaceEntries_OngoingTournament_ExposesRoundAndTournamentId()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateTournamentWithRaceAsync(f, TournamentStatus.Draft);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);

        var controller = BuildHorsesController(f, userId);
        var entries = UnwrapList(await controller.GetMyRaceEntries());

        Assert.Single(entries);
        Assert.Equal(tournamentId, GetProp<Guid?>(entries[0], "TournamentId"));
        Assert.Equal(1, GetProp<int?>(entries[0], "RoundNumber"));
        // No result recorded yet — must never fabricate a rank.
        Assert.Null(GetProp<int?>(entries[0], "FinishPosition"));
    }

    [Fact]
    public async Task MyRaceEntries_NoRecordedResult_FinishPositionStaysNull()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateTournamentWithRaceAsync(f, TournamentStatus.Finished);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);

        var controller = BuildHorsesController(f, userId);
        var entries = UnwrapList(await controller.GetMyRaceEntries());

        Assert.Single(entries);
        // Task C1 §3: a Finished Tournament with no recorded FinishPosition for this Horse must
        // surface as null — the FE renders "Chưa có thứ hạng đầy đủ" for null, never a fabricated 0/1.
        Assert.Null(GetProp<int?>(entries[0], "FinishPosition"));
    }
}
