using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public class C1FinalDomainCorrectionTests
{
    private static ClaimsPrincipal Principal(Guid userId, string? role = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (!string.IsNullOrWhiteSpace(role)) claims.Add(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static TournamentsController BuildTournamentsController(RaceLifecycleTests.LifecycleFixture f, string? role = null)
    {
        return new TournamentsController(f.TournamentSvc, f.RoundSvc)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(Guid.NewGuid(), role) }
            }
        };
    }

    private static TournamentRegistrationsController BuildTournamentRegistrationsController(
        RaceLifecycleTests.LifecycleFixture f, Guid userId)
    {
        return new TournamentRegistrationsController(f.Db, f.UnitOfWork)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(userId, "HorseOwner") }
            }
        };
    }

    private static IReadOnlyList<TournamentResponse> TournamentList(ActionResult result)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        var api = Assert.IsType<ApiResult<IEnumerable<TournamentResponse>>>(objectResult.Value);
        Assert.True(api.Success, api.Message);
        return api.Data!.ToList();
    }

    private static (int status, string? message) StatusAndMessage(ActionResult result)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        var message = objectResult.Value?.GetType().GetProperty("message")?.GetValue(objectResult.Value) as string
            ?? objectResult.Value?.GetType().GetProperty("Message")?.GetValue(objectResult.Value) as string;
        return (objectResult.StatusCode ?? 200, message);
    }

    private static async Task<Guid> CreateTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f,
        TournamentStatus status,
        DateTime start,
        DateTime end,
        DateTime? registrationDeadline = null,
        int? maxParticipants = 10)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = $"Tournament-{Guid.NewGuid():N}",
            StartDate = start,
            EndDate = end,
            RegistrationDeadline = registrationDeadline,
            MinParticipants = 3,
            MaxParticipants = maxParticipants,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        f.Db.Tournaments.Add(tournament);
        await f.Db.SaveChangesAsync();
        return tournament.Id;
    }

    private static async Task<Guid> CreateTrackAsync(RaceLifecycleTests.LifecycleFixture f, int capacity = 20)
    {
        var track = new Track { Id = Guid.NewGuid(), Name = $"Track-{Guid.NewGuid():N}", Capacity = capacity, CreatedAt = DateTime.UtcNow };
        f.Db.Tracks.Add(track);
        await f.Db.SaveChangesAsync();
        return track.Id;
    }

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

    private static async Task DirectTournamentRegistrationAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid horseId, Guid ownerId, RegistrationStatus status)
    {
        f.Db.TournamentHorseRegistrations.Add(new TournamentHorseRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            HorseId = horseId,
            OwnerId = ownerId,
            Status = status,
            CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();
    }

    private static RaceEntryService BuildRaceEntryService(RaceLifecycleTests.LifecycleFixture f) =>
        new(new OwnerRepository(f.Db), new HorseRepository(f.Db), new JockeyRepository(f.Db), f.RaceRepo, f.EntryRepo, f.UnitOfWork, f.Db);

    private static async Task<Guid> CreateRoundAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, string name, int roundNumber,
        DateTime start, DateTime end, int? advanceCount)
    {
        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = name,
            TournamentId = tournamentId,
            RoundNumber = roundNumber,
            ScheduledStartDate = start,
            ScheduledEndDate = end,
            AdvanceCount = advanceCount
        });
        Assert.True(result.Result.Success, result.Result.Message);
        return result.Result.Data!.Id;
    }

    private static async Task<Guid> CreateRaceAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid roundId,
        DateTime scheduledAt, int maxParticipants, int? qualificationSlots)
    {
        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = $"Race-{Guid.NewGuid():N}",
            TournamentId = tournamentId,
            RoundId = roundId,
            ScheduledAt = scheduledAt,
            ScheduledEndAt = scheduledAt.AddHours(1),
            MaxParticipants = maxParticipants,
            QualificationSlots = qualificationSlots
        });
        Assert.True(result.Result.Success, result.Result.Message);
        return result.Result.Data!.Id;
    }

    private static async Task<(Guid tournamentId, Guid round1RaceId, Guid round2RaceId)> CreateTwoRoundDraftTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(10), start.AddDays(-1));
        var round1Id = await CreateRoundAsync(f, tournamentId, "Round 1", 1, start, start.AddDays(2), advanceCount: 1);
        var round2Id = await CreateRoundAsync(f, tournamentId, "Final", 2, start.AddDays(2), start.AddDays(4), advanceCount: 0);
        var race1Id = await CreateRaceAsync(f, tournamentId, round1Id, start.AddHours(1), maxParticipants: 4, qualificationSlots: 1);
        var race2Id = await CreateRaceAsync(f, tournamentId, round2Id, start.AddDays(2).AddHours(1), maxParticipants: 4, qualificationSlots: 0);
        return (tournamentId, race1Id, race2Id);
    }

    private static async Task<(Guid tournamentId, Guid raceId)> CreateSingleRoundFinalDraftTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(5), start.AddDays(-1));
        var roundId = await CreateRoundAsync(f, tournamentId, "Final", 1, start, start.AddDays(2), advanceCount: 0);
        var raceId = await CreateRaceAsync(f, tournamentId, roundId, start.AddHours(1), maxParticipants: 4, qualificationSlots: 0);
        return (tournamentId, raceId);
    }

    [Fact]
    public async Task TournamentVisibility_AdminSeesDraft_NonAdminAndAnonymousDoNot()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var draftId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(5), start.AddDays(-1));
        var publishedId = await CreateTournamentAsync(f, TournamentStatus.Published, start.AddDays(10), start.AddDays(15), start.AddDays(9));

        var adminList = TournamentList(await BuildTournamentsController(f, "Admin").GetAllTournaments());
        Assert.Contains(adminList, t => t.Id == draftId);
        Assert.Contains(adminList, t => t.Id == publishedId);

        var ownerList = TournamentList(await BuildTournamentsController(f, "HorseOwner").GetAllTournaments());
        Assert.DoesNotContain(ownerList, t => t.Id == draftId);
        Assert.Contains(ownerList, t => t.Id == publishedId);

        var spectatorList = TournamentList(await BuildTournamentsController(f, "Spectator").GetAllTournaments());
        Assert.DoesNotContain(spectatorList, t => t.Id == draftId);

        var anonymousList = TournamentList(await BuildTournamentsController(f).GetAllTournaments());
        Assert.DoesNotContain(anonymousList, t => t.Id == draftId);
    }

    [Fact]
    public async Task TournamentDetail_DraftHiddenFromNonAdmin_ButAdminCanAccess()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var draftId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(5), start.AddDays(-1));

        var ownerResult = await BuildTournamentsController(f, "HorseOwner").GetTournament(draftId);
        Assert.IsType<NotFoundObjectResult>(ownerResult);

        var spectatorResult = await BuildTournamentsController(f, "Spectator").GetTournament(draftId);
        Assert.IsType<NotFoundObjectResult>(spectatorResult);

        var adminResult = Assert.IsType<ObjectResult>(await BuildTournamentsController(f, "Admin").GetTournament(draftId));
        Assert.Equal(200, adminResult.StatusCode);
    }

    [Fact]
    public async Task DirectOwnerTournamentRegistrationAgainstDraft_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "draft");
        var start = DateTime.UtcNow.AddDays(10);
        var draftId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(5), start.AddDays(-1));

        var controller = BuildTournamentRegistrationsController(f, userId);
        var (status, _) = StatusAndMessage(await controller.Register(new RegisterTournamentHorseRequest
        {
            TournamentId = draftId,
            HorseId = horseId
        }));

        Assert.Equal(400, status);
        Assert.False(await f.Db.TournamentHorseRegistrations.AnyAsync(r => r.TournamentId == draftId && r.HorseId == horseId));
    }

    [Fact]
    public async Task OwnerDirectRaceRegistration_Rejected_AndCreatesNoRaceEntry()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateSingleRoundFinalDraftTournamentAsync(f);
        await f.RaceManagement.OpenRegistrationAsync(raceId);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "direct-race");
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await BuildRaceEntryService(f).RegisterAsync(userId, horseId, raceId, new RaceRegistrationRequest());

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("không còn được hỗ trợ", result.Result.Message);
        Assert.False(await f.Db.RaceEntries.AnyAsync(e => e.RaceId == raceId && e.HorseId == horseId));
    }

    [Fact]
    public async Task OwnerDirectRaceRegistration_LaterRound_Rejected_AndCreatesNoRaceEntry()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, round2RaceId) = await CreateTwoRoundDraftTournamentAsync(f);
        await f.RaceManagement.OpenRegistrationAsync(round2RaceId);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "later-direct");
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await BuildRaceEntryService(f).RegisterAsync(userId, horseId, round2RaceId, new RaceRegistrationRequest());

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.False(await f.Db.RaceEntries.AnyAsync(e => e.RaceId == round2RaceId && e.HorseId == horseId));
    }

    [Fact]
    public async Task AdminAssignHorse_RoundOneApprovedRegistration_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1RaceId, _) = await CreateTwoRoundDraftTournamentAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "round1");
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(round1RaceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.True(await f.Db.RaceEntries.AnyAsync(e => e.RaceId == round1RaceId && e.HorseId == horseId));
    }

    [Fact]
    public async Task AdminAssignHorse_OlderWithdrawnThenNewerApprovedRegistration_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1RaceId, _) = await CreateTwoRoundDraftTournamentAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "withdrawn-approved");
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Withdrawn);
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(round1RaceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.True(await f.Db.RaceEntries.AnyAsync(e => e.RaceId == round1RaceId && e.HorseId == horseId));
    }

    [Fact]
    public async Task AdminAssignHorse_WithdrawnOnlyRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1RaceId, _) = await CreateTwoRoundDraftTournamentAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "withdrawn-only");
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Withdrawn);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(round1RaceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("chưa được duyệt", result.Result.Message);
        Assert.False(await f.Db.RaceEntries.AnyAsync(e => e.RaceId == round1RaceId && e.HorseId == horseId));
    }

    [Fact]
    public async Task AdminAssignHorse_OlderRejectedThenNewerApprovedRegistration_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1RaceId, _) = await CreateTwoRoundDraftTournamentAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "rejected-approved");
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Rejected);
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(round1RaceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.True(await f.Db.RaceEntries.AnyAsync(e => e.RaceId == round1RaceId && e.HorseId == horseId));
    }

    [Fact]
    public async Task AdminAssignHorse_ExistingRaceEntryDuplicate_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1RaceId, _) = await CreateTwoRoundDraftTournamentAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "duplicate-entry");
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);
        var first = await f.RaceManagement.AssignHorseToRaceAsync(round1RaceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(first.Result.Success, first.Result.Message);

        var duplicate = await f.RaceManagement.AssignHorseToRaceAsync(round1RaceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.False(duplicate.Result.Success);
        Assert.Equal(400, duplicate.StatusCode);
        Assert.Equal(1, await f.Db.RaceEntries.CountAsync(e => e.RaceId == round1RaceId && e.HorseId == horseId));
    }
    [Fact]
    public async Task AdminAssignHorse_RoundTwoRejectedUntilQualificationExists()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, round2RaceId) = await CreateTwoRoundDraftTournamentAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "round2");
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(round2RaceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("vòng trước", result.Result.Message);
        Assert.False(await f.Db.RaceEntries.AnyAsync(e => e.RaceId == round2RaceId && e.HorseId == horseId));
    }

    [Fact]
    public async Task AdminBulkAssignHorse_RoundTwoRejectedUntilQualificationExists()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, round2RaceId) = await CreateTwoRoundDraftTournamentAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "bulk-round2");
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.BulkAssignHorsesToRaceAsync(
            round2RaceId,
            new BulkAssignHorsesToRaceRequest { HorseIds = new[] { horseId } });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("vòng trước", result.Result.Message);
        Assert.False(await f.Db.RaceEntries.AnyAsync(e => e.RaceId == round2RaceId && e.HorseId == horseId));
    }
    [Fact]
    public async Task AdminAssignHorse_SingleRoundFinalRoundOne_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateSingleRoundFinalDraftTournamentAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "single-final");
        await DirectTournamentRegistrationAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task StartRace_ScheduledToInProgress_WhenReadinessPasses()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();

        var result = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(RaceStatus.InProgress, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    [Theory]
    [InlineData(RaceStatus.RegistrationOpen)]
    [InlineData(RaceStatus.RegistrationClosed)]
    public async Task StartRace_LegacyPreRaceStatusesStillSupported(RaceStatus status)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entity = await f.RaceRepo.GetByIdAsync(race.Id);
        entity!.Status = status;
        await f.UnitOfWork.SaveChangesAsync();

        var result = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(RaceStatus.InProgress, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    [Fact]
    public async Task StartRace_ReadinessFailureStillRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var healthChecks = await f.Db.HorseHealthChecks.Where(h => h.RaceId == race.Id).ToListAsync();
        f.Db.HorseHealthChecks.RemoveRange(healthChecks);
        await f.Db.SaveChangesAsync();

        var result = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(RaceStatus.Scheduled, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    [Theory]
    [InlineData(-1, 6)]
    [InlineData(6, 6)]
    [InlineData(7, 6)]
    public async Task QualificationSlots_CreateInvalidValuesRejected(int qualificationSlots, int maxParticipants)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(5), start.AddDays(-1));
        var roundId = await CreateRoundAsync(f, tournamentId, "Round 1", 1, start, start.AddDays(2), advanceCount: 6);

        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Invalid slots",
            TournamentId = tournamentId,
            RoundId = roundId,
            ScheduledAt = start.AddHours(1),
            ScheduledEndAt = start.AddHours(2),
            MaxParticipants = maxParticipants,
            QualificationSlots = qualificationSlots
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task QualificationSlots_CumulativeGreaterThanAdvanceCount_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(5), start.AddDays(-1));
        var roundId = await CreateRoundAsync(f, tournamentId, "Round 1", 1, start, start.AddDays(2), advanceCount: 3);
        await CreateRaceAsync(f, tournamentId, roundId, start.AddHours(1), maxParticipants: 6, qualificationSlots: 2);

        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Too many cumulative slots",
            TournamentId = tournamentId,
            RoundId = roundId,
            ScheduledAt = start.AddHours(3),
            ScheduledEndAt = start.AddHours(4),
            MaxParticipants = 6,
            QualificationSlots = 2
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task QualificationSlots_PartialAndExactTotalsAllowedDuringDraft()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(5), start.AddDays(-1));
        var roundId = await CreateRoundAsync(f, tournamentId, "Round 1", 1, start, start.AddDays(2), advanceCount: 6);

        var partial = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Partial",
            TournamentId = tournamentId,
            RoundId = roundId,
            ScheduledAt = start.AddHours(1),
            ScheduledEndAt = start.AddHours(2),
            MaxParticipants = 8,
            QualificationSlots = 3
        });
        Assert.True(partial.Result.Success, partial.Result.Message);

        var exact = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Exact",
            TournamentId = tournamentId,
            RoundId = roundId,
            ScheduledAt = start.AddHours(3),
            ScheduledEndAt = start.AddHours(4),
            MaxParticipants = 8,
            QualificationSlots = 3
        });
        Assert.True(exact.Result.Success, exact.Result.Message);
    }

    [Fact]
    public async Task QualificationSlots_FinalRoundPositiveRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(5), start.AddDays(-1));
        var roundId = await CreateRoundAsync(f, tournamentId, "Final", 1, start, start.AddDays(2), advanceCount: 0);

        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final invalid",
            TournamentId = tournamentId,
            RoundId = roundId,
            ScheduledAt = start.AddHours(1),
            ScheduledEndAt = start.AddHours(2),
            MaxParticipants = 8,
            QualificationSlots = 1
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task QualificationSlots_UpdateCumulativeGreaterThanAdvanceCount_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(5), start.AddDays(-1));
        var roundId = await CreateRoundAsync(f, tournamentId, "Round 1", 1, start, start.AddDays(2), advanceCount: 3);
        await CreateRaceAsync(f, tournamentId, roundId, start.AddHours(1), maxParticipants: 6, qualificationSlots: 2);
        var race2Id = await CreateRaceAsync(f, tournamentId, roundId, start.AddHours(3), maxParticipants: 6, qualificationSlots: 1);

        var result = await f.RaceManagement.UpdateRaceAsync(race2Id, new UpdateRaceRequest { QualificationSlots = 2 });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task PublishStillRejectsQualificationSlotTotalNotEqualAdvanceCount()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(10), start.AddDays(-1));
        var round1Id = await CreateRoundAsync(f, tournamentId, "Round 1", 1, start, start.AddDays(2), advanceCount: 6);
        var round2Id = await CreateRoundAsync(f, tournamentId, "Final", 2, start.AddDays(2), start.AddDays(4), advanceCount: 0);
        await CreateRaceAsync(f, tournamentId, round1Id, start.AddHours(1), maxParticipants: 8, qualificationSlots: 3);
        await CreateRaceAsync(f, tournamentId, round2Id, start.AddDays(2).AddHours(1), maxParticipants: 4, qualificationSlots: 0);

        var publish = await f.TournamentSvc.ChangeStatusAsync(
            tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published },
            Guid.NewGuid());

        Assert.False(publish.Result.Success);
        Assert.Equal(400, publish.StatusCode);
    }
}