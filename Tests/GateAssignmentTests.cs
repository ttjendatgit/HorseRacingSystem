using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// GATE-V1: Referee-only starting gate assignment. Covers authorization (Confirmed assignment to
/// the exact Race, not merely "has some assignment record"), participating-entry semantics (same
/// filter as R1a: Status != Rejected &amp;&amp; ScratchedAt == null), gate range/uniqueness, Race
/// lifecycle mutability, the extended read projection, the new StartRace gate-readiness rule, Q1
/// compatibility (generated entries stay GateNumber == null), and the DB-level unique index.
/// Wired against a real Sqlite in-memory DB and the actual production services, reusing
/// RaceLifecycleTests.LifecycleFixture (same convention as PrizeV1Tests/Q1QualificationTests).
/// </summary>
public class GateAssignmentTests
{
    // ── Shared builders ─────────────────────────────────────────────────

    private static async Task<Guid> CreateTrackAsync(RaceLifecycleTests.LifecycleFixture f, int capacity)
    {
        var track = new Track { Id = Guid.NewGuid(), Name = $"Track-{Guid.NewGuid():N}", Capacity = capacity, CreatedAt = DateTime.UtcNow };
        f.Db.Add(track);
        await f.Db.SaveChangesAsync();
        return track.Id;
    }

    /// <summary>Draft Tournament (single Final Round) + one pre-start (Scheduled) Race. No entries,
    /// no Referee assignment yet — callers add what each test needs. FINAL CAPACITY CORRECTION:
    /// trackCapacity defaults to equal maxParticipants (the common case, and what Publish
    /// readiness always requires: Race.MaxParticipants &lt;= Track.Capacity), but callers testing
    /// the corrected gate-bound rule pass a larger trackCapacity explicitly so the two can differ.</summary>
    private static async Task<(Guid tournamentId, Guid raceId)> BuildRaceAsync(
        RaceLifecycleTests.LifecycleFixture f, int maxParticipants = 12, int? trackCapacity = null)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = $"Gate-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(10),
            RegistrationDeadline = start.AddDays(-1), MinParticipants = 3, MaxParticipants = maxParticipants, MaxRounds = 1
        });
        Assert.True(create.Result.Success, create.Result.Message);
        var tournamentId = create.Result.Data!.Id;

        var round = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Final", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(5), AdvanceCount = 0
        });
        Assert.True(round.Result.Success, round.Result.Message);

        var track = await CreateTrackAsync(f, capacity: trackCapacity ?? maxParticipants);
        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = round.Result.Data!.Id,
            ScheduledAt = start, ScheduledEndAt = start.AddHours(2),
            TrackId = track, MaxParticipants = maxParticipants, QualificationSlots = 0
        });
        Assert.True(race.Result.Success, race.Result.Message);
        return (tournamentId, race.Result.Data!.Id);
    }

    private static async Task<(Guid userId, Guid refereeId)> CreateRefereeAsync(RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var userId = Guid.NewGuid();
        f.Db.Add(new User { Id = userId, Email = $"ref-{tag}-{Guid.NewGuid():N}@test.com", PasswordHash = "x", FullName = "Referee", Role = UserRole.Referee });
        var referee = new Referee { Id = Guid.NewGuid(), UserId = userId, LicenseNumber = $"LIC-{tag}-{Guid.NewGuid():N}", IsActive = true };
        f.Db.Add(referee);
        await f.Db.SaveChangesAsync();
        return (userId, referee.Id);
    }

    private static async Task CreateAssignmentAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId, Guid refereeId, RefereeAssignmentStatus status)
    {
        f.Db.Add(new RefereeAssignment
        {
            Id = Guid.NewGuid(), RaceId = raceId, RefereeId = refereeId, Role = "Chief Referee",
            Status = status, AssignedAt = DateTime.UtcNow,
            ConfirmedAt = status == RefereeAssignmentStatus.Confirmed ? DateTime.UtcNow : null
        });
        await f.Db.SaveChangesAsync();
    }

    /// <summary>Minimal RaceEntry (no health check, no Jockey) — sufficient for gate-assignment
    /// tests, which never drive the Race through StartRace themselves.</summary>
    private static async Task<Guid> AddEntryAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid raceId, string tag,
        RegistrationStatus status = RegistrationStatus.Approved, DateTime? scratchedAt = null, int? gateNumber = null)
    {
        var ownerUserId = Guid.NewGuid();
        f.Db.Add(new User { Id = ownerUserId, Email = $"owner-{tag}-{Guid.NewGuid():N}@test.com", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner });
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUserId, OwnerCode = $"OWN-{tag}-{Guid.NewGuid():N}" };
        f.Db.Add(owner);
        var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse-{tag}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.Add(horse);
        var entry = new RaceEntry
        {
            Id = Guid.NewGuid(), RaceId = raceId, HorseId = horse.Id,
            Status = status, OwnerConfirmed = true, JockeyConfirmed = true,
            ScratchedAt = scratchedAt, GateNumber = gateNumber
        };
        f.Db.Add(entry);
        await f.Db.SaveChangesAsync();
        return entry.Id;
    }

    /// <summary>One Horse (Approved) + one Jockey (Approved), Passed+ApprovedToRace health check —
    /// fully readiness-eligible per R1a, for the StartRace gate-readiness tests.</summary>
    private static async Task<Guid> AddQualifiableEntryAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid raceId, Guid refereeId, string tag, int? gateNumber)
    {
        var ownerUserId = Guid.NewGuid();
        f.Db.Add(new User { Id = ownerUserId, Email = $"owner-{tag}-{Guid.NewGuid():N}@test.com", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner });
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUserId, OwnerCode = $"OWN-{tag}-{Guid.NewGuid():N}" };
        f.Db.Add(owner);
        var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse-{tag}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.Add(horse);

        var jockeyUserId = Guid.NewGuid();
        f.Db.Add(new User { Id = jockeyUserId, Email = $"jockey-{tag}-{Guid.NewGuid():N}@test.com", PasswordHash = "x", FullName = "Jockey", Role = UserRole.Jockey });
        var jockey = new Jockey { Id = Guid.NewGuid(), UserId = jockeyUserId, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.Add(jockey);

        var entry = new RaceEntry
        {
            Id = Guid.NewGuid(), RaceId = raceId, HorseId = horse.Id, JockeyId = jockey.Id,
            Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = true,
            GateNumber = gateNumber
        };
        f.Db.Add(entry);
        f.Db.Add(new HorseHealthCheck
        {
            Id = Guid.NewGuid(), HorseId = horse.Id, RaceId = raceId, RefereeId = refereeId,
            Status = HealthCheckStatus.Passed, ApprovedToRace = true, CheckedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();
        return entry.Id;
    }

    private static async Task SetTournamentOngoingAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId)
    {
        var tournamentId = (await f.Db.Races.SingleAsync(r => r.Id == raceId)).TournamentId;
        var tournament = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentId);
        tournament.Status = TournamentStatus.Ongoing;
        tournament.IsActive = true;
        await f.Db.SaveChangesAsync();
    }

    // ── Controller harness (direct instantiation — no HTTP pipeline exists anywhere in this
    // suite; see HorseOwnerAuthorizationTests.cs for the established pattern/rationale) ──────

    private sealed class ThrowingRefereeService : IRefereeService
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

    private static RefereesController BuildController(RaceLifecycleTests.LifecycleFixture f, Guid? userId)
    {
        var controller = new RefereesController(
            new ThrowingRefereeService(),
            new RefereeRepository(f.Db),
            new RefereeAssignmentRepository(f.Db),
            f.EntryRepo,
            f.LiveResult,
            f.RaceManagement,
            f.UnitOfWork);

        var httpContext = new DefaultHttpContext();
        if (userId.HasValue)
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static (int status, bool? success, string? message) Unwrap(ActionResult result)
    {
        if (result is ForbidResult) return (403, null, null);
        if (result is ObjectResult obj)
        {
            var value = obj.Value;
            var successProp = value?.GetType().GetProperty("Success");
            var messageProp = value?.GetType().GetProperty("Message") ?? value?.GetType().GetProperty("message");
            bool? success = successProp?.GetValue(value) as bool?;
            var message = messageProp?.GetValue(value) as string;
            return (obj.StatusCode ?? 200, success, message);
        }
        throw new InvalidOperationException($"Unexpected result type {result.GetType()}");
    }

    // ── AUTHORIZATION ────────────────────────────────────────────────────

    [Fact]
    public async Task AssignGate_NonReferee_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        var strangerUserId = Guid.NewGuid(); // no Referee profile at all

        var controller = BuildController(f, strangerUserId);
        var (status, _, _) = Unwrap(await controller.AssignGateNumber(raceId, entryId, new AssignGateNumberRequest { GateNumber = 1 }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task AssignGate_UnassignedReferee_Forbidden()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        var (userId, _) = await CreateRefereeAsync(f, "unassigned");
        // No RefereeAssignment row at all for this Referee/Race.

        var controller = BuildController(f, userId);
        var (status, _, _) = Unwrap(await controller.AssignGateNumber(raceId, entryId, new AssignGateNumberRequest { GateNumber = 1 }));
        Assert.Equal(403, status);
    }

    [Fact]
    public async Task AssignGate_AssignedButNotConfirmed_Forbidden()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        var (userId, refereeId) = await CreateRefereeAsync(f, "assigned");
        await CreateAssignmentAsync(f, raceId, refereeId, RefereeAssignmentStatus.Assigned);

        var controller = BuildController(f, userId);
        var (status, _, _) = Unwrap(await controller.AssignGateNumber(raceId, entryId, new AssignGateNumberRequest { GateNumber = 1 }));
        Assert.Equal(403, status);
    }

    [Fact]
    public async Task AssignGate_ConfirmedReferee_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        var (userId, refereeId) = await CreateRefereeAsync(f, "confirmed");
        await CreateAssignmentAsync(f, raceId, refereeId, RefereeAssignmentStatus.Confirmed);

        var controller = BuildController(f, userId);
        var (status, success, message) = Unwrap(await controller.AssignGateNumber(raceId, entryId, new AssignGateNumberRequest { GateNumber = 1 }));
        Assert.True(success == true, message);
        Assert.Equal(200, status);
    }

    [Fact]
    public async Task AssignGate_OtherRaceEntry_Rejected()
    {
        // A Confirmed Referee for Race A must not manage gates via a raceId that doesn't match
        // the entry's own RaceId — even though this Referee IS Confirmed for Race A itself.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceIdA) = await BuildRaceAsync(f);
        var (_, raceIdB) = await BuildRaceAsync(f);
        var entryInRaceB = await AddEntryAsync(f, raceIdB, "h1");
        var (userId, refereeId) = await CreateRefereeAsync(f, "confirmedA");
        await CreateAssignmentAsync(f, raceIdA, refereeId, RefereeAssignmentStatus.Confirmed);

        var controller = BuildController(f, userId);
        var (status, _, _) = Unwrap(await controller.AssignGateNumber(raceIdA, entryInRaceB, new AssignGateNumberRequest { GateNumber = 1 }));
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task AssignGate_ConfirmedForOtherRace_DoesNotAuthorizeThisRace()
    {
        // A Confirmed assignment to a DIFFERENT Race must not authorize gate management here —
        // "multiple Confirmed Referees for a Race" is supported, but confirmation is per-Race.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceIdA) = await BuildRaceAsync(f);
        var (_, raceIdB) = await BuildRaceAsync(f);
        var entryInRaceB = await AddEntryAsync(f, raceIdB, "h1");
        var (userId, refereeId) = await CreateRefereeAsync(f, "confirmedElsewhere");
        await CreateAssignmentAsync(f, raceIdA, refereeId, RefereeAssignmentStatus.Confirmed);

        var controller = BuildController(f, userId);
        var (status, _, _) = Unwrap(await controller.AssignGateNumber(raceIdB, entryInRaceB, new AssignGateNumberRequest { GateNumber = 1 }));
        Assert.Equal(403, status);
    }

    [Fact]
    public async Task AssignGate_MultipleConfirmedReferees_EitherMayManageGates()
    {
        // Locked business: "Any Confirmed Referee assigned to the Race may manage its gates" —
        // there is no invented "Main Referee" concept from the free-text Role field.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        var (userId1, refereeId1) = await CreateRefereeAsync(f, "chief");
        var (userId2, refereeId2) = await CreateRefereeAsync(f, "assistant");
        await CreateAssignmentAsync(f, raceId, refereeId1, RefereeAssignmentStatus.Confirmed);
        await CreateAssignmentAsync(f, raceId, refereeId2, RefereeAssignmentStatus.Confirmed);

        var controller1 = BuildController(f, userId1);
        var (status1, success1, msg1) = Unwrap(await controller1.AssignGateNumber(raceId, entryId, new AssignGateNumberRequest { GateNumber = 1 }));
        Assert.True(success1 == true, msg1);

        var controller2 = BuildController(f, userId2);
        var (status2, success2, msg2) = Unwrap(await controller2.AssignGateNumber(raceId, entryId, new AssignGateNumberRequest { GateNumber = 2 }));
        Assert.True(success2 == true, msg2);
    }

    // ── ENTRY (participation semantics) ─────────────────────────────────

    [Fact]
    public async Task AssignGate_RejectedEntry_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1", status: RegistrationStatus.Rejected);

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 1);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AssignGate_ScratchedEntry_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1", status: RegistrationStatus.Approved, scratchedAt: DateTime.UtcNow);

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 1);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AssignGate_ParticipatingEntry_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 1);
        Assert.True(result.Result.Success, result.Result.Message);
    }

    // ── RANGE ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GateZero_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5);
        var entryId = await AddEntryAsync(f, raceId, "h1");

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 0);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task GateNegative_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5);
        var entryId = await AddEntryAsync(f, raceId, "h1");

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, -1);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    // FINAL CAPACITY CORRECTION: GateNumber's upper bound is Track.Capacity, a physical Track
    // slot count — never Race.MaxParticipants, which only caps how many RaceEntries the Race
    // holds. Track.Capacity=10 / Race.MaxParticipants=5 below is the task's own worked example:
    // gates above MaxParticipants but within Capacity (6, 10) must still succeed.

    [Fact]
    public async Task GateAboveRaceMaxParticipantsButWithinTrackCapacity_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5, trackCapacity: 10);
        var entryId = await AddEntryAsync(f, raceId, "h1");

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 6);
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task GateEqualRaceMaxParticipants_StillSucceeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5, trackCapacity: 10);
        var entryId = await AddEntryAsync(f, raceId, "h1");

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 5);
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task GateEqualTrackCapacity_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5, trackCapacity: 10);
        var entryId = await AddEntryAsync(f, raceId, "h1");

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 10);
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task GateAboveTrackCapacity_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5, trackCapacity: 10);
        var entryId = await AddEntryAsync(f, raceId, "h1");

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 11);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task NonContiguousGatesWithinTrackCapacity_AllValid()
    {
        // Track.Capacity=10, Race.MaxParticipants=3, exactly 3 participating entries — gates
        // 2, 6, 9 must all succeed; contiguity/matching-participant-count is never required.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 3, trackCapacity: 10);
        var e1 = await AddEntryAsync(f, raceId, "h1");
        var e2 = await AddEntryAsync(f, raceId, "h2");
        var e3 = await AddEntryAsync(f, raceId, "h3");

        var r1 = await f.RaceManagement.AssignGateNumberAsync(raceId, e1, 2);
        var r2 = await f.RaceManagement.AssignGateNumberAsync(raceId, e2, 6);
        var r3 = await f.RaceManagement.AssignGateNumberAsync(raceId, e3, 9);
        Assert.True(r1.Result.Success, r1.Result.Message);
        Assert.True(r2.Result.Success, r2.Result.Message);
        Assert.True(r3.Result.Success, r3.Result.Message);
    }

    // ── UNIQUENESS ───────────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateGateSameRace_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entry1 = await AddEntryAsync(f, raceId, "h1");
        var entry2 = await AddEntryAsync(f, raceId, "h2");
        var first = await f.RaceManagement.AssignGateNumberAsync(raceId, entry1, 3);
        Assert.True(first.Result.Success, first.Result.Message);

        var second = await f.RaceManagement.AssignGateNumberAsync(raceId, entry2, 3);
        Assert.False(second.Result.Success);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task SameGateDifferentRace_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceIdA) = await BuildRaceAsync(f);
        var (_, raceIdB) = await BuildRaceAsync(f);
        var entryA = await AddEntryAsync(f, raceIdA, "h1");
        var entryB = await AddEntryAsync(f, raceIdB, "h1");

        var first = await f.RaceManagement.AssignGateNumberAsync(raceIdA, entryA, 3);
        Assert.True(first.Result.Success, first.Result.Message);
        var second = await f.RaceManagement.AssignGateNumberAsync(raceIdB, entryB, 3);
        Assert.True(second.Result.Success, second.Result.Message);
    }

    [Fact]
    public async Task ReassignSameEntrySameGate_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        var first = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 4);
        Assert.True(first.Result.Success, first.Result.Message);

        var again = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 4);
        Assert.True(again.Result.Success, again.Result.Message);
    }

    [Fact]
    public async Task ChangeEntryToFreeGate_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        var first = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 1);
        Assert.True(first.Result.Success, first.Result.Message);

        var changed = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 2);
        Assert.True(changed.Result.Success, changed.Result.Message);

        var row = await f.Db.RaceEntries.AsNoTracking().SingleAsync(e => e.Id == entryId);
        Assert.Equal(2, row.GateNumber);
    }

    // ── LIFECYCLE ────────────────────────────────────────────────────────

    private static async Task SetRaceStatusAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId, RaceStatus status)
    {
        var race = await f.Db.Races.SingleAsync(r => r.Id == raceId);
        race.Status = status;
        await f.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Scheduled_AllowsGate()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1"); // Race created as Scheduled by default

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 1);
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task RegistrationOpen_AllowsGate()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        await SetRaceStatusAsync(f, raceId, RaceStatus.RegistrationOpen);

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 1);
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task RegistrationClosed_AllowsGate()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        await SetRaceStatusAsync(f, raceId, RaceStatus.RegistrationClosed);

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 1);
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task InProgress_RejectsGateChange()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        await SetRaceStatusAsync(f, raceId, RaceStatus.InProgress);

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 1);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Finished_RejectsGateChange()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        await SetRaceStatusAsync(f, raceId, RaceStatus.Finished);

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 1);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Cancelled_RejectsGateChange()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1");
        await SetRaceStatusAsync(f, raceId, RaceStatus.Cancelled);

        var result = await f.RaceManagement.AssignGateNumberAsync(raceId, entryId, 1);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    // ── READ ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefereeRaceEntries_ReturnGateNumber()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entryId = await AddEntryAsync(f, raceId, "h1", gateNumber: 7);

        var controller = BuildController(f, userId: null);
        var actionResult = await controller.GetRaceEntries(raceId);
        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value).Cast<object>().ToList();
        var item = Assert.Single(items);
        var gateProp = item.GetType().GetProperty("GateNumber");
        Assert.NotNull(gateProp);
        Assert.Equal(7, gateProp!.GetValue(item));
        var entryIdProp = item.GetType().GetProperty("EntryId");
        Assert.Equal(entryId, entryIdProp!.GetValue(item));
    }

    // ── STARTRACE READINESS ──────────────────────────────────────────────

    [Fact]
    public async Task StartRace_MissingGate_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5);
        var (_, refereeId) = await CreateRefereeAsync(f, "start-missing");
        await CreateAssignmentAsync(f, raceId, refereeId, RefereeAssignmentStatus.Confirmed);
        await AddQualifiableEntryAsync(f, raceId, refereeId, "h1", gateNumber: null);
        await SetTournamentOngoingAsync(f, raceId);

        var start = await f.RaceManagement.StartRaceAsync(raceId);
        Assert.False(start.Result.Success);
        Assert.Equal(400, start.StatusCode);
        Assert.Contains("cổng xuất phát", start.Result.Message);
    }

    [Fact]
    public async Task StartRace_AllParticipatingEntriesHaveGates_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5);
        var (_, refereeId) = await CreateRefereeAsync(f, "start-ok");
        await CreateAssignmentAsync(f, raceId, refereeId, RefereeAssignmentStatus.Confirmed);
        await AddQualifiableEntryAsync(f, raceId, refereeId, "h1", gateNumber: 1);
        await AddQualifiableEntryAsync(f, raceId, refereeId, "h2", gateNumber: 2);
        await SetTournamentOngoingAsync(f, raceId);

        var start = await f.RaceManagement.StartRaceAsync(raceId);
        Assert.True(start.Result.Success, start.Result.Message);
    }

    [Fact]
    public async Task StartRace_RejectedEntryMissingGate_DoesNotBlock()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5);
        var (_, refereeId) = await CreateRefereeAsync(f, "start-rejected");
        await CreateAssignmentAsync(f, raceId, refereeId, RefereeAssignmentStatus.Confirmed);
        await AddQualifiableEntryAsync(f, raceId, refereeId, "h1", gateNumber: 1);
        await AddEntryAsync(f, raceId, "rej", status: RegistrationStatus.Rejected, gateNumber: null);
        await SetTournamentOngoingAsync(f, raceId);

        var start = await f.RaceManagement.StartRaceAsync(raceId);
        Assert.True(start.Result.Success, start.Result.Message);
    }

    [Fact]
    public async Task StartRace_ScratchedEntryMissingGate_DoesNotBlock()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5);
        var (_, refereeId) = await CreateRefereeAsync(f, "start-scratched");
        await CreateAssignmentAsync(f, raceId, refereeId, RefereeAssignmentStatus.Confirmed);
        await AddQualifiableEntryAsync(f, raceId, refereeId, "h1", gateNumber: 1);
        await AddEntryAsync(f, raceId, "scr", status: RegistrationStatus.Approved, scratchedAt: DateTime.UtcNow, gateNumber: null);
        await SetTournamentOngoingAsync(f, raceId);

        var start = await f.RaceManagement.StartRaceAsync(raceId);
        Assert.True(start.Result.Success, start.Result.Message);
    }

    // GATE-V1: legacy-data defense-in-depth. The DB unique index and the write endpoint both make
    // an in-DB duplicate/out-of-range gate unreachable via normal use (and this Sqlite test DB is
    // built from the CURRENT model, so the index is already active — a genuine duplicate row can't
    // even be persisted here). ValidateGateReadinessForStart is invoked directly via reflection
    // against a manually constructed (never-persisted) entry list, which is exactly the shape
    // "legacy data predating GATE-V1" would have taken had it existed before the constraint did.
    private static ServiceResult<bool> InvokeGateReadiness(IRaceManagementService svc, List<RaceEntry> entries, int? trackCapacity)
    {
        var method = typeof(RaceManagementService).GetMethod("ValidateGateReadinessForStart", BindingFlags.NonPublic | BindingFlags.Instance);
        return (ServiceResult<bool>)method!.Invoke(svc, new object?[] { entries, trackCapacity })!;
    }

    [Fact]
    public async Task StartRace_LegacyDuplicateGate_DefensivelyRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var entries = new List<RaceEntry>
        {
            new() { Id = Guid.NewGuid(), RaceId = Guid.NewGuid(), HorseId = Guid.NewGuid(), Status = RegistrationStatus.Approved, GateNumber = 1 },
            new() { Id = Guid.NewGuid(), RaceId = Guid.NewGuid(), HorseId = Guid.NewGuid(), Status = RegistrationStatus.Approved, GateNumber = 1 },
        };

        var result = InvokeGateReadiness(f.RaceManagement, entries, trackCapacity: 10);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task StartRace_LegacyOutOfRangeGate_DefensivelyRejected()
    {
        // FINAL CAPACITY CORRECTION: "out of range" now means > Track.Capacity, not >
        // Race.MaxParticipants — 99 exceeds even a generous Track.Capacity of 10.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var entries = new List<RaceEntry>
        {
            new() { Id = Guid.NewGuid(), RaceId = Guid.NewGuid(), HorseId = Guid.NewGuid(), Status = RegistrationStatus.Approved, GateNumber = 99 },
        };

        var result = InvokeGateReadiness(f.RaceManagement, entries, trackCapacity: 10);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task StartRace_LegacyGateAboveTrackCapacity_DefensivelyRejected_ViaFullFlow()
    {
        // End-to-end (not reflection): a real Race/Track pair with a legacy out-of-range gate
        // seeded directly (bypassing AssignGateNumberAsync, which would itself reject it) —
        // proves StartRaceAsync's own defensive re-check catches it, not just the unit-level
        // ValidateGateReadinessForStart call above.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 5, trackCapacity: 10);
        var (_, refereeId) = await CreateRefereeAsync(f, "start-legacy-oor");
        await CreateAssignmentAsync(f, raceId, refereeId, RefereeAssignmentStatus.Confirmed);
        var entryId = await AddQualifiableEntryAsync(f, raceId, refereeId, "h1", gateNumber: 1);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.Id == entryId);
        entry.GateNumber = 11; // > Track.Capacity(10), seeded directly to bypass write-time validation
        await f.Db.SaveChangesAsync();
        await SetTournamentOngoingAsync(f, raceId);

        var start = await f.RaceManagement.StartRaceAsync(raceId);
        Assert.False(start.Result.Success);
        Assert.Equal(400, start.StatusCode);
    }

    [Fact]
    public async Task StartRace_NonContiguousGatesWithinTrackCapacity_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f, maxParticipants: 3, trackCapacity: 10);
        var (_, refereeId) = await CreateRefereeAsync(f, "start-noncontig");
        await CreateAssignmentAsync(f, raceId, refereeId, RefereeAssignmentStatus.Confirmed);
        await AddQualifiableEntryAsync(f, raceId, refereeId, "h1", gateNumber: 2);
        await AddQualifiableEntryAsync(f, raceId, refereeId, "h2", gateNumber: 6);
        await AddQualifiableEntryAsync(f, raceId, refereeId, "h3", gateNumber: 9);
        await SetTournamentOngoingAsync(f, raceId);

        var start = await f.RaceManagement.StartRaceAsync(raceId);
        Assert.True(start.Result.Success, start.Result.Message);
    }

    // ── Q1 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GeneratedNextRoundEntries_StillHaveNullGateNumber()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = $"Gate-Q1-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(20),
            RegistrationDeadline = start.AddDays(-1), MinParticipants = 3, MaxParticipants = 10, MaxRounds = 2
        });
        Assert.True(create.Result.Success, create.Result.Message);
        var tournamentId = create.Result.Data!.Id;

        var r1 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(5), AdvanceCount = 2
        });
        var r2 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Final", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(5), ScheduledEndDate = start.AddDays(10), AdvanceCount = 0
        });

        var track1 = await CreateTrackAsync(f, capacity: 4);
        var track2 = await CreateTrackAsync(f, capacity: 10);
        var raceA = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race A", TournamentId = tournamentId, RoundId = r1.Result.Data!.Id,
            ScheduledAt = start, ScheduledEndAt = start.AddHours(2), TrackId = track1, MaxParticipants = 4, QualificationSlots = 2
        });
        var raceFinal = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final", TournamentId = tournamentId, RoundId = r2.Result.Data!.Id,
            ScheduledAt = start.AddDays(5), ScheduledEndAt = start.AddDays(5).AddHours(2), TrackId = track2, MaxParticipants = 10, QualificationSlots = 0
        });

        var (_, refereeId) = await CreateRefereeAsync(f, "q1");
        await CreateAssignmentAsync(f, raceA.Result.Data!.Id, refereeId, RefereeAssignmentStatus.Confirmed);
        var h1Entry = await AddQualifiableEntryAsync(f, raceA.Result.Data!.Id, refereeId, "h1", gateNumber: 1);
        var h2Entry = await AddQualifiableEntryAsync(f, raceA.Result.Data!.Id, refereeId, "h2", gateNumber: 2);
        var h1 = (await f.Db.RaceEntries.AsNoTracking().SingleAsync(e => e.Id == h1Entry)).HorseId;
        var h2 = (await f.Db.RaceEntries.AsNoTracking().SingleAsync(e => e.Id == h2Entry)).HorseId;

        await SetTournamentOngoingAsync(f, raceA.Result.Data!.Id);
        Assert.True((await f.RaceManagement.OpenRegistrationAsync(raceA.Result.Data!.Id)).Result.Success);
        Assert.True((await f.RaceManagement.CloseRegistrationAsync(raceA.Result.Data!.Id)).Result.Success);
        var startResult = await f.RaceManagement.StartRaceAsync(raceA.Result.Data!.Id);
        Assert.True(startResult.Result.Success, startResult.Result.Message);
        Assert.True((await f.RaceManagement.EndRaceAsync(raceA.Result.Data!.Id)).Result.Success);

        var submit = await f.LiveResult.UpdateRaceResultAsync(raceA.Result.Data!.Id, new RaceResultRequest
        {
            Rankings = new List<RaceResultRankingItemRequest>
            {
                new() { HorseId = h1, Position = 1 },
                new() { HorseId = h2, Position = 2 },
            }
        });
        Assert.True(submit.Result.Success, submit.Result.Message);

        f.Db.Add(new RaceReport { Id = Guid.NewGuid(), RaceId = raceA.Result.Data!.Id, RefereeId = refereeId, CompletedAt = DateTime.UtcNow, Details = "Clean race.", CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        var approve = await f.Admin.ApproveRaceResultAsync(raceA.Result.Data!.Id);
        Assert.True(approve.Result.Success, approve.Result.Message);

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(r1.Result.Data!.Id);
        Assert.True(generate.Result.Success, generate.Result.Message);

        var finalEntries = await f.Db.RaceEntries.AsNoTracking().Where(e => e.RaceId == raceFinal.Result.Data!.Id).ToListAsync();
        Assert.NotEmpty(finalEntries);
        Assert.All(finalEntries, e => Assert.Null(e.GateNumber));
    }

    // ── DB ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RaceGateUniqueIndex_Exists()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var entityType = f.Db.Model.FindEntityType(typeof(RaceEntry))!;
        var index = entityType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "IX_RaceEntries_RaceId_GateNumber_Active");
        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
        Assert.Equal(new[] { "RaceId", "GateNumber" }, index.Properties.Select(p => p.Name));
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateNonNullGateWithinSameRace()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var ownerUserId = Guid.NewGuid();
        f.Db.Add(new User { Id = ownerUserId, Email = $"owner-dup-{Guid.NewGuid():N}@test.com", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner });
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUserId, OwnerCode = $"OWN-dup-{Guid.NewGuid():N}" };
        f.Db.Add(owner);
        var horseA = new Horse { Id = Guid.NewGuid(), Name = "A", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
        var horseB = new Horse { Id = Guid.NewGuid(), Name = "B", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.AddRange(horseA, horseB);
        await f.Db.SaveChangesAsync();

        f.Db.AddRange(
            new RaceEntry { Id = Guid.NewGuid(), RaceId = raceId, HorseId = horseA.Id, Status = RegistrationStatus.Approved, GateNumber = 1 },
            new RaceEntry { Id = Guid.NewGuid(), RaceId = raceId, HorseId = horseB.Id, Status = RegistrationStatus.Approved, GateNumber = 1 });

        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabaseAllowsSameGateAcrossDifferentRaces()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceIdA) = await BuildRaceAsync(f);
        var (_, raceIdB) = await BuildRaceAsync(f);
        var entryA = await AddEntryAsync(f, raceIdA, "a", gateNumber: 1);
        var entryB = await AddEntryAsync(f, raceIdB, "b", gateNumber: 1);

        Assert.Equal(1, (await f.Db.RaceEntries.AsNoTracking().SingleAsync(e => e.Id == entryA)).GateNumber);
        Assert.Equal(1, (await f.Db.RaceEntries.AsNoTracking().SingleAsync(e => e.Id == entryB)).GateNumber);
    }

    [Fact]
    public async Task DatabaseAllowsMultipleNullGateNumbers()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await BuildRaceAsync(f);
        var entry1 = await AddEntryAsync(f, raceId, "a", gateNumber: null);
        var entry2 = await AddEntryAsync(f, raceId, "b", gateNumber: null);

        Assert.Null((await f.Db.RaceEntries.AsNoTracking().SingleAsync(e => e.Id == entry1)).GateNumber);
        Assert.Null((await f.Db.RaceEntries.AsNoTracking().SingleAsync(e => e.Id == entry2)).GateNumber);
    }
}
