using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// R0.1: Result/Referee safety guards on top of R0's full-ranking Official
/// result. Covers: latest-health-check StartRace readiness, unresolved
/// Violation/Protest blocking Approve, and post-Official immutability of
/// Violation/Protest. Reuses RaceLifecycleTests.LifecycleFixture.
/// </summary>
public class R01ResultRefereeSafetyTests
{
    private static SubmitRaceResultRequest Ranking(params (Guid HorseId, int Position)[] items) =>
        new()
        {
            Rankings = items.Select(i => new SubmitRankingEntry { HorseId = i.HorseId, Position = i.Position, Status = "Completed" }).ToList()
        };

    private static async Task ProgressToFinishedAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId)
    {
        await f.RaceManagement.OpenRegistrationAsync(raceId);
        await f.RaceManagement.CloseRegistrationAsync(raceId);
        await f.RaceManagement.StartRaceAsync(raceId);
        await f.RaceManagement.EndRaceAsync(raceId);
    }

    private static async Task AddHealthCheckAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid horseId, Guid raceId,
        HealthCheckStatus status, bool approvedToRace, DateTime checkedAt)
    {
        f.Db.HorseHealthChecks.Add(new HorseHealthCheck
        {
            Id = Guid.NewGuid(),
            HorseId = horseId,
            RaceId = raceId,
            RefereeId = f.RefereeId,
            Status = status,
            ApprovedToRace = approvedToRace,
            CheckedAt = checkedAt,
        });
        await f.Db.SaveChangesAsync();
    }

    // ── A: latest health check is authoritative for StartRace ──────────────

    [Fact]
    public async Task StartRace_OldPassedButLatestFailed_IsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync(); // seeds Passed+Approved for both horses "now"

        await AddHealthCheckAsync(f, race.WinnerHorseId, race.Id, HealthCheckStatus.Failed, approvedToRace: false, DateTime.UtcNow.AddMinutes(5));

        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        var start = await f.RaceManagement.StartRaceAsync(race.Id);
        Assert.False(start.Result.Success);
    }

    [Fact]
    public async Task StartRace_OldFailedButLatestPassedApproved_ReadinessPasses()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();

        var seeded = await f.Db.HorseHealthChecks.FirstAsync(h => h.HorseId == race.WinnerHorseId && h.RaceId == race.Id);
        seeded.Status = HealthCheckStatus.Failed;
        seeded.ApprovedToRace = false;
        seeded.CheckedAt = DateTime.UtcNow.AddHours(-1);
        await f.Db.SaveChangesAsync();

        await AddHealthCheckAsync(f, race.WinnerHorseId, race.Id, HealthCheckStatus.Passed, approvedToRace: true, DateTime.UtcNow);

        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        var start = await f.RaceManagement.StartRaceAsync(race.Id);
        Assert.True(start.Result.Success, start.Result.Message);
    }

    [Fact]
    public async Task StartRace_LatestRequiresRecheck_IsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();

        await AddHealthCheckAsync(f, race.WinnerHorseId, race.Id, HealthCheckStatus.RequiresRecheck, approvedToRace: false, DateTime.UtcNow.AddMinutes(5));

        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        var start = await f.RaceManagement.StartRaceAsync(race.Id);
        Assert.False(start.Result.Success);
    }

    [Fact]
    public async Task StartRace_NoHealthCheckAtAll_IsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();

        var winnerChecks = f.Db.HorseHealthChecks.Where(h => h.HorseId == race.WinnerHorseId && h.RaceId == race.Id);
        f.Db.HorseHealthChecks.RemoveRange(winnerChecks);
        await f.Db.SaveChangesAsync();

        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        var start = await f.RaceManagement.StartRaceAsync(race.Id);
        Assert.False(start.Result.Success);
    }

    // ── E: unresolved Violation/Protest block Approve ───────────────────────

    [Fact]
    public async Task Approve_UnresolvedViolation_IsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedAsync(f, race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, Ranking((race.WinnerHorseId, 1), (race.LoserHorseId, 2)));
        await f.AddMandatoryReportAsync(race.Id);

        var winnerEntryId = (await f.EntryRepo.GetByRaceAsync(race.Id)).Single(e => e.HorseId == race.WinnerHorseId).Id;
        f.Db.ViolationRecords.Add(new ViolationRecord
        {
            Id = Guid.NewGuid(), RaceId = race.Id, RaceEntryId = winnerEntryId, RefereeId = f.RefereeId,
            ViolationType = ViolationType.Interference, Description = "test violation",
            RecordedAt = DateTime.UtcNow, PenaltyType = null,
        });
        await f.Db.SaveChangesAsync();

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.False(approve.Result.Success);
        Assert.Equal(409, approve.StatusCode);
    }

    [Fact]
    public async Task Approve_ResolvedViolation_DoesNotBlock()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedAsync(f, race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, Ranking((race.WinnerHorseId, 1), (race.LoserHorseId, 2)));
        await f.AddMandatoryReportAsync(race.Id);

        var winnerEntryId = (await f.EntryRepo.GetByRaceAsync(race.Id)).Single(e => e.HorseId == race.WinnerHorseId).Id;
        f.Db.ViolationRecords.Add(new ViolationRecord
        {
            Id = Guid.NewGuid(), RaceId = race.Id, RaceEntryId = winnerEntryId, RefereeId = f.RefereeId,
            ViolationType = ViolationType.Interference, Description = "test violation",
            RecordedAt = DateTime.UtcNow, PenaltyType = "Cảnh cáo bằng lời",
        });
        await f.Db.SaveChangesAsync();

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.True(approve.Result.Success, approve.Result.Message);
    }

    [Theory]
    [InlineData(ProtestStatus.Pending)]
    [InlineData(ProtestStatus.UnderReview)]
    public async Task Approve_OpenProtest_IsRejected(ProtestStatus status)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedAsync(f, race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, Ranking((race.WinnerHorseId, 1), (race.LoserHorseId, 2)));
        await f.AddMandatoryReportAsync(race.Id);

        var winnerEntryId = (await f.EntryRepo.GetByRaceAsync(race.Id)).Single(e => e.HorseId == race.WinnerHorseId).Id;
        f.Db.Protests.Add(new Protest
        {
            Id = Guid.NewGuid(), RaceId = race.Id, FiledByUserId = f.RefereeUserId, AgainstEntryId = winnerEntryId,
            Reason = "test protest", Status = status, FiledAt = DateTime.UtcNow,
        });
        await f.Db.SaveChangesAsync();

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.False(approve.Result.Success);
        Assert.Equal(409, approve.StatusCode);
    }

    [Theory]
    [InlineData(ProtestStatus.Upheld)]
    [InlineData(ProtestStatus.Rejected)]
    [InlineData(ProtestStatus.Withdrawn)]
    public async Task Approve_TerminalProtest_DoesNotBlock(ProtestStatus status)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedAsync(f, race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, Ranking((race.WinnerHorseId, 1), (race.LoserHorseId, 2)));
        await f.AddMandatoryReportAsync(race.Id);

        var winnerEntryId = (await f.EntryRepo.GetByRaceAsync(race.Id)).Single(e => e.HorseId == race.WinnerHorseId).Id;
        f.Db.Protests.Add(new Protest
        {
            Id = Guid.NewGuid(), RaceId = race.Id, FiledByUserId = f.RefereeUserId, AgainstEntryId = winnerEntryId,
            Reason = "test protest", Status = status, FiledAt = DateTime.UtcNow,
        });
        await f.Db.SaveChangesAsync();

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.True(approve.Result.Success, approve.Result.Message);
    }

    [Fact]
    public async Task Approve_MissingRaceReport_StillBlocks()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedAsync(f, race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, Ranking((race.WinnerHorseId, 1), (race.LoserHorseId, 2)));
        // No AddMandatoryReportAsync call.

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.False(approve.Result.Success);
    }

    // ── F: post-Official immutability ───────────────────────────────────────

    private static async Task<Guid> ReachOfficialAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedAsync(f, race.Id);
        await f.LiveResult.UpdateRaceResultAsync(race.Id, Ranking((race.WinnerHorseId, 1), (race.LoserHorseId, 2)));
        await f.AddMandatoryReportAsync(race.Id);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.True(approve.Result.Success, approve.Result.Message);
        return race.Id;
    }

    [Fact]
    public async Task CreateViolation_AfterOfficial_IsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var raceId = await ReachOfficialAsync(f);
        var winnerEntry = (await f.EntryRepo.GetByRaceAsync(raceId)).First();

        var result = await f.ViolationSvc.RecordViolationAsync(new CreateViolationRequest
        {
            RaceId = raceId,
            HorseId = winnerEntry.HorseId,
            ViolationType = (int)ViolationType.Interference,
            Description = "attempted post-official violation",
        }, f.RefereeUserId);

        Assert.False(result.Result.Success);
    }

    /// <summary>
    /// Under the normal flow, Approve already blocks Official while any
    /// unresolved Violation exists (see Approve_UnresolvedViolation_IsRejected),
    /// and Violation creation is InProgress-only — so a legitimately-created
    /// unresolved Violation can never coexist with an Official result. This
    /// test simulates the only way that state could otherwise arise (direct
    /// data manipulation / a hypothetical future bypass) to prove the
    /// defense-in-depth guard on ResolveViolationAsync itself still holds.
    /// </summary>
    [Fact]
    public async Task ResolveViolation_AfterOfficial_IsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var raceId = await ReachOfficialAsync(f);
        var winnerEntry = (await f.EntryRepo.GetByRaceAsync(raceId)).First();

        var violation = new ViolationRecord
        {
            Id = Guid.NewGuid(), RaceId = raceId, RaceEntryId = winnerEntry.Id, RefereeId = f.RefereeId,
            ViolationType = ViolationType.Interference, Description = "post-official simulated violation",
            RecordedAt = DateTime.UtcNow, PenaltyType = null,
        };
        f.Db.ViolationRecords.Add(violation);
        await f.Db.SaveChangesAsync();

        var resolve = await f.Admin.ResolveViolationAsync(violation.Id, "Cảnh cáo");
        Assert.False(resolve.Result.Success);
        Assert.Equal(409, resolve.StatusCode);
    }

    [Fact]
    public async Task CreateProtest_AfterOfficial_IsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var raceId = await ReachOfficialAsync(f);
        var winnerEntry = (await f.EntryRepo.GetByRaceAsync(raceId)).First();

        var result = await f.ProtestSvc.FileAsync(new CreateProtestRequest
        {
            RaceId = raceId,
            AgainstEntryId = winnerEntry.Id,
            Reason = "attempted post-official protest",
        }, f.RefereeUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(409, result.StatusCode);
    }

    /// <summary>See ResolveViolation_AfterOfficial_IsRejected for why a Pending
    /// Protest can only coexist with Official via simulated direct data
    /// manipulation, not the normal Approve/File flow.</summary>
    [Fact]
    public async Task RuleProtest_AfterOfficial_IsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var raceId = await ReachOfficialAsync(f);
        var winnerEntry = (await f.EntryRepo.GetByRaceAsync(raceId)).First();

        var protest = new Protest
        {
            Id = Guid.NewGuid(), RaceId = raceId, FiledByUserId = f.RefereeUserId, AgainstEntryId = winnerEntry.Id,
            Reason = "post-official simulated protest", Status = ProtestStatus.Pending, FiledAt = DateTime.UtcNow,
        };
        f.Db.Protests.Add(protest);
        await f.Db.SaveChangesAsync();

        var rule = await f.ProtestSvc.RuleAsync(protest.Id, new RuleProtestRequest { Ruling = "Upheld - test" }, f.RefereeUserId);
        Assert.False(rule.Result.Success);
        Assert.Equal(409, rule.StatusCode);
    }

    [Fact]
    public async Task ResultResubmit_AfterOfficial_ExistingRejectionPreserved()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var raceId = await ReachOfficialAsync(f);
        var entries = await f.EntryRepo.GetByRaceAsync(raceId);
        var winner = entries.First().HorseId;
        var loser = entries.Last().HorseId;

        var resubmit = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking((loser, 1), (winner, 2)));
        Assert.False(resubmit.Result.Success);
    }

    // ── G: Violation creation Race-status window ────────────────────────────

    [Fact]
    public async Task CreateViolation_WhileInProgress_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id); // InProgress

        var result = await f.ViolationSvc.RecordViolationAsync(new CreateViolationRequest
        {
            RaceId = race.Id,
            HorseId = race.WinnerHorseId,
            ViolationType = (int)ViolationType.Interference,
            Description = "in-progress violation",
        }, f.RefereeUserId);

        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task CreateViolation_WhileScheduled_IsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync(); // still Scheduled

        var result = await f.ViolationSvc.RecordViolationAsync(new CreateViolationRequest
        {
            RaceId = race.Id,
            HorseId = race.WinnerHorseId,
            ViolationType = (int)ViolationType.Interference,
            Description = "pre-start violation",
        }, f.RefereeUserId);

        Assert.False(result.Result.Success);
    }

    // NOTE: this test used to be CreateViolation_WhileFinished_IsRejected, asserting
    // Finished always rejects violation creation. RecordViolationAsync's window was
    // deliberately widened (see its comments) to also allow Finished-but-not-yet-
    // Official — only Finished+Official is rejected, which CreateViolation_AfterOfficial_
    // IsRejected already covers — so this now asserts success instead.
    [Fact]
    public async Task CreateViolation_WhileFinishedButNotOfficial_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedAsync(f, race.Id);

        var result = await f.ViolationSvc.RecordViolationAsync(new CreateViolationRequest
        {
            RaceId = race.Id,
            HorseId = race.WinnerHorseId,
            ViolationType = (int)ViolationType.Interference,
            Description = "post-race violation",
        }, f.RefereeUserId);

        Assert.True(result.Result.Success, result.Result.Message);
    }
}
