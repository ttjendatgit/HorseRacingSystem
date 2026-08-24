using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests;

/// <summary>
/// Phase2B: end-to-end coverage of the split RaceStatus (event progress) /
/// RaceResultStatus (result lifecycle) lifecycle, wired against a real
/// Sqlite in-memory DB and the actual production services (not mocks) so the
/// assertions exercise the real gates.
/// </summary>
public class RaceLifecycleTests
{
    /// <summary>
    /// R0: every submission is now a full ranking, not a bare WinningHorseId.
    /// This is the standard two-horse (winner/loser) full ranking shared by
    /// every pre-R0 lifecycle test in this file and in Phase3ContractTests —
    /// LifecycleFixture only ever seeds exactly these two RaceEntries.
    /// </summary>
    public static RaceResultRequest WinnerLoserRanking(Guid winnerHorseId, Guid loserHorseId) =>
        new()
        {
            Rankings = new List<RaceResultRankingItemRequest>
            {
                new() { HorseId = winnerHorseId, Position = 1 },
                new() { HorseId = loserHorseId, Position = 2 },
            }
        };

    [Fact]
    public async Task EventProgress_ScheduledToFinished_FollowsLockedSequence()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();

        Assert.Equal(RaceStatus.Scheduled, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);

        var open = await f.RaceManagement.OpenRegistrationAsync(race.Id);
        Assert.True(open.Result.Success);
        Assert.Equal(RaceStatus.RegistrationOpen, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);

        var close = await f.RaceManagement.CloseRegistrationAsync(race.Id);
        Assert.True(close.Result.Success);
        Assert.Equal(RaceStatus.RegistrationClosed, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);
        Assert.True(start.Result.Success);
        Assert.Equal(RaceStatus.InProgress, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);

        // Finishing does not require a result.
        var finish = await f.RaceManagement.EndRaceAsync(race.Id);
        Assert.True(finish.Result.Success);
        var finished = await f.RaceRepo.GetByIdAsync(race.Id);
        Assert.Equal(RaceStatus.Finished, finished!.Status);
        Assert.Null(await f.RaceResultRepo.GetByRaceIdAsync(race.Id));
    }

    [Fact]
    public async Task ResultSubmission_Rejected_WhileInProgress()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);

        var submit = await f.LiveResult.UpdateRaceResultAsync(race.Id,
            WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));

        Assert.False(submit.Result.Success);
    }

    [Fact]
    public async Task FinishingRace_DoesNotSettlePredictions()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);

        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(1000m);
        await f.AddPendingPredictionAsync(race.Id, spectatorId, race.WinnerHorseId, betAmount: 100m, odds: 2m);

        await f.RaceManagement.EndRaceAsync(race.Id);

        var prediction = (await f.PredictionRepo.GetByRaceAsync(race.Id)).Single();
        Assert.Equal(PredictionStatus.Pending, prediction.Status);
        Assert.Equal(walletBefore, await f.GetWalletBalanceAsync(spectatorId));
    }

    [Fact]
    public async Task ResultLifecycle_SubmitRejectResubmitApprove_NeverMutatesRaceStatus()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);

        // First submission -> Provisional
        var submit = await f.LiveResult.UpdateRaceResultAsync(race.Id,
            WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        Assert.True(submit.Result.Success);

        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        Assert.NotNull(result);
        Assert.Equal(RaceResultStatus.Provisional, result!.Status);
        Assert.Null(result.RejectedReason);
        Assert.Equal(RaceStatus.Finished, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);

        // Reject -> stays Provisional, gains RejectedReason, Race.Status untouched
        var reject = await f.Admin.RejectRaceResultAsync(race.Id, "Sai ngựa thắng");
        Assert.True(reject.Result.Success);
        result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        Assert.Equal(RaceResultStatus.Provisional, result!.Status);
        Assert.Equal("Sai ngựa thắng", result.RejectedReason);
        Assert.Equal(RaceStatus.Finished, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);

        // Resubmit -> stays Provisional, clears RejectedReason
        var resubmit = await f.LiveResult.UpdateRaceResultAsync(race.Id,
            WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        Assert.True(resubmit.Result.Success);
        result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        Assert.Equal(RaceResultStatus.Provisional, result!.Status);
        Assert.Null(result.RejectedReason);
        Assert.Equal(RaceStatus.Finished, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);

        // Approve without a RaceReport must fail (V1.1 mandatory-report gate)
        var approveNoReport = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.False(approveNoReport.Result.Success);

        await f.AddMandatoryReportAsync(race.Id);

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.True(approve.Result.Success, approve.Result.Message);
        result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        Assert.Equal(RaceResultStatus.Official, result!.Status);
        Assert.NotNull(result.ApprovedAt);
        Assert.Equal(RaceStatus.Finished, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    [Fact]
    public async Task Approval_SettlesPredictions_OnlyAfterOfficial()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);

        var (winnerId, winnerWalletStart) = await f.CreateSpectatorWithWalletAsync(0m);
        var (loserId, loserWalletStart) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, winnerId, race.WinnerHorseId, betAmount: 100m, odds: 2m);
        await f.AddPendingPredictionAsync(race.Id, loserId, race.LoserHorseId, betAmount: 50m, odds: 3m);

        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));

        // Direct settlement attempt on a Finished-but-Provisional race must fail.
        var earlySettle = await f.Prediction.SettlePredictionAsync(race.Id, race.WinnerHorseId);
        Assert.False(earlySettle.Result.Success);
        Assert.Equal(winnerWalletStart, await f.GetWalletBalanceAsync(winnerId));

        await f.AddMandatoryReportAsync(race.Id);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.True(approve.Result.Success);

        var predictions = await f.GetPredictionsFreshAsync(race.Id);
        var winnerPrediction = predictions.Single(p => p.SpectatorUserId == winnerId);
        var loserPrediction = predictions.Single(p => p.SpectatorUserId == loserId);

        Assert.Equal(PredictionStatus.Won, winnerPrediction.Status);
        Assert.Equal(PredictionStatus.Lost, loserPrediction.Status);
        Assert.Equal(winnerWalletStart + 200m, await f.GetWalletBalanceAsync(winnerId)); // 100 * odds 2
        Assert.Equal(loserWalletStart, await f.GetWalletBalanceAsync(loserId));

        // Calling settlement again must NOT double-credit the winner's wallet.
        var againResult = await f.Prediction.SettlePredictionAsync(race.Id, race.WinnerHorseId);
        Assert.True(againResult.Result.Success);
        Assert.Equal(winnerWalletStart + 200m, await f.GetWalletBalanceAsync(winnerId));
    }

    // Correction-pass settlement failure-injection test: two spectators bet on
    // the same winning horse. Winner A's payout succeeds, winner B's payout is
    // deliberately made to throw. Proves (1) approval still commits (the
    // TransactionScope does NOT roll back on a swallowed per-winner payout
    // exception — see report section E/F/G), (2) winner A is paid exactly
    // once and is never touched again, (3) winner B is left safely Pending
    // (not stranded as "Won with no payout"), and (4) a later retry pays B
    // exactly once without re-paying A.
    [Fact]
    public async Task Settlement_PartialPayoutFailure_DoesNotStrandOrDoublePay()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);

        var (winnerAId, winnerAStart) = await f.CreateSpectatorWithWalletAsync(0m);
        var (winnerBId, winnerBStart) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, winnerAId, race.WinnerHorseId, betAmount: 100m, odds: 2m);
        await f.AddPendingPredictionAsync(race.Id, winnerBId, race.WinnerHorseId, betAmount: 50m, odds: 4m);

        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        await f.AddMandatoryReportAsync(race.Id);

        // Inject a payout failure for winner B only.
        f.FaultWallet.FailForUserIds.Add(winnerBId);

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        // CASE A/B determination: approval still succeeds — the per-winner
        // payout exception is caught inside PredictionService and never
        // propagates to AdminService's TransactionScope, so scope.Complete()
        // still runs and the Provisional->Official transition commits.
        Assert.True(approve.Result.Success, approve.Result.Message);

        var resultAfterFailure = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        Assert.Equal(RaceResultStatus.Official, resultAfterFailure!.Status);

        var predictionsAfterFailure = await f.GetPredictionsFreshAsync(race.Id);
        var aAfterFailure = predictionsAfterFailure.Single(p => p.SpectatorUserId == winnerAId);
        var bAfterFailure = predictionsAfterFailure.Single(p => p.SpectatorUserId == winnerBId);

        Assert.Equal(PredictionStatus.Won, aAfterFailure.Status);
        Assert.Equal(winnerAStart + 200m, await f.GetWalletBalanceAsync(winnerAId));

        // Winner B must NOT be stranded as "Won" with no payout — it must be
        // back to Pending so a retry can pick it up.
        Assert.Equal(PredictionStatus.Pending, bAfterFailure.Status);
        Assert.Null(bAfterFailure.PayoutAmount);
        Assert.Equal(winnerBStart, await f.GetWalletBalanceAsync(winnerBId));

        // Retry after the transient fault clears.
        f.FaultWallet.FailForUserIds.Remove(winnerBId);
        var retry = await f.Prediction.SettlePredictionAsync(race.Id, race.WinnerHorseId);
        Assert.True(retry.Result.Success, retry.Result.Message);

        var predictionsAfterRetry = await f.GetPredictionsFreshAsync(race.Id);
        var aAfterRetry = predictionsAfterRetry.Single(p => p.SpectatorUserId == winnerAId);
        var bAfterRetry = predictionsAfterRetry.Single(p => p.SpectatorUserId == winnerBId);

        Assert.Equal(PredictionStatus.Won, bAfterRetry.Status);
        Assert.Equal(winnerBStart + 200m, await f.GetWalletBalanceAsync(winnerBId)); // 50 * odds 4

        // Winner A must not have been re-paid by the retry.
        Assert.Equal(PredictionStatus.Won, aAfterRetry.Status);
        Assert.Equal(winnerAStart + 200m, await f.GetWalletBalanceAsync(winnerAId));
    }

    // Correction-pass admin retry-settlement tests: AdminService.
    // SettlePredictionsAsync(raceId) is the exposed admin-only retry path
    // for predictions left Pending by a prior payout failure. It must derive
    // the winning horse itself from the Official RaceResult — the method
    // signature takes only raceId, so there is no client-supplied horse to
    // even trust incorrectly (covers requirement F by construction).
    [Fact]
    public async Task AdminRetrySettlement_PaysRemainingPendingWinner_WithoutRePayingAlreadySettledWinner()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);

        var (winnerAId, winnerAStart) = await f.CreateSpectatorWithWalletAsync(0m);
        var (winnerBId, winnerBStart) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, winnerAId, race.WinnerHorseId, betAmount: 100m, odds: 2m);
        await f.AddPendingPredictionAsync(race.Id, winnerBId, race.WinnerHorseId, betAmount: 50m, odds: 4m);

        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        await f.AddMandatoryReportAsync(race.Id);

        // Winner B's payout fails during approval; winner A succeeds normally.
        f.FaultWallet.FailForUserIds.Add(winnerBId);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.True(approve.Result.Success, approve.Result.Message);

        var afterFailure = await f.GetPredictionsFreshAsync(race.Id);
        Assert.Equal(PredictionStatus.Pending, afterFailure.Single(p => p.SpectatorUserId == winnerBId).Status);
        Assert.Equal(winnerAStart + 200m, await f.GetWalletBalanceAsync(winnerAId));
        Assert.Equal(winnerBStart, await f.GetWalletBalanceAsync(winnerBId));

        // Fault clears, admin retries via the exposed retry path — no
        // winning horse is passed in; it must come from the Official result.
        f.FaultWallet.FailForUserIds.Remove(winnerBId);
        var retry = await f.Admin.SettlePredictionsAsync(race.Id);
        Assert.True(retry.Result.Success, retry.Result.Message);

        var afterRetry = await f.GetPredictionsFreshAsync(race.Id);
        var aAfterRetry = afterRetry.Single(p => p.SpectatorUserId == winnerAId);
        var bAfterRetry = afterRetry.Single(p => p.SpectatorUserId == winnerBId);

        // A: the previously-failed winner is now settled.
        Assert.Equal(PredictionStatus.Won, bAfterRetry.Status);
        Assert.Equal(winnerBStart + 200m, await f.GetWalletBalanceAsync(winnerBId)); // 50 * odds 4

        // B: the already-paid winner is untouched by the retry.
        Assert.Equal(PredictionStatus.Won, aAfterRetry.Status);
        Assert.Equal(winnerAStart + 200m, await f.GetWalletBalanceAsync(winnerAId));
    }

    [Fact]
    public async Task AdminRetrySettlement_NoPendingRemaining_IsSafeNoOpAndDoesNotTouchWallets()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);

        var (winnerId, winnerStart) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, winnerId, race.WinnerHorseId, betAmount: 100m, odds: 2m);

        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        await f.AddMandatoryReportAsync(race.Id);

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.True(approve.Result.Success, approve.Result.Message);
        Assert.Equal(winnerStart + 200m, await f.GetWalletBalanceAsync(winnerId));

        // C: nothing left Pending — retry must succeed as a no-op and must
        // not call the wallet again (FailForUserIds would make a second
        // payout attempt throw, so a passing assert here proves no payout
        // call was made).
        f.FaultWallet.FailForUserIds.Add(winnerId);
        var retry = await f.Admin.SettlePredictionsAsync(race.Id);
        Assert.True(retry.Result.Success, retry.Result.Message);
        Assert.Equal(winnerStart + 200m, await f.GetWalletBalanceAsync(winnerId));

        var prediction = (await f.GetPredictionsFreshAsync(race.Id)).Single();
        Assert.Equal(PredictionStatus.Won, prediction.Status);
    }

    [Fact]
    public async Task AdminRetrySettlement_RejectedWhenResultIsOnlyProvisional()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));

        // D: result exists but is still Provisional (never approved).
        var retry = await f.Admin.SettlePredictionsAsync(race.Id);
        Assert.False(retry.Result.Success);
    }

    [Fact]
    public async Task AdminRetrySettlement_RejectedWhenRaceIsNotFinished()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();

        // E: race is still Scheduled — never opened, started, or finished.
        var retry = await f.Admin.SettlePredictionsAsync(race.Id);
        Assert.False(retry.Result.Success);
    }

    [Fact]
    public async Task ProvisionalResult_IsNeverTreatedAsOfficial()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));

        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        Assert.Equal(RaceResultStatus.Provisional, result!.Status);
        Assert.NotEqual(RaceResultStatus.Official, result.Status);
    }

    [Fact]
    public async Task OfficialResult_CannotBeResubmittedThroughNormalPath()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        await f.AddMandatoryReportAsync(race.Id);
        await f.Admin.ApproveRaceResultAsync(race.Id);

        var resubmit = await f.LiveResult.UpdateRaceResultAsync(race.Id,
            WinnerLoserRanking(race.LoserHorseId, race.WinnerHorseId));

        Assert.False(resubmit.Result.Success);
        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        Assert.Equal(race.WinnerHorseId, result!.WinningHorseId);
        Assert.Equal(RaceResultStatus.Official, result.Status);
    }

    // Correction-pass Test A: submit -> reject -> approve WITHOUT resubmit => must FAIL.
    [Fact]
    public async Task Approve_AfterRejection_WithoutResubmit_Fails()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        await f.AddMandatoryReportAsync(race.Id);

        var reject = await f.Admin.RejectRaceResultAsync(race.Id, "Sai ngựa thắng");
        Assert.True(reject.Result.Success, reject.Result.Message);

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.False(approve.Result.Success);
        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        Assert.Equal(RaceResultStatus.Provisional, result!.Status);
        Assert.Equal("Sai ngựa thắng", result.RejectedReason);
        Assert.Null(result.ApprovedAt);
    }

    // Correction-pass Test B: submit -> reject -> resubmit -> approve => must PASS.
    [Fact]
    public async Task Approve_AfterRejectionAndResubmit_Succeeds()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        await f.AddMandatoryReportAsync(race.Id);

        var reject = await f.Admin.RejectRaceResultAsync(race.Id, "Sai ngựa thắng");
        Assert.True(reject.Result.Success, reject.Result.Message);

        var resubmit = await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        Assert.True(resubmit.Result.Success, resubmit.Result.Message);

        var resultAfterResubmit = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        Assert.Null(resultAfterResubmit!.RejectedReason);

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.True(approve.Result.Success, approve.Result.Message);
        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        Assert.Equal(RaceResultStatus.Official, result!.Status);
        Assert.Null(result.RejectedReason);
        Assert.NotNull(result.ApprovedAt);
    }

    [Theory]
    [InlineData(RaceStatus.Scheduled, true)]
    [InlineData(RaceStatus.InProgress, true)]
    public async Task Cancellation_AllowedBeforeFinished(RaceStatus atStatus, bool expectSuccess)
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        if (atStatus == RaceStatus.InProgress)
            await f.RaceManagement.StartRaceAsync(race.Id);
        else
        {
            // Cancellation from Scheduled: reopen a Scheduled race scenario directly.
            var scheduledRace = await f.RaceRepo.GetByIdAsync(race.Id);
            scheduledRace!.Status = RaceStatus.Scheduled;
            await f.RaceRepo.UpdateAsync(scheduledRace);
            await f.UnitOfWork.SaveChangesAsync();
        }

        var cancel = await f.RaceManagement.CancelRaceAsync(race.Id);
        Assert.Equal(expectSuccess, cancel.Result.Success);
        if (expectSuccess)
            Assert.Equal(RaceStatus.Cancelled, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    [Fact]
    public async Task Cancellation_Blocked_OnceFinished_RegardlessOfResultStatus()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);

        // Finished with no result yet — still not cancellable.
        var cancelNoResult = await f.RaceManagement.CancelRaceAsync(race.Id);
        Assert.False(cancelNoResult.Result.Success);

        await f.LiveResult.UpdateRaceResultAsync(race.Id, WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        await f.AddMandatoryReportAsync(race.Id);
        await f.Admin.ApproveRaceResultAsync(race.Id);

        // Finished with an Official result — must not be cancellable either.
        var cancelOfficial = await f.RaceManagement.CancelRaceAsync(race.Id);
        Assert.False(cancelOfficial.Result.Success);
    }

    [Fact]
    public async Task Cancellation_RefundsPendingPredictions()
    {
        await using var f = await LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(race.Id, spectatorId, race.WinnerHorseId, betAmount: 75m, odds: 2m);

        var cancel = await f.RaceManagement.CancelRaceAsync(race.Id);
        Assert.True(cancel.Result.Success);

        // Fresh (AsNoTracking) read: PredictionRefundHelper claims via bulk ExecuteUpdateAsync,
        // which bypasses the change tracker — a tracked PredictionRepo read would return the
        // stale in-memory instance from AddPendingPredictionAsync.
        var prediction = (await f.GetPredictionsFreshAsync(race.Id)).Single();
        Assert.Equal(PredictionStatus.Lost, prediction.Status); // refund marker, per existing convention
        Assert.Equal(walletBefore + 75m, await f.GetWalletBalanceAsync(spectatorId));
    }

    [Fact]
    public void RaceStatus_RetainedNumericValuesAreUnchanged()
    {
        Assert.Equal(1, (int)RaceStatus.Scheduled);
        Assert.Equal(2, (int)RaceStatus.InProgress);
        Assert.Equal(3, (int)RaceStatus.Finished);
        Assert.Equal(4, (int)RaceStatus.Cancelled);
        Assert.Equal(7, (int)RaceStatus.RegistrationOpen);
        Assert.Equal(8, (int)RaceStatus.RegistrationClosed);
    }

    [Fact]
    public void RaceStatus_RemovedValuesNoLongerExist()
    {
        Assert.False(Enum.TryParse<RaceStatus>("AwaitingResult", out _));
        Assert.False(Enum.TryParse<RaceStatus>("ResultPendingApproval", out _));
        Assert.False(Enum.TryParse<RaceStatus>("ResultApproved", out _));
    }

    [Fact]
    public void RaceResultStatus_HasExactlyProvisionalAndOfficial()
    {
        var values = Enum.GetValues<RaceResultStatus>();
        Assert.Equal(2, values.Length);
        Assert.Equal(1, (int)RaceResultStatus.Provisional);
        Assert.Equal(2, (int)RaceResultStatus.Official);
        Assert.False(Enum.TryParse<RaceResultStatus>("Rejected", out _));
    }

    /// <summary>
    /// Wraps a real IWalletService and lets a test force AddPointsAsync to
    /// throw for specific spectators, to prove settlement handles a partial
    /// payout failure without stranding or double-paying predictions.
    /// </summary>
    public sealed class FaultInjectingWalletService : IWalletService
    {
        private readonly IWalletService _inner;
        public HashSet<Guid> FailForUserIds { get; } = new();
        /// <summary>
        /// Unlike FailForUserIds (throws), these users make AddPointsAsync return a Fail
        /// ServiceResult WITHOUT throwing — proves callers correctly inspect
        /// walletResult.Result.Success instead of only reacting to exceptions.
        /// </summary>
        public HashSet<Guid> FailResultForUserIds { get; } = new();

        public FaultInjectingWalletService(IWalletService inner) => _inner = inner;

        public Task<ServiceResult<object>> GetBalanceAsync(Guid userId) => _inner.GetBalanceAsync(userId);
        public Task<ServiceResult<object>> AddFundsAsync(Guid userId, decimal amountVnd, string reference) => _inner.AddFundsAsync(userId, amountVnd, reference);

        public Task<ServiceResult<object>> AddPointsAsync(Guid userId, decimal points, string reference)
        {
            if (FailForUserIds.Contains(userId))
                throw new InvalidOperationException($"Injected payout failure for user {userId}");
            if (FailResultForUserIds.Contains(userId))
                return Task.FromResult(ServiceResult<object>.Fail(400, $"Injected refund failure (no exception) for user {userId}"));
            return _inner.AddPointsAsync(userId, points, reference);
        }

        public Task<ServiceResult<object>> DeductFundsAsync(Guid userId, decimal amount, string reference) => _inner.DeductFundsAsync(userId, amount, reference);
        public Task<ServiceResult<decimal>> ConvertPointsToVndAsync(Guid userId, decimal points) => _inner.ConvertPointsToVndAsync(userId, points);
        public decimal GetPointsPerVnd() => _inner.GetPointsPerVnd();
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto)
            => Task.FromResult(ServiceResult<NotificationDto>.Ok(new NotificationDto()));
        public Task<ServiceResult<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId)
            => throw new NotSupportedException();
        public Task<ServiceResult<List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId)
            => throw new NotSupportedException();
        public Task<ServiceResult<List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter)
            => throw new NotSupportedException();
        public Task<ServiceResult<NotificationDetailDto>> GetNotificationByIdAsync(Guid id)
            => throw new NotSupportedException();
        public Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId)
            => throw new NotSupportedException();
        public Task<ServiceResult<bool>> MarkMultipleAsReadAsync(MarkNotificationsAsReadDto dto)
            => throw new NotSupportedException();
        public Task<ServiceResult<bool>> DeleteNotificationAsync(Guid notificationId)
            => throw new NotSupportedException();
        public Task<ServiceResult<bool>> DeleteAllNotificationsAsync(Guid userId)
            => throw new NotSupportedException();
        public Task<ServiceResult<int>> GetUnreadCountAsync(Guid userId)
            => throw new NotSupportedException();
        public Task<ServiceResult<NotificationStatsDto>> GetNotificationStatsAsync(Guid userId)
            => throw new NotSupportedException();
        public Task<ServiceResult<bool>> SendBulkNotificationsAsync(BulkNotificationDto dto)
            => throw new NotSupportedException();
        public Task<ServiceResult<List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId)
            => throw new NotSupportedException();
        public Task ProcessUnsentNotificationsAsync()
            => throw new NotSupportedException();
    }

    private sealed class FakeRefereeService : IRefereeService
    {
        public Task<ServiceResult<RefereeResponse>> CreateRefereeAsync(CreateRefereeRequest request) => throw new NotSupportedException();
        public Task<ServiceResult<RefereeResponse>> GetRefereeAsync(Guid id) => throw new NotSupportedException();
        public Task<ServiceResult<IEnumerable<RefereeResponse>>> GetAllRefereesAsync() => throw new NotSupportedException();
        public Task<ServiceResult<IEnumerable<RefereeResponse>>> GetActiveRefereesAsync() => throw new NotSupportedException();
        public Task<ServiceResult<RefereeResponse>> UpdateRefereeAsync(Guid id, UpdateRefereeRequest request) => throw new NotSupportedException();
        public Task<ServiceResult<bool>> DeleteRefereeAsync(Guid id) => throw new NotSupportedException();
        public Task<ServiceResult<RefereeAssignmentResponse>> AssignRefereeToRaceAsync(AssignRefereeRequest request) => throw new NotSupportedException();
        public Task<ServiceResult<IEnumerable<RefereeAssignmentResponse>>> GetRaceAssignmentsAsync(Guid raceId) => throw new NotSupportedException();
        public Task<ServiceResult<IEnumerable<RefereeAssignmentResponse>>> GetRefereeAssignmentsAsync(Guid refereeId) => throw new NotSupportedException();
        public Task<ServiceResult<IEnumerable<RefereeAssignmentResponse>>> GetAllAssignmentsAsync() => throw new NotSupportedException();
        public Task<ServiceResult<RefereeAssignmentResponse>> ConfirmAssignmentAsync(ConfirmRefereeAssignmentRequest request) => throw new NotSupportedException();
    }

    internal sealed record SeededRace(Guid Id, Guid WinnerHorseId, Guid LoserHorseId);

    internal sealed class LifecycleFixture : IAsyncDisposable
    {
        public SqliteConnection Connection { get; }
        public ApplicationDbContext Db { get; }
        public IUnitOfWork UnitOfWork { get; }
        public IRaceRepository RaceRepo { get; }
        public IRaceEntryRepository EntryRepo { get; }
        public IRaceResultRepository RaceResultRepo { get; }
        public IPredictionRepository PredictionRepo { get; }
        public IRaceManagementService RaceManagement { get; }
        public ILiveResultService LiveResult { get; }
        public IAdminService Admin { get; }
        public IPredictionService Prediction { get; }
        public ITournamentService TournamentSvc { get; }
        public IRoundService RoundSvc { get; }
        public IRaceService RaceSvc { get; }
        public ITournamentRepository TournamentRepo { get; }
        public IRoundRepository RoundRepoPublic { get; }
        public FaultInjectingWalletService FaultWallet { get; private set; } = null!;
        public IViolationRecordRepository ViolationRepo { get; }
        public IProtestRepository ProtestRepo { get; }
        public IHealthCheckRepository HealthCheckRepo { get; }
        public IViolationRecordService ViolationSvc { get; }
        public IProtestService ProtestSvc { get; }
        public IRefereeHealthCheckService HealthCheckSvc { get; }

        private readonly IOwnerRepository _ownerRepo;
        private readonly IHorseRepository _horseRepo;
        private readonly IJockeyRepository _jockeyRepo;
        private readonly IUserRepository _userRepo;
        private readonly ITournamentRepository _tournamentRepo;
        private readonly IRoundRepository _roundRepo;
        private readonly IRefereeAssignmentRepository _assignmentRepo;
        private readonly IRaceReportRepository _reportRepo;
        private readonly IWalletRepository _walletRepo;
        private Guid _refereeId;
        private Guid _refereeUserId;
        private int _counter;

        public Guid RefereeUserId => _refereeUserId;
        public Guid RefereeId => _refereeId;

        private LifecycleFixture(SqliteConnection connection, ApplicationDbContext db)
        {
            Connection = connection;
            Db = db;
            UnitOfWork = new UnitOfWork(db);

            RaceRepo = new RaceRepository(db);
            EntryRepo = new RaceEntryRepository(db);
            RaceResultRepo = new RaceResultRepository(db);
            PredictionRepo = new PredictionRepository(db);
            _ownerRepo = new OwnerRepository(db);
            _horseRepo = new HorseRepository(db);
            _jockeyRepo = new JockeyRepository(db);
            _userRepo = new UserRepository(db);
            _tournamentRepo = new TournamentRepository(db);
            _roundRepo = new RoundRepository(db);
            _assignmentRepo = new RefereeAssignmentRepository(db);
            _reportRepo = new RaceReportRepository(db);
            _walletRepo = new WalletRepository(db);
            ViolationRepo = new ViolationRecordRepository(db);
            ProtestRepo = new ProtestRepository(db);
            HealthCheckRepo = new HealthCheckRepository(db);

            var config = new ConfigurationBuilder().Build();
            FaultWallet = new FaultInjectingWalletService(new WalletService(_walletRepo, _userRepo, UnitOfWork, config));
            IWalletService walletService = FaultWallet;
            IRaceEntryService raceEntryService = new RaceEntryService(_ownerRepo, _horseRepo, _jockeyRepo, RaceRepo, EntryRepo, UnitOfWork, db);

            RaceManagement = new RaceManagementService(
                RaceRepo, EntryRepo, _horseRepo, _jockeyRepo, _tournamentRepo, _roundRepo,
                PredictionRepo, _assignmentRepo, walletService, UnitOfWork,
                NullLogger<RaceManagementService>.Instance, raceEntryService, db);

            LiveResult = new LiveResultService(
                RaceRepo, EntryRepo, _horseRepo, _jockeyRepo,
                new RaceManagementRepository(db), RaceResultRepo,
                Prediction = new PredictionService(RaceRepo, PredictionRepo, walletService, _walletRepo, new FakeNotificationService(), UnitOfWork),
                UnitOfWork);

            Admin = new AdminService(
                _userRepo, new UserRegistrationRepository(db), _horseRepo, _jockeyRepo,
                new RefereeRepository(db), RaceRepo, _tournamentRepo,
                new FakeRefereeService(), LiveResult, Prediction, PredictionRepo,
                RaceResultRepo, EntryRepo, _reportRepo, ViolationRepo, ProtestRepo, UnitOfWork);

            ViolationSvc = new ViolationRecordService(
                ViolationRepo, RaceRepo, EntryRepo, new RefereeRepository(db),
                new FakeNotificationService(), _userRepo, UnitOfWork);

            ProtestSvc = new ProtestService(ProtestRepo, RaceRepo, UnitOfWork);

            HealthCheckSvc = new RefereeHealthCheckService(
                HealthCheckRepo, RaceRepo, _horseRepo, new RefereeRepository(db), UnitOfWork);

            TournamentRepo = _tournamentRepo;
            RoundRepoPublic = _roundRepo;
            TournamentSvc = new TournamentService(
                _tournamentRepo, new FakeNotificationService(), _userRepo, RaceRepo, EntryRepo,
                _assignmentRepo, _roundRepo, _horseRepo, _jockeyRepo, db, UnitOfWork, walletService);
            RoundSvc = new RoundService(_roundRepo, _tournamentRepo, UnitOfWork, db);
            RaceSvc = new RaceService(RaceRepo, RaceResultRepo, _tournamentRepo, RaceManagement);
        }

        public static async Task<LifecycleFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            // AdminService.ApproveRaceResultAsync wraps its work in a
            // System.Transactions.TransactionScope, which the Sqlite test
            // provider cannot enlist in (it works fine against the real
            // Npgsql provider in production). Every operation in these tests
            // already runs against the single shared in-memory connection, so
            // ignoring the warning here does not weaken what the test proves.
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.AmbientTransactionWarning))
                .Options);
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            return new LifecycleFixture(connection, db);
        }

        public async Task<SeededRace> CreateReadyToStartRaceAsync()
        {
            var n = ++_counter;
            var now = DateTime.UtcNow;

            var ownerUser = new User { Id = Guid.NewGuid(), Email = $"owner{n}@test.com", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner };
            var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUser.Id, OwnerCode = $"OWN-{n}" };
            var jockeyUser = new User { Id = Guid.NewGuid(), Email = $"jockey{n}@test.com", PasswordHash = "x", FullName = "Jockey", Role = UserRole.Jockey };
            var jockey = new Jockey { Id = Guid.NewGuid(), UserId = jockeyUser.Id, ApprovalStatus = ApprovalStatus.Approved };
            var jockeyUser2 = new User { Id = Guid.NewGuid(), Email = $"jockey2-{n}@test.com", PasswordHash = "x", FullName = "Jockey2", Role = UserRole.Jockey };
            var jockey2 = new Jockey { Id = Guid.NewGuid(), UserId = jockeyUser2.Id, ApprovalStatus = ApprovalStatus.Approved };
            var refereeUser = new User { Id = Guid.NewGuid(), Email = $"ref{n}@test.com", PasswordHash = "x", FullName = "Referee", Role = UserRole.Referee };
            var referee = new Referee { Id = Guid.NewGuid(), UserId = refereeUser.Id, LicenseNumber = $"LIC-{n}", IsActive = true };
            _refereeId = referee.Id;
            _refereeUserId = refereeUser.Id;

            var winnerHorse = new Horse { Id = Guid.NewGuid(), Name = $"Winner{n}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
            var loserHorse = new Horse { Id = Guid.NewGuid(), Name = $"Loser{n}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };

            // CreateRaceAsync requires the Tournament to still be Draft, so it is seeded that way
            // (the model default) and only flipped to Ongoing afterward — see R1a note below.
            var tournament = new Tournament { Id = Guid.NewGuid(), Name = $"Tournament{n}", StartDate = now, EndDate = now.AddDays(7) };
            var round = new Round { Id = Guid.NewGuid(), Name = "Round 1", TournamentId = tournament.Id, RoundNumber = 1, ScheduledStartDate = now, ScheduledEndDate = now.AddDays(1) };
            // GATE-V1 FINAL CAPACITY CORRECTION: GateNumber's upper bound is Track.Capacity, not
            // Race.MaxParticipants — this helper is named "ready to start", so it needs a real
            // Track with headroom above the base 2 entries for AddExtraApprovedEntryAsync callers.
            var track = new Track { Id = Guid.NewGuid(), Name = $"Track{n}", Capacity = 20, CreatedAt = now };

            Db.AddRange(ownerUser, owner, jockeyUser, jockey, jockeyUser2, jockey2, refereeUser, referee, winnerHorse, loserHorse, tournament, round, track);
            await Db.SaveChangesAsync();

            var createResult = await RaceManagement.CreateRaceAsync(new CreateRaceRequest
            {
                Name = $"Race{n}",
                TournamentId = tournament.Id,
                RoundId = round.Id,
                ScheduledAt = now.AddHours(1),
                MaxParticipants = 8,
                TrackId = track.Id,
                Distance = 1200
            });
            var raceId = createResult.Result.Data!.Id;

            // R1a: StartRace now requires the parent Tournament to be Ongoing — this helper is
            // named "ready to start", so the seeded Tournament must already satisfy that gate.
            tournament.Status = TournamentStatus.Ongoing;
            tournament.IsActive = true;

            // GATE-V1: StartRace now requires every participating entry to have a unique, in-range
            // GateNumber — this helper is named "ready to start", so the seeded entries must
            // already satisfy that gate too (gates are otherwise unrelated to what each test here
            // actually exercises, so any distinct in-range pair works).
            var winnerEntry = new RaceEntry
            {
                Id = Guid.NewGuid(), RaceId = raceId, HorseId = winnerHorse.Id, JockeyId = jockey.Id,
                Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = true, GateNumber = 1
            };
            var loserEntry = new RaceEntry
            {
                Id = Guid.NewGuid(), RaceId = raceId, HorseId = loserHorse.Id, JockeyId = jockey2.Id,
                Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = true, GateNumber = 2
            };
            Db.AddRange(winnerEntry, loserEntry);

            Db.Add(new HorseHealthCheck
            {
                Id = Guid.NewGuid(), HorseId = winnerHorse.Id, RaceId = raceId, RefereeId = referee.Id,
                Status = HealthCheckStatus.Passed, ApprovedToRace = true, CheckedAt = now
            });
            Db.Add(new HorseHealthCheck
            {
                Id = Guid.NewGuid(), HorseId = loserHorse.Id, RaceId = raceId, RefereeId = referee.Id,
                Status = HealthCheckStatus.Passed, ApprovedToRace = true, CheckedAt = now
            });
            Db.Add(new RefereeAssignment
            {
                Id = Guid.NewGuid(), RaceId = raceId, RefereeId = referee.Id, Role = "Chief Referee",
                Status = RefereeAssignmentStatus.Confirmed, AssignedAt = now, ConfirmedAt = now
            });
            await Db.SaveChangesAsync();

            return new SeededRace(raceId, winnerHorse.Id, loserHorse.Id);
        }

        /// <summary>
        /// R0: adds one more start-eligible RaceEntry (own Owner/Horse/Jockey,
        /// Approved+Confirmed, health check Passed+Approved) to an already
        /// seeded race, so multi-horse (3+) full-ranking scenarios can be
        /// built on top of CreateReadyToStartRaceAsync's base two. Must be
        /// called before OpenRegistration/StartRace. Requires
        /// CreateReadyToStartRaceAsync to have run first (uses its Referee).
        /// </summary>
        public async Task<Guid> AddExtraApprovedEntryAsync(Guid raceId)
        {
            var n = ++_counter;
            var ownerUser = new User { Id = Guid.NewGuid(), Email = $"owner{n}@test.com", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner };
            var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUser.Id, OwnerCode = $"OWN-{n}" };
            var jockeyUser = new User { Id = Guid.NewGuid(), Email = $"jockey{n}@test.com", PasswordHash = "x", FullName = "Jockey", Role = UserRole.Jockey };
            var jockey = new Jockey { Id = Guid.NewGuid(), UserId = jockeyUser.Id, ApprovalStatus = ApprovalStatus.Approved };
            var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse{n}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
            Db.AddRange(ownerUser, owner, jockeyUser, jockey, horse);
            await Db.SaveChangesAsync();

            // GATE-V1: assign the next free gate dynamically (base race already holds gates 1/2
            // from CreateReadyToStartRaceAsync) so repeated calls for a 3rd/4th/etc. horse never
            // collide with each other or with the base pair.
            var nextGate = await Db.RaceEntries.CountAsync(e => e.RaceId == raceId) + 1;
            Db.Add(new RaceEntry
            {
                Id = Guid.NewGuid(), RaceId = raceId, HorseId = horse.Id, JockeyId = jockey.Id,
                Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = true, GateNumber = nextGate
            });
            Db.Add(new HorseHealthCheck
            {
                Id = Guid.NewGuid(), HorseId = horse.Id, RaceId = raceId, RefereeId = _refereeId,
                Status = HealthCheckStatus.Passed, ApprovedToRace = true, CheckedAt = DateTime.UtcNow
            });
            await Db.SaveChangesAsync();

            return horse.Id;
        }

        public async Task AddMandatoryReportAsync(Guid raceId)
        {
            await _reportRepo.AddAsync(new RaceReport
            {
                Id = Guid.NewGuid(),
                RaceId = raceId,
                RefereeId = _refereeId,
                CompletedAt = DateTime.UtcNow,
                Details = "Clean race, no incidents.",
                CreatedAt = DateTime.UtcNow
            });
            await UnitOfWork.SaveChangesAsync();
        }

        public async Task<(Guid userId, decimal startingBalance)> CreateSpectatorWithWalletAsync(decimal balance)
        {
            var n = ++_counter;
            var user = new User { Id = Guid.NewGuid(), Email = $"spectator{n}@test.com", PasswordHash = "x", FullName = "Spectator", Role = UserRole.Spectator };
            var wallet = new Wallet { Id = Guid.NewGuid(), UserId = user.Id, Balance = balance };
            Db.AddRange(user, wallet);
            await Db.SaveChangesAsync();
            return (user.Id, balance);
        }

        public async Task<List<Prediction>> GetPredictionsFreshAsync(Guid raceId)
        {
            // AsNoTracking: PredictionService.SettlePredictionAsync updates
            // via bulk ExecuteUpdateAsync, which bypasses the change tracker.
            return await Db.Predictions.AsNoTracking().Where(p => p.RaceId == raceId).ToListAsync();
        }

        public async Task<decimal> GetWalletBalanceAsync(Guid userId)
        {
            // AsNoTracking: WalletRepository.AddBalanceAsync uses a bulk
            // ExecuteUpdateAsync, which bypasses the change tracker — a
            // tracked read here would return the stale in-memory instance.
            var wallet = await Db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId);
            return wallet?.Balance ?? 0m;
        }

        public async Task AddPendingPredictionAsync(Guid raceId, Guid spectatorId, Guid predictedHorseId, decimal betAmount, decimal odds)
        {
            Db.Add(new Prediction
            {
                Id = Guid.NewGuid(),
                RaceId = raceId,
                SpectatorUserId = spectatorId,
                PredictedHorseId = predictedHorseId,
                Status = PredictionStatus.Pending,
                BetAmount = betAmount,
                Odds = odds,
                PotentialPayout = betAmount * odds,
                CreatedAt = DateTime.UtcNow
            });
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
