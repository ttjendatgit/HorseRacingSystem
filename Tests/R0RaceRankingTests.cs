using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// R0: full ordered Race Result ranking. A submitted result must now carry
/// the complete finishing order (Rankings[]) of every current RaceEntry —
/// WinningHorseId is always derived as Rankings[Position == 1].HorseId, never
/// an independently-editable second source. These tests cover the new
/// submission/validation/approval contract on top of the existing
/// Provisional/Official lifecycle already covered by RaceLifecycleTests
/// (reused here via RaceLifecycleTests.LifecycleFixture — all 44 of those
/// pre-existing tests still pass unmodified in behavior, only updated to
/// send a full ranking instead of a bare WinningHorseId).
/// </summary>
public class R0RaceRankingTests
{
    private static SubmitRaceResultRequest Ranking(params (Guid HorseId, int Position)[] items) =>
        new()
        {
            Rankings = items.Select(i => new SubmitRankingEntry { HorseId = i.HorseId, Position = i.Position, Status = "Completed" }).ToList()
        };

    private static async Task<(Guid raceId, Guid a, Guid c, Guid b, Guid d)> Seed4HorseFinishedRaceAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var race = await f.CreateReadyToStartRaceAsync(); // seeds A (WinnerHorseId) + B (LoserHorseId)
        var c = await f.AddExtraApprovedEntryAsync(race.Id);
        var d = await f.AddExtraApprovedEntryAsync(race.Id);
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);
        return (race.Id, race.WinnerHorseId, c, race.LoserHorseId, d);
    }

    private static async Task<(Guid raceId, Guid winner, Guid loser)> Seed2HorseFinishedRaceAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);
        return (race.Id, race.WinnerHorseId, race.LoserHorseId);
    }

    // ── SUCCESS ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_Full4HorseRanking_StoresOrderedRankingsJson_AndDerivesWinner()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, a, c, b, d) = await Seed4HorseFinishedRaceAsync(f);

        var submit = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((a, 1), (c, 2), (b, 3), (d, 4)));
        Assert.True(submit.Result.Success, submit.Result.Message);

        var result = await f.RaceResultRepo.GetByRaceIdAsync(raceId);
        Assert.Equal(RaceResultStatus.Provisional, result!.Status);
        Assert.Equal(a, result.WinningHorseId);

        var stored = JsonSerializer.Deserialize<List<RaceResultRankingItemRequest>>(result.RankingsJson!)!
            .OrderBy(x => x.Position).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4 }, stored.Select(x => x.Position));
        Assert.Equal(new[] { a, c, b, d }, stored.Select(x => x.HorseId));
    }

    [Fact]
    public async Task Submit_ShuffledRequestOrder_StillCanonicalizesStorageAscendingByPosition()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, a, c, b, d) = await Seed4HorseFinishedRaceAsync(f);

        // Deliberately out of order in the request payload.
        var submit = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((d, 4), (a, 1), (c, 2), (b, 3)));
        Assert.True(submit.Result.Success, submit.Result.Message);

        var result = await f.RaceResultRepo.GetByRaceIdAsync(raceId);
        var stored = JsonSerializer.Deserialize<List<RaceResultRankingItemRequest>>(result!.RankingsJson!)!;
        Assert.Equal(new[] { 1, 2, 3, 4 }, stored.Select(x => x.Position));
        Assert.Equal(a, result.WinningHorseId);
    }

    [Fact]
    public async Task RejectThenResubmit_WithNewRanking_UpdatesRankingsJsonAndWinner_ClearsRejection_StaysProvisional()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, a, c, b, d) = await Seed4HorseFinishedRaceAsync(f);

        await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((a, 1), (c, 2), (b, 3), (d, 4)));
        await f.AddMandatoryReportAsync(raceId);

        var reject = await f.Admin.RejectRaceResultAsync(raceId, "Sai vị trí");
        Assert.True(reject.Result.Success, reject.Result.Message);

        // New order: C now wins instead of A.
        var resubmit = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((c, 1), (a, 2), (b, 3), (d, 4)));
        Assert.True(resubmit.Result.Success, resubmit.Result.Message);

        var result = await f.RaceResultRepo.GetByRaceIdAsync(raceId);
        Assert.Equal(RaceResultStatus.Provisional, result!.Status);
        Assert.Null(result.RejectedReason);
        Assert.Equal(c, result.WinningHorseId);
        var stored = JsonSerializer.Deserialize<List<RaceResultRankingItemRequest>>(result.RankingsJson!)!;
        Assert.Equal(c, stored.Single(x => x.Position == 1).HorseId);
    }

    [Fact]
    public async Task Approve_ValidFullRanking_SetsOfficialAndFinishPositionsForAllFour()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, a, c, b, d) = await Seed4HorseFinishedRaceAsync(f);

        await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((a, 1), (c, 2), (b, 3), (d, 4)));
        await f.AddMandatoryReportAsync(raceId);

        var approve = await f.Admin.ApproveRaceResultAsync(raceId);
        Assert.True(approve.Result.Success, approve.Result.Message);

        var result = await f.RaceResultRepo.GetByRaceIdAsync(raceId);
        Assert.Equal(RaceResultStatus.Official, result!.Status);

        var entries = await f.EntryRepo.GetByRaceAsync(raceId);
        Assert.Equal(1, entries.Single(e => e.HorseId == a).FinishPosition);
        Assert.Equal(2, entries.Single(e => e.HorseId == c).FinishPosition);
        Assert.Equal(3, entries.Single(e => e.HorseId == b).FinishPosition);
        Assert.Equal(4, entries.Single(e => e.HorseId == d).FinishPosition);
    }

    [Fact]
    public async Task BettingWinner_RemainsRankingsPosition1_AfterApproval()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, a, c, b, d) = await Seed4HorseFinishedRaceAsync(f);

        var (winnerBettorId, winnerStart) = await f.CreateSpectatorWithWalletAsync(0m);
        var (loserBettorId, loserStart) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(raceId, winnerBettorId, a, betAmount: 100m, odds: 2m);
        await f.AddPendingPredictionAsync(raceId, loserBettorId, b, betAmount: 50m, odds: 3m);

        await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((a, 1), (c, 2), (b, 3), (d, 4)));
        await f.AddMandatoryReportAsync(raceId);
        var approve = await f.Admin.ApproveRaceResultAsync(raceId);
        Assert.True(approve.Result.Success, approve.Result.Message);

        var predictions = await f.GetPredictionsFreshAsync(raceId);
        Assert.Equal(PredictionStatus.Won, predictions.Single(p => p.SpectatorUserId == winnerBettorId).Status);
        Assert.Equal(PredictionStatus.Lost, predictions.Single(p => p.SpectatorUserId == loserBettorId).Status);
        Assert.Equal(winnerStart + 200m, await f.GetWalletBalanceAsync(winnerBettorId));
        Assert.Equal(loserStart, await f.GetWalletBalanceAsync(loserBettorId));
    }

    [Fact]
    public async Task Approve_OnlyIncrementsStatsForOfficialPosition1_NotForOtherPositions()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, a, c, b, d) = await Seed4HorseFinishedRaceAsync(f);

        await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((a, 1), (c, 2), (b, 3), (d, 4)));
        await f.AddMandatoryReportAsync(raceId);
        var approve = await f.Admin.ApproveRaceResultAsync(raceId);
        Assert.True(approve.Result.Success, approve.Result.Message);

        var horseA = await f.Db.Horses.AsNoTracking().SingleAsync(h => h.Id == a);
        var horseC = await f.Db.Horses.AsNoTracking().SingleAsync(h => h.Id == c);
        var horseB = await f.Db.Horses.AsNoTracking().SingleAsync(h => h.Id == b);
        var horseD = await f.Db.Horses.AsNoTracking().SingleAsync(h => h.Id == d);

        Assert.Equal(1, horseA.TotalWins);
        Assert.Equal(0, horseC.TotalWins);
        Assert.Equal(0, horseB.TotalWins);
        Assert.Equal(0, horseD.TotalWins);
        // Everyone who ran gets a race counted, only position 1 gets a win.
        Assert.Equal(1, horseA.TotalRaces);
        Assert.Equal(1, horseC.TotalRaces);
        Assert.Equal(1, horseB.TotalRaces);
        Assert.Equal(1, horseD.TotalRaces);
    }

    // ── INVALID ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_DuplicateHorse_Returns400()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, winner, loser) = await Seed2HorseFinishedRaceAsync(f);

        var submit = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((winner, 1), (winner, 2)));
        Assert.False(submit.Result.Success);
        Assert.Equal(400, submit.StatusCode);
    }

    // NOTE: Submit_DuplicatePosition_Returns400 and Submit_GapInPositions_Returns400
    // were removed here — the new SubmitRaceResultRequest contract deliberately allows
    // duplicate Position (dead heat) and non-contiguous Position (DNF/DSQ sentinel
    // values like 99), so both scenarios are now valid submissions, not 400s.

    [Fact]
    public async Task Submit_ZeroOrNegativePosition_Returns400()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, winner, loser) = await Seed2HorseFinishedRaceAsync(f);

        var zeroPosition = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((winner, 0), (loser, 1)));
        Assert.False(zeroPosition.Result.Success);
        Assert.Equal(400, zeroPosition.StatusCode);

        var negativePosition = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((winner, -1), (loser, 1)));
        Assert.False(negativePosition.Result.Success);
        Assert.Equal(400, negativePosition.StatusCode);
    }

    [Fact]
    public async Task Submit_InvalidStatus_Returns400()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, winner, loser) = await Seed2HorseFinishedRaceAsync(f);

        // Validated at submission time (LiveResultService), not deferred to approval — a referee
        // typo like lowercase "completed" or a made-up string must be caught here, before it can
        // silently corrupt downstream qualifier-counting logic.
        var submit = await f.LiveResult.UpdateRaceResultAsync(raceId, new SubmitRaceResultRequest
        {
            Rankings = new List<SubmitRankingEntry>
            {
                new() { HorseId = winner, Position = 1, Status = "completed" }, // wrong casing
                new() { HorseId = loser, Position = 2, Status = "Completed" },
            }
        });
        Assert.False(submit.Result.Success);
        Assert.Equal(400, submit.StatusCode);
    }

    [Fact]
    public async Task Submit_HorseNotInRace_Returns400()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, winner, loser) = await Seed2HorseFinishedRaceAsync(f);

        var submit = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((Guid.NewGuid(), 1)));
        Assert.False(submit.Result.Success);
        Assert.Equal(400, submit.StatusCode);
    }

    [Fact]
    public async Task Submit_MissingParticipant_Returns400()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, winner, loser) = await Seed2HorseFinishedRaceAsync(f);

        // Only the winner ranked — loser is a real participant left out entirely.
        var submit = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((winner, 1)));
        Assert.False(submit.Result.Success);
        Assert.Equal(400, submit.StatusCode);
    }

    [Fact]
    public async Task Submit_ExtraParticipantNotInRace_Returns400()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, winner, loser) = await Seed2HorseFinishedRaceAsync(f);

        // Both real participants plus one horse that does not belong here.
        var submit = await f.LiveResult.UpdateRaceResultAsync(
            raceId, Ranking((winner, 1), (loser, 2), (Guid.NewGuid(), 3)));
        Assert.False(submit.Result.Success);
        Assert.Equal(400, submit.StatusCode);
    }

    [Fact]
    public async Task Submit_EmptyRanking_Returns400()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, _, _) = await Seed2HorseFinishedRaceAsync(f);

        var nullRankings = await f.LiveResult.UpdateRaceResultAsync(raceId, new SubmitRaceResultRequest { Rankings = null! });
        Assert.False(nullRankings.Result.Success);
        Assert.Equal(400, nullRankings.StatusCode);

        var emptyRankings = await f.LiveResult.UpdateRaceResultAsync(
            raceId, new SubmitRaceResultRequest { Rankings = new List<SubmitRankingEntry>() });
        Assert.False(emptyRankings.Result.Success);
        Assert.Equal(400, emptyRankings.StatusCode);
    }

    // NOTE: Submit_WinningHorseIdMismatchesPosition1_Returns400 was removed here —
    // SubmitRaceResultRequest no longer has a WinningHorseId field; the winner is
    // always derived from Rankings, so there is nothing left to disagree with.

    [Fact]
    public async Task Submit_WhenRaceNotFinished_ExistingFailurePreserved()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id); // InProgress, not Finished

        var submit = await f.LiveResult.UpdateRaceResultAsync(
            race.Id, Ranking((race.WinnerHorseId, 1), (race.LoserHorseId, 2)));
        Assert.False(submit.Result.Success);
        Assert.Equal(400, submit.StatusCode);
    }

    [Fact]
    public async Task Resubmit_AfterOfficial_IsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, winner, loser) = await Seed2HorseFinishedRaceAsync(f);

        await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((winner, 1), (loser, 2)));
        await f.AddMandatoryReportAsync(raceId);
        await f.Admin.ApproveRaceResultAsync(raceId);

        var resubmit = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((loser, 1), (winner, 2)));
        Assert.False(resubmit.Result.Success);
        Assert.Equal(400, resubmit.StatusCode);

        var result = await f.RaceResultRepo.GetByRaceIdAsync(raceId);
        Assert.Equal(winner, result!.WinningHorseId);
        Assert.Equal(RaceResultStatus.Official, result.Status);
    }

    [Fact]
    public async Task Approve_MalformedStoredRankingsJson_IsRejectedAndNotPartiallyApplied()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, winner, loser) = await Seed2HorseFinishedRaceAsync(f);

        await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((winner, 1), (loser, 2)));
        await f.AddMandatoryReportAsync(raceId);

        // Corrupt storage directly at the DB layer — something the normal
        // submission API can never produce — to prove Approve re-validates
        // defensively instead of trusting the stored value blindly.
        var stored = await f.Db.RaceResults.FirstAsync(r => r.RaceId == raceId);
        stored.RankingsJson = "{not-valid-json";
        await f.Db.SaveChangesAsync();

        var approve = await f.Admin.ApproveRaceResultAsync(raceId);
        Assert.False(approve.Result.Success);
        Assert.Equal(400, approve.StatusCode);

        var resultAfter = await f.RaceResultRepo.GetByRaceIdAsync(raceId);
        Assert.Equal(RaceResultStatus.Provisional, resultAfter!.Status);

        var entries = await f.EntryRepo.GetByRaceAsync(raceId);
        Assert.All(entries, e => Assert.Null(e.FinishPosition));
    }

    [Fact]
    public async Task Approve_StaleStoredRankingNoLongerMatchingParticipants_IsRejectedAndNotPartiallyApplied()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, winner, loser) = await Seed2HorseFinishedRaceAsync(f);

        await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((winner, 1), (loser, 2)));
        await f.AddMandatoryReportAsync(raceId);

        // Simulate a stored ranking that references a horse no longer part
        // of this race's entries (structurally valid JSON, but stale data).
        var stored = await f.Db.RaceResults.FirstAsync(r => r.RaceId == raceId);
        stored.RankingsJson = JsonSerializer.Serialize(new List<RaceResultRankingItemRequest>
        {
            new() { HorseId = winner, Position = 1 },
            new() { HorseId = Guid.NewGuid(), Position = 2 },
        });
        await f.Db.SaveChangesAsync();

        var approve = await f.Admin.ApproveRaceResultAsync(raceId);
        Assert.False(approve.Result.Success);
        Assert.Equal(400, approve.StatusCode);

        var entries = await f.EntryRepo.GetByRaceAsync(raceId);
        Assert.All(entries, e => Assert.Null(e.FinishPosition));
    }

    [Fact]
    public async Task LegacyResult_WithNullRankingsJson_ReadsSafely_WinnerStillFromWinningHorseId()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (raceId, winner, loser) = await Seed2HorseFinishedRaceAsync(f);

        // Simulate a pre-R0 legacy row: WinningHorseId set, RankingsJson
        // never populated — inserted directly, bypassing UpdateRaceResultAsync
        // (which always writes a valid RankingsJson under R0).
        f.Db.RaceResults.Add(new RaceResult
        {
            Id = Guid.NewGuid(),
            RaceId = raceId,
            WinningHorseId = winner,
            RankingsJson = null,
            RecordedAt = DateTime.UtcNow,
            Status = RaceResultStatus.Official,
            ApprovedAt = DateTime.UtcNow,
        });
        await f.Db.SaveChangesAsync();

        var read = await f.RaceSvc.GetRaceResultAsync(raceId);
        Assert.True(read.Result.Success);
        var dto = Assert.IsType<RaceResultResponse>(read.Result.Data);
        Assert.Equal(winner, dto.WinningHorseId);
        Assert.Null(dto.Rankings);
    }
}
