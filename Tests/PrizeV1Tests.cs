using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// PRIZE-V1 / PRIZE-V1.2: Tournament Prize Allocation Management. Prize rows allocate
/// Tournament.PrizePool by FINAL Tournament ranking Position via PercentageOfPool — Admin
/// configures a percentage, Amount is entirely backend-derived (see PrizeAmountCalculator).
/// Draft-only mutable, read-only once Published/Ongoing/Finished/Cancelled. Covers Create/Update/
/// Delete rules, PrizePool mutability, Publish readiness, the DB-level unique (TournamentId,
/// Position) index, and public read access. Reuses RaceLifecycleTests.LifecycleFixture for a real
/// Sqlite in-memory DB and the production TournamentService; PrizeService is constructed directly
/// since the shared fixture does not expose it.
///
/// PRIZE-V1.2 migration note: tests that submitted a request-level Amount (CreatePrizeRequest/
/// UpdatePrizeRequest) were converted to submit an equivalent PercentageOfPool instead — Amount
/// was removed from both write DTOs (Part 6). Tests that seed a Prize ENTITY directly (bypassing
/// the DTO layer, e.g. DB-constraint/legacy-row tests) are unchanged, since Prize.Amount still
/// exists on the entity. Two tests whose entire premise became obsolete under the new design
/// (see PrizeV1_1_2Tests.cs for their percentage-based replacements) were removed rather than
/// force-fit: CreatePrize_ZeroPrizePool_AnyPositiveAmountRejected (PercentageOfPool validity is
/// now independent of the current PrizePool value — see PRIZE-V1.2 report) and the two
/// "lower PrizePool below allocated Amount" tests (PrizePool changes now always succeed and
/// recalculate Amounts, never reject — Part 5).
/// </summary>
public class PrizeV1Tests
{
    private static PrizeService MakePrizeService(RaceLifecycleTests.LifecycleFixture f)
        => new PrizeService(new PrizeRepository(f.Db), f.TournamentRepo, f.UnitOfWork, f.Db, f.RaceSvc, f.FaultWallet);

    private static async Task<Guid> CreateDraftTournamentAsync(RaceLifecycleTests.LifecycleFixture f, decimal prizePool)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "Prize Tournament " + Guid.NewGuid().ToString("N")[..8],
            StartDate = start,
            EndDate = start.AddDays(10),
            RegistrationDeadline = start.AddDays(-1),
            MinParticipants = 3,
            MaxParticipants = 10,
            MaxRounds = 1,
            PrizePool = prizePool,
        });
        Assert.True(create.Result.Success, create.Result.Message);
        return create.Result.Data!.Id;
    }

    private static async Task PublishStatusOnlyAsync(RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, TournamentStatus status)
    {
        // Bypass full structural Publish-readiness (Round/Race/Track setup is irrelevant to Prize
        // tests) by seeding the Tournament.Status directly — these tests target Prize CRUD guards
        // that key off Tournament.Status, not the full Publish workflow.
        var t = await f.Db.Tournaments.FirstAsync(x => x.Id == tournamentId);
        t.Status = status;
        await f.Db.SaveChangesAsync();
    }

    // ── CREATE ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePrize_NoTournamentId_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = null, Position = 1, PercentageOfPool = 50 });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_TournamentNotFound_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = Guid.NewGuid(), Position = 1, PercentageOfPool = 50 });
        Assert.False(result.Result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_TournamentPublished_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        await PublishStatusOnlyAsync(f, tid, TournamentStatus.Published);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_PositionZero_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 0, PercentageOfPool = 50 });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_PositionNegative_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = -1, PercentageOfPool = 50 });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_DuplicatePosition_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        var first = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(first.Result.Success, first.Result.Message);

        var dup = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 30 });
        Assert.False(dup.Result.Success);
        Assert.Equal(409, dup.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_SamePositionDifferentTournament_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid1 = await CreateDraftTournamentAsync(f, 10000);
        var tid2 = await CreateDraftTournamentAsync(f, 10000);
        var p1 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid1, Position = 1, PercentageOfPool = 50 });
        Assert.True(p1.Result.Success, p1.Result.Message);

        var p2 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid2, Position = 1, PercentageOfPool = 50 });
        Assert.True(p2.Result.Success, p2.Result.Message);
    }

    [Fact]
    public async Task CreatePrize_SumExceedsPrizePool_Rejected()
    {
        // PRIZE-V1.2: "sum" is now a percentage sum (80% + 50% > 100%), not a dollar sum.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);
        var first = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 80 });
        Assert.True(first.Result.Success, first.Result.Message);

        var second = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 50 });
        Assert.False(second.Result.Success);
        Assert.Equal(400, second.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_SumExactlyEqualsPrizePool_Allowed()
    {
        // PRIZE-V1.2: percentages summing to exactly 100% (not Amounts summing to PrizePool).
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);
        var first = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 70 });
        Assert.True(first.Result.Success, first.Result.Message);

        var second = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 30 });
        Assert.True(second.Result.Success, second.Result.Message);
    }

    [Fact]
    public async Task CreatePrize_ValidRow_SetsInertLegacyFieldsAndDerivesAmount()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50, SponsorName = "Acme" });
        Assert.True(result.Result.Success, result.Result.Message);

        var row = await f.Db.Prizes.FirstAsync(p => p.Id == result.Result.Data!.Id);
        Assert.Null(row.RaceId);
        Assert.Equal("VND", row.Currency);
        Assert.Equal(50m, row.PercentageOfPool); // PRIZE-V1.2: now Admin-controlled, not hardcoded 0
        Assert.Equal(5000m, row.Amount); // 50% of 10,000 — backend-derived
        Assert.False(row.IsDistributed);
        Assert.Null(row.DistributedAt);
        Assert.Equal("Acme", row.SponsorName);
    }

    [Fact]
    public async Task CreatePrize_GapInPositions_AllowedInDraft()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        var p1 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(p1.Result.Success, p1.Result.Message);

        // Position 2 skipped entirely — Draft allows temporary gaps, contiguity is Publish-only.
        var p3 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 3, PercentageOfPool = 20 });
        Assert.True(p3.Result.Success, p3.Result.Message);
    }

    // ── UPDATE ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePrize_NotFound_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var result = await svc.UpdateAsync(Guid.NewGuid(), new UpdatePrizeRequest { Position = 1, PercentageOfPool = 50 });
        Assert.False(result.Result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UpdatePrize_TournamentPublished_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);
        await PublishStatusOnlyAsync(f, tid, TournamentStatus.Published);

        var update = await svc.UpdateAsync(create.Result.Data!.Id, new UpdatePrizeRequest { Position = 1, PercentageOfPool = 60 });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task UpdatePrize_PositionInvalid_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);

        var update = await svc.UpdateAsync(create.Result.Data!.Id, new UpdatePrizeRequest { Position = 0, PercentageOfPool = 50 });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task UpdatePrize_PercentageInvalid_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);

        var update = await svc.UpdateAsync(create.Result.Data!.Id, new UpdatePrizeRequest { Position = 1, PercentageOfPool = 0 });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task UpdatePrize_ToDuplicatePosition_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        var p2 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 20 });
        Assert.True(p2.Result.Success, p2.Result.Message);

        var update = await svc.UpdateAsync(p2.Result.Data!.Id, new UpdatePrizeRequest { Position = 1, PercentageOfPool = 20 });
        Assert.False(update.Result.Success);
        Assert.Equal(409, update.StatusCode);
    }

    [Fact]
    public async Task UpdatePrize_ToOwnCurrentPosition_AllowedAndRecalculatesAmount()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);

        // Same Position(1), new PercentageOfPool — must not self-collide with the duplicate-position check.
        var update = await svc.UpdateAsync(create.Result.Data!.Id, new UpdatePrizeRequest { Position = 1, PercentageOfPool = 80 });
        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal(80m, update.Result.Data!.PercentageOfPool);
        Assert.Equal(8000m, update.Result.Data!.Amount); // 80% of 10,000, re-derived
    }

    [Fact]
    public async Task UpdatePrize_SumExceedsPrizePool_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);
        var p1 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        var p2 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 40 });
        Assert.True(p2.Result.Success, p2.Result.Message);

        // 50% (p1) + 70% (p2 new) = 120% > 100%
        var update = await svc.UpdateAsync(p2.Result.Data!.Id, new UpdatePrizeRequest { Position = 2, PercentageOfPool = 70 });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task UpdatePrize_TournamentIdCannotBeReassigned()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        var otherTid = await CreateDraftTournamentAsync(f, 10000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);

        // UpdatePrizeRequest has no TournamentId field at all — verify the persisted row's
        // TournamentId is untouched by an Update regardless.
        await svc.UpdateAsync(create.Result.Data!.Id, new UpdatePrizeRequest { Position = 1, PercentageOfPool = 60 });
        var row = await f.Db.Prizes.FirstAsync(p => p.Id == create.Result.Data!.Id);
        Assert.Equal(tid, row.TournamentId);
        Assert.NotEqual(otherTid, row.TournamentId);
    }

    // ── DELETE ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePrize_NotFound_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var result = await svc.DeleteAsync(Guid.NewGuid());
        Assert.False(result.Result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task DeletePrize_DraftTournament_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);

        var delete = await svc.DeleteAsync(create.Result.Data!.Id);
        Assert.True(delete.Result.Success, delete.Result.Message);
        Assert.False(await f.Db.Prizes.AnyAsync(p => p.Id == create.Result.Data!.Id));
    }

    [Fact]
    public async Task DeletePrize_PublishedTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);
        await PublishStatusOnlyAsync(f, tid, TournamentStatus.Published);

        var delete = await svc.DeleteAsync(create.Result.Data!.Id);
        Assert.False(delete.Result.Success);
        Assert.Equal(400, delete.StatusCode);
        Assert.True(await f.Db.Prizes.AnyAsync(p => p.Id == create.Result.Data!.Id));
    }

    [Fact]
    public async Task DeletePrize_OngoingTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 10000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(create.Result.Success, create.Result.Message);
        await PublishStatusOnlyAsync(f, tid, TournamentStatus.Ongoing);

        var delete = await svc.DeleteAsync(create.Result.Data!.Id);
        Assert.False(delete.Result.Success);
        Assert.Equal(400, delete.StatusCode);
    }

    [Fact]
    public async Task DeletePrize_OrphanRaceScopedRow_AllowedRegardlessOfStatus()
    {
        // Legacy race-scoped Prize (TournamentId == null) is outside the V1 Draft-lock: its
        // deletion is never blocked by Tournament.Status since it has none to check.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var orphan = new Prize { Id = Guid.NewGuid(), TournamentId = null, RaceId = Guid.NewGuid(), Name = "Legacy", Amount = 500, Position = 1, CreatedAt = DateTime.UtcNow };
        f.Db.Prizes.Add(orphan);
        await f.Db.SaveChangesAsync();

        var delete = await svc.DeleteAsync(orphan.Id);
        Assert.True(delete.Result.Success, delete.Result.Message);
    }

    // ── PRIZEPOOL MUTABILITY (via TournamentService.UpdateTournamentAsync) ───

    [Fact]
    public async Task UpdateTournament_PrizePool_Draft_Increase_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tid = await CreateDraftTournamentAsync(f, 1000);
        var update = await f.TournamentSvc.UpdateTournamentAsync(tid, new UpdateTournamentRequest { PrizePool = 2000 });
        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal(2000m, update.Result.Data!.PrizePool);
    }

    [Fact]
    public async Task UpdateTournament_PrizePool_Published_AnyChangeRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tid = await CreateDraftTournamentAsync(f, 1000);
        await PublishStatusOnlyAsync(f, tid, TournamentStatus.Published);

        var update = await f.TournamentSvc.UpdateTournamentAsync(tid, new UpdateTournamentRequest { PrizePool = 1500 });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task UpdateTournament_PrizePool_Published_SameValue_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tid = await CreateDraftTournamentAsync(f, 1000);
        await PublishStatusOnlyAsync(f, tid, TournamentStatus.Published);

        // Submitting the same PrizePool value alongside other legitimately-mutable fields must not
        // be treated as a change.
        var update = await f.TournamentSvc.UpdateTournamentAsync(tid, new UpdateTournamentRequest { PrizePool = 1000, Name = "Renamed" });
        Assert.True(update.Result.Success, update.Result.Message);
    }

    [Fact]
    public async Task UpdateTournament_Name_Published_StillMutable()
    {
        // Regression guard: Part 4's PrizePool-immutability change must not have also locked Name.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tid = await CreateDraftTournamentAsync(f, 1000);
        await PublishStatusOnlyAsync(f, tid, TournamentStatus.Published);

        var update = await f.TournamentSvc.UpdateTournamentAsync(tid, new UpdateTournamentRequest { Name = "New Name" });
        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal("New Name", update.Result.Data!.Name);
    }

    // ── PUBLISH READINESS ───────────────────────────────────────────────

    [Fact]
    public async Task Publish_ZeroPrizePool_ZeroPrizeRows_Valid()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tid, roundId, roundStart, roundEnd) = await Phase5StructuralTestsHelper.BuildDraftSingleFinalRoundAsync(f, prizePool: 0);
        var track = await Phase5StructuralTestsHelper.CreateTrackAsync(f, capacity: 5);
        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tid, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(race.Result.Success, race.Result.Message);

        var publish = await f.TournamentSvc.ChangeStatusAsync(tid, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());
        Assert.True(publish.Result.Success, publish.Result.Message);
    }

    [Fact]
    public async Task Publish_PrizePoolZero_WithExistingPrizeRows_Rejected()
    {
        // FINAL HARDENING Part 1: the current Create/Update API can no longer produce this state
        // (Publish itself requires PrizePool == 0 to have zero rows), but historical databases may
        // already contain Prize rows written before that validation existed — seed directly to
        // prove Publish still catches this legacy state instead of silently allowing it.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tid, roundId, roundStart, roundEnd) = await Phase5StructuralTestsHelper.BuildDraftSingleFinalRoundAsync(f, prizePool: 0);
        var track = await Phase5StructuralTestsHelper.CreateTrackAsync(f, capacity: 5);
        await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tid, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        f.Db.Prizes.Add(new Prize { Id = Guid.NewGuid(), TournamentId = tid, Name = "Legacy Row", Amount = 500, Position = 1, CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();

        var publish = await f.TournamentSvc.ChangeStatusAsync(tid, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());
        Assert.False(publish.Result.Success);
        Assert.Equal(400, publish.StatusCode);
        Assert.Contains("Giải đấu không có quỹ thưởng nhưng vẫn tồn tại cơ cấu giải thưởng.", publish.Result.Message);
        // The legacy row must never be auto-deleted as a side effect of a failed Publish attempt.
        Assert.True(await f.Db.Prizes.AnyAsync(p => p.TournamentId == tid));
    }

    [Fact]
    public async Task Publish_PositivePrizePool_ZeroPrizeRows_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tid, roundId, roundStart, roundEnd) = await Phase5StructuralTestsHelper.BuildDraftSingleFinalRoundAsync(f, prizePool: 1000);
        var track = await Phase5StructuralTestsHelper.CreateTrackAsync(f, capacity: 5);
        await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tid, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });

        var publish = await f.TournamentSvc.ChangeStatusAsync(tid, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());
        Assert.False(publish.Result.Success);
        Assert.Equal(400, publish.StatusCode);
    }

    [Fact]
    public async Task Publish_PositivePrizePool_PercentageSumBelow100_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var (tid, roundId, roundStart, roundEnd) = await Phase5StructuralTestsHelper.BuildDraftSingleFinalRoundAsync(f, prizePool: 1000);
        var track = await Phase5StructuralTestsHelper.CreateTrackAsync(f, capacity: 5);
        await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tid, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 80 });

        var publish = await f.TournamentSvc.ChangeStatusAsync(tid, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());
        Assert.False(publish.Result.Success);
        Assert.Equal(400, publish.StatusCode);
    }

    [Fact]
    public async Task Publish_PositivePrizePool_NonContiguousPositions_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var (tid, roundId, roundStart, roundEnd) = await Phase5StructuralTestsHelper.BuildDraftSingleFinalRoundAsync(f, prizePool: 1000);
        var track = await Phase5StructuralTestsHelper.CreateTrackAsync(f, capacity: 5);
        await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tid, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        // Positions 1 and 3 (gap at 2) — percentages still sum to 100%, isolating the contiguity check.
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 70 });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 3, PercentageOfPool = 30 });

        var publish = await f.TournamentSvc.ChangeStatusAsync(tid, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());
        Assert.False(publish.Result.Success);
        Assert.Equal(400, publish.StatusCode);
    }

    [Fact]
    public async Task Publish_PositivePrizePool_ContiguousExactSum_Valid()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var (tid, roundId, roundStart, roundEnd) = await Phase5StructuralTestsHelper.BuildDraftSingleFinalRoundAsync(f, prizePool: 1000);
        var track = await Phase5StructuralTestsHelper.CreateTrackAsync(f, capacity: 5);
        await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tid, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 60 });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 30 });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 3, PercentageOfPool = 10 });

        var publish = await f.TournamentSvc.ChangeStatusAsync(tid, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());
        Assert.True(publish.Result.Success, publish.Result.Message);
    }

    // ── DB CONSTRAINT ───────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateTournamentIdPosition_RejectedByDbConstraint()
    {
        // Proves the unique index itself, bypassing PrizeService entirely.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tid = await CreateDraftTournamentAsync(f, 10000);
        f.Db.Prizes.AddRange(
            new Prize { Id = Guid.NewGuid(), TournamentId = tid, Name = "A", Amount = 100, Position = 1, CreatedAt = DateTime.UtcNow },
            new Prize { Id = Guid.NewGuid(), TournamentId = tid, Name = "B", Amount = 100, Position = 1, CreatedAt = DateTime.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task MultipleNullTournamentIdSamePosition_NotBlockedByFilteredIndex()
    {
        // The unique index is filtered to TournamentId IS NOT NULL — legacy race-scoped rows with
        // TournamentId == null must not collide with each other on Position.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        f.Db.Prizes.AddRange(
            new Prize { Id = Guid.NewGuid(), TournamentId = null, RaceId = Guid.NewGuid(), Name = "A", Amount = 100, Position = 1, CreatedAt = DateTime.UtcNow },
            new Prize { Id = Guid.NewGuid(), TournamentId = null, RaceId = Guid.NewGuid(), Name = "B", Amount = 100, Position = 1, CreatedAt = DateTime.UtcNow });

        await f.Db.SaveChangesAsync(); // must not throw
        Assert.Equal(2, await f.Db.Prizes.CountAsync(p => p.TournamentId == null && p.Position == 1));
    }

    // ── PUBLIC READ ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetByTournament_ReturnsPositionOrder()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 3, PercentageOfPool = 10 });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 60 });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 30 });

        var result = await svc.GetByTournamentAsync(tid);
        Assert.True(result.Result.Success, result.Result.Message);
        var positions = result.Result.Data!.Select(p => p.Position).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, positions);
    }

    [Fact]
    public async Task GetByTournament_EmptyForTournamentWithNoPrizes()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);

        var result = await svc.GetByTournamentAsync(tid);
        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Empty(result.Result.Data!);
    }

    [Fact]
    public void PrizeResponse_ExposesPercentageAndAmountButNotDistributionFields()
    {
        // PRIZE-V1.2 Part 12: PercentageOfPool is now DELIBERATELY public (Owner/Jockey/Spectator
        // need to see it) — only IsDistributed/DistributedAt/RaceId/Currency stay hidden, since no
        // payout/distribution mechanism exists in this contract.
        var props = typeof(PrizeResponse).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("PercentageOfPool", props);
        Assert.Contains("Amount", props);
        Assert.DoesNotContain("IsDistributed", props);
        Assert.DoesNotContain("DistributedAt", props);
        Assert.DoesNotContain("RaceId", props);
        Assert.DoesNotContain("Currency", props);
    }
}

/// <summary>Small local helpers mirroring Phase5StructuralTests' private builders, needed here
/// because those are private to that class; kept minimal and scoped to what PrizeV1Tests needs
/// (a Draft single-Final-Round Tournament with a controllable PrizePool).</summary>
internal static class Phase5StructuralTestsHelper
{
    public static async Task<(Guid tournamentId, Guid roundId, DateTime roundStart, DateTime roundEnd)> BuildDraftSingleFinalRoundAsync(
        RaceLifecycleTests.LifecycleFixture f, decimal prizePool, int maxParticipants = 5, int minParticipants = 3)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(20);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "Prize Publish Tournament " + Guid.NewGuid().ToString("N")[..8],
            StartDate = start, EndDate = end, RegistrationDeadline = start.AddDays(-1),
            MinParticipants = minParticipants, MaxParticipants = maxParticipants, MaxRounds = 1,
            PrizePool = prizePool,
        });
        Assert.True(create.Result.Success, create.Result.Message);
        var tournamentId = create.Result.Data!.Id;

        var roundStart = start;
        var roundEnd = start.AddDays(5);
        var round = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Final", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = roundStart, ScheduledEndDate = roundEnd, AdvanceCount = 0
        });
        Assert.True(round.Result.Success, round.Result.Message);

        return (tournamentId, round.Result.Data!.Id, roundStart, roundEnd);
    }

    public static async Task<Guid> CreateTrackAsync(RaceLifecycleTests.LifecycleFixture f, int? capacity)
    {
        var track = new Track { Id = Guid.NewGuid(), Name = $"Track-{Guid.NewGuid():N}", Capacity = capacity, CreatedAt = DateTime.UtcNow };
        f.Db.Add(track);
        await f.Db.SaveChangesAsync();
        return track.Id;
    }
}
