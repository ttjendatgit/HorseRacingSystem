using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Task B: Tournament Registration + RaceEntry gates — TournamentHorseRegistration submit/approve
/// gating (§1/§2/§3/§4), Horse-Tournament overlap (§5), the Approved-registration RaceEntry gate
/// (§6), and legacy Race-RegistrationOpen independence (§7). Wired against a real Sqlite
/// in-memory DB and the actual production controller/services (not mocks), reusing
/// RaceLifecycleTests.LifecycleFixture. TournamentRegistrationsController talks directly to the
/// DbContext (no service layer), so it is instantiated directly here, matching the existing
/// TracksController pattern in Phase3ContractTests.cs.
/// </summary>
public class TournamentRegistrationTests
{
    // ── Shared builders ─────────────────────────────────────────────────

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

    private static async Task<Guid> CreateApprovedHorseForOwnerAsync(RaceLifecycleTests.LifecycleFixture f, Guid ownerId, string tag)
    {
        var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse-{tag}", OwnerId = ownerId, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.Add(horse);
        await f.Db.SaveChangesAsync();
        return horse.Id;
    }

    private static async Task<Guid> CreateTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, TournamentStatus status, DateTime start, DateTime end,
        DateTime? registrationDeadline, int? maxParticipants = 10)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = $"T-{Guid.NewGuid():N}", StartDate = start, EndDate = end,
            RegistrationDeadline = registrationDeadline, MaxParticipants = maxParticipants, MinParticipants = 3,
            Status = status, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(tournament);
        await f.Db.SaveChangesAsync();
        return tournament.Id;
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

    private static async Task<Guid> DirectRegisterAsync(
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

    private static async Task<Guid> CreateTrackAsync(RaceLifecycleTests.LifecycleFixture f, int capacity = 10)
    {
        var track = new Track { Id = Guid.NewGuid(), Name = $"Track-{Guid.NewGuid():N}", Capacity = capacity, CreatedAt = DateTime.UtcNow };
        f.Db.Add(track);
        await f.Db.SaveChangesAsync();
        return track.Id;
    }

    /// <summary>Draft Tournament + 1 Draft-stage Round + 1 Draft-stage Race, ready to test RaceEntry assignment (no Publish needed — AssignHorseToRaceAsync only requires Draft).</summary>
    private static async Task<(Guid tournamentId, Guid raceId)> CreateDraftTournamentWithRaceAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(20);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, end, start.AddDays(-1));
        var round = new Round { Id = Guid.NewGuid(), Name = "R1", TournamentId = tournamentId, RoundNumber = 1, ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1) };
        f.Db.Add(round);
        await f.Db.SaveChangesAsync();
        var track = await CreateTrackAsync(f);
        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = round.Id,
            ScheduledAt = start, ScheduledEndAt = start.AddHours(1), TrackId = track, MaxParticipants = 10
        });
        return (tournamentId, race.Result.Data!.Id);
    }

    // ── REGISTRATION ────────────────────────────────────────────────────

    [Fact]
    public async Task Register_DraftTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start, start.AddDays(5), start.AddDays(-1));

        var controller = BuildController(f, userId);
        var (status, _) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task Register_PublishedBeforeDeadline_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));

        var controller = BuildController(f, userId);
        var (status, _) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.True(status is 200 or 201, $"expected success, got {status}");
    }

    [Fact]
    public async Task Register_DeadlineEquality_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        // Deadline captured as "now" at Tournament-creation time — by the time Register executes,
        // real UtcNow has already advanced past it, exercising the inclusive `now >= deadline` edge.
        var deadline = DateTime.UtcNow;
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), deadline);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.Equal(400, status);
        Assert.Contains("hết hạn", message ?? "");
    }

    [Fact]
    public async Task Register_AfterDeadline_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), DateTime.UtcNow.AddMinutes(-5));

        var controller = BuildController(f, userId);
        var (status, _) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task Register_DuplicateActiveHorse_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, userId);
        var (status, _) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task Register_AfterRejected_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Rejected);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Register_AfterWithdrawn_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Withdrawn);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    // ── OWNER ONE-ACTIVE-HORSE RULE (Task B Correction 2 §1, locked spec §5/§21.1) ─────────
    // An Owner may have at most ONE active (Pending/Approved) registration per Tournament,
    // regardless of how many Horses they own — distinct from, and checked alongside, the
    // Horse-scoped duplicate rule above (§21.2).

    [Fact]
    public async Task Register_OwnerFirstHorse_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Register_OwnerSecondHorseSameTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horse1Id) = await CreateApprovedOwnerHorseAsync(f, "a");
        var horse2Id = await CreateApprovedHorseForOwnerAsync(f, ownerId, "b");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));
        await DirectRegisterAsync(f, tournamentId, horse1Id, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horse2Id }));
        Assert.Equal(400, status);
        Assert.Equal("Bạn đã có một ngựa đang đăng ký hoặc đã được duyệt cho giải đấu này.", message);
    }

    [Fact]
    public async Task Register_OwnerSecondHorse_AfterFirstRejected_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horse1Id) = await CreateApprovedOwnerHorseAsync(f, "a");
        var horse2Id = await CreateApprovedHorseForOwnerAsync(f, ownerId, "b");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));
        await DirectRegisterAsync(f, tournamentId, horse1Id, ownerId, RegistrationStatus.Rejected);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horse2Id }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Register_OwnerSecondHorse_AfterFirstWithdrawn_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horse1Id) = await CreateApprovedOwnerHorseAsync(f, "a");
        var horse2Id = await CreateApprovedHorseForOwnerAsync(f, ownerId, "b");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));
        await DirectRegisterAsync(f, tournamentId, horse1Id, ownerId, RegistrationStatus.Withdrawn);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horse2Id }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Register_OwnerSecondHorse_WhileFirstApproved_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horse1Id) = await CreateApprovedOwnerHorseAsync(f, "a");
        var horse2Id = await CreateApprovedHorseForOwnerAsync(f, ownerId, "b");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));
        await DirectRegisterAsync(f, tournamentId, horse1Id, ownerId, RegistrationStatus.Approved);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horse2Id }));
        Assert.Equal(400, status);
        Assert.Equal("Bạn đã có một ngựa đang đăng ký hoặc đã được duyệt cho giải đấu này.", message);
    }

    [Fact]
    public async Task Register_DifferentOwnerHorseSameTournament_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (owner1Id, _, horse1Id) = await CreateApprovedOwnerHorseAsync(f, "a");
        var (_, user2Id, horse2Id) = await CreateApprovedOwnerHorseAsync(f, "b");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));
        await DirectRegisterAsync(f, tournamentId, horse1Id, owner1Id, RegistrationStatus.Pending);

        var controller = BuildController(f, user2Id);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horse2Id }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Register_OwnerHorseInDifferentNonConflictingTournament_Allowed()
    {
        // The Owner rule is scoped PER Tournament — an active registration in Tournament A must
        // never block the same Owner (even the same Horse) from registering into an unrelated,
        // non-overlapping Tournament B.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var aStart = DateTime.UtcNow.AddDays(10);
        var aEnd = aStart.AddDays(5);
        var tournamentAId = await CreateTournamentAsync(f, TournamentStatus.Published, aStart, aEnd, aStart.AddDays(-1));
        await DirectRegisterAsync(f, tournamentAId, horseId, ownerId, RegistrationStatus.Pending);

        var bStart = aEnd.AddDays(30); // well clear of A — no overlap
        var tournamentBId = await CreateTournamentAsync(f, TournamentStatus.Published, bStart, bStart.AddDays(5), bStart.AddDays(-1));

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentBId, HorseId = horseId }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    // ── APPROVAL ────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_PendingToApproved_Transitions()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));
        var regId = await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, Guid.NewGuid());
        var (status, message) = Unwrap(await controller.Approve(regId));
        Assert.Equal(200, status);

        var reloaded = await f.Db.TournamentHorseRegistrations.AsNoTracking().FirstAsync(x => x.Id == regId);
        Assert.Equal(RegistrationStatus.Approved, reloaded.Status);
        Assert.NotNull(reloaded.ApprovedAt);
    }

    [Fact]
    public async Task Approve_UpToMaxParticipants_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1), maxParticipants: 4);
        var controller = BuildController(f, Guid.NewGuid());

        for (var i = 0; i < 4; i++)
        {
            var (_, _, horseId) = await CreateApprovedOwnerHorseAsync(f, $"h{i}");
            var regId = await DirectRegisterAsync(f, tournamentId, horseId, Guid.NewGuid(), RegistrationStatus.Pending);
            var (status, _) = Unwrap(await controller.Approve(regId));
            Assert.Equal(200, status);
        }

        var approvedCount = await f.Db.TournamentHorseRegistrations.CountAsync(x => x.TournamentId == tournamentId && x.Status == RegistrationStatus.Approved);
        Assert.Equal(4, approvedCount);
    }

    [Fact]
    public async Task Approve_ExceedingMaxParticipants_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1), maxParticipants: 4);
        var controller = BuildController(f, Guid.NewGuid());

        for (var i = 0; i < 4; i++)
        {
            var (_, _, horseId) = await CreateApprovedOwnerHorseAsync(f, $"h{i}");
            var regId = await DirectRegisterAsync(f, tournamentId, horseId, Guid.NewGuid(), RegistrationStatus.Pending);
            var (status, _) = Unwrap(await controller.Approve(regId));
            Assert.Equal(200, status);
        }

        // 5th registration — capacity must be re-checked against the CURRENT Approved count, not
        // a stale/cached value from submit time.
        var (_, _, horse5Id) = await CreateApprovedOwnerHorseAsync(f, "h5");
        var reg5Id = await DirectRegisterAsync(f, tournamentId, horse5Id, Guid.NewGuid(), RegistrationStatus.Pending);
        var (status5, message5) = Unwrap(await controller.Approve(reg5Id));
        Assert.Equal(400, status5);
        Assert.Contains("đủ số lượng", message5 ?? "");

        var approvedCount = await f.Db.TournamentHorseRegistrations.CountAsync(x => x.TournamentId == tournamentId && x.Status == RegistrationStatus.Approved);
        Assert.Equal(4, approvedCount); // still exactly Max — the 5th never got counted
    }

    // ── OVERLAP ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_OverlappingActiveTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var aStart = DateTime.UtcNow.AddDays(10);
        var aEnd = aStart.AddDays(5);
        var tournamentAId = await CreateTournamentAsync(f, TournamentStatus.Published, aStart, aEnd, aStart.AddDays(-1));
        await DirectRegisterAsync(f, tournamentAId, horseId, ownerId, RegistrationStatus.Approved);

        // Tournament B overlaps A's window (B starts before A ends).
        var bStart = aEnd.AddDays(-1);
        var bEnd = bStart.AddDays(5);
        var tournamentBId = await CreateTournamentAsync(f, TournamentStatus.Published, bStart, bEnd, bStart.AddDays(-1));

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentBId, HorseId = horseId }));
        Assert.Equal(400, status);
        Assert.Equal("Ngựa đang tham gia một giải đấu có thời gian trùng.", message);
    }

    [Fact]
    public async Task Register_BackToBackTournaments_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var aStart = DateTime.UtcNow.AddDays(10);
        var aEnd = aStart.AddDays(5);
        var tournamentAId = await CreateTournamentAsync(f, TournamentStatus.Published, aStart, aEnd, aStart.AddDays(-1));
        await DirectRegisterAsync(f, tournamentAId, horseId, ownerId, RegistrationStatus.Approved);

        // Tournament B starts exactly when A ends — touching, not overlapping.
        var bStart = aEnd;
        var bEnd = bStart.AddDays(5);
        var tournamentBId = await CreateTournamentAsync(f, TournamentStatus.Published, bStart, bEnd, bStart.AddDays(-1));

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentBId, HorseId = horseId }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Register_OverlapWithCancelledTournament_Ignored()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var aStart = DateTime.UtcNow.AddDays(10);
        var aEnd = aStart.AddDays(5);
        var tournamentAId = await CreateTournamentAsync(f, TournamentStatus.Cancelled, aStart, aEnd, aStart.AddDays(-1));
        await DirectRegisterAsync(f, tournamentAId, horseId, ownerId, RegistrationStatus.Approved);

        var bStart = aEnd.AddDays(-1);
        var tournamentBId = await CreateTournamentAsync(f, TournamentStatus.Published, bStart, bStart.AddDays(5), bStart.AddDays(-1));

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentBId, HorseId = horseId }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Register_OverlapWithFinishedTournament_Ignored()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var aStart = DateTime.UtcNow.AddDays(10);
        var aEnd = aStart.AddDays(5);
        var tournamentAId = await CreateTournamentAsync(f, TournamentStatus.Finished, aStart, aEnd, aStart.AddDays(-1));
        await DirectRegisterAsync(f, tournamentAId, horseId, ownerId, RegistrationStatus.Approved);

        var bStart = aEnd.AddDays(-1);
        var tournamentBId = await CreateTournamentAsync(f, TournamentStatus.Published, bStart, bStart.AddDays(5), bStart.AddDays(-1));

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentBId, HorseId = horseId }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Register_DifferentHorseOverlap_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (owner1Id, user1Id, horse1Id) = await CreateApprovedOwnerHorseAsync(f, "a");
        var (owner2Id, user2Id, horse2Id) = await CreateApprovedOwnerHorseAsync(f, "b"); // different Owner, different Horse
        var aStart = DateTime.UtcNow.AddDays(10);
        var aEnd = aStart.AddDays(5);
        var tournamentAId = await CreateTournamentAsync(f, TournamentStatus.Published, aStart, aEnd, aStart.AddDays(-1));
        await DirectRegisterAsync(f, tournamentAId, horse1Id, owner1Id, RegistrationStatus.Approved);

        var bStart = aEnd.AddDays(-1);
        var tournamentBId = await CreateTournamentAsync(f, TournamentStatus.Published, bStart, bStart.AddDays(5), bStart.AddDays(-1));

        var controller = BuildController(f, user2Id);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentBId, HorseId = horse2Id }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Approve_OverlappingActiveTournament_RejectedEvenIfSubmittedDirectly()
    {
        // The public Register endpoint can never itself produce two overlapping active
        // registrations for one Horse (submit-time overlap check already blocks that) — so to
        // prove Approve's own independent re-check, seed both registrations directly, bypassing
        // Register entirely (e.g. simulating legacy data), then confirm Approve still blocks it.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var aStart = DateTime.UtcNow.AddDays(10);
        var aEnd = aStart.AddDays(5);
        var tournamentAId = await CreateTournamentAsync(f, TournamentStatus.Published, aStart, aEnd, aStart.AddDays(-1));
        await DirectRegisterAsync(f, tournamentAId, horseId, ownerId, RegistrationStatus.Approved);

        var bStart = aEnd.AddDays(-1);
        var tournamentBId = await CreateTournamentAsync(f, TournamentStatus.Published, bStart, bStart.AddDays(5), bStart.AddDays(-1));
        var regBId = await DirectRegisterAsync(f, tournamentBId, horseId, ownerId, RegistrationStatus.Pending);

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Approve(regBId));
        Assert.Equal(400, status);
        Assert.Equal("Ngựa đang tham gia một giải đấu có thời gian trùng.", message);
    }

    // ── RACEENTRY GATE ──────────────────────────────────────────────────

    [Fact]
    public async Task AssignHorseToRace_ApprovedRegistration_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateDraftTournamentWithRaceAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task AssignHorseToRace_PendingRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateDraftTournamentWithRaceAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AssignHorseToRace_RejectedRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateDraftTournamentWithRaceAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Rejected);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AssignHorseToRace_WithdrawnRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateDraftTournamentWithRaceAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Withdrawn);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AssignHorseToRace_NoRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await CreateDraftTournamentWithRaceAsync(f);
        var (_, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AssignHorseToRace_RegistrationForDifferentTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await CreateDraftTournamentWithRaceAsync(f);
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");

        var otherStart = DateTime.UtcNow.AddDays(30);
        var otherTournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, otherStart, otherStart.AddDays(5), otherStart.AddDays(-1));
        await DirectRegisterAsync(f, otherTournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    // ── LEGACY ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_DoesNotRequireRaceRegistrationOpen()
    {
        // Tournament-level registration must work with zero Races (let alone a Race in
        // RegistrationOpen) — proves Owner registration is Tournament-derived only (Task B §7).
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, start, start.AddDays(5), start.AddDays(-1));
        Assert.Equal(0, await f.Db.Races.CountAsync(r => r.TournamentId == tournamentId));

        var controller = BuildController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournamentId, HorseId = horseId }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    // ── GLOBAL RACEENTRY INVARIANT — RaceEntryService.RegisterAsync (Task B Final §1) ────
    // The Owner self-registration path (POST /api/horses/{id}/races/{id}/registrations, still
    // RaceStatus.RegistrationOpen-gated for legacy Race-lifecycle compatibility, §5) must apply
    // the exact same Approved-TournamentHorseRegistration gate as the admin-assign path.

    private static RaceEntryService BuildRaceEntryService(RaceLifecycleTests.LifecycleFixture f)
    {
        return new RaceEntryService(
            new OwnerRepository(f.Db), new HorseRepository(f.Db), new JockeyRepository(f.Db),
            f.RaceRepo, f.EntryRepo, f.UnitOfWork, f.Db);
    }

    /// <summary>Draft Tournament + Round + Race, with the Race flipped to RegistrationOpen (required by RaceEntryService.RegisterAsync's own gate).</summary>
    private static async Task<(Guid tournamentId, Guid raceId)> CreateRegistrationOpenRaceAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var (tournamentId, raceId) = await CreateDraftTournamentWithRaceAsync(f);
        var opened = await f.RaceManagement.OpenRegistrationAsync(raceId);
        Assert.True(opened.Result.Success, opened.Result.Message);
        return (tournamentId, raceId);
    }

    [Fact]
    public async Task RaceEntryServiceRegister_NoTournamentRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await CreateRegistrationOpenRaceAsync(f);
        var (_, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");

        var service = BuildRaceEntryService(f);
        var result = await service.RegisterAsync(userId, horseId, raceId, new RaceRegistrationRequest());
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task RaceEntryServiceRegister_PendingRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateRegistrationOpenRaceAsync(f);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var service = BuildRaceEntryService(f);
        var result = await service.RegisterAsync(userId, horseId, raceId, new RaceRegistrationRequest());
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task RaceEntryServiceRegister_RejectedRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateRegistrationOpenRaceAsync(f);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Rejected);

        var service = BuildRaceEntryService(f);
        var result = await service.RegisterAsync(userId, horseId, raceId, new RaceRegistrationRequest());
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task RaceEntryServiceRegister_WithdrawnRegistration_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateRegistrationOpenRaceAsync(f);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Withdrawn);

        var service = BuildRaceEntryService(f);
        var result = await service.RegisterAsync(userId, horseId, raceId, new RaceRegistrationRequest());
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task RaceEntryServiceRegister_ApprovedRegistration_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateRegistrationOpenRaceAsync(f);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await DirectRegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var service = BuildRaceEntryService(f);
        var result = await service.RegisterAsync(userId, horseId, raceId, new RaceRegistrationRequest());
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task RaceEntryServiceRegister_RegistrationForAnotherTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await CreateRegistrationOpenRaceAsync(f);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");

        var otherStart = DateTime.UtcNow.AddDays(30);
        var otherTournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, otherStart, otherStart.AddDays(5), otherStart.AddDays(-1));
        await DirectRegisterAsync(f, otherTournamentId, horseId, ownerId, RegistrationStatus.Approved);

        var service = BuildRaceEntryService(f);
        var result = await service.RegisterAsync(userId, horseId, raceId, new RaceRegistrationRequest());
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }
}
