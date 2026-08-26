using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// Task B0: Tournament cancellation must refund every Pending Prediction belonging to Races in
/// that Tournament — using the same refund convention (wallet credit + Prediction marked Lost)
/// as the pre-existing direct Race cancel path (RaceManagementService.CancelRaceAsync), and
/// atomically (Tournament status + Race cascade + refunds all commit together or not at all).
/// Wired against a real Sqlite in-memory DB and the actual production services, reusing
/// RaceLifecycleTests.LifecycleFixture (same convention as TournamentValidationTests).
/// </summary>
public class TournamentCancelRefundTests
{
    /// <summary>
    /// Adds a bare Race row directly to an existing Tournament/Round, without the full
    /// entry/health-check/referee scaffolding CreateReadyToStartRaceAsync builds — B0's refund
    /// logic only needs Race.TournamentId/Status and a Prediction pointing at it.
    /// </summary>
    private static Race MakeRace(Guid tournamentId, Guid roundId, RaceStatus status, string name)
        => new Race
        {
            Id = Guid.NewGuid(),
            Name = name,
            TournamentId = tournamentId,
            RoundId = roundId,
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            Status = status,
            MaxParticipants = 8,
            CreatedAt = DateTime.UtcNow
        };

    /// <summary>
    /// The Tournament-cancel Race cascade updates via bulk ExecuteUpdateAsync (see
    /// TournamentAndRoundService.cs), which bypasses the change tracker — same reason
    /// TournamentValidationTests.Cancel_CascadesToChildRaces_ByCurrentRaceStatus reads Race
    /// status via AsNoTracking instead of the (identity-map-cached, stale) repository.
    /// </summary>
    private static Task<RaceStatus> RaceStatusOfAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId)
        => f.Db.Races.AsNoTracking().Where(r => r.Id == raceId).Select(r => r.Status).FirstAsync();

    // ── A: single Race, single Pending Prediction ──────────────────────

    [Fact]
    public async Task CancelTournament_SingleRaceSinglePendingPrediction_RefundsExactly()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var tournamentId = (await f.RaceRepo.GetByIdAsync(race.Id))!.TournamentId;
        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, spectatorId, race.WinnerHorseId, betAmount: 50m, odds: 2m);

        var cancel = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "B0 test" },
            Guid.NewGuid());

        Assert.True(cancel.Result.Success, cancel.Result.Message);
        Assert.Equal(TournamentStatus.Cancelled, cancel.Result.Data!.Status);
        Assert.Equal(RaceStatus.Cancelled, await RaceStatusOfAsync(f, race.Id));

        // Fresh (AsNoTracking) read: PredictionRefundHelper claims via bulk ExecuteUpdateAsync,
        // which bypasses the change tracker — a tracked PredictionRepo read would return the
        // stale in-memory instance from AddPendingPredictionAsync.
        var prediction = (await f.GetPredictionsFreshAsync(race.Id)).Single();
        Assert.NotEqual(PredictionStatus.Pending, prediction.Status);
        Assert.Equal(PredictionStatus.Lost, prediction.Status); // refund marker, per existing convention
        Assert.NotNull(prediction.SettledAt);
        Assert.Equal(walletBefore + 50m, await f.GetWalletBalanceAsync(spectatorId));
    }

    // ── B: multiple Races, multiple Pending Predictions ─────────────────

    [Fact]
    public async Task CancelTournament_MultipleRacesMultiplePendingPredictions_AllRefundedOnce()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race1 = await f.CreateReadyToStartRaceAsync();
        var race1Entity = (await f.RaceRepo.GetByIdAsync(race1.Id))!;
        var tournamentId = race1Entity.TournamentId;

        var race2 = MakeRace(tournamentId, race1Entity.RoundId, RaceStatus.Scheduled, "Race2");
        var race3 = MakeRace(tournamentId, race1Entity.RoundId, RaceStatus.RegistrationOpen, "Race3");
        f.Db.AddRange(race2, race3);
        await f.Db.SaveChangesAsync();

        var (spectator1, balance1) = await f.CreateSpectatorWithWalletAsync(0m);
        var (spectator2, balance2) = await f.CreateSpectatorWithWalletAsync(0m);
        var (spectator3, balance3) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race1.Id, spectator1, race1.WinnerHorseId, betAmount: 10m, odds: 2m);
        await f.AddPendingPredictionAsync(race2.Id, spectator2, race1.WinnerHorseId, betAmount: 20m, odds: 2m);
        await f.AddPendingPredictionAsync(race3.Id, spectator3, race1.WinnerHorseId, betAmount: 30m, odds: 2m);

        var cancel = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "B0 test" },
            Guid.NewGuid());

        Assert.True(cancel.Result.Success, cancel.Result.Message);
        Assert.Equal(RaceStatus.Cancelled, await RaceStatusOfAsync(f, race1.Id));
        Assert.Equal(RaceStatus.Cancelled, await RaceStatusOfAsync(f, race2.Id));
        Assert.Equal(RaceStatus.Cancelled, await RaceStatusOfAsync(f, race3.Id));

        Assert.Equal(balance1 + 10m, await f.GetWalletBalanceAsync(spectator1));
        Assert.Equal(balance2 + 20m, await f.GetWalletBalanceAsync(spectator2));
        Assert.Equal(balance3 + 30m, await f.GetWalletBalanceAsync(spectator3));

        foreach (var raceId in new[] { race1.Id, race2.Id, race3.Id })
        {
            var p = (await f.GetPredictionsFreshAsync(raceId)).Single();
            Assert.Equal(PredictionStatus.Lost, p.Status);
        }
    }

    // ── C: same user, multiple Pending Predictions across races ─────────

    [Fact]
    public async Task CancelTournament_SameUserMultiplePendingPredictions_ReceivesSumOfStakes()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race1 = await f.CreateReadyToStartRaceAsync();
        var race1Entity = (await f.RaceRepo.GetByIdAsync(race1.Id))!;
        var tournamentId = race1Entity.TournamentId;

        var race2 = MakeRace(tournamentId, race1Entity.RoundId, RaceStatus.Scheduled, "Race2");
        f.Db.Add(race2);
        await f.Db.SaveChangesAsync();

        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race1.Id, spectatorId, race1.WinnerHorseId, betAmount: 40m, odds: 2m);
        await f.AddPendingPredictionAsync(race2.Id, spectatorId, race1.WinnerHorseId, betAmount: 60m, odds: 2m);

        var cancel = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "B0 test" },
            Guid.NewGuid());

        Assert.True(cancel.Result.Success, cancel.Result.Message);
        Assert.Equal(walletBefore + 100m, await f.GetWalletBalanceAsync(spectatorId));
    }

    // ── D: already-resolved Prediction must not be refunded ─────────────

    [Fact]
    public async Task CancelTournament_AlreadyWonOrLostPrediction_NotRefunded()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var tournamentId = (await f.RaceRepo.GetByIdAsync(race.Id))!.TournamentId;

        var (wonSpectator, wonBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        var (lostSpectator, lostBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, wonSpectator, race.WinnerHorseId, betAmount: 15m, odds: 2m);
        await f.AddPendingPredictionAsync(race.Id, lostSpectator, race.LoserHorseId, betAmount: 25m, odds: 2m);

        var wonPrediction = (await f.PredictionRepo.GetByRaceAsync(race.Id)).Single(p => p.SpectatorUserId == wonSpectator);
        var lostPrediction = (await f.PredictionRepo.GetByRaceAsync(race.Id)).Single(p => p.SpectatorUserId == lostSpectator);
        wonPrediction.Status = PredictionStatus.Won;
        wonPrediction.SettledAt = DateTime.UtcNow;
        lostPrediction.Status = PredictionStatus.Lost;
        lostPrediction.SettledAt = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();

        var cancel = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "B0 test" },
            Guid.NewGuid());

        Assert.True(cancel.Result.Success, cancel.Result.Message);
        Assert.Equal(wonBefore, await f.GetWalletBalanceAsync(wonSpectator));
        Assert.Equal(lostBefore, await f.GetWalletBalanceAsync(lostSpectator));
    }

    // ── E: mix of Pending + Won + Lost — only Pending refunded ───────────

    [Fact]
    public async Task CancelTournament_MixOfStatuses_OnlyPendingRefunded()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var tournamentId = (await f.RaceRepo.GetByIdAsync(race.Id))!.TournamentId;

        var (pendingSpectator, pendingBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        var (wonSpectator, wonBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        var (lostSpectator, lostBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, pendingSpectator, race.WinnerHorseId, betAmount: 12m, odds: 2m);
        await f.AddPendingPredictionAsync(race.Id, wonSpectator, race.WinnerHorseId, betAmount: 22m, odds: 2m);
        await f.AddPendingPredictionAsync(race.Id, lostSpectator, race.LoserHorseId, betAmount: 32m, odds: 2m);

        var predictions = await f.PredictionRepo.GetByRaceAsync(race.Id);
        predictions.Single(p => p.SpectatorUserId == wonSpectator).Status = PredictionStatus.Won;
        predictions.Single(p => p.SpectatorUserId == lostSpectator).Status = PredictionStatus.Lost;
        await f.Db.SaveChangesAsync();

        var cancel = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "B0 test" },
            Guid.NewGuid());

        Assert.True(cancel.Result.Success, cancel.Result.Message);
        Assert.Equal(pendingBefore + 12m, await f.GetWalletBalanceAsync(pendingSpectator));
        Assert.Equal(wonBefore, await f.GetWalletBalanceAsync(wonSpectator));
        Assert.Equal(lostBefore, await f.GetWalletBalanceAsync(lostSpectator));
    }

    // ── F: retry / idempotency ───────────────────────────────────────────

    [Fact]
    public async Task CancelTournament_RetryAfterSuccess_DoesNotDoubleRefund()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var tournamentId = (await f.RaceRepo.GetByIdAsync(race.Id))!.TournamentId;
        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, spectatorId, race.WinnerHorseId, betAmount: 100m, odds: 2m);

        var firstCancel = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "B0 test" },
            Guid.NewGuid());
        Assert.True(firstCancel.Result.Success, firstCancel.Result.Message);
        Assert.Equal(walletBefore + 100m, await f.GetWalletBalanceAsync(spectatorId));

        // Cancelled -> Cancelled is not a valid state transition (whitelist-only), so a naive
        // retry of the whole operation is rejected up front — the refund path never re-runs.
        var retry = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "B0 test retry" },
            Guid.NewGuid());
        Assert.False(retry.Result.Success);

        // wallet unchanged — no second refund.
        Assert.Equal(walletBefore + 100m, await f.GetWalletBalanceAsync(spectatorId));

        // Directly re-running the shared refund helper against the now-Lost Prediction (simulating
        // a lower-level retry) must also be a no-op — Status == Pending is the idempotency boundary.
        var raceIds = new[] { race.Id };
        var refundedAgain = await HorseRacing.Services.PredictionRefundHelper.RefundPendingPredictionsAsync(
            f.Db, f.FaultWallet, raceIds, "retry_test", swallowIndividualFailures: false);
        Assert.Equal(0, refundedAgain);
        Assert.Equal(walletBefore + 100m, await f.GetWalletBalanceAsync(spectatorId));
    }

    // ── G: direct Race cancellation regression ───────────────────────────

    [Fact]
    public async Task DirectRaceCancellation_StillRefundsExactlyAsBefore()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, spectatorId, race.WinnerHorseId, betAmount: 75m, odds: 2m);

        var cancel = await f.RaceManagement.CancelRaceAsync(race.Id);
        Assert.True(cancel.Result.Success, cancel.Result.Message);

        var prediction = (await f.GetPredictionsFreshAsync(race.Id)).Single();
        Assert.Equal(PredictionStatus.Lost, prediction.Status);
        Assert.Equal(walletBefore + 75m, await f.GetWalletBalanceAsync(spectatorId));
    }

    // ── H: refund failure inside the cancellation transaction rolls back everything,
    //      INCLUDING an already-executed successful wallet mutation for an earlier prediction ──

    [Fact]
    public async Task CancelTournament_SecondRefundFails_RollsBackFirstRefundAlreadyApplied()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var tournamentId = (await f.RaceRepo.GetByIdAsync(race.Id))!.TournamentId;

        // Prediction A (created first) and Prediction B (created second) — PredictionRefundHelper
        // processes candidates ordered by CreatedAt, so A's wallet credit executes and its
        // ExecuteUpdateAsync claim (Pending -> Lost) is issued against the DB — uncommitted, but
        // already applied on the connection — BEFORE B's refund is attempted and fails.
        var (userA, balanceA) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, userA, race.WinnerHorseId, betAmount: 100m, odds: 2m);
        await Task.Delay(5); // ensure CreatedAt orders strictly after A
        var (userB, balanceB) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, userB, race.LoserHorseId, betAmount: 200m, odds: 2m);

        // FaultWallet is the same IWalletService instance wired into both RaceManagement and
        // TournamentSvc for this fixture — only User B's refund fails; User A's must succeed
        // first for this test to actually prove rollback of an already-applied mutation.
        f.FaultWallet.FailForUserIds.Add(userB);

        var cancel = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "B0 test" },
            Guid.NewGuid());

        Assert.False(cancel.Result.Success, "Cancellation must fail when any refund fails, not silently swallow it.");
        Assert.Equal(500, cancel.StatusCode);

        var tournamentAfter = await f.Db.Tournaments.AsNoTracking().FirstAsync(t => t.Id == tournamentId);
        Assert.NotEqual(TournamentStatus.Cancelled, tournamentAfter.Status);
        Assert.NotEqual(RaceStatus.Cancelled, await RaceStatusOfAsync(f, race.Id));

        var predictions = await f.GetPredictionsFreshAsync(race.Id);
        var predictionA = predictions.Single(p => p.SpectatorUserId == userA);
        var predictionB = predictions.Single(p => p.SpectatorUserId == userB);
        Assert.Equal(PredictionStatus.Pending, predictionA.Status);
        Assert.Null(predictionA.SettledAt);
        Assert.Equal(PredictionStatus.Pending, predictionB.Status);
        Assert.Null(predictionB.SettledAt);

        // The critical assertion: User A's wallet credit DID execute (via ExecuteUpdateAsync)
        // before User B's failure aborted the transaction — proving the rollback undoes an
        // already-applied mutation, not just a mutation that never ran.
        Assert.Equal(balanceA, await f.GetWalletBalanceAsync(userA));
        Assert.Equal(balanceB, await f.GetWalletBalanceAsync(userB));
    }

    // ── §5: atomic claim proof — a second invocation against an already-claimed Prediction
    //        must affect 0 rows and must NOT credit the wallet again ────────────────────────

    [Fact]
    public async Task Helper_AtomicClaim_SecondInvocationOnSamePrediction_AffectsZeroRows_NoDoubleCredit()
    {
        // NOTE on scope: the Sqlite in-memory provider used by this test fixture runs on a
        // single shared connection, which serializes all commands issued against it — it cannot
        // host two genuinely overlapping concurrent transactions racing on the same row the way
        // Npgsql/Postgres can in production. What IS provable deterministically, and what this
        // test proves, is the actual mechanism that would decide such a race: the conditional
        // ExecuteUpdateAsync claim (Id match AND Status == Pending) is the sole gate — a second
        // invocation that finds the row already claimed does 0 work and credits nothing, exactly
        // as it would if it lost a real race to a concurrent transaction in production.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, spectatorId, race.WinnerHorseId, betAmount: 100m, odds: 2m);

        var raceIds = new[] { race.Id };

        // First caller: claims the Prediction (Pending -> Lost) and credits the wallet.
        var firstRefund = await HorseRacing.Services.PredictionRefundHelper.RefundPendingPredictionsAsync(
            f.Db, f.FaultWallet, raceIds, "concurrent_claim_test", swallowIndividualFailures: false);
        Assert.Equal(1, firstRefund);
        Assert.Equal(walletBefore + 100m, await f.GetWalletBalanceAsync(spectatorId));

        // Second caller (simulating a concurrent/competing invocation that would have raced the
        // first for the same row): the candidate SELECT now finds nothing Pending, so the
        // conditional claim never even runs a second time — 0 rows affected, 0 wallet credit.
        var secondRefund = await HorseRacing.Services.PredictionRefundHelper.RefundPendingPredictionsAsync(
            f.Db, f.FaultWallet, raceIds, "concurrent_claim_test", swallowIndividualFailures: false);
        Assert.Equal(0, secondRefund);
        Assert.Equal(walletBefore + 100m, await f.GetWalletBalanceAsync(spectatorId));
    }

    // ── §7B/C/D: direct Race cancel — wallet-failure compensation and repeated-attempt safety ──

    [Fact]
    public async Task DirectRaceCancel_WalletThrows_PredictionStaysPending_NoCredit()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, spectatorId, race.WinnerHorseId, betAmount: 60m, odds: 2m);

        f.FaultWallet.FailForUserIds.Add(spectatorId);

        var cancel = await f.RaceManagement.CancelRaceAsync(race.Id);

        // Direct Race cancel's own convention (swallowIndividualFailures: true, no surrounding
        // transaction): a failed refund does not block the Race cancellation itself.
        Assert.True(cancel.Result.Success, cancel.Result.Message);
        Assert.Equal(RaceStatus.Cancelled, await RaceStatusOfAsync(f, race.Id));

        var prediction = (await f.GetPredictionsFreshAsync(race.Id)).Single();
        Assert.Equal(PredictionStatus.Pending, prediction.Status);
        Assert.Null(prediction.SettledAt);
        Assert.Equal(walletBefore, await f.GetWalletBalanceAsync(spectatorId));
    }

    [Fact]
    public async Task DirectRaceCancel_WalletReturnsFailWithoutThrowing_SameAsThrow()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, spectatorId, race.WinnerHorseId, betAmount: 60m, odds: 2m);

        // FailResultForUserIds: AddPointsAsync returns ServiceResult.Fail WITHOUT throwing —
        // proves the helper checks walletResult.Result.Success rather than only reacting to
        // exceptions (Task B0 micro-fix §2).
        f.FaultWallet.FailResultForUserIds.Add(spectatorId);

        var cancel = await f.RaceManagement.CancelRaceAsync(race.Id);

        Assert.True(cancel.Result.Success, cancel.Result.Message);
        Assert.Equal(RaceStatus.Cancelled, await RaceStatusOfAsync(f, race.Id));

        var prediction = (await f.GetPredictionsFreshAsync(race.Id)).Single();
        Assert.Equal(PredictionStatus.Pending, prediction.Status);
        Assert.Null(prediction.SettledAt);
        Assert.Equal(walletBefore, await f.GetWalletBalanceAsync(spectatorId));
    }

    [Fact]
    public async Task DirectRaceConvention_RepeatedCompetingRefundAttempt_NoDoubleWalletCredit()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, spectatorId, race.WinnerHorseId, betAmount: 45m, odds: 2m);

        var raceIds = new[] { race.Id };

        // First attempt, using the direct-Race convention (swallowIndividualFailures: true).
        var first = await HorseRacing.Services.PredictionRefundHelper.RefundPendingPredictionsAsync(
            f.Db, f.FaultWallet, raceIds, "refund", swallowIndividualFailures: true);
        Assert.Equal(1, first);
        Assert.Equal(walletBefore + 45m, await f.GetWalletBalanceAsync(spectatorId));

        // A repeated/competing attempt against the same Prediction — e.g. a retried CancelRaceAsync
        // call, or a Tournament cancel racing the direct Race cancel — must find nothing left to
        // claim and must not credit the wallet again.
        var second = await HorseRacing.Services.PredictionRefundHelper.RefundPendingPredictionsAsync(
            f.Db, f.FaultWallet, raceIds, "refund", swallowIndividualFailures: true);
        Assert.Equal(0, second);
        Assert.Equal(walletBefore + 45m, await f.GetWalletBalanceAsync(spectatorId));
    }
}
