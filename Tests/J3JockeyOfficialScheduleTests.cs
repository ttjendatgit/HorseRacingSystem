using System;
using System.Collections.Generic;
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
/// J3 schedule correctness follow-up: RaceEntryRepository.GetByJockeyAsync used to also surface a
/// RaceEntry when the Horse had an Accepted invitation for the Jockey (or when the caller happened
/// to own the Horse) even though RaceEntry.JockeyId was still null. Under J2/J3 semantics an
/// Accepted invitation is not an official assignment — only Owner Final Confirm setting
/// RaceEntry.JockeyId makes a race "official". These tests pin GetAssignedRacesAsync to that rule.
/// </summary>
public class J3JockeyOfficialScheduleTests
{
    [Fact]
    public async Task GetAssignedRaces_AcceptedInvitationButJockeyIdNull_NotReturned()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j3-sched-accepted-only");
        var (jockeyUserId, _, _) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j3-sched-accepted-only");

        var result = await BuildJockeyService(f).GetAssignedRacesAsync(jockeyUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        var races = Assert.IsAssignableFrom<IEnumerable<JockeyAssignedRaceResponse>>(result.Result.Data);
        Assert.Empty(races);
    }

    [Fact]
    public async Task GetAssignedRaces_MultipleAcceptedInvitations_NoneBecomeOfficialAutomatically()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-sched-multi-a");
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "j3-sched-multi-b");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "j3-sched-multi-shared", ApprovalStatus.Approved);
        await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId, jockeyUserId);
        await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId, jockeyUserId);

        var result = await BuildJockeyService(f).GetAssignedRacesAsync(jockeyUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        var races = Assert.IsAssignableFrom<IEnumerable<JockeyAssignedRaceResponse>>(result.Result.Data);
        Assert.Empty(races);
    }

    [Fact]
    public async Task GetAssignedRaces_AfterOwnerFinalConfirm_ExactlyThatHorseRaceAppears()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j3-sched-confirmed");
        var (jockeyUserId, jockeyId, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j3-sched-confirmed");

        var confirm = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });
        Assert.True(confirm.Result.Success, confirm.Result.Message);

        var result = await BuildJockeyService(f).GetAssignedRacesAsync(jockeyUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        var races = Assert.IsAssignableFrom<IEnumerable<JockeyAssignedRaceResponse>>(result.Result.Data).ToList();
        var race = Assert.Single(races);
        Assert.Equal(raceId, race.RaceId);
        Assert.Equal(horseId, race.Horse.Id);
    }

    [Fact]
    public async Task GetAssignedRaces_AnotherAcceptedInvitationForSameJockey_StaysOutOfOfficialSchedule()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-sched-partial-a");
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "j3-sched-partial-b");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "j3-sched-partial-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId, jockeyUserId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId, jockeyUserId);

        var confirm = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirm.Result.Success, confirm.Result.Message);

        var result = await BuildJockeyService(f).GetAssignedRacesAsync(jockeyUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        var races = Assert.IsAssignableFrom<IEnumerable<JockeyAssignedRaceResponse>>(result.Result.Data).ToList();
        var race = Assert.Single(races);
        Assert.Equal(raceAId, race.RaceId); // only the officially confirmed one — not raceB

        var invitationB = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationBId);
        Assert.Equal(JockeyInvitationStatus.Accepted, invitationB.Status); // still Accepted, untouched
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceBId && e.HorseId == horseBId);
        Assert.Null(entryB.JockeyId); // still not official
    }

    [Fact]
    public async Task GetAssignedRaces_MultipleOfficialRaceEntriesForSameHorseAcrossRounds_AllShown()
    {
        // J3 BUSINESS PIVOT §7/§8: under the new one-Jockey-one-Horse-per-Tournament pairing, a
        // Jockey is never officially paired with two DIFFERENT Horses in the same Tournament — but
        // the SAME Horse can carry the SAME Jockey across multiple Rounds (future Qualification
        // will create these RaceEntries with JockeyId already set, simulated here via a direct
        // insert since Qualification itself isn't implemented yet). GetByJockeyAsync must return
        // every one of them, with no GroupBy(TournamentId) collapsing them down to one.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var raceAStart = DateTime.UtcNow.AddDays(14);
        var raceAEnd = raceAStart.AddMinutes(30);
        var raceBStart = raceAEnd.AddMinutes(30);
        var raceBEnd = raceBStart.AddMinutes(30);

        var (ownerUserId, _, horseId, raceAId) = await CreateAssignedHorseAsync(f, "j3-sched-multiround-a", raceAStart, raceAEnd);
        var (jockeyUserId, jockeyId, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceAId, "j3-sched-multiround-shared");

        var confirmA = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        // A later-Round RaceEntry for the SAME Horse, carrying the SAME Jockey forward automatically.
        var raceBId = await CreateAdditionalRaceInSameTournamentAsync(f, raceAId, "j3-sched-multiround-b", raceBStart, raceBEnd);
        await CreateRawRaceEntryWithJockeyAsync(f, raceBId, horseId, jockeyId);

        var result = await BuildJockeyService(f).GetAssignedRacesAsync(jockeyUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        var races = Assert.IsAssignableFrom<IEnumerable<JockeyAssignedRaceResponse>>(result.Result.Data).ToList();
        Assert.Equal(2, races.Count);
        Assert.Contains(races, r => r.RaceId == raceAId && r.Horse.Id == horseId);
        Assert.Contains(races, r => r.RaceId == raceBId && r.Horse.Id == horseId);
    }

    [Fact]
    public async Task GetAssignedRaces_HistoricalAndActiveOfficialAssignments_BothShown()
    {
        // Schedule display is not the lock validator (§9 item 5) — an old assignment in a now-
        // Finished Tournament and a new assignment in a different Published Tournament must BOTH
        // appear together; only Owner Final Confirm enforces the one-active-Tournament rule.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-sched-history-a");
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "j3-sched-history-b");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "j3-sched-history-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId, jockeyUserId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId, jockeyUserId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        // Tournament A concludes — a Finished Tournament no longer locks the Jockey, so a NEW
        // official assignment can be made in a different (Published) Tournament B.
        var tournamentAId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var tournamentA = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentAId);
        tournamentA.Status = TournamentStatus.Finished;
        await f.Db.SaveChangesAsync();

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });
        Assert.True(confirmB.Result.Success, confirmB.Result.Message);

        var result = await BuildJockeyService(f).GetAssignedRacesAsync(jockeyUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        var races = Assert.IsAssignableFrom<IEnumerable<JockeyAssignedRaceResponse>>(result.Result.Data).ToList();
        Assert.Equal(2, races.Count);
        Assert.Contains(races, r => r.RaceId == raceAId); // historical, Finished Tournament
        Assert.Contains(races, r => r.RaceId == raceBId); // active, Published Tournament
    }

    private static HorseService BuildHorseService(RaceLifecycleTests.LifecycleFixture f)
        => new(
            new HorseRepository(f.Db),
            new OwnerRepository(f.Db),
            new JockeyRepository(f.Db),
            new RaceRepository(f.Db),
            new RaceEntryRepository(f.Db),
            new JockeyInvitationRepository(f.Db),
            f.UnitOfWork,
            new NoopNotificationService(),
            f.Db);

    private static JockeyService BuildJockeyService(RaceLifecycleTests.LifecycleFixture f)
        => new(
            new UserRepository(f.Db),
            new JockeyRepository(f.Db),
            new JockeyInvitationRepository(f.Db),
            new RaceEntryRepository(f.Db),
            new RaceRepository(f.Db),
            f.UnitOfWork,
            new NoopNotificationService());

    private static async Task<(Guid ownerUserId, Guid ownerId, Guid horseId)> CreateApprovedOwnerHorseAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var ownerUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"owner-{tag}-{suffix}@test.local",
            PasswordHash = "x",
            FullName = $"Owner {tag}",
            Role = UserRole.HorseOwner,
            IsActive = true
        };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUser.Id, OwnerCode = $"OWN-{suffix.Substring(0, 8)}" };
        var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse {tag}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };

        f.Db.AddRange(ownerUser, owner, horse);
        await f.Db.SaveChangesAsync();
        return (ownerUser.Id, owner.Id, horse.Id);
    }

    private static async Task<(Guid jockeyUserId, Guid jockeyId)> CreateJockeyAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag, ApprovalStatus approvalStatus, bool userActive = true)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var jockeyUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"jockey-{tag}-{suffix}@test.local",
            PasswordHash = "x",
            FullName = $"Jockey {tag}",
            Role = UserRole.Jockey,
            IsActive = userActive
        };
        var jockey = new Jockey
        {
            Id = Guid.NewGuid(),
            UserId = jockeyUser.Id,
            LicenseNumber = $"LIC-{suffix.Substring(0, 8)}",
            ApprovalStatus = approvalStatus
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
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            OwnerId = ownerId,
            HorseId = horseId,
            Status = status,
            ApprovedAt = status == RegistrationStatus.Approved ? DateTime.UtcNow : null
        });
        await f.Db.SaveChangesAsync();
    }

    private static async Task<(Guid tournamentId, Guid raceId)> CreateRaceAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag, DateTime? scheduledAt = null, DateTime? scheduledEndAt = null)
    {
        var start = scheduledAt ?? DateTime.UtcNow.AddDays(10);
        var end = scheduledEndAt ?? start.AddMinutes(60);
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = $"Tournament {tag}",
            StartDate = start.Date,
            EndDate = start.Date.AddDays(3),
            Status = TournamentStatus.Published,
            IsActive = true,
            MaxRounds = 1,
            MaxParticipants = 8,
            RegistrationDeadline = start.Date.AddDays(-1)
        };
        var round = new Round
        {
            Id = Guid.NewGuid(),
            Name = $"Round {tag}",
            TournamentId = tournament.Id,
            RoundNumber = 1,
            AdvanceCount = 1,
            ScheduledStartDate = start,
            ScheduledEndDate = end
        };
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = $"Race {tag}",
            TournamentId = tournament.Id,
            RoundId = round.Id,
            ScheduledAt = start,
            ScheduledEndAt = end,
            Status = RaceStatus.Scheduled,
            MaxParticipants = 8,
            Distance = 1200
        };

        f.Db.AddRange(tournament, round, race);
        await f.Db.SaveChangesAsync();
        return (tournament.Id, race.Id);
    }

    private static async Task<(Guid ownerUserId, Guid ownerId, Guid horseId, Guid raceId)> CreateAssignedHorseAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag, DateTime? scheduledAt = null, DateTime? scheduledEndAt = null)
    {
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, tag);
        var (tournamentId, raceId) = await CreateRaceAsync(f, tag, scheduledAt, scheduledEndAt);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);
        return (ownerUserId, ownerId, horseId, raceId);
    }

    /// <summary>Inserts a RaceEntry directly with JockeyId already set — simulates the shape a
    /// future Qualification Round would produce for a Horse that already has a Tournament pairing
    /// (no new invitation, no new Owner Final Confirm; see J3 §7).</summary>
    private static async Task<Guid> CreateRawRaceEntryWithJockeyAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid raceId, Guid horseId, Guid jockeyId)
    {
        // O1: any valid Admin-lineage RaceEntry has OwnerConfirmed = true — Owner consent already
        // happened at Tournament registration.
        var entry = new RaceEntry
        {
            Id = Guid.NewGuid(),
            RaceId = raceId,
            HorseId = horseId,
            JockeyId = jockeyId,
            Status = RegistrationStatus.Approved,
            OwnerConfirmed = true,
            JockeyConfirmed = true
        };
        f.Db.RaceEntries.Add(entry);
        await f.Db.SaveChangesAsync();
        return entry.Id;
    }

    /// <summary>Creates an additional Race inside the SAME Tournament + Round1 as an existing Race —
    /// a Round may hold multiple Races (heats), and only Round1 is eligible for direct
    /// AssignHorseToRaceAsync.</summary>
    private static async Task<Guid> CreateAdditionalRaceInSameTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid existingRaceId, string tag,
        DateTime scheduledAt, DateTime scheduledEndAt)
    {
        var existingRace = await f.Db.Races.SingleAsync(r => r.Id == existingRaceId);
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = $"Race {tag}",
            TournamentId = existingRace.TournamentId,
            RoundId = existingRace.RoundId,
            ScheduledAt = scheduledAt,
            ScheduledEndAt = scheduledEndAt,
            Status = RaceStatus.Scheduled,
            MaxParticipants = 8,
            Distance = 1200
        };
        f.Db.Races.Add(race);
        await f.Db.SaveChangesAsync();
        return race.Id;
    }

    private static async Task<(Guid jockeyUserId, Guid jockeyId, Guid invitationId)> InviteAndAcceptAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid ownerUserId, Guid horseId, Guid raceId, string tag)
    {
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, tag, ApprovalStatus.Approved);
        var invitationId = await InviteAndAcceptExistingJockeyAsync(f, ownerUserId, horseId, raceId, jockeyId, jockeyUserId);
        return (jockeyUserId, jockeyId, invitationId);
    }

    private static async Task<Guid> InviteAndAcceptExistingJockeyAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid ownerUserId, Guid horseId, Guid raceId, Guid jockeyId, Guid jockeyUserId)
    {
        var invite = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId,
            new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        var accept = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(accept.Result.Success, accept.Result.Message);
        return invitation.Id;
    }

    private sealed class NoopNotificationService : INotificationService
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
