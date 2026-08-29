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
/// PRIZE-V1.2: percentage-based Prize allocation. Admin configures Prize.PercentageOfPool;
/// Prize.Amount is entirely backend-derived (Tournament.PrizePool * PercentageOfPool / 100,
/// decimal-rounded — see PrizeAmountCalculator). Covers percentage validity/total rules, Amount
/// derivation, the VND rounding-remainder strategy, PrizePool-update recalculation, and the
/// percentage-based Publish readiness rule (SUM(PercentageOfPool) == 100% is now the
/// source-of-truth completeness check, not the Amount sum). Wired against a real Sqlite in-memory
/// DB and the actual production services, reusing RaceLifecycleTests.LifecycleFixture and the
/// internal Phase5StructuralTestsHelper (defined in PrizeV1Tests.cs, same assembly) for
/// Publish-readiness setup — kept in its own file per the established one-file-per-increment
/// convention (PrizeV1Tests.cs / PrizeV1_1Tests.cs), so this task never risks the already-passing
/// earlier Prize suites.
/// </summary>
public class PrizeV1_2Tests
{
    private static PrizeService MakePrizeService(RaceLifecycleTests.LifecycleFixture f)
        => new PrizeService(new PrizeRepository(f.Db), f.TournamentRepo, f.UnitOfWork, f.Db, f.RaceSvc, f.FaultWallet);

    private static async Task<Guid> CreateDraftTournamentAsync(RaceLifecycleTests.LifecycleFixture f, decimal prizePool)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "PrizeV1.2 Tournament " + Guid.NewGuid().ToString("N")[..8],
            StartDate = start, EndDate = start.AddDays(10), RegistrationDeadline = start.AddDays(-1),
            MinParticipants = 3, MaxParticipants = 10, MaxRounds = 1, PrizePool = prizePool,
        });
        Assert.True(create.Result.Success, create.Result.Message);
        return create.Result.Data!.Id;
    }

    // ── CREATE ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePrize_PercentageZero_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 0 });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_NegativePercentage_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = -10 });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_PercentageAbove100_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 101 });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_ValidPercentage_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 12.5m });
        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(12.5m, result.Result.Data!.PercentageOfPool);
    }

    [Fact]
    public async Task CreatePrize_PercentageMoreThanTwoDecimals_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 33.333m });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreatePrize_TotalPercentageExceeds100_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);
        var first = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 60 });
        Assert.True(first.Result.Success, first.Result.Message);

        var second = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 50 });
        Assert.False(second.Result.Success);
        Assert.Equal(400, second.StatusCode);
        Assert.Contains("100%", second.Result.Message);
    }

    [Fact]
    public async Task CreatePrize_DerivesAmountFromPrizePool()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 2_000_000_000m);

        var result = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(1_000_000_000m, result.Result.Data!.Amount); // 50% of 2,000,000,000
    }

    // ── UPDATE ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePrize_RecalculatesAmount()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 2_000_000_000m);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 30 });
        Assert.True(create.Result.Success, create.Result.Message);
        Assert.Equal(600_000_000m, create.Result.Data!.Amount);

        var update = await svc.UpdateAsync(create.Result.Data!.Id, new UpdatePrizeRequest { Position = 1, PercentageOfPool = 50 });
        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal(1_000_000_000m, update.Result.Data!.Amount);
    }

    [Fact]
    public async Task UpdatePrize_TotalPercentageExceeds100_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 60 });
        var p2 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 30 });
        Assert.True(p2.Result.Success, p2.Result.Message);

        // 60% (p1) + 50% (p2 new) = 110% > 100%
        var update = await svc.UpdateAsync(p2.Result.Data!.Id, new UpdatePrizeRequest { Position = 2, PercentageOfPool = 50 });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task UpdatePrize_ValidPercentageChange_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 30 });
        Assert.True(create.Result.Success, create.Result.Message);

        var update = await svc.UpdateAsync(create.Result.Data!.Id, new UpdatePrizeRequest { Position = 1, PercentageOfPool = 40 });
        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal(40m, update.Result.Data!.PercentageOfPool);
    }

    [Fact]
    public async Task UpdatePrize_PercentageMoreThanTwoDecimals_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 1000);
        var create = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 30 });
        Assert.True(create.Result.Success, create.Result.Message);

        var update = await svc.UpdateAsync(create.Result.Data!.Id, new UpdatePrizeRequest { Position = 1, PercentageOfPool = 33.333m });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    // ── PRIZEPOOL RECALCULATION (Part 5) ──────────────────────────────────

    [Fact]
    public async Task UpdateDraftPrizePool_RecalculatesExistingAmounts()
    {
        // Task's own worked example: PrizePool 2b -> 3b, percentages 50/30/20 unchanged,
        // Amounts become 1.5b/900m/600m.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 2_000_000_000m);
        var p1 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        var p2 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 30 });
        var p3 = await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 3, PercentageOfPool = 20 });
        Assert.True(p1.Result.Success, p1.Result.Message);
        Assert.True(p2.Result.Success, p2.Result.Message);
        Assert.True(p3.Result.Success, p3.Result.Message);

        var update = await f.TournamentSvc.UpdateTournamentAsync(tid, new UpdateTournamentRequest { PrizePool = 3_000_000_000m });
        Assert.True(update.Result.Success, update.Result.Message);

        var rows = await f.Db.Prizes.Where(p => p.TournamentId == tid).OrderBy(p => p.Position).ToListAsync();
        Assert.Equal(1_500_000_000m, rows[0].Amount);
        Assert.Equal(900_000_000m, rows[1].Amount);
        Assert.Equal(600_000_000m, rows[2].Amount);
    }

    [Fact]
    public async Task UpdatePrizePool_PreservesPercentages()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 2_000_000_000m);
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 30 });

        var update = await f.TournamentSvc.UpdateTournamentAsync(tid, new UpdateTournamentRequest { PrizePool = 3_000_000_000m });
        Assert.True(update.Result.Success, update.Result.Message);

        var rows = await f.Db.Prizes.Where(p => p.TournamentId == tid).OrderBy(p => p.Position).ToListAsync();
        Assert.Equal(50m, rows[0].PercentageOfPool);
        Assert.Equal(30m, rows[1].PercentageOfPool);
    }

    // ── ROUNDING (Part 4) ──────────────────────────────────────────────────

    [Fact]
    public async Task PercentageAllocation_33_33_33_33_33_34_TotalAmountEqualsPrizePool()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 3_000_000m);
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 33.33m });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 33.33m });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 3, PercentageOfPool = 33.34m });

        var rows = await f.Db.Prizes.Where(p => p.TournamentId == tid).OrderBy(p => p.Position).ToListAsync();
        Assert.Equal(3_000_000m, rows.Sum(p => p.Amount));
    }

    [Fact]
    public async Task FinalConfiguredRankAbsorbsRoundingRemainder()
    {
        // PrizePool=100, 33.33/33.33/33.34: naive per-row rounding would give 33+33+33=99 (a
        // 1-VND remainder lost) — the last configured row (by Position) must absorb it instead,
        // landing on 33+33+34=100 exactly.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var svc = MakePrizeService(f);
        var tid = await CreateDraftTournamentAsync(f, 100m);
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 33.33m });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 2, PercentageOfPool = 33.33m });
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 3, PercentageOfPool = 33.34m });

        var rows = await f.Db.Prizes.Where(p => p.TournamentId == tid).OrderBy(p => p.Position).ToListAsync();
        Assert.Equal(33m, rows[0].Amount);
        Assert.Equal(33m, rows[1].Amount);
        Assert.Equal(34m, rows[2].Amount); // absorbs the rounding remainder
        Assert.Equal(100m, rows.Sum(p => p.Amount));
    }

    // ── PUBLISH READINESS (Part 10) ───────────────────────────────────────

    [Fact]
    public async Task Publish_PercentageTotalLessThan100_Rejected()
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
        await svc.CreateAsync(new CreatePrizeRequest { TournamentId = tid, Position = 1, PercentageOfPool = 50 });

        var publish = await f.TournamentSvc.ChangeStatusAsync(tid, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());
        Assert.False(publish.Result.Success);
        Assert.Equal(400, publish.StatusCode);
        Assert.Contains("100%", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_PercentageTotalEquals100_Succeeds()
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

    [Fact]
    public async Task Publish_PercentageTotalAbove100_DefensivelyRejected()
    {
        // The write API can no longer produce this state (Create/Update both reject a total over
        // 100%) — seed directly to prove Publish's own defensive re-check catches legacy/direct-
        // DB-write data.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tid, roundId, roundStart, roundEnd) = await Phase5StructuralTestsHelper.BuildDraftSingleFinalRoundAsync(f, prizePool: 1000);
        var track = await Phase5StructuralTestsHelper.CreateTrackAsync(f, capacity: 5);
        await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tid, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        f.Db.Prizes.AddRange(
            new Prize { Id = Guid.NewGuid(), TournamentId = tid, Name = "A", Amount = 600, PercentageOfPool = 60, Position = 1, CreatedAt = DateTime.UtcNow },
            new Prize { Id = Guid.NewGuid(), TournamentId = tid, Name = "B", Amount = 600, PercentageOfPool = 60, Position = 2, CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();

        var publish = await f.TournamentSvc.ChangeStatusAsync(tid, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());
        Assert.False(publish.Result.Success);
        Assert.Equal(400, publish.StatusCode);
        Assert.Contains("100%", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_AmountSumMatchesPrizePool()
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

        var total = await f.Db.Prizes.Where(p => p.TournamentId == tid).SumAsync(p => p.Amount);
        Assert.Equal(1000m, total);
    }
}
