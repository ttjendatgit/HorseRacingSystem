using System.Reflection;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public class RaceComplaintTests
{
    private static async Task<List<RaceEntry>> EntriesAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId) =>
        await f.EntryRepo.GetByRaceAsync(raceId);

    private static async Task<RefereeAssignment> ConfirmedAssignmentAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId) =>
        await f.Db.RefereeAssignments.FirstAsync(a => a.RaceId == raceId && a.Status == RefereeAssignmentStatus.Confirmed);

    private static async Task<RaceComplaint> AddComplaintAsync(
        RaceLifecycleTests.LifecycleFixture f,
        Guid raceId,
        Guid filedByUserId,
        RaceComplaintStatus status,
        RaceComplaintType type = RaceComplaintType.ResultJudging,
        Guid? assignedRefereeAssignmentId = null)
    {
        var complaint = new RaceComplaint
        {
            Id = Guid.NewGuid(),
            RaceId = raceId,
            FiledByUserId = filedByUserId,
            Type = type,
            Reason = $"seeded {status}",
            Status = status,
            AssignedRefereeAssignmentId = assignedRefereeAssignmentId,
            CreatedAt = DateTime.UtcNow,
        };
        f.Db.RaceComplaints.Add(complaint);
        await f.Db.SaveChangesAsync();
        return complaint;
    }

    private static async Task<Guid> CreateUnrelatedOwnerUserAsync(RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"rc-owner-{tag}@test.com", PasswordHash = "x", FullName = $"Owner {tag}", Role = UserRole.HorseOwner };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = user.Id, OwnerCode = $"RC-OWN-{tag}" };
        f.Db.AddRange(user, owner);
        await f.Db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> CreateUnrelatedJockeyUserAsync(RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"rc-jockey-{tag}@test.com", PasswordHash = "x", FullName = $"Jockey {tag}", Role = UserRole.Jockey };
        var jockey = new Jockey { Id = Guid.NewGuid(), UserId = user.Id, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.AddRange(user, jockey);
        await f.Db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> CreateSpectatorUserAsync(RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"rc-spectator-{tag}@test.com", PasswordHash = "x", FullName = $"Spectator {tag}", Role = UserRole.Spectator };
        f.Db.Add(user);
        await f.Db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<(Guid UserId, Guid AssignmentId)> CreateUnconfirmedRefereeAssignmentAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId, string tag)
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"rc-ref-{tag}@test.com", PasswordHash = "x", FullName = $"Referee {tag}", Role = UserRole.Referee };
        var referee = new Referee { Id = Guid.NewGuid(), UserId = user.Id, LicenseNumber = $"RC-LIC-{tag}", IsActive = true };
        var assignment = new RefereeAssignment
        {
            Id = Guid.NewGuid(), RaceId = raceId, RefereeId = referee.Id, Role = "Assistant",
            Status = RefereeAssignmentStatus.Assigned, AssignedAt = DateTime.UtcNow,
        };
        f.Db.AddRange(user, referee, assignment);
        await f.Db.SaveChangesAsync();
        return (user.Id, assignment.Id);
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

    // ── Filing standing & eligibility ──

    [Fact]
    public async Task OwnerWithParticipationCanFile()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.ResultJudging,
            Reason = "Owner believes judging was unfair.",
        }, filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(RaceComplaintStatus.Pending.ToString(), result.Result.Data!.Status);
        Assert.Equal(race.Id, result.Result.Data.RaceId);
    }

    [Fact]
    public async Task OwnerWithoutParticipationIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var unrelatedOwnerUserId = await CreateUnrelatedOwnerUserAsync(f, "nostanding");

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.ResultJudging,
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
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerJockeyUserId = entries[0].Jockey!.UserId;

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.RaceOperation,
            Reason = "Jockey disputes race operation.",
        }, filerJockeyUserId);

        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task UnrelatedJockeyIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var unrelatedJockeyUserId = await CreateUnrelatedJockeyUserAsync(f, "nostanding");

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.ResultJudging,
            Reason = "Unrelated jockey attempt.",
        }, unrelatedJockeyUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task SpectatorCannotFile()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var spectatorUserId = await CreateSpectatorUserAsync(f, "s1");

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.ResultJudging,
            Reason = "Spectator attempt.",
        }, spectatorUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task RefereeCannotFile()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.ResultJudging,
            Reason = "Referee attempt.",
        }, f.RefereeUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task CancelledRaceIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var raceEntity = await f.RaceRepo.GetByIdAsync(race.Id);
        raceEntity!.Status = RaceStatus.Cancelled;
        await f.RaceRepo.UpdateAsync(raceEntity);
        await f.Db.SaveChangesAsync();

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.ResultJudging,
            Reason = "Cancelled race attempt.",
        }, filerUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task NonFinishedRaceIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.ResultJudging,
            Reason = "Race not finished yet.",
        }, filerUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task OfficialResultRejectsNewComplaint()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);
        Assert.True(approve.Result.Success, approve.Result.Message);

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.ResultJudging,
            Reason = "Too late, already official.",
        }, filerUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task DuplicateActiveComplaintBySameFilerRaceAndTypeIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var request = new CreateRaceComplaintRequest { RaceId = race.Id, Type = RaceComplaintType.ResultJudging, Reason = "First filing." };

        var first = await f.RaceComplaintSvc.FileAsync(request, filerUserId);
        var duplicate = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.ResultJudging,
            Reason = "Duplicate filing.",
        }, filerUserId);

        Assert.True(first.Result.Success, first.Result.Message);
        Assert.False(duplicate.Result.Success);
        Assert.Equal(409, duplicate.StatusCode);
    }

    [Fact]
    public async Task DifferentTypeIsNotBlockedAsDuplicate()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest { RaceId = race.Id, Type = RaceComplaintType.ResultJudging, Reason = "Result judging." }, filerUserId);

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.RaceOperation,
            Reason = "Race operation, different type.",
        }, filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task TerminalPreviousComplaintAllowsNewActiveComplaint()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Rejected);

        var result = await f.RaceComplaintSvc.FileAsync(new CreateRaceComplaintRequest
        {
            RaceId = race.Id,
            Type = RaceComplaintType.ResultJudging,
            Reason = "New complaint after terminal case.",
        }, filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
    }

    // ── Admin routing ──

    [Fact]
    public async Task AdminCanRoutePendingComplaintToConfirmedAssignment()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.Pending);
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);

        var result = await f.RaceComplaintSvc.RouteAsync(complaint.Id, new RouteRaceComplaintRequest { RefereeAssignmentId = assignment.Id }, Guid.NewGuid());

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(RaceComplaintStatus.AwaitingRefereeResponse.ToString(), result.Result.Data!.Status);
        Assert.Equal(assignment.Id, result.Result.Data.AssignedRefereeAssignmentId);
    }

    [Fact]
    public async Task RouteRejectsAssignmentFromDifferentRace()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var otherRace = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.Pending);
        var otherAssignment = await ConfirmedAssignmentAsync(f, otherRace.Id);

        var result = await f.RaceComplaintSvc.RouteAsync(complaint.Id, new RouteRaceComplaintRequest { RefereeAssignmentId = otherAssignment.Id }, Guid.NewGuid());

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task RouteRejectsUnconfirmedAssignment()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.Pending);
        var (_, unconfirmedAssignmentId) = await CreateUnconfirmedRefereeAssignmentAsync(f, race.Id, "unconfirmed");

        var result = await f.RaceComplaintSvc.RouteAsync(complaint.Id, new RouteRaceComplaintRequest { RefereeAssignmentId = unconfirmedAssignmentId }, Guid.NewGuid());

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    // ── Referee response ──

    [Fact]
    public async Task UnrelatedRefereeCannotRespond()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.AwaitingRefereeResponse, assignedRefereeAssignmentId: assignment.Id);
        var (unrelatedRefereeUserId, _) = await CreateUnconfirmedRefereeAssignmentAsync(f, race.Id, "unrelated");

        var result = await f.RaceComplaintSvc.RespondAsync(complaint.Id, new RespondRaceComplaintRequest { Response = "I was not assigned to this." }, unrelatedRefereeUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task AssignedRefereeCanRespondAndMovesToUnderReview()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.AwaitingRefereeResponse, assignedRefereeAssignmentId: assignment.Id);

        var result = await f.RaceComplaintSvc.RespondAsync(complaint.Id, new RespondRaceComplaintRequest { Response = "Here is my explanation." }, f.RefereeUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(RaceComplaintStatus.UnderReview.ToString(), result.Result.Data!.Status);
        Assert.Equal("Here is my explanation.", result.Result.Data.RefereeResponse);
    }

    [Fact]
    public async Task EmptyResponseIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.AwaitingRefereeResponse, assignedRefereeAssignmentId: assignment.Id);

        var result = await f.RaceComplaintSvc.RespondAsync(complaint.Id, new RespondRaceComplaintRequest { Response = "   " }, f.RefereeUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void RefereeRoleCannotAccessRoutingOrRulingEndpoints()
    {
        static string? RolesFor(string methodName) => typeof(ManagementController)
            .GetMethod(methodName)!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Roles;

        Assert.Equal("Admin", RolesFor(nameof(ManagementController.RouteRaceComplaint)));
        Assert.Equal("Admin", RolesFor(nameof(ManagementController.RuleRaceComplaint)));
        Assert.Equal("Admin", RolesFor(nameof(ManagementController.GetRaceComplaints)));
        Assert.Equal("Referee", RolesFor(nameof(ManagementController.RespondRaceComplaint)));
        Assert.Equal("Referee", RolesFor(nameof(ManagementController.GetRefereeRaceComplaints)));
    }

    [Fact]
    public async Task RefereeOnlySeesComplaintsRoutedToTheirOwnAssignment()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.AwaitingRefereeResponse, assignedRefereeAssignmentId: assignment.Id);
        // an unrouted Pending complaint on the same race must not leak to the referee
        await AddComplaintAsync(f, race.Id, entries[1].Horse!.Owner!.UserId, RaceComplaintStatus.Pending);

        var mine = await f.RaceComplaintSvc.GetForRefereeAsync(f.RefereeUserId);

        Assert.True(mine.Result.Success);
        Assert.Single(mine.Result.Data!);
    }

    // ── Admin final ruling ──

    [Fact]
    public async Task AdminCanRejectAtIntakeFromPending()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.Pending);

        var result = await f.RaceComplaintSvc.RuleAsync(complaint.Id, new RuleRaceComplaintRequest
        {
            Outcome = RaceComplaintStatus.Rejected,
            Ruling = "Không đủ căn cứ để chuyển cho trọng tài.",
        }, Guid.NewGuid());

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(RaceComplaintStatus.Rejected.ToString(), result.Result.Data!.Status);
    }

    [Fact]
    public async Task PendingComplaintCannotBeUpheldDirectlyAtIntake()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.Pending);

        var result = await f.RaceComplaintSvc.RuleAsync(complaint.Id, new RuleRaceComplaintRequest
        {
            Outcome = RaceComplaintStatus.Upheld,
            Ruling = "Trying to skip referee routing.",
            AffectsResult = false,
        }, Guid.NewGuid());

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AdminCanRejectUnderReview()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.UnderReview);

        var result = await f.RaceComplaintSvc.RuleAsync(complaint.Id, new RuleRaceComplaintRequest
        {
            Outcome = RaceComplaintStatus.Rejected,
            Ruling = "Giải trình của trọng tài hợp lý.",
        }, Guid.NewGuid());

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(RaceComplaintStatus.Rejected.ToString(), result.Result.Data!.Status);
    }

    [Fact]
    public async Task UpheldRequiresExplicitAffectsResult()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.UnderReview);

        var result = await f.RaceComplaintSvc.RuleAsync(complaint.Id, new RuleRaceComplaintRequest
        {
            Outcome = RaceComplaintStatus.Upheld,
            Ruling = "Chấp nhận khiếu nại.",
            AffectsResult = null,
        }, Guid.NewGuid());

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task UpheldFalseDoesNotInvalidateResult()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.UnderReview);

        var rule = await f.RaceComplaintSvc.RuleAsync(complaint.Id, new RuleRaceComplaintRequest
        {
            Outcome = RaceComplaintStatus.Upheld,
            Ruling = "Chấp nhận nhưng không ảnh hưởng kết quả.",
            AffectsResult = false,
        }, Guid.NewGuid());
        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.True(rule.Result.Success, rule.Result.Message);
        Assert.Null(result!.RejectedReason);
        Assert.True(approve.Result.Success, approve.Result.Message);
    }

    [Fact]
    public async Task UpheldTrueSetsCorrectionRequiredMarker()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.UnderReview);

        var rule = await f.RaceComplaintSvc.RuleAsync(complaint.Id, new RuleRaceComplaintRequest
        {
            Outcome = RaceComplaintStatus.Upheld,
            Ruling = "Chấp nhận, ảnh hưởng kết quả.",
            AffectsResult = true,
        }, Guid.NewGuid());
        var result = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);
        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.True(rule.Result.Success, rule.Result.Message);
        Assert.Equal(RaceResultCorrectionMessages.UpheldRaceComplaintRequiresCorrection, result!.RejectedReason);
        Assert.Equal(RaceResultStatus.Provisional, result.Status);
        Assert.False(approve.Result.Success);
        Assert.Equal(RaceResultCorrectionMessages.UpheldRaceComplaintApprovalBlocked, approve.Result.Message);
    }

    [Fact]
    public async Task UpheldTrueDoesNotMutateRankingsJson()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var before = (await f.RaceResultRepo.GetByRaceIdAsync(race.Id))!.RankingsJson;
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.UnderReview);

        await f.RaceComplaintSvc.RuleAsync(complaint.Id, new RuleRaceComplaintRequest
        {
            Outcome = RaceComplaintStatus.Upheld,
            Ruling = "Chấp nhận, ảnh hưởng kết quả.",
            AffectsResult = true,
        }, Guid.NewGuid());
        var after = await f.RaceResultRepo.GetByRaceIdAsync(race.Id);

        Assert.Equal(before, after!.RankingsJson);
    }

    [Fact]
    public async Task UpheldTrueDoesNotWriteFinishPosition()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.UnderReview);

        await f.RaceComplaintSvc.RuleAsync(complaint.Id, new RuleRaceComplaintRequest
        {
            Outcome = RaceComplaintStatus.Upheld,
            Ruling = "Chấp nhận, ảnh hưởng kết quả.",
            AffectsResult = true,
        }, Guid.NewGuid());
        var updatedEntries = await f.Db.RaceEntries.AsNoTracking().Where(e => e.RaceId == race.Id).ToListAsync();

        Assert.All(updatedEntries, e => Assert.Null(e.FinishPosition));
    }

    // ── Official approval gating ──

    [Theory]
    [InlineData(RaceComplaintStatus.Pending, false)]
    [InlineData(RaceComplaintStatus.AwaitingRefereeResponse, false)]
    [InlineData(RaceComplaintStatus.UnderReview, false)]
    [InlineData(RaceComplaintStatus.Rejected, true)]
    [InlineData(RaceComplaintStatus.Withdrawn, true)]
    public async Task OfficialApprovalRespectsComplaintBlockingMatrix(RaceComplaintStatus status, bool shouldApprove)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, status);

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.Equal(shouldApprove, approve.Result.Success);
    }

    [Fact]
    public async Task CorrectedFullResultResubmissionClearsMarkerAndAllowsOfficial()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, entries[0].Horse!.Owner!.UserId, RaceComplaintStatus.UnderReview);
        await f.RaceComplaintSvc.RuleAsync(complaint.Id, new RuleRaceComplaintRequest
        {
            Outcome = RaceComplaintStatus.Upheld,
            Ruling = "Chấp nhận, ảnh hưởng kết quả.",
            AffectsResult = true,
        }, Guid.NewGuid());

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
    public async Task UnresolvedLegacyProtestStillBlocksOfficialAlongsideComplaints()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        // no active RaceComplaint at all -- a legacy Protest alone must still gate Official
        f.Db.Protests.Add(new Protest
        {
            Id = Guid.NewGuid(),
            RaceId = race.Id,
            FiledByUserId = entries[0].Horse!.Owner!.UserId,
            AgainstEntryId = entries[1].Id,
            Reason = "Legacy protest still open.",
            Status = ProtestStatus.Pending,
            FiledAt = DateTime.UtcNow,
        });
        await f.Db.SaveChangesAsync();

        var approve = await f.Admin.ApproveRaceResultAsync(race.Id);

        Assert.False(approve.Result.Success);
    }

    // ── Withdrawal ──

    [Fact]
    public async Task FilerCanWithdrawActiveComplaint()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);

        var result = await f.RaceComplaintSvc.WithdrawAsync(complaint.Id, filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(RaceComplaintStatus.Withdrawn.ToString(), result.Result.Data!.Status);
    }

    [Fact]
    public async Task OtherUserCannotWithdraw()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var otherUserId = await CreateUnrelatedOwnerUserAsync(f, "withdraw-other");
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);

        var result = await f.RaceComplaintSvc.WithdrawAsync(complaint.Id, otherUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task TerminalComplaintCannotBeWithdrawn()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Rejected);

        var result = await f.RaceComplaintSvc.WithdrawAsync(complaint.Id, filerUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }
}
