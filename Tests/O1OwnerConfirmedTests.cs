using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// O1: Owner consent happens at Tournament-registration level (Owner registers -> Admin approves
/// the TournamentHorseRegistration -> Admin assigns the Approved Horse to a Round 1 Race). That
/// Admin assignment IS the authoritative RaceEntry creation, so RaceEntry.OwnerConfirmed = true
/// immediately — there is no second manual Owner "Xác nhận tham gia cuộc đua" step anymore.
/// JockeyConfirmed remains independent and stays false until Owner Final Confirm (J3) establishes
/// the official Jockey. StartRace's existing OwnerConfirmed readiness check is preserved unchanged
/// (now an invariant/safety guard rather than something an Owner action is needed to satisfy).
/// </summary>
public class O1OwnerConfirmedTests
{
    [Fact]
    public async Task AssignHorseToRaceAsync_SingleAssignment_OwnerConfirmedTrue_JockeyConfirmedFalse_JockeyIdNull()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "o1-single");
        var (tournamentId, raceId) = await CreateRoundOneRaceAsync(f, "o1-single");
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.True(result.Result.Success, result.Result.Message);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.True(entry.OwnerConfirmed);
        Assert.False(entry.JockeyConfirmed);
    }

    [Fact]
    public async Task BulkAssignHorsesToRaceAsync_EveryCreatedEntry_OwnerConfirmedTrue_JockeyConfirmedFalse_JockeyIdNull()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, ownerAId, horseAId) = await CreateApprovedOwnerHorseAsync(f, "o1-bulk-a");
        var (_, ownerBId, horseBId) = await CreateApprovedOwnerHorseAsync(f, "o1-bulk-b");
        var (_, ownerCId, horseCId) = await CreateApprovedOwnerHorseAsync(f, "o1-bulk-c");
        var (tournamentId, raceId) = await CreateRoundOneRaceAsync(f, "o1-bulk", maxParticipants: 8);
        await RegisterTournamentAsync(f, tournamentId, ownerAId, horseAId, RegistrationStatus.Approved);
        await RegisterTournamentAsync(f, tournamentId, ownerBId, horseBId, RegistrationStatus.Approved);
        await RegisterTournamentAsync(f, tournamentId, ownerCId, horseCId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.BulkAssignHorsesToRaceAsync(raceId,
            new BulkAssignHorsesToRaceRequest { HorseIds = new[] { horseAId, horseBId, horseCId } });

        Assert.True(result.Result.Success, result.Result.Message);
        var entries = await f.Db.RaceEntries.Where(e => e.RaceId == raceId).ToListAsync();
        Assert.Equal(3, entries.Count);
        foreach (var entry in entries)
        {
            Assert.Null(entry.JockeyId);
            Assert.True(entry.OwnerConfirmed, $"Entry for horse {entry.HorseId} must have OwnerConfirmed=true from Admin bulk-assignment.");
            Assert.False(entry.JockeyConfirmed);
        }
    }

    [Fact]
    public async Task FinalConfirmJockey_OnO1CreatedEntry_SetsOfficialJockey_LeavesOwnerConfirmedTrue_UnchangedPairingRules()
    {
        // End-to-end O1 + J3: Admin assignment sets OwnerConfirmed=true with no Jockey; Owner Final
        // Confirm (J3) only ever sets JockeyId/JockeyConfirmed — it must not be the thing that
        // "repairs" OwnerConfirmed, because O1 already guaranteed it was true from creation.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "o1-j3-integration");
        var (tournamentId, raceId) = await CreateRoundOneRaceAsync(f, "o1-j3-integration");
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);
        var entryAfterAssign = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.True(entryAfterAssign.OwnerConfirmed);
        Assert.Null(entryAfterAssign.JockeyId);

        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "o1-j3-integration");
        var horseService = BuildHorseService(f);
        var jockeyService = BuildJockeyService(f);

        var invite = await horseService.InviteJockeyAsync(ownerUserId, horseId,
            new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId);
        var accept = await jockeyService.RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(accept.Result.Success, accept.Result.Message);

        var confirm = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitation.Id });
        Assert.True(confirm.Result.Success, confirm.Result.Message);

        var entryAfterConfirm = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Equal(jockeyId, entryAfterConfirm.JockeyId);
        Assert.True(entryAfterConfirm.JockeyConfirmed);
        Assert.True(entryAfterConfirm.OwnerConfirmed); // still true — Final Confirm never touched it
    }

    [Fact]
    public async Task StartRace_StillRejectsWhenOwnerConfirmedFalse_GuardRemainsAsInvariant()
    {
        // O1 does not redesign StartRace — the OwnerConfirmed readiness check must still function
        // as a safety guard against a malformed/legacy RaceEntry (pre-O1 data, or any future bug),
        // even though authoritative Admin assignment never produces OwnerConfirmed=false anymore.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();

        var entry = await f.Db.RaceEntries.FirstAsync(e => e.RaceId == race.Id);
        entry.OwnerConfirmed = false;
        await f.Db.SaveChangesAsync();

        var result = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("OwnerConfirmed", result.Result.Message);
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

    private static async Task RegisterTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid ownerId, Guid horseId, RegistrationStatus status)
    {
        f.Db.TournamentHorseRegistrations.Add(new TournamentHorseRegistration
        {
            Id = Guid.NewGuid(), TournamentId = tournamentId, OwnerId = ownerId, HorseId = horseId,
            Status = status, ApprovedAt = status == RegistrationStatus.Approved ? DateTime.UtcNow : null
        });
        await f.Db.SaveChangesAsync();
    }

    private static async Task<(Guid tournamentId, Guid raceId)> CreateRoundOneRaceAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag, int maxParticipants = 8)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = $"Tournament {tag}-{Guid.NewGuid():N}", StartDate = start.Date, EndDate = start.Date.AddDays(3),
            Status = TournamentStatus.Published, IsActive = true, MaxRounds = 1, MaxParticipants = maxParticipants,
            RegistrationDeadline = start.Date.AddDays(-1)
        };
        var round = new Round
        {
            Id = Guid.NewGuid(), Name = "Round 1", TournamentId = tournament.Id, RoundNumber = 1, AdvanceCount = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddHours(2)
        };
        var race = new Race
        {
            Id = Guid.NewGuid(), Name = $"Race {tag}", TournamentId = tournament.Id, RoundId = round.Id,
            ScheduledAt = start, ScheduledEndAt = start.AddMinutes(60), Status = RaceStatus.Scheduled,
            MaxParticipants = maxParticipants, Distance = 1200
        };
        f.Db.AddRange(tournament, round, race);
        await f.Db.SaveChangesAsync();
        return (tournament.Id, race.Id);
    }

    private sealed class NoopNotificationService : INotificationService
    {
        public Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto)
            => Task.FromResult(ServiceResult<NotificationDto>.Ok(new NotificationDto()));

        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetUserNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));

        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));

        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));

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

        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));

        public Task ProcessUnsentNotificationsAsync()
            => Task.CompletedTask;
    }
}
