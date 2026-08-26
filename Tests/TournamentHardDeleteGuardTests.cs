using System;
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
/// T-D1/T-D2: hard delete (DELETE /api/tournaments/{id} -> TournamentService.DeleteTournamentAsync)
/// only ever succeeds for Tournament.Status == Draft or Cancelled. Published/Ongoing/Finished are
/// historical/auditable data and the destructive delete transaction (RaceDeletionHelper ->
/// TournamentHorseRegistrations -> Prizes -> Rounds -> Tournament row) must never run for them.
/// The guard is checked before any query touches the Race/RaceEntry/etc. graph, so a rejected
/// delete leaves every row exactly as it was — no partial cascade. T-D2 additionally blocks
/// deleting a Cancelled Tournament that still has an unresolved (Pending) Prediction/stake, since
/// RaceDeletionHelper hard-deletes Predictions unconditionally with no refund.
/// </summary>
public class TournamentHardDeleteGuardTests
{
    [Fact]
    public async Task DeleteTournament_Draft_Succeeds_AndRemovesDependentGraph()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, raceId) = await CreateTournamentWithRaceAsync(f, TournamentStatus.Draft);

        var result = await f.TournamentSvc.DeleteTournamentAsync(tournamentId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.False(await f.Db.Tournaments.AnyAsync(t => t.Id == tournamentId));
        Assert.False(await f.Db.Rounds.AnyAsync(r => r.Id == roundId));
        Assert.False(await f.Db.Races.AnyAsync(r => r.Id == raceId));
    }

    [Fact]
    public async Task DeleteTournament_Cancelled_Succeeds_AndRemovesDependentGraph()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, raceId) = await CreateTournamentWithRaceAsync(f, TournamentStatus.Cancelled);

        var result = await f.TournamentSvc.DeleteTournamentAsync(tournamentId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.False(await f.Db.Tournaments.AnyAsync(t => t.Id == tournamentId));
        Assert.False(await f.Db.Rounds.AnyAsync(r => r.Id == roundId));
        Assert.False(await f.Db.Races.AnyAsync(r => r.Id == raceId));
    }

    [Fact]
    public async Task DeleteTournament_Cancelled_WithSettledPrediction_StillSucceeds_AndCascadeRemovesIt()
    {
        // A non-Pending (already settled/refunded) Prediction carries no unresolved stake, so it
        // does not block deletion — and is removed by RaceDeletionHelper's existing cascade like
        // every other Race-scoped row.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, raceId) = await CreateTournamentWithRaceAsync(f, TournamentStatus.Cancelled);
        var (spectatorId, _) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(raceId, spectatorId, Guid.NewGuid(), betAmount: 50m, odds: 2m);
        var prediction = await f.Db.Predictions.SingleAsync(p => p.RaceId == raceId);
        prediction.Status = PredictionStatus.Lost; // refund/settlement already resolved this one
        await f.Db.SaveChangesAsync();

        var result = await f.TournamentSvc.DeleteTournamentAsync(tournamentId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.False(await f.Db.Tournaments.AnyAsync(t => t.Id == tournamentId));
        Assert.False(await f.Db.Predictions.AnyAsync(p => p.RaceId == raceId));
    }

    [Fact]
    public async Task DeleteTournament_Cancelled_WithPendingPrediction_ReturnsConflict_NothingDeleted()
    {
        // T-D2 financial safety: ChangeStatusAsync's Cancelled branch never refunds Predictions
        // (it only bulk-flips Race.Status), so a Cancelled Tournament can still carry a real
        // Pending stake. RaceDeletionHelper would hard-delete it unconditionally with no refund —
        // the delete must be rejected instead of silently destroying that stake.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, raceId) = await CreateTournamentWithRaceAsync(f, TournamentStatus.Cancelled);
        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(raceId, spectatorId, Guid.NewGuid(), betAmount: 75m, odds: 2m);

        var result = await f.TournamentSvc.DeleteTournamentAsync(tournamentId);

        Assert.False(result.Result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.True(await f.Db.Tournaments.AnyAsync(t => t.Id == tournamentId));
        Assert.True(await f.Db.Rounds.AnyAsync(r => r.Id == roundId));
        Assert.True(await f.Db.Races.AnyAsync(r => r.Id == raceId));
        var prediction = await f.Db.Predictions.SingleAsync(p => p.RaceId == raceId);
        Assert.Equal(PredictionStatus.Pending, prediction.Status);
        Assert.Equal(walletBefore, await f.GetWalletBalanceAsync(spectatorId)); // stake untouched, not silently lost
    }

    [Theory]
    [InlineData(TournamentStatus.Published)]
    [InlineData(TournamentStatus.Ongoing)]
    public async Task DeleteTournament_PublishedOrOngoing_ReturnsConflict_TournamentRemains(TournamentStatus status)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, raceId) = await CreateTournamentWithRaceAsync(f, status);

        var result = await f.TournamentSvc.DeleteTournamentAsync(tournamentId);

        Assert.False(result.Result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.True(await f.Db.Tournaments.AnyAsync(t => t.Id == tournamentId));
        Assert.True(await f.Db.Rounds.AnyAsync(r => r.Id == roundId));
        Assert.True(await f.Db.Races.AnyAsync(r => r.Id == raceId));
    }

    [Fact]
    public async Task DeleteTournament_Finished_WithHistoricalOfficialPairing_ReturnsConflict_NoPartialCascade()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var scenario = await CreateHistoricalScenarioAsync(f, TournamentStatus.Finished);

        var result = await f.TournamentSvc.DeleteTournamentAsync(scenario.TournamentId);

        Assert.False(result.Result.Success);
        Assert.Equal(409, result.StatusCode);

        // Every row the destructive path would have touched, in the exact order it would touch
        // them, must still be present untouched — proves the guard rejected BEFORE any deletion,
        // not merely rolled back mid-way.
        Assert.True(await f.Db.Tournaments.AnyAsync(t => t.Id == scenario.TournamentId));
        Assert.True(await f.Db.TournamentHorseRegistrations.AnyAsync(r => r.TournamentId == scenario.TournamentId));
        Assert.True(await f.Db.Rounds.AnyAsync(r => r.Id == scenario.RoundId));
        Assert.True(await f.Db.Races.AnyAsync(r => r.Id == scenario.RaceId));
        Assert.True(await f.Db.JockeyInvitations.AnyAsync(i => i.HorseId == scenario.HorseId && i.RaceId == scenario.RaceId));
        var entry = await f.Db.RaceEntries.SingleOrDefaultAsync(e => e.Id == scenario.RaceEntryId);
        Assert.NotNull(entry);
        Assert.Equal(scenario.JockeyId, entry!.JockeyId); // official Horse/Jockey history intact

        // And it stays queryable through the same read path Jockey Schedule uses.
        var jockeyEntries = await new RaceEntryRepository(f.Db).GetByJockeyAsync(scenario.JockeyId);
        Assert.Contains(jockeyEntries, e => e.Id == scenario.RaceEntryId);
    }

    private sealed record HistoricalScenario(
        Guid TournamentId, Guid RoundId, Guid RaceId, Guid HorseId, Guid RaceEntryId, Guid JockeyId);

    private static async Task<(Guid TournamentId, Guid RoundId, Guid RaceId)> CreateTournamentWithRaceAsync(
        RaceLifecycleTests.LifecycleFixture f, TournamentStatus status)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = $"T-D1-{status}-{Guid.NewGuid():N}", StartDate = start.Date, EndDate = start.Date.AddDays(3),
            Status = status, IsActive = status != TournamentStatus.Cancelled, MaxRounds = 1, MaxParticipants = 8,
            RegistrationDeadline = start.Date.AddDays(-1)
        };
        var round = new Round
        {
            Id = Guid.NewGuid(), Name = "Round 1", TournamentId = tournament.Id, RoundNumber = 1, AdvanceCount = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddHours(2)
        };
        var race = new Race
        {
            Id = Guid.NewGuid(), Name = "Race 1", TournamentId = tournament.Id, RoundId = round.Id,
            ScheduledAt = start, ScheduledEndAt = start.AddMinutes(60), Status = RaceStatus.Scheduled,
            MaxParticipants = 8, Distance = 1200
        };
        f.Db.AddRange(tournament, round, race);
        await f.Db.SaveChangesAsync();
        return (tournament.Id, round.Id, race.Id);
    }

    /// <summary>
    /// Builds a full Tournament -> Round -> Race -> RaceEntry -> Horse -> official JockeyId
    /// pairing via the normal Owner invite/accept/Final-Confirm flow while the Tournament is still
    /// Published (Final Confirm's own lifecycle guard requires that), then transitions the
    /// Tournament (and, for Finished, the Race) to <paramref name="finalStatus"/> afterward —
    /// mirroring how a real Tournament naturally becomes historical after the pairing was
    /// already established.
    /// </summary>
    private static async Task<HistoricalScenario> CreateHistoricalScenarioAsync(
        RaceLifecycleTests.LifecycleFixture f, TournamentStatus finalStatus)
    {
        var (tournamentId, roundId, raceId) = await CreateTournamentWithRaceAsync(f, TournamentStatus.Published);

        var suffix = Guid.NewGuid().ToString("N");
        var ownerUser = new User { Id = Guid.NewGuid(), Email = $"owner-{suffix}@test.local", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner, IsActive = true };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUser.Id, OwnerCode = $"OWN-{suffix.Substring(0, 8)}" };
        var horse = new Horse { Id = Guid.NewGuid(), Name = "Historical Horse", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
        var jockeyUser = new User { Id = Guid.NewGuid(), Email = $"jockey-{suffix}@test.local", PasswordHash = "x", FullName = "Jockey", Role = UserRole.Jockey, IsActive = true };
        var jockey = new Jockey { Id = Guid.NewGuid(), UserId = jockeyUser.Id, LicenseNumber = $"LIC-{suffix.Substring(0, 8)}", ApprovalStatus = ApprovalStatus.Approved };
        f.Db.AddRange(ownerUser, owner, horse, jockeyUser, jockey);
        await f.Db.SaveChangesAsync();

        f.Db.TournamentHorseRegistrations.Add(new TournamentHorseRegistration
        {
            Id = Guid.NewGuid(), TournamentId = tournamentId, OwnerId = owner.Id, HorseId = horse.Id,
            Status = RegistrationStatus.Approved, ApprovedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horse.Id });
        Assert.True(assign.Result.Success, assign.Result.Message);

        var horseService = BuildHorseService(f);
        var jockeyService = BuildJockeyService(f);

        var invite = await horseService.InviteJockeyAsync(ownerUser.Id, horse.Id,
            new JockeyInvitationCreateRequest { JockeyId = jockey.Id, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horse.Id && i.JockeyId == jockey.Id);
        var accept = await jockeyService.RespondInvitationAsync(jockeyUser.Id, invitation.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(accept.Result.Success, accept.Result.Message);

        var confirm = await horseService.FinalConfirmJockeyAsync(ownerUser.Id, horse.Id, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitation.Id });
        Assert.True(confirm.Result.Success, confirm.Result.Message);

        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horse.Id);

        var tournament = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentId);
        tournament.Status = finalStatus;
        if (finalStatus == TournamentStatus.Cancelled) tournament.IsActive = false;
        if (finalStatus == TournamentStatus.Finished)
        {
            var race = await f.Db.Races.SingleAsync(r => r.Id == raceId);
            race.Status = RaceStatus.Finished;
        }
        await f.Db.SaveChangesAsync();

        return new HistoricalScenario(tournamentId, roundId, raceId, horse.Id, entry.Id, jockey.Id);
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

    private sealed class NoopNotificationService : HorseRacing.Services.Interfaces.INotificationService
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
