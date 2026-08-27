using System.Reflection;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public class ProtestHardeningTests
{
    private static async Task<List<RaceEntry>> EntriesAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId) =>
        await f.EntryRepo.GetByRaceAsync(raceId);

    private static async Task<Protest> AddProtestAsync(
        RaceLifecycleTests.LifecycleFixture f,
        Guid raceId,
        Guid filedByUserId,
        Guid againstEntryId,
        ProtestStatus status)
    {
        var protest = new Protest
        {
            Id = Guid.NewGuid(),
            RaceId = raceId,
            FiledByUserId = filedByUserId,
            AgainstEntryId = againstEntryId,
            Reason = $"seeded {status}",
            Status = status,
            FiledAt = DateTime.UtcNow,
        };
        f.Db.Protests.Add(protest);
        await f.Db.SaveChangesAsync();
        return protest;
    }

    private static async Task<Guid> CreateUnrelatedOwnerUserAsync(RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"owner-{tag}@test.com",
            PasswordHash = "x",
            FullName = $"Owner {tag}",
            Role = UserRole.HorseOwner,
        };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = user.Id, OwnerCode = $"OWN-{tag}" };
        f.Db.AddRange(user, owner);
        await f.Db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> CreateUnrelatedJockeyUserAsync(RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"jockey-{tag}@test.com",
            PasswordHash = "x",
            FullName = $"Jockey {tag}",
            Role = UserRole.Jockey,
        };
        var jockey = new Jockey { Id = Guid.NewGuid(), UserId = user.Id, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.AddRange(user, jockey);
        await f.Db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task ProgressToFinishedWithProvisionalResultAsync(
        RaceLifecycleTests.LifecycleFixture f,
        RaceLifecycleTests.SeededRace race)
    {
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(
            race.Id,
            RaceLifecycleTests.WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        await f.AddMandatoryReportAsync(race.Id);
    }

    private static async Task<Protest> AddOpenProtestForRaceAsync(
        RaceLifecycleTests.LifecycleFixture f,
        Guid raceId,
        ProtestStatus status = ProtestStatus.Pending)
    {
        var entries = await EntriesAsync(f, raceId);
        return await AddProtestAsync(f, raceId, entries[0].Horse!.Owner!.UserId, entries[1].Id, status);
    }

    private static async Task<ServiceResult<ProtestResponse>> RuleAsync(
        RaceLifecycleTests.LifecycleFixture f,
        Protest protest,
        ProtestStatus outcome)
    {
        return await f.ProtestSvc.RuleAsync(protest.Id, new RuleProtestRequest
        {
            Outcome = outcome,
            Ruling = outcome == ProtestStatus.Upheld
                ? "Chấp nhận khiếu nại, cần nộp lại kết quả."
                : "Bác khiếu nại.",
        }, protest.FiledByUserId);
    }

    [Fact]
    public async Task OwnerWithRaceEntryStandingCanFile()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;

        var result = await f.ProtestSvc.FileAsync(new CreateProtestRequest
        {
            RaceId = race.Id,
            AgainstEntryId = entries[1].Id,
            Reason = "Owner disputes the result.",
        }, filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(ProtestStatus.Pending.ToString(), result.Result.Data!.Status);
    }

    [Fact]
    public async Task OwnerWithoutRaceStandingIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var unrelatedOwnerUserId = await CreateUnrelatedOwnerUserAsync(f, "nostanding");

        var result = await f.ProtestSvc.FileAsync(new CreateProtestRequest
        {
            RaceId = race.Id,
            AgainstEntryId = entries[0].Id,
            Reason = "Unrelated owner attempt.",
        }, unrelatedOwnerUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task AssignedJockeyCanFile()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var filerJockeyUserId = entries[0].Jockey!.UserId;

        var result = await f.ProtestSvc.FileAsync(new CreateProtestRequest
        {
            RaceId = race.Id,
            AgainstEntryId = entries[1].Id,
            Reason = "Jockey disputes interference.",
        }, filerJockeyUserId);

        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task UnrelatedJockeyIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var unrelatedJockeyUserId = await CreateUnrelatedJockeyUserAsync(f, "nostanding");

        var result = await f.ProtestSvc.FileAsync(new CreateProtestRequest
        {
            RaceId = race.Id,
            AgainstEntryId = entries[0].Id,
            Reason = "Unrelated jockey attempt.",
        }, unrelatedJockeyUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task AgainstEntryFromAnotherRaceIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var otherRace = await f.CreateReadyToStartRaceAsync();
        var raceEntries = await EntriesAsync(f, race.Id);
        var otherRaceEntries = await EntriesAsync(f, otherRace.Id);
        var filerUserId = raceEntries[0].Horse!.Owner!.UserId;

        var result = await f.ProtestSvc.FileAsync(new CreateProtestRequest
        {
            RaceId = race.Id,
            AgainstEntryId = otherRaceEntries[0].Id,
            Reason = "Cross-race target attempt.",
        }, filerUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task DuplicateActiveProtestBySameFilerRaceAndEntryIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var request = new CreateProtestRequest
        {
            RaceId = race.Id,
            AgainstEntryId = entries[1].Id,
            Reason = "First filing.",
        };

        var first = await f.ProtestSvc.FileAsync(request, filerUserId);
        var duplicate = await f.ProtestSvc.FileAsync(new CreateProtestRequest
        {
            RaceId = race.Id,
            AgainstEntryId = entries[1].Id,
            Reason = "Duplicate filing.",
        }, filerUserId);

        Assert.True(first.Result.Success, first.Result.Message);
        Assert.False(duplicate.Result.Success);
        Assert.Equal(409, duplicate.StatusCode);
    }

    [Fact]
    public async Task TerminalPreviousProtestAllowsNewActiveProtest()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        await AddProtestAsync(f, race.Id, filerUserId, entries[1].Id, ProtestStatus.Rejected);

        var result = await f.ProtestSvc.FileAsync(new CreateProtestRequest
        {
            RaceId = race.Id,
            AgainstEntryId = entries[1].Id,
            Reason = "New protest after terminal case.",
        }, filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task ExplicitUpheldOutcomeStoresUpheldWithVietnameseDecisionText()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var protest = await AddProtestAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, entries[1].Id, ProtestStatus.Pending);

        var result = await f.ProtestSvc.RuleAsync(protest.Id, new RuleProtestRequest
        {
            Outcome = ProtestStatus.Upheld,
            Ruling = "Chấp nhận khiếu nại vì có va chạm.",
        }, entries[0].Horse!.Owner!.UserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(ProtestStatus.Upheld.ToString(), result.Result.Data!.Status);
    }

    [Fact]
    public async Task ExplicitRejectedOutcomeStoresRejectedEvenIfRulingMentionsUpheld()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var protest = await AddProtestAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, entries[1].Id, ProtestStatus.Pending);

        var result = await f.ProtestSvc.RuleAsync(protest.Id, new RuleProtestRequest
        {
            Outcome = ProtestStatus.Rejected,
            Ruling = "The word Upheld appears in notes but the explicit outcome rejects it.",
        }, entries[0].Horse!.Owner!.UserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(ProtestStatus.Rejected.ToString(), result.Result.Data!.Status);
    }

    [Fact]
    public async Task PendingCanMoveToUnderReviewAndEndpointIsAdminOnly()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var protest = await AddProtestAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, entries[1].Id, ProtestStatus.Pending);

        var result = await f.ProtestSvc.MarkUnderReviewAsync(protest.Id, entries[0].Horse!.Owner!.UserId);
        var roles = typeof(ManagementController)
            .GetMethod(nameof(ManagementController.MarkProtestUnderReview))!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Roles;

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(ProtestStatus.UnderReview.ToString(), result.Result.Data!.Status);
        Assert.Equal("Admin", roles);
    }

    [Fact]
    public async Task OriginalFilerCanWithdrawOpenProtest()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var protest = await AddProtestAsync(f, race.Id, filerUserId, entries[1].Id, ProtestStatus.Pending);

        var result = await f.ProtestSvc.WithdrawAsync(protest.Id, filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(ProtestStatus.Withdrawn.ToString(), result.Result.Data!.Status);
    }

    [Fact]
    public async Task OtherUserCannotWithdraw()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var otherUserId = await CreateUnrelatedOwnerUserAsync(f, "withdraw-other");
        var protest = await AddProtestAsync(f, race.Id, filerUserId, entries[1].Id, ProtestStatus.Pending);

        var result = await f.ProtestSvc.WithdrawAsync(protest.Id, otherUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task TerminalCaseCannotBeChanged()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var protest = await AddProtestAsync(f, race.Id, filerUserId, entries[1].Id, ProtestStatus.Upheld);

        var underReview = await f.ProtestSvc.MarkUnderReviewAsync(protest.Id, filerUserId);
        var rerule = await f.ProtestSvc.RuleAsync(protest.Id, new RuleProtestRequest
        {
            Outcome = ProtestStatus.Rejected,
            Ruling = "Attempted rerule.",
        }, filerUserId);
        var withdraw = await f.ProtestSvc.WithdrawAsync(protest.Id, filerUserId);

        Assert.False(underReview.Result.Success);
        Assert.False(rerule.Result.Success);
        Assert.False(withdraw.Result.Success);
    }

    [Fact]
    public void RefereeCannotReadAllOrPendingProtests()
    {
        static string? RolesFor(string methodName) => typeof(ManagementController)
            .GetMethod(methodName)!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Roles;

        Assert.Equal("Admin", RolesFor(nameof(ManagementController.GetProtests)));
        Assert.Equal("Admin", RolesFor(nameof(ManagementController.GetPendingProtests)));
    }

    [Theory]
    [InlineData(ProtestStatus.Pending, false)]
    [InlineData(ProtestStatus.UnderReview, false)]
    [InlineData(ProtestStatus.Rejected, true)]
    [InlineData(ProtestStatus.Withdrawn, true)]
    public async Task OfficialApprovalRespectsProtestBlockingMatrix(ProtestStatus status, bool shouldApprove)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        await AddProtestAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, entries[1].Id, status);

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.Equal(shouldApprove, approve.Result.Success);
    }

    [Fact]
    public async Task UpheldPendingProtestRequiresCorrectedResultBeforeOfficial()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var protest = await AddOpenProtestForRaceAsync(f, race.Id);

        var rule = await RuleAsync(f, protest, ProtestStatus.Upheld);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);

        Assert.True(rule.Result.Success, rule.Result.Message);
        Assert.False(approve.Result.Success);
        Assert.Equal(RaceResultCorrectionMessages.UpheldProtestApprovalBlocked, approve.Result.Message);
        Assert.Equal(RaceResultStatus.Provisional, result!.Status);
        Assert.Equal(RaceResultCorrectionMessages.UpheldProtestRequiresCorrection, result.RejectedReason);
        Assert.Null(result.ApprovedAt);
    }

    [Fact]
    public async Task UpheldUnderReviewProtestRequiresCorrectedResultBeforeOfficial()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var protest = await AddOpenProtestForRaceAsync(f, race.Id, ProtestStatus.UnderReview);

        var rule = await RuleAsync(f, protest, ProtestStatus.Upheld);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.True(rule.Result.Success, rule.Result.Message);
        Assert.False(approve.Result.Success);
        Assert.Equal(RaceResultCorrectionMessages.UpheldProtestApprovalBlocked, approve.Result.Message);
    }

    [Fact]
    public async Task CorrectedFullResultResubmissionClearsUpheldCorrectionAndAllowsOfficial()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var protest = await AddOpenProtestForRaceAsync(f, race.Id);
        await RuleAsync(f, protest, ProtestStatus.Upheld);

        var resubmit = await f.LiveResult.UpdateRaceResultAsync(
            race.Id,
            RaceLifecycleTests.WinnerLoserRanking(race.LoserHorseId, race.WinnerHorseId));
        var afterResubmit = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        var finalResult = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);

        Assert.True(resubmit.Result.Success, resubmit.Result.Message);
        Assert.Null(afterResubmit!.RejectedReason);
        Assert.True(approve.Result.Success, approve.Result.Message);
        Assert.Equal(RaceResultStatus.Official, finalResult!.Status);
        Assert.Equal(race.LoserHorseId, finalResult.WinningHorseId);
    }

    [Fact]
    public async Task RejectedProtestDoesNotRequireResultResubmission()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var protest = await AddOpenProtestForRaceAsync(f, race.Id);

        var rule = await RuleAsync(f, protest, ProtestStatus.Rejected);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.True(rule.Result.Success, rule.Result.Message);
        Assert.True(approve.Result.Success, approve.Result.Message);
    }

    [Fact]
    public async Task RejectedSecondProtestDoesNotClearPriorUpheldCorrection()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var upheld = await AddOpenProtestForRaceAsync(f, race.Id);
        await RuleAsync(f, upheld, ProtestStatus.Upheld);
        var unrelated = await AddProtestAsync(
            f,
            race.Id,
            entries[1].Horse!.Owner!.UserId,
            entries[0].Id,
            ProtestStatus.Pending);

        var reject = await RuleAsync(f, unrelated, ProtestStatus.Rejected);
        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.True(reject.Result.Success, reject.Result.Message);
        Assert.Equal(RaceResultCorrectionMessages.UpheldProtestRequiresCorrection, result!.RejectedReason);
        Assert.False(approve.Result.Success);
        Assert.Equal(RaceResultCorrectionMessages.UpheldProtestApprovalBlocked, approve.Result.Message);
    }

    [Fact]
    public async Task CorrectedResubmitStillBlockedByAnotherPendingProtest()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var upheld = await AddOpenProtestForRaceAsync(f, race.Id);
        await RuleAsync(f, upheld, ProtestStatus.Upheld);
        var pending = await AddProtestAsync(
            f,
            race.Id,
            entries[1].Horse!.Owner!.UserId,
            entries[0].Id,
            ProtestStatus.Pending);

        var resubmit = await f.LiveResult.UpdateRaceResultAsync(
            race.Id,
            RaceLifecycleTests.WinnerLoserRanking(race.LoserHorseId, race.WinnerHorseId));
        var afterResubmit = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.NotEqual(Guid.Empty, pending.Id);
        Assert.True(resubmit.Result.Success, resubmit.Result.Message);
        Assert.Null(afterResubmit!.RejectedReason);
        Assert.False(approve.Result.Success);
        Assert.Contains("khiếu nại chưa được giải quyết", approve.Result.Message);
    }

    [Fact]
    public async Task InvalidCorrectedResubmitKeepsUpheldCorrectionMarker()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var protest = await AddOpenProtestForRaceAsync(f, race.Id);
        await RuleAsync(f, protest, ProtestStatus.Upheld);

        // Duplicate HorseId (not duplicate Position, which is now a valid dead-heat
        // under the new contract) — still rejected because a horse may not appear
        // more than once in the ranking.
        var invalid = await f.LiveResult.UpdateRaceResultAsync(race.Id, new SubmitRaceResultRequest
        {
            Rankings = new List<SubmitRankingEntry>
            {
                new() { HorseId = race.WinnerHorseId, Position = 1, Status = "Completed" },
                new() { HorseId = race.WinnerHorseId, Position = 2, Status = "Completed" },
            }
        });
        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);

        Assert.False(invalid.Result.Success);
        Assert.Equal(RaceResultCorrectionMessages.UpheldProtestRequiresCorrection, result!.RejectedReason);
    }

    [Fact]
    public async Task UpheldDoesNotAutomaticallyMutateRankingsJson()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var before = (await f.RaceResultRepo.GetByRaceIdAsync(race.Id))!.RankingsJson;
        var protest = await AddOpenProtestForRaceAsync(f, race.Id);

        await RuleAsync(f, protest, ProtestStatus.Upheld);
        var after = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);

        Assert.Equal(before, after!.RankingsJson);
    }

    [Fact]
    public async Task UpheldDoesNotAutomaticallyWriteFinishPosition()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var protest = await AddOpenProtestForRaceAsync(f, race.Id);

        await RuleAsync(f, protest, ProtestStatus.Upheld);
        var entries = await f.Db.RaceEntries.AsNoTracking().Where(e => e.RaceId == race.Id).ToListAsync();

        Assert.All(entries, e => Assert.Null(e.FinishPosition));
    }

    [Fact]
    public async Task RetryingFinalRulingDoesNotCorruptUpheldCorrectionState()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var protest = await AddOpenProtestForRaceAsync(f, race.Id);
        var beforeJson = (await f.RaceResultRepo.GetByRaceIdAsync(race.Id))!.RankingsJson;

        var first = await RuleAsync(f, protest, ProtestStatus.Upheld);
        var retry = await RuleAsync(f, protest, ProtestStatus.Upheld);
        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);

        Assert.True(first.Result.Success, first.Result.Message);
        Assert.False(retry.Result.Success);
        Assert.Equal(RaceResultCorrectionMessages.UpheldProtestRequiresCorrection, result!.RejectedReason);
        Assert.Equal(beforeJson, result.RankingsJson);
        Assert.Equal(RaceResultStatus.Provisional, result.Status);
    }
}
