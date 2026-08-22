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
/// J3.1: once Owner Final Confirm establishes RaceEntry.JockeyId for a Horse in a Tournament,
/// that pairing must be immutable via invitation-side actions (Owner RemoveJockeyAsync, Jockey
/// WithdrawInvitationAsync/RespondInvitationAsync-reject, legacy DeclineRaceEntryAsync). Official-
/// ness is always determined from RaceEntry.JockeyId — an Accepted invitation never changes
/// status after Final Confirm, so Status alone can never be trusted to detect it.
/// </summary>
public class J31OfficialPairingImmutabilityTests
{
    // ── A: Owner cannot RemoveJockeyAsync the official invitation ──────────────────────────────

    [Fact]
    public async Task RemoveJockey_OnOfficialInvitation_Returns409_LeavesOfficialPairingUntouched()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j31-remove-official");
        var (_, jockeyId, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j31-remove-official");

        var horseService = BuildHorseService(f);
        var confirm = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });
        Assert.True(confirm.Result.Success, confirm.Result.Message);

        var remove = await horseService.RemoveJockeyAsync(ownerUserId, horseId, raceId,
            new JockeyRemovalRequest { InvitationId = invitationId, Reason = "Muốn đổi kỵ sĩ" });

        Assert.False(remove.Result.Success);
        Assert.Equal(409, remove.StatusCode);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Equal(jockeyId, entry.JockeyId);
        Assert.True(entry.JockeyConfirmed);
    }

    // ── B: Jockey cannot WithdrawInvitationAsync the official invitation ───────────────────────

    [Fact]
    public async Task WithdrawInvitation_OnOfficialInvitation_Returns409_LeavesOfficialPairingUntouched()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j31-withdraw-official");
        var (jockeyUserId, jockeyId, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j31-withdraw-official");

        var horseService = BuildHorseService(f);
        var confirm = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });
        Assert.True(confirm.Result.Success, confirm.Result.Message);

        var withdraw = await BuildJockeyService(f).WithdrawInvitationAsync(jockeyUserId, invitationId,
            new JockeyInvitationWithdrawRequest { Reason = "Bận việc riêng" });

        Assert.False(withdraw.Result.Success);
        Assert.Equal(409, withdraw.StatusCode);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Equal(jockeyId, entry.JockeyId);
        Assert.True(entry.JockeyConfirmed);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationId);
        Assert.Equal(JockeyInvitationStatus.Accepted, invitation.Status); // never touched
    }

    // ── C: rejecting a Pending invitation never touches a historical Finished-Tournament pairing ─

    [Fact]
    public async Task RejectPendingInvitation_SameJockeyDifferentTournament_LeavesHistoricalOfficialPairingUntouched()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        // Tournament X: Jockey A becomes official for Horse H, then Tournament X concludes.
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "j31-reject-history");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "j31-reject-history-jockey", ApprovalStatus.Approved);
        var (tournamentXId, raceXId) = await CreateRaceAsync(f, "j31-reject-history-x");
        await RegisterTournamentAsync(f, tournamentXId, ownerId, horseId, RegistrationStatus.Approved);
        var assignX = await f.RaceManagement.AssignHorseToRaceAsync(raceXId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assignX.Result.Success, assignX.Result.Message);
        var invitationXId = await InviteAndAcceptExistingJockeyAsync(f, ownerUserId, horseId, raceXId, jockeyId, jockeyUserId);
        var confirmX = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceXId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationXId });
        Assert.True(confirmX.Result.Success, confirmX.Result.Message);

        var tournamentX = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentXId);
        tournamentX.Status = TournamentStatus.Finished;
        var raceX = await f.Db.Races.SingleAsync(r => r.Id == raceXId);
        raceX.Status = RaceStatus.Finished; // must also be Finished so IsHorseInActiveRaceAsync frees the Horse
        await f.Db.SaveChangesAsync();

        // Tournament Y (still open): same Horse, same Jockey A gets a fresh Pending invitation,
        // which the Jockey then rejects.
        var (tournamentYId, raceYId) = await CreateRaceAsync(f, "j31-reject-history-y");
        await RegisterTournamentAsync(f, tournamentYId, ownerId, horseId, RegistrationStatus.Approved);
        var assignY = await f.RaceManagement.AssignHorseToRaceAsync(raceYId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assignY.Result.Success, assignY.Result.Message);
        var inviteY = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId,
            new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceYId });
        Assert.True(inviteY.Result.Success, inviteY.Result.Message);
        var invitationY = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceYId);

        var reject = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitationY.Id,
            new JockeyInvitationRespondRequest { Accept = false });

        Assert.True(reject.Result.Success, reject.Result.Message);
        var invitationYAfter = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationY.Id);
        Assert.Equal(JockeyInvitationStatus.Declined, invitationYAfter.Status);

        // The historical Tournament X pairing must remain fully intact.
        var entryX = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceXId && e.HorseId == horseId);
        Assert.Equal(jockeyId, entryX.JockeyId);
        Assert.True(entryX.JockeyConfirmed);
    }

    // ── D: rejecting a Pending invitation never clears any RaceEntry.JockeyId ───────────────────

    [Fact]
    public async Task RejectPendingInvitation_NeverClearsRaceEntryJockeyId()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j31-reject-basic");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "j31-reject-basic-jockey", ApprovalStatus.Approved);
        var invite = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId,
            new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        var reject = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitation.Id,
            new JockeyInvitationRespondRequest { Accept = false });

        Assert.True(reject.Result.Success, reject.Result.Message);
        var invitationAfter = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Declined, invitationAfter.Status);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.JockeyConfirmed);
    }

    // ── E: a non-official Accepted invitation can still be withdrawn ───────────────────────────

    [Fact]
    public async Task WithdrawInvitation_OnNonOfficialAcceptedInvitation_StillSucceeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j31-withdraw-nonofficial");
        var (_, chosenJockeyId, chosenInvitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j31-withdraw-chosen");
        var (otherJockeyUserId, otherJockeyId, otherInvitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j31-withdraw-other");

        var horseService = BuildHorseService(f);
        var confirm = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = chosenInvitationId });
        Assert.True(confirm.Result.Success, confirm.Result.Message);

        var withdraw = await BuildJockeyService(f).WithdrawInvitationAsync(otherJockeyUserId, otherInvitationId,
            new JockeyInvitationWithdrawRequest { Reason = "Không còn rảnh" });

        Assert.True(withdraw.Result.Success, withdraw.Result.Message);
        var otherInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == otherInvitationId);
        Assert.Equal(JockeyInvitationStatus.Withdrawn, otherInvitation.Status);
        // Official pairing (the OTHER jockey) is untouched.
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Equal(chosenJockeyId, entry.JockeyId);
    }

    // ── F: Owner can still remove a non-official Pending/Accepted invitation ───────────────────

    [Fact]
    public async Task RemoveJockey_OnNonOfficialAcceptedInvitation_StillSucceeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j31-remove-nonofficial");
        var (_, chosenJockeyId, chosenInvitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j31-remove-chosen");
        var (_, otherJockeyId, otherInvitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j31-remove-other");

        var horseService = BuildHorseService(f);
        var confirm = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = chosenInvitationId });
        Assert.True(confirm.Result.Success, confirm.Result.Message);

        var remove = await horseService.RemoveJockeyAsync(ownerUserId, horseId, raceId,
            new JockeyRemovalRequest { InvitationId = otherInvitationId, Reason = "Không cần nữa" });

        Assert.True(remove.Result.Success, remove.Result.Message);
        var otherInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == otherInvitationId);
        Assert.Equal(JockeyInvitationStatus.Declined, otherInvitation.Status);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Equal(chosenJockeyId, entry.JockeyId); // official pairing untouched
    }

    // ── G: legacy DeclineRaceEntryAsync no longer clears an official pairing ───────────────────

    [Fact]
    public async Task DeclineRaceEntry_Legacy_OnOfficialPairing_Returns409_LeavesItUntouched()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j31-legacy-decline");
        var (jockeyUserId, jockeyId, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j31-legacy-decline");

        var horseService = BuildHorseService(f);
        var confirm = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });
        Assert.True(confirm.Result.Success, confirm.Result.Message);

        var entryId = (await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId)).Id;
        var decline = await BuildJockeyService(f).DeclineRaceEntryAsync(jockeyUserId, entryId);

        Assert.False(decline.Result.Success);
        Assert.Equal(409, decline.StatusCode);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.Id == entryId);
        Assert.Equal(jockeyId, entry.JockeyId);
        Assert.True(entry.JockeyConfirmed);
    }

    // ── H: Final Confirm Jockey B is still rejected once Jockey A is official ──────────────────

    [Fact]
    public async Task FinalConfirm_JockeyB_AfterJockeyAOfficial_StillReturnsConflict()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j31-still-conflict");
        var (_, jockeyAId, invitationAId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j31-still-conflict-a");
        var (_, _, invitationBId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j31-still-conflict-b");

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(confirmB.Result.Success);
        Assert.Equal(409, confirmB.StatusCode);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Equal(jockeyAId, entry.JockeyId);
    }

    // ── shared helpers (mirrors J3OwnerFinalConfirmTests' private helpers) ─────────────────────

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
        RaceLifecycleTests.LifecycleFixture f, string tag, ApprovalStatus approvalStatus)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var jockeyUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"jockey-{tag}-{suffix}@test.local",
            PasswordHash = "x",
            FullName = $"Jockey {tag}",
            Role = UserRole.Jockey,
            IsActive = true
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
        RaceLifecycleTests.LifecycleFixture f, string tag,
        TournamentStatus tournamentStatus = TournamentStatus.Published,
        RaceStatus raceStatus = RaceStatus.Scheduled)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddMinutes(60);
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = $"Tournament {tag}",
            StartDate = start.Date,
            EndDate = start.Date.AddDays(3),
            Status = tournamentStatus,
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
            Status = raceStatus,
            MaxParticipants = 8,
            Distance = 1200
        };

        f.Db.AddRange(tournament, round, race);
        await f.Db.SaveChangesAsync();
        return (tournament.Id, race.Id);
    }

    private static async Task<(Guid ownerUserId, Guid ownerId, Guid horseId, Guid raceId)> CreateAssignedHorseAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, tag);
        var (tournamentId, raceId) = await CreateRaceAsync(f, tag);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);
        return (ownerUserId, ownerId, horseId, raceId);
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
