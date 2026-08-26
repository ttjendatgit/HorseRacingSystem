using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// TOURNAMENT-REGISTRATION-LOCK-AT-START-V1: Admin Approve/Reject on a TournamentHorseRegistration
/// must only ever act on a Tournament that is still Published — Draft (not open yet) and
/// Ongoing/Finished/Cancelled (roster already frozen or the Tournament is over) must all reject,
/// without mutating the registration row. Reject additionally requires the registration itself to
/// still be Pending. Reuses the direct-controller-instantiation pattern from
/// TournamentRegistrationWithdrawTests.cs and the direct-DbContext-seeding pattern from
/// StartTournamentHorseReadinessTests.cs.
/// </summary>
public class TournamentRegistrationApproveRejectLockTests
{
    private static async Task<(Guid ownerId, Guid userId, Guid horseId)> CreateApprovedOwnerHorseAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = $"owner-{tag}@test.com", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = userId, OwnerCode = $"OWN-{tag}" };
        var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse-{tag}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.AddRange(user, owner, horse);
        await f.Db.SaveChangesAsync();
        return (owner.Id, userId, horse.Id);
    }

    private static async Task<Guid> CreateTournamentAsync(RaceLifecycleTests.LifecycleFixture f, TournamentStatus status, DateTime start)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = $"T-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(5),
            RegistrationDeadline = start.AddDays(-1), MaxParticipants = 10, MinParticipants = 3,
            Status = status, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(tournament);
        await f.Db.SaveChangesAsync();
        return tournament.Id;
    }

    private static async Task<Guid> RegisterAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid horseId, Guid ownerId, RegistrationStatus status)
    {
        var registration = new TournamentHorseRegistration
        {
            Id = Guid.NewGuid(), TournamentId = tournamentId, HorseId = horseId, OwnerId = ownerId,
            Status = status, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(registration);
        await f.Db.SaveChangesAsync();
        return registration.Id;
    }

    private static TournamentRegistrationsController BuildController(RaceLifecycleTests.LifecycleFixture f, Guid userId)
    {
        var controller = new TournamentRegistrationsController(f.Db, f.UnitOfWork);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static (int status, string? message) Unwrap(ActionResult result)
    {
        if (result is ObjectResult obj)
            return (obj.StatusCode ?? 200, obj.Value?.GetType().GetProperty("message")?.GetValue(obj.Value) as string);
        throw new InvalidOperationException($"Unexpected result type {result.GetType()}");
    }

    // ── APPROVE ────────────────────────────────────────────────────────────

    // 8. Pending + Published -> succeeds
    [Fact]
    public async Task Approve_PendingAndPublished_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Approve(regId));

        Assert.Equal(200, status);
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Approved, reloaded.Status);
        Assert.NotNull(reloaded.ApprovedAt);
    }

    // 9. Pending + Ongoing -> rejected
    [Fact]
    public async Task Approve_PendingButTournamentOngoing_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Ongoing, DateTime.UtcNow.AddDays(-1));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, message) = Unwrap(await controller.Approve(regId));

        Assert.Equal(400, status);
        Assert.Contains("Đã công bố", message ?? "");
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Pending, reloaded.Status);
        Assert.Null(reloaded.ApprovedAt);
    }

    // 10. Pending + Finished -> rejected
    [Fact]
    public async Task Approve_PendingButTournamentFinished_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Finished, DateTime.UtcNow.AddDays(-10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Approve(regId));

        Assert.Equal(400, status);
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Pending, reloaded.Status);
    }

    // 11. Pending + Cancelled -> rejected
    [Fact]
    public async Task Approve_PendingButTournamentCancelled_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Cancelled, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Approve(regId));

        Assert.Equal(400, status);
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Pending, reloaded.Status);
    }

    // 12. Pending + Draft -> rejected
    [Fact]
    public async Task Approve_PendingButTournamentDraft_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Approve(regId));

        Assert.Equal(400, status);
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Pending, reloaded.Status);
    }

    // 13. rejected Approve does not mutate registration (covers all four non-Published statuses at once)
    [Theory]
    [InlineData(TournamentStatus.Draft)]
    [InlineData(TournamentStatus.Ongoing)]
    [InlineData(TournamentStatus.Finished)]
    [InlineData(TournamentStatus.Cancelled)]
    public async Task Approve_RejectedByTournamentStatus_NeverMutatesRegistration(TournamentStatus tournamentStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, tournamentStatus, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);
        var before = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Approve(regId));

        Assert.Equal(400, status);
        var after = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.ApprovedAt, after.ApprovedAt);
        Assert.Equal(before.Note, after.Note);
    }

    // ── REJECT ─────────────────────────────────────────────────────────────

    // 14. Pending + Published -> succeeds
    [Fact]
    public async Task Reject_PendingAndPublished_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Reject(regId, new RejectTournamentRegistrationRequest { Reason = "Không đạt tiêu chuẩn" }));

        Assert.Equal(200, status);
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Rejected, reloaded.Status);
        Assert.Equal("Không đạt tiêu chuẩn", reloaded.Note);
    }

    // 15. Pending + Ongoing -> rejected
    [Fact]
    public async Task Reject_PendingButTournamentOngoing_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Ongoing, DateTime.UtcNow.AddDays(-1));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, message) = Unwrap(await controller.Reject(regId, new RejectTournamentRegistrationRequest { Reason = "x" }));

        Assert.Equal(400, status);
        Assert.Contains("Đã công bố", message ?? "");
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Pending, reloaded.Status);
        Assert.Null(reloaded.Note);
    }

    // 16. Approved + Published -> rejected (Reject only ever applies to Pending)
    [Fact]
    public async Task Reject_ApprovedRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Reject(regId, new RejectTournamentRegistrationRequest { Reason = "x" }));

        Assert.Equal(400, status);
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Approved, reloaded.Status);
    }

    // 17. Withdrawn + Published -> rejected
    [Fact]
    public async Task Reject_WithdrawnRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Withdrawn);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Reject(regId, new RejectTournamentRegistrationRequest { Reason = "x" }));

        Assert.Equal(400, status);
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Withdrawn, reloaded.Status);
    }

    // 18. Rejected + Published -> rejected (re-rejecting an already-Rejected row)
    [Fact]
    public async Task Reject_AlreadyRejectedRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Rejected);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Reject(regId, new RejectTournamentRegistrationRequest { Reason = "Lý do khác" }));

        Assert.Equal(400, status);
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Rejected, reloaded.Status);
    }

    // 19. rejected Reject does not mutate registration status/note
    [Theory]
    [InlineData(RegistrationStatus.Approved)]
    [InlineData(RegistrationStatus.Rejected)]
    [InlineData(RegistrationStatus.Withdrawn)]
    public async Task Reject_RejectedByRegistrationStatus_NeverMutatesStatusOrNote(RegistrationStatus initialStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, initialStatus);
        var before = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Reject(regId, new RejectTournamentRegistrationRequest { Reason = "Không nên áp dụng" }));

        Assert.Equal(400, status);
        var after = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.Note, after.Note);
    }

    // 19b. Rejecting a Pending registration whose Tournament is not Published must also never
    // mutate Note/Status — same invariant as above, driven by the Tournament-status gate instead
    // of the registration-status gate.
    [Fact]
    public async Task Reject_RejectedByTournamentStatus_NeverMutatesStatusOrNote()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Ongoing, DateTime.UtcNow.AddDays(-1));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);
        var before = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, _) = Unwrap(await controller.Reject(regId, new RejectTournamentRegistrationRequest { Reason = "Không nên áp dụng" }));

        Assert.Equal(400, status);
        var after = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.Note, after.Note);
    }

    // ── 21 (regression). Existing Withdraw behavior remains unchanged ──────
    // Full coverage lives in TournamentRegistrationWithdrawTests.cs (untouched by this task); these
    // two are a lightweight in-file confirmation that Withdraw's own Published-only gate — the one
    // rule this task intentionally leaves alone — still behaves exactly as before.
    [Fact]
    public async Task Withdraw_StillAllowedWhilePublished()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, userId);
        var (status, _) = Unwrap(await controller.Withdraw(regId));

        Assert.Equal(200, status);
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Withdrawn, reloaded.Status);
    }

    [Fact]
    public async Task Withdraw_StillRejectedOnceOngoing()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Ongoing, DateTime.UtcNow.AddDays(-1));
        var regId = await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Withdraw(regId));

        Assert.Equal(400, status);
        Assert.Contains("Đã công bố", message ?? "");
        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Approved, reloaded.Status);
    }
}
