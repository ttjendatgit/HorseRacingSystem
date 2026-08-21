using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tests;

/// <summary>
/// Regression hotfix: GET /api/horses/all (Admin -> "/admin/horses") used to project the raw
/// h.Owner / h.RaceEntries / h.JockeyInvitations navigation entities directly into the response.
/// EF Core's change-tracker fix-up wires those navigations back to each other across every Horse
/// loaded in the same query (Horse -> RaceEntries -> Race -> Entries (other Horses) -> Horse ->
/// JockeyInvitations -> Jockey -> Invitations -> Horse -> ...), which — once enough interlinked
/// data exists (multiple Horses sharing a Race, a Jockey holding invitations from multiple
/// Horses) — fans out deep/wide enough to exceed System.Text.Json's MaxDepth even with the
/// app-wide ReferenceHandler.IgnoreCycles already configured in Program.cs (that option only
/// catches a literal same-instance cycle, not "too deep"). These tests replicate that exact data
/// shape and assert the endpoint's response serializes cleanly under the DEFAULT (no
/// IgnoreCycles) JsonSerializerOptions — a stronger guarantee than relying on the global app
/// setting, proving the fix is structural (a bounded projection), not incidental.
/// </summary>
public class AdminHorsesRegressionTests
{
    [Fact]
    public async Task GetAllHorses_RichlyInterlinkedGraph_SerializesWithoutCycleOrDepthException()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var scenario = await CreateInterlinkedScenarioAsync(f);

        var controller = BuildHorsesController(f);
        var actionResult = await controller.GetAllHorses();
        var okResult = Assert.IsType<OkObjectResult>(actionResult);

        // Default options: no ReferenceHandler.IgnoreCycles, default MaxDepth (64). If the
        // projection still leaked raw navigation entities, this throws JsonException exactly like
        // the reported regression — independent of whatever Program.cs happens to configure.
        var json = JsonSerializer.Serialize(okResult.Value, new JsonSerializerOptions());
        Assert.False(string.IsNullOrEmpty(json));
    }

    [Fact]
    public async Task GetAllHorses_ApprovedHorse_StillAppearsInResponse()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var scenario = await CreateInterlinkedScenarioAsync(f);

        var controller = BuildHorsesController(f);
        var actionResult = await controller.GetAllHorses();
        var okResult = Assert.IsType<OkObjectResult>(actionResult);

        var data = GetDataList(okResult.Value);
        var horseIds = data.Select(h => (Guid)GetProp(h, "Id")!).ToHashSet();
        Assert.Contains(scenario.HorseAId, horseIds);
        Assert.Contains(scenario.HorseBId, horseIds);
    }

    [Fact]
    public async Task GetAllHorses_ExistingOfficialPairing_DoesNotRemoveHorseFromList()
    {
        // scenario already establishes an official RaceEntry.JockeyId pairing (Horse A + Jockey X)
        // via the same J3 Final Confirm path — the Admin list must still show both Horses.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var scenario = await CreateInterlinkedScenarioAsync(f);

        var entryA = await f.Db.RaceEntries.SingleAsync(e => e.HorseId == scenario.HorseAId);
        Assert.Equal(scenario.JockeyXId, entryA.JockeyId); // sanity: pairing genuinely established

        var controller = BuildHorsesController(f);
        var actionResult = await controller.GetAllHorses();
        var okResult = Assert.IsType<OkObjectResult>(actionResult);

        var data = GetDataList(okResult.Value);
        Assert.Equal(2, data.Count);
        var horseA = data.Single(h => (Guid)GetProp(h, "Id")! == scenario.HorseAId);
        Assert.Equal(scenario.JockeyXId, (Guid?)GetProp(horseA, "AssignedJockeyId"));
    }

    [Fact]
    public async Task GetAllHorses_ImageUrlPresent_IsReturned()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var scenario = await CreateInterlinkedScenarioAsync(f);

        var horseA = await f.Db.Horses.SingleAsync(h => h.Id == scenario.HorseAId);
        horseA.ImageUrl = "https://example.test/horse-a.jpg";
        await f.Db.SaveChangesAsync();

        var controller = BuildHorsesController(f);
        var actionResult = await controller.GetAllHorses();
        var okResult = Assert.IsType<OkObjectResult>(actionResult);

        var data = GetDataList(okResult.Value);
        var mapped = data.Single(h => (Guid)GetProp(h, "Id")! == scenario.HorseAId);
        Assert.Equal("https://example.test/horse-a.jpg", (string?)GetProp(mapped, "ImageUrl"));
    }

    private sealed record Scenario(Guid HorseAId, Guid HorseBId, Guid JockeyXId, Guid RaceId);

    /// <summary>
    /// Two Horses officially assigned to the SAME Race (same Tournament), a shared Jockey holding
    /// Accepted invitations from BOTH Horses, and one of those invitations promoted to an official
    /// pairing via Owner Final Confirm — the exact interlinked shape (Horse -> RaceEntries -> Race
    /// -> Entries -> other Horse -> JockeyInvitations -> Jockey -> Invitations -> Horse -> ...)
    /// that triggered the original serialization regression.
    /// </summary>
    private static async Task<Scenario> CreateInterlinkedScenarioAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = "Admin-Regression-Tournament", StartDate = start.Date, EndDate = start.Date.AddDays(3),
            Status = TournamentStatus.Published, IsActive = true, MaxRounds = 1, MaxParticipants = 8,
            RegistrationDeadline = start.Date.AddDays(-1)
        };
        var round = new Round
        {
            Id = Guid.NewGuid(), Name = "Round 1", TournamentId = tournament.Id, RoundNumber = 1, AdvanceCount = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddHours(2)
        };
        var race = new Race
        {
            Id = Guid.NewGuid(), Name = "Admin-Regression-Race", TournamentId = tournament.Id, RoundId = round.Id,
            ScheduledAt = start, ScheduledEndAt = start.AddMinutes(60), Status = RaceStatus.Scheduled,
            MaxParticipants = 8, Distance = 1200
        };
        f.Db.AddRange(tournament, round, race);
        await f.Db.SaveChangesAsync();

        var (ownerAUserId, ownerAId, horseAId) = await CreateApprovedOwnerHorseAsync(f, "regress-a");
        var (ownerBUserId, ownerBId, horseBId) = await CreateApprovedOwnerHorseAsync(f, "regress-b");
        await RegisterTournamentAsync(f, tournament.Id, ownerAId, horseAId);
        await RegisterTournamentAsync(f, tournament.Id, ownerBId, horseBId);

        var assignA = await f.RaceManagement.AssignHorseToRaceAsync(race.Id, new AssignHorseToRaceRequest { HorseId = horseAId });
        Assert.True(assignA.Result.Success, assignA.Result.Message);
        var assignB = await f.RaceManagement.AssignHorseToRaceAsync(race.Id, new AssignHorseToRaceRequest { HorseId = horseBId });
        Assert.True(assignB.Result.Success, assignB.Result.Message);

        var (jockeyXUserId, jockeyXId) = await CreateJockeyAsync(f, "regress-x");

        var horseService = BuildHorseService(f);
        var jockeyService = BuildJockeyService(f);

        // Jockey X accepts invitations from BOTH Horses (J2: allowed) — this is what makes
        // Jockey.Invitations fan out to multiple Horses, each of which fans back out via
        // Horse.JockeyInvitations to the same Jockey.
        var inviteA = await horseService.InviteJockeyAsync(ownerAUserId, horseAId,
            new JockeyInvitationCreateRequest { JockeyId = jockeyXId, RaceId = race.Id });
        Assert.True(inviteA.Result.Success, inviteA.Result.Message);
        var invitationA = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseAId && i.JockeyId == jockeyXId);
        var acceptA = await jockeyService.RespondInvitationAsync(jockeyXUserId, invitationA.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(acceptA.Result.Success, acceptA.Result.Message);

        var inviteB = await horseService.InviteJockeyAsync(ownerBUserId, horseBId,
            new JockeyInvitationCreateRequest { JockeyId = jockeyXId, RaceId = race.Id });
        Assert.True(inviteB.Result.Success, inviteB.Result.Message);
        var invitationB = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseBId && i.JockeyId == jockeyXId);
        var acceptB = await jockeyService.RespondInvitationAsync(jockeyXUserId, invitationB.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(acceptB.Result.Success, acceptB.Result.Message);

        // Owner Final Confirm promotes Horse A's invitation to an official pairing.
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, race.Id,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationA.Id });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        return new Scenario(horseAId, horseBId, jockeyXId, race.Id);
    }

    private static async Task<(Guid ownerUserId, Guid ownerId, Guid horseId)> CreateApprovedOwnerHorseAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var ownerUser = new User
        {
            Id = Guid.NewGuid(), Email = $"owner-{tag}-{suffix}@test.local", PasswordHash = "x",
            FullName = $"Owner {tag}", Role = UserRole.HorseOwner, IsActive = true
        };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUser.Id, OwnerCode = $"OWN-{suffix.Substring(0, 8)}" };
        var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse {tag}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };

        f.Db.AddRange(ownerUser, owner, horse);
        await f.Db.SaveChangesAsync();
        return (ownerUser.Id, owner.Id, horse.Id);
    }

    private static async Task<(Guid jockeyUserId, Guid jockeyId)> CreateJockeyAsync(RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var jockeyUser = new User
        {
            Id = Guid.NewGuid(), Email = $"jockey-{tag}-{suffix}@test.local", PasswordHash = "x",
            FullName = $"Jockey {tag}", Role = UserRole.Jockey, IsActive = true
        };
        var jockey = new Jockey
        {
            Id = Guid.NewGuid(), UserId = jockeyUser.Id, LicenseNumber = $"LIC-{suffix.Substring(0, 8)}",
            ApprovalStatus = ApprovalStatus.Approved
        };
        f.Db.AddRange(jockeyUser, jockey);
        await f.Db.SaveChangesAsync();
        return (jockeyUser.Id, jockey.Id);
    }

    private static async Task RegisterTournamentAsync(RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid ownerId, Guid horseId)
    {
        f.Db.TournamentHorseRegistrations.Add(new TournamentHorseRegistration
        {
            Id = Guid.NewGuid(), TournamentId = tournamentId, OwnerId = ownerId, HorseId = horseId,
            Status = RegistrationStatus.Approved, ApprovedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();
    }

    private static HorseService BuildHorseService(RaceLifecycleTests.LifecycleFixture f)
        => new(
            new HorseRepository(f.Db), new OwnerRepository(f.Db), new JockeyRepository(f.Db),
            new RaceRepository(f.Db), new RaceEntryRepository(f.Db), new JockeyInvitationRepository(f.Db),
            f.UnitOfWork, new NoopNotificationService(), f.Db);

    private static JockeyService BuildJockeyService(RaceLifecycleTests.LifecycleFixture f)
        => new(
            new UserRepository(f.Db), new JockeyRepository(f.Db), new JockeyInvitationRepository(f.Db),
            new RaceEntryRepository(f.Db), new RaceRepository(f.Db), f.UnitOfWork, new NoopNotificationService());

    /// <summary>GetAllHorses only touches HttpContext.RequestServices (its own DbContext scope) —
    /// none of the constructor-injected dependencies are used by that action.</summary>
    private static HorsesController BuildHorsesController(RaceLifecycleTests.LifecycleFixture f)
    {
        var services = new ServiceCollection();
        services.AddSingleton(f.Db);
        var provider = services.BuildServiceProvider();

        var controller = new HorsesController(null!, null!, null!, null!);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity), RequestServices = provider }
        };
        return controller;
    }

    // GetAllHorses returns anonymous-type projections declared in the BE assembly — anonymous
    // types are effectively assembly-internal, so property access here goes through reflection.
    private static List<object> GetDataList(object? okValue)
    {
        var dataProp = okValue!.GetType().GetProperty("data", BindingFlags.Public | BindingFlags.Instance)!;
        var data = dataProp.GetValue(okValue)!;
        return ((IEnumerable)data).Cast<object>().ToList();
    }

    private static object? GetProp(object instance, string name)
        => instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);

    private sealed class NoopNotificationService : HorseRacing.Services.Interfaces.INotificationService
    {
        public Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto)
            => Task.FromResult(ServiceResult<NotificationDto>.Ok(new NotificationDto()));

        public Task<ServiceResult<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));

        public Task<ServiceResult<List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));

        public Task<ServiceResult<List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter)
            => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));

        public Task<ServiceResult<NotificationDetailDto>> GetNotificationByIdAsync(Guid id)
            => Task.FromResult(ServiceResult<NotificationDetailDto>.Ok(new NotificationDetailDto()));

        public Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId)
            => Task.FromResult(ServiceResult<bool>.Ok(true));

        public Task<ServiceResult<bool>> MarkMultipleAsReadAsync(MarkNotificationsAsReadDto dto)
            => Task.FromResult(ServiceResult<bool>.Ok(true));

        public Task<ServiceResult<bool>> DeleteNotificationAsync(Guid notificationId)
            => Task.FromResult(ServiceResult<bool>.Ok(true));

        public Task<ServiceResult<bool>> DeleteAllNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<bool>.Ok(true));

        public Task<ServiceResult<int>> GetUnreadCountAsync(Guid userId)
            => Task.FromResult(ServiceResult<int>.Ok(0));

        public Task<ServiceResult<NotificationStatsDto>> GetNotificationStatsAsync(Guid userId)
            => Task.FromResult(ServiceResult<NotificationStatsDto>.Ok(new NotificationStatsDto()));

        public Task<ServiceResult<bool>> SendBulkNotificationsAsync(BulkNotificationDto dto)
            => Task.FromResult(ServiceResult<bool>.Ok(true));

        public Task<ServiceResult<List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId)
            => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));

        public Task ProcessUnsentNotificationsAsync()
            => Task.CompletedTask;
    }
}
