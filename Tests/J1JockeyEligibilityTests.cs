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

public class J1JockeyEligibilityTests
{
    [Fact]
    public async Task AdminAssignHorse_RoundOneCreatesEntryWithoutJockeyOrConfirmations()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "admin-null");
        var (tournamentId, raceId) = await CreateRaceAsync(f, "admin-null", roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest
        {
            HorseId = horseId
        });

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(201, result.StatusCode);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.OwnerConfirmed);
        Assert.False(entry.JockeyConfirmed);
        Assert.Equal(RegistrationStatus.Approved, entry.Status);
        Assert.NotEqual(Guid.Empty, ownerUserId);
    }

    [Fact]
    public async Task AdminAssignHorse_LegacyJockeyIdIsIgnored()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "legacy-jockey");
        var (_, jockeyId) = await CreateJockeyAsync(f, "legacy-jockey", ApprovalStatus.Approved);
        var (tournamentId, raceId) = await CreateRaceAsync(f, "legacy-jockey", roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest
        {
            HorseId = horseId,
            JockeyId = jockeyId
        });

        Assert.True(result.Result.Success, result.Result.Message);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.OwnerConfirmed);
        Assert.False(entry.JockeyConfirmed);
    }

    [Fact]
    public async Task AdminAssignHorse_DuplicateRaceEntryStillRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "duplicate");
        var (tournamentId, raceId) = await CreateRaceAsync(f, "duplicate", roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var first = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(first.Result.Success, first.Result.Message);

        var duplicate = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.False(duplicate.Result.Success);
        Assert.Equal(400, duplicate.StatusCode);
        Assert.Equal(1, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceId && e.HorseId == horseId));
    }

    [Fact]
    public async Task AdminAssignHorse_RoundGreaterThanOneStillRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "round-two");
        var (tournamentId, raceId) = await CreateRaceAsync(f, "round-two", roundNumber: 2);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await f.Db.RaceEntries.Where(e => e.RaceId == raceId && e.HorseId == horseId).ToListAsync());
    }

    [Fact]
    public async Task AvailableJockeys_OnlyApprovedJockeyAppears()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, _) = await CreateApprovedOwnerHorseAsync(f, "availability-owner");
        var (_, approvedJockeyId) = await CreateJockeyAsync(f, "availability-approved", ApprovalStatus.Approved);
        var (_, pendingJockeyId) = await CreateJockeyAsync(f, "availability-pending", ApprovalStatus.Pending);
        var (_, rejectedJockeyId) = await CreateJockeyAsync(f, "availability-rejected", ApprovalStatus.Rejected);

        var result = await BuildJockeyService(f).GetAvailableJockeysAsync(ownerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        var jockeys = Assert.IsAssignableFrom<IEnumerable<JockeyListResponse>>(result.Result.Data);
        var ids = jockeys.Select(jockey => jockey.Id).ToHashSet();
        Assert.Contains(approvedJockeyId, ids);
        Assert.DoesNotContain(pendingJockeyId, ids);
        Assert.DoesNotContain(rejectedJockeyId, ids);
    }

    [Theory]
    [InlineData(ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task OwnerInviteJockey_UnapprovedJockeyRejected(ApprovalStatus approvalStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, $"invite-{approvalStatus}");
        var (_, jockeyId) = await CreateJockeyAsync(f, $"invite-{approvalStatus}", approvalStatus);
        var (tournamentId, raceId) = await CreateRaceAsync(f, $"invite-{approvalStatus}", roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await f.Db.JockeyInvitations.Where(i => i.HorseId == horseId && i.JockeyId == jockeyId).ToListAsync());
    }

    [Fact]
    public async Task OwnerInviteJockey_InactiveApprovedJockeyRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "invite-inactive-approved");
        var (_, jockeyId) = await CreateJockeyAsync(f, "invite-inactive-approved", ApprovalStatus.Approved, userActive: false);
        var (tournamentId, raceId) = await CreateRaceAsync(f, "invite-inactive-approved", roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await f.Db.JockeyInvitations.Where(i => i.HorseId == horseId && i.JockeyId == jockeyId).ToListAsync());
    }
    [Theory]
    [InlineData(ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task JockeyRespondInvitation_UnapprovedJockeyCannotAcceptNewInvitation(ApprovalStatus approvalStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, $"respond-{approvalStatus}");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, $"respond-{approvalStatus}", approvalStatus);
        var invitation = new JockeyInvitation
        {
            Id = Guid.NewGuid(),
            HorseId = horseId,
            JockeyId = jockeyId,
            RaceId = raceId,
            Status = JockeyInvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        f.Db.JockeyInvitations.Add(invitation);
        await f.Db.SaveChangesAsync();

        var result = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest
        {
            Accept = true
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        var storedInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Pending, storedInvitation.Status);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.JockeyConfirmed);
    }

    [Fact]
    public async Task JockeyRespondInvitation_InactiveApprovedJockeyCannotAccept()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "respond-inactive-approved");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "respond-inactive-approved", ApprovalStatus.Approved, userActive: false);
        var invitation = new JockeyInvitation
        {
            Id = Guid.NewGuid(),
            HorseId = horseId,
            JockeyId = jockeyId,
            RaceId = raceId,
            Status = JockeyInvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        f.Db.JockeyInvitations.Add(invitation);
        await f.Db.SaveChangesAsync();

        var result = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest
        {
            Accept = true
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        var storedInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Pending, storedInvitation.Status);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.JockeyConfirmed);
    }
    [Fact]
    public async Task ApprovedJockey_ExistingInvitationFlowStillWorks()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "approved-flow");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "approved-flow", ApprovalStatus.Approved);

        var invite = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId,
            Message = "Ready to race"
        });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        var accept = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest
        {
            Accept = true
        });

        Assert.True(accept.Result.Success, accept.Result.Message);
        var storedInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Accepted, storedInvitation.Status);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Equal(jockeyId, entry.JockeyId);
        Assert.True(entry.JockeyConfirmed);
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
            f.UnitOfWork,
            new NoopNotificationService());

    private static async Task<(Guid ownerUserId, Guid ownerId, Guid horseId)> CreateApprovedOwnerHorseAsync(
        RaceLifecycleTests.LifecycleFixture f,
        string tag)
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
        var owner = new Owner
        {
            Id = Guid.NewGuid(),
            UserId = ownerUser.Id,
            OwnerCode = $"OWN-{suffix.Substring(0, 8)}"
        };
        var horse = new Horse
        {
            Id = Guid.NewGuid(),
            Name = $"Horse {tag}",
            OwnerId = owner.Id,
            ApprovalStatus = ApprovalStatus.Approved
        };

        f.Db.AddRange(ownerUser, owner, horse);
        await f.Db.SaveChangesAsync();
        return (ownerUser.Id, owner.Id, horse.Id);
    }

    private static async Task<(Guid jockeyUserId, Guid jockeyId)> CreateJockeyAsync(
        RaceLifecycleTests.LifecycleFixture f,
        string tag,
        ApprovalStatus approvalStatus,
        bool userActive = true)
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

    private static async Task<(Guid tournamentId, Guid raceId)> CreateRaceAsync(
        RaceLifecycleTests.LifecycleFixture f,
        string tag,
        int roundNumber)
    {
        var start = DateTime.UtcNow.AddDays(10 + roundNumber);
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = $"Tournament {tag}",
            StartDate = start.Date,
            EndDate = start.Date.AddDays(3),
            Status = TournamentStatus.Draft,
            IsActive = true,
            MaxRounds = Math.Max(1, roundNumber),
            MaxParticipants = 8,
            RegistrationDeadline = start.Date.AddDays(-1)
        };
        var round = new Round
        {
            Id = Guid.NewGuid(),
            Name = $"Round {roundNumber}",
            TournamentId = tournament.Id,
            RoundNumber = roundNumber,
            AdvanceCount = roundNumber == 1 ? 1 : 0,
            ScheduledStartDate = start,
            ScheduledEndDate = start.AddHours(2)
        };
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = $"Race {tag}",
            TournamentId = tournament.Id,
            RoundId = round.Id,
            ScheduledAt = start.AddMinutes(10),
            ScheduledEndAt = start.AddMinutes(70),
            Status = RaceStatus.Scheduled,
            MaxParticipants = 8,
            Distance = 1200
        };

        f.Db.AddRange(tournament, round, race);
        await f.Db.SaveChangesAsync();
        return (tournament.Id, race.Id);
    }

    private static async Task RegisterTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f,
        Guid tournamentId,
        Guid ownerId,
        Guid horseId,
        RegistrationStatus status)
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

    private static async Task<(Guid ownerUserId, Guid ownerId, Guid horseId, Guid raceId)> CreateAssignedHorseForInvitationAsync(
        RaceLifecycleTests.LifecycleFixture f,
        string tag)
    {
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, tag);
        var (tournamentId, raceId) = await CreateRaceAsync(f, tag, roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);
        return (ownerUserId, ownerId, horseId, raceId);
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