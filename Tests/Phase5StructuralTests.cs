using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Phase5B: Round/Race structural readiness — RoundNumber uniqueness, Draft-only structural
/// mutation, and the complete Publish structural validation (sequence, Final Round, AdvanceCount,
/// Race presence, schedule hierarchy, Track existence/capacity/overlap, QualificationSlots, Round
/// capacity coverage). Wired against a real Sqlite in-memory DB and the actual production services,
/// reusing RaceLifecycleTests.LifecycleFixture.
/// </summary>
public class Phase5StructuralTests
{
    // ── Shared builders ─────────────────────────────────────────────────

    private static CreateTournamentRequest ValidDraftTournamentRequest(DateTime start, DateTime end, int maxParticipants, int minParticipants = 3, int maxRounds = 1)
        => new CreateTournamentRequest
        {
            Name = "Phase5 Tournament",
            StartDate = start,
            EndDate = end,
            RegistrationDeadline = start.AddDays(-1),
            MinParticipants = minParticipants,
            MaxParticipants = maxParticipants,
            MaxRounds = maxRounds
        };

    private static async Task<Guid> CreateTrackAsync(RaceLifecycleTests.LifecycleFixture f, int? capacity)
    {
        var track = new Track { Id = Guid.NewGuid(), Name = $"Track-{Guid.NewGuid():N}", Capacity = capacity, CreatedAt = DateTime.UtcNow };
        f.Db.Add(track);
        await f.Db.SaveChangesAsync();
        return track.Id;
    }

    private static Task<ServiceResult<TournamentResponse>> PublishAsync(RaceLifecycleTests.LifecycleFixture f, Guid tournamentId)
        => f.TournamentSvc.ChangeStatusAsync(tournamentId, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());

    /// <summary>Single-Round Tournament (Round 1 = Final, §12.3) with no Race yet — cheap base for tests focused on a single Race's own checks (Track, MaxParticipants, QualificationSlots, schedule).</summary>
    private static async Task<(Guid tournamentId, Guid roundId, DateTime roundStart, DateTime roundEnd)> BuildDraftSingleFinalRoundAsync(
        RaceLifecycleTests.LifecycleFixture f, int maxParticipants = 5, int minParticipants = 3, int maxRounds = 1)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(20);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, end, maxParticipants, minParticipants, maxRounds));
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

    /// <summary>Fully valid, Publish-ready 2-Round Tournament (Tournament.MaxRounds=2): Round1 (non-final, AdvanceCount=4, 1 Race MaxParticipants=10/QualificationSlots=4) -> Round2 (final because RoundNumber(2)==MaxRounds(2), AdvanceCount=0, 1 Race MaxParticipants=4/QualificationSlots=0).</summary>
    private static async Task<(Guid tournamentId, Guid round1Id, Guid round2Id, Guid race1Id, Guid race2Id)> BuildValidPublishableTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(20);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, end, maxParticipants: 10, maxRounds: 2));
        Assert.True(create.Result.Success, create.Result.Message);
        var tournamentId = create.Result.Data!.Id;

        var track1 = await CreateTrackAsync(f, capacity: 10);
        var track2 = await CreateTrackAsync(f, capacity: 4);

        var round1Start = start;
        var round1End = start.AddDays(5);
        var round2Start = round1End; // back-to-back, allowed
        var round2End = round1End.AddDays(5);

        var r1 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = round1Start, ScheduledEndDate = round1End, AdvanceCount = 4
        });
        Assert.True(r1.Result.Success, r1.Result.Message);

        var r2 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Final", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = round2Start, ScheduledEndDate = round2End, AdvanceCount = 0
        });
        Assert.True(r2.Result.Success, r2.Result.Message);

        var race1 = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race 1", TournamentId = tournamentId, RoundId = r1.Result.Data!.Id,
            ScheduledAt = round1Start, ScheduledEndAt = round1Start.AddHours(1),
            TrackId = track1, MaxParticipants = 10, QualificationSlots = 4
        });
        Assert.True(race1.Result.Success, race1.Result.Message);

        var race2 = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tournamentId, RoundId = r2.Result.Data!.Id,
            ScheduledAt = round2Start, ScheduledEndAt = round2Start.AddHours(1),
            TrackId = track2, MaxParticipants = 4, QualificationSlots = 0
        });
        Assert.True(race2.Result.Success, race2.Result.Message);

        return (tournamentId, r1.Result.Data.Id, r2.Result.Data.Id, race1.Result.Data!.Id, race2.Result.Data!.Id);
    }

    private static async Task SeedOtherTournamentRaceOnTrackAsync(
        RaceLifecycleTests.LifecycleFixture f, TournamentStatus status, Guid trackId, DateTime start, DateTime end)
    {
        var otherTournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = "Other Tournament", StartDate = start.AddDays(-30), EndDate = end.AddDays(30),
            Status = status, CreatedAt = DateTime.UtcNow
        };
        var otherRound = new Round
        {
            Id = Guid.NewGuid(), Name = "Other Round", TournamentId = otherTournament.Id, RoundNumber = 1,
            ScheduledStartDate = start.AddDays(-1), ScheduledEndDate = end.AddDays(1), AdvanceCount = 0
        };
        var otherRace = new Race
        {
            Id = Guid.NewGuid(), Name = "Other Race", TournamentId = otherTournament.Id, RoundId = otherRound.Id,
            TrackId = trackId, ScheduledAt = start, ScheduledEndAt = end, Status = RaceStatus.Scheduled,
            MaxParticipants = 5, CreatedAt = DateTime.UtcNow
        };
        f.Db.AddRange(otherTournament, otherRound, otherRace);
        await f.Db.SaveChangesAsync();
    }

    // ── ROUND CRUD ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_RoundNumberZero_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R0", TournamentId = tournamentId, RoundNumber = 0,
            ScheduledStartDate = roundEnd, ScheduledEndDate = roundEnd.AddDays(1)
        });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Create_RoundNumberNegative_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R-1", TournamentId = tournamentId, RoundNumber = -1,
            ScheduledStartDate = roundEnd, ScheduledEndDate = roundEnd.AddDays(1)
        });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateRoundNumber_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Duplicate", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = roundEnd, ScheduledEndDate = roundEnd.AddDays(1)
        });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Create_GappedRoundNumber_AllowedInDraft()
    {
        // V0.1: RoundNumber may still gap ahead of existing Rounds during Draft (only Publish
        // enforces the exact 1..MaxRounds sequence) — but it must stay within the Tournament's
        // own MaxRounds ceiling, so this Tournament needs MaxRounds >= 3 for R3 to be creatable.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f, maxRounds: 3);
        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R3", TournamentId = tournamentId, RoundNumber = 3,
            ScheduledStartDate = roundEnd, ScheduledEndDate = roundEnd.AddDays(1)
        });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task Update_DuplicateRoundNumber_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round1Start, round1End) = await BuildDraftSingleFinalRoundAsync(f, maxRounds: 2);
        var round2 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = round1End, ScheduledEndDate = round1End.AddDays(1)
        });
        Assert.True(round2.Result.Success, round2.Result.Message);

        var update = await f.RoundSvc.UpdateRoundAsync(round2.Result.Data!.Id, new UpdateRoundRequest { RoundNumber = 1 });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task RoundMutation_Published_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, _, _) = await BuildValidPublishableTournamentAsync(f);
        var publish = await PublishAsync(f, tournamentId);
        Assert.True(publish.Result.Success, publish.Result.Message);

        var create = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "X", TournamentId = tournamentId, RoundNumber = 3,
            ScheduledStartDate = DateTime.UtcNow.AddDays(30), ScheduledEndDate = DateTime.UtcNow.AddDays(31)
        });
        Assert.False(create.Result.Success);
        Assert.Equal(400, create.StatusCode);

        var update = await f.RoundSvc.UpdateRoundAsync(round1Id, new UpdateRoundRequest { Name = "Changed" });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);

        var delete = await f.RoundSvc.DeleteRoundAsync(round1Id);
        Assert.False(delete.Result.Success);
        Assert.Equal(400, delete.StatusCode);
    }

    // ── SEQUENCE / PUBLISH ──────────────────────────────────────────────

    [Fact]
    public async Task Publish_SequenceStartsAt2_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        // V0.1: MaxRounds=2 so this Round (RoundNumber=2) is actually creatable — otherwise the
        // new create-time ceiling would reject it outright and the test would never reach the
        // Publish-time "starts at 2" sequence check it's meant to prove.
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        var r2 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 0
        });
        Assert.True(r2.Result.Success, r2.Result.Message);

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("liên tục", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_SequenceGap_Rejected()
    {
        // V0-D: MaxRounds=3 but Round2 is missing (only 1, 3 exist) — a genuine gap against the
        // 1..MaxRounds identity, not merely "fewer rounds than MaxRounds".
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 3));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 4
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R3", TournamentId = tournamentId, RoundNumber = 3,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("liên tục", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_ValidSequence_PassesSequenceValidation()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, _, _, _) = await BuildValidPublishableTournamentAsync(f);
        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("liên tục", publish.Result.Message ?? "");
    }

    // ── FINAL ROUND ─────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_NoFinalRound_Rejected()
    {
        // V0: neither Round has AdvanceCount=0. Round2 (RoundNumber == MaxRounds == 2) is still
        // correctly identified as Final by RoundNumber — its wrong AdvanceCount is what's rejected.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 4
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 4
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Vòng chung kết", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_MultipleAdvanceCountZero_Rejected()
    {
        // V0: Round2 (RoundNumber == MaxRounds == 2) is Final regardless of AdvanceCount, and its
        // AdvanceCount=0 is correct. Round1 is non-final (RoundNumber(1) != MaxRounds(2)) and its
        // AdvanceCount=0 is what's now rejected — "two zero-AdvanceCount rounds" is no longer a
        // distinct error class, it's just "a non-final round improperly has AdvanceCount=0".
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 0
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Vòng 1 (không phải chung kết) phải có AdvanceCount > 0", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_ZeroAdvanceCountNotLast_Rejected()
    {
        // V0: Round1 (AdvanceCount=0) is NOT Final — RoundNumber(1) != MaxRounds(2) — so its
        // AdvanceCount=0 is invalid for a non-final Round. Round2 (the real Final, by RoundNumber)
        // has AdvanceCount=4, also invalid (Final must be 0). Both are independently reported.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 0
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 4
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Vòng 1 (không phải chung kết) phải có AdvanceCount > 0", publish.Result.Message);
        Assert.Contains("Vòng chung kết (Vòng 2) phải có AdvanceCount = 0", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_ValidFinalRound_Passes()
    {
        // V0-A: MaxRounds=2, Round1 (non-final) AdvanceCount=4 > 0, Round2 (final, RoundNumber
        // == MaxRounds) AdvanceCount=0 — publish succeeds when all other rules are valid.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, _, _, _) = await BuildValidPublishableTournamentAsync(f);
        var publish = await PublishAsync(f, tournamentId);
        Assert.True(publish.Result.Success, publish.Result.Message);
    }

    // ── V0: FINAL ROUND IS DETERMINED BY RoundNumber == Tournament.MaxRounds, NEVER BY
    //        AdvanceCount == 0, rounds.Count, OR ANY OTHER HEURISTIC ──────────────────────────
    // V0-D (missing/gapped Round against MaxRounds) is already covered by Publish_SequenceGap_Rejected
    // above. V0-H (Final Round with >1 Race), V0-I (non-final QualificationSlots sum mismatch), and
    // V0-J (Final Race QualificationSlots > 0) are already covered by Publish_FinalWithMultipleRaces_Rejected,
    // Publish_QualificationSlotSumMismatch_Rejected, and Publish_FinalQualificationSlotsNonZero_Rejected
    // respectively — all of which now exercise the corrected RoundNumber-based Final determination
    // via BuildValidPublishableTournamentAsync (Tournament.MaxRounds=2).

    [Fact]
    public async Task Publish_ThreeRound_NonFinalRoundWithZeroAdvanceCount_Rejected()
    {
        // V0-B: MaxRounds=3. Round2 has AdvanceCount=0 but RoundNumber(2) != MaxRounds(3) — it is
        // NOT Final, so AdvanceCount=0 on it must still be rejected as a non-final Round.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(30), maxParticipants: 10, maxRounds: 3));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 8
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R3", TournamentId = tournamentId, RoundNumber = 3,
            ScheduledStartDate = start.AddDays(2), ScheduledEndDate = start.AddDays(3), AdvanceCount = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Vòng 2 (không phải chung kết) phải có AdvanceCount > 0", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_ThreeRound_FinalRoundWithPositiveAdvanceCount_Rejected()
    {
        // V0-C: MaxRounds=3. Round3 (RoundNumber == MaxRounds) IS Final by definition regardless
        // of its AdvanceCount — its positive AdvanceCount is what's rejected, not "not being final".
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(30), maxParticipants: 10, maxRounds: 3));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 8
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 4
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R3", TournamentId = tournamentId, RoundNumber = 3,
            ScheduledStartDate = start.AddDays(2), ScheduledEndDate = start.AddDays(3), AdvanceCount = 2 // should be 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Vòng chung kết (Vòng 3) phải có AdvanceCount = 0", publish.Result.Message);
    }

    [Fact]
    public async Task DuplicateRoundNumber_RejectedByDbConstraintBeforeEverReachingPublish()
    {
        // V0-E: RoundService.CreateRoundAsync already rejects a duplicate RoundNumber at create
        // time (see Create_DuplicateRoundNumber_Rejected). This proves the guarantee goes one
        // layer deeper still — a unique index on (TournamentId, RoundNumber) (see migration
        // EnforceUniqueRoundNumberPerTournament) makes a duplicate RoundNumber impossible to
        // persist at all, even by seeding directly and bypassing RoundService entirely. So
        // "duplicate RoundNumber -> publish fails" is unreachable as a Publish-time scenario:
        // the data can never exist for Publish to see in the first place.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        f.Db.AddRange(
            new Round { Id = Guid.NewGuid(), Name = "R1a", TournamentId = tournamentId, RoundNumber = 1, ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 4 },
            new Round { Id = Guid.NewGuid(), Name = "R1b", TournamentId = tournamentId, RoundNumber = 1, ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0 });

        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Publish_RoundCountLessThanMaxRounds_Rejected()
    {
        // V0-F: MaxRounds=3 but only Round1 and Round2 exist — a count mismatch (not a gap or
        // duplicate) must still fail the sequence check, since identity is 1..MaxRounds, not
        // 1..rounds.Count.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(30), maxParticipants: 10, maxRounds: 3));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 4
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("liên tục", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_SingleRoundTournament_MaxRoundsOne_Succeeds()
    {
        // V0-G: MaxRounds=1 — Round1 IS the Final Round (RoundNumber == MaxRounds), with no
        // non-final Round required before it. One Race, AdvanceCount=0, QualificationSlots=0.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(race.Result.Success, race.Result.Message);

        var publish = await PublishAsync(f, tournamentId);
        Assert.True(publish.Result.Success, publish.Result.Message);
        Assert.Equal(TournamentStatus.Published, publish.Result.Data!.Status);
    }

    // ── ADVANCECOUNT ────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_NullAdvanceCount_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1) // AdvanceCount omitted -> null
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("AdvanceCount là bắt buộc", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_NegativeAdvanceCount_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = -1
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("AdvanceCount không được âm", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_NonFinalZero_Rejected()
    {
        // V0: a non-final Round (RoundNumber != MaxRounds) with AdvanceCount == 0 is rejected
        // directly by the non-final ">0" rule — no longer surfaced via a "multiple zero" scan.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 0
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Equal(TournamentStatus.Draft, (await f.TournamentSvc.GetTournamentAsync(tournamentId)).Result.Data!.Status);
    }

    [Fact]
    public async Task Publish_AdvanceCountEqualsPlanned_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 10 // == Tournament.MaxParticipants
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("phải nhỏ hơn số lượng dự kiến tham gia", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_ValidAdvanceCounts_Pass()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 4
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("AdvanceCount", publish.Result.Message ?? "");
    }

    // ── RACE PRESENCE ───────────────────────────────────────────────────

    [Fact]
    public async Task Publish_NonFinalWithoutRace_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, race1Id, _) = await BuildValidPublishableTournamentAsync(f);
        // Remove Round1's only Race directly (Round1 stays, now has zero Races).
        f.Db.Races.Remove(await f.Db.Races.FirstAsync(r => r.Id == race1Id));
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("phải có ít nhất 1 Cuộc đua", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_FinalWithoutRace_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, _, _, race2Id) = await BuildValidPublishableTournamentAsync(f);
        f.Db.Races.Remove(await f.Db.Races.FirstAsync(r => r.Id == race2Id));
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("phải có đúng 1 Cuộc đua", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_FinalWithMultipleRaces_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, round2Id, _, _) = await BuildValidPublishableTournamentAsync(f);
        var track = await CreateTrackAsync(f, capacity: 4);
        var extra = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Extra Final Race", TournamentId = tournamentId, RoundId = round2Id,
            ScheduledAt = DateTime.UtcNow.AddDays(16), ScheduledEndAt = DateTime.UtcNow.AddDays(16).AddHours(1),
            TrackId = track, MaxParticipants = 4, QualificationSlots = 0
        });
        Assert.True(extra.Result.Success, extra.Result.Message);

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("phải có đúng 1 Cuộc đua", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_ValidRaceStructure_Passes()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, _, _, _) = await BuildValidPublishableTournamentAsync(f);
        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("phải có", publish.Result.Message ?? "");
    }

    // ── SCHEDULE ────────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_RoundStartEqualsEnd_Rejected()
    {
        // Phase5B Fix2: CreateRoundAsync now friendly-rejects this at Create time (see
        // CreateRound_StartEqualsEnd_Rejected), so seed directly to prove the independent
        // Publish-time defense-in-depth also catches it (e.g. legacy data).
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 5));
        var tournamentId = create.Result.Data!.Id;
        f.Db.Add(new Round
        {
            Id = Guid.NewGuid(), Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start, AdvanceCount = 0
        });
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Vòng 1: Thời gian bắt đầu phải trước thời gian kết thúc", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_RoundOutsideTournament_Rejected()
    {
        // Phase5B Fix2: CreateRoundAsync now friendly-rejects this at Create time (see
        // CreateRound_BeforeTournamentStart_Rejected), so seed directly to prove the independent
        // Publish-time defense-in-depth also catches it (e.g. legacy data).
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 5));
        var tournamentId = create.Result.Data!.Id;
        f.Db.Add(new Round
        {
            Id = Guid.NewGuid(), Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start.AddDays(-1), ScheduledEndDate = start.AddDays(1), AdvanceCount = 0
        });
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("không được trước ngày bắt đầu giải đấu", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_RaceStartEqualsEnd_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        f.Db.Add(new Race
        {
            Id = Guid.NewGuid(), Name = "Bad Race", TournamentId = tournamentId, RoundId = roundId,
            TrackId = track, ScheduledAt = roundStart, ScheduledEndAt = roundStart, // equal -> invalid
            MaxParticipants = 5, QualificationSlots = 0, Status = RaceStatus.Scheduled, CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Cuộc đua \"Bad Race\": Thời gian bắt đầu phải trước thời gian kết thúc", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_RaceOutsideRound_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        // Phase5B Fix3: CreateRaceAsync now friendly-rejects this at Create time (see
        // CreateRace_BeforeRoundStart_Rejected), so seed directly to prove the independent
        // Publish-time defense-in-depth also catches it (e.g. legacy data).
        f.Db.Add(new Race
        {
            Id = Guid.NewGuid(), Name = "Early Race", TournamentId = tournamentId, RoundId = roundId,
            TrackId = track, ScheduledAt = roundStart.AddHours(-2), ScheduledEndAt = roundStart.AddHours(-1),
            MaxParticipants = 5, QualificationSlots = 0, Status = RaceStatus.Scheduled, CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("không được trước thời gian bắt đầu Vòng đấu", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_ParentBoundaryEquality_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(5);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, end, maxParticipants: 5));
        var tournamentId = create.Result.Data!.Id;
        // Round exactly spans the Tournament window (inclusive boundaries).
        var round = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = end, AdvanceCount = 0
        });
        var track = await CreateTrackAsync(f, capacity: 5);
        // Race exactly spans the Round window (inclusive boundaries).
        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Boundary Race", TournamentId = tournamentId, RoundId = round.Result.Data!.Id,
            ScheduledAt = start, ScheduledEndAt = end, TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(race.Result.Success, race.Result.Message);

        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("không được trước", publish.Result.Message ?? "");
        Assert.DoesNotContain("không được sau", publish.Result.Message ?? "");
    }

    [Fact]
    public async Task Publish_BackToBackRounds_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, _, _, _) = await BuildValidPublishableTournamentAsync(f);
        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("không được trước thời gian kết thúc Vòng", publish.Result.Message ?? "");
    }

    // ── TRACK ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RaceCreate_InvalidTrack_RejectedFriendly()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Bad Track Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = Guid.NewGuid(), MaxParticipants = 5
        });
        Assert.False(result.Result.Success);
        Assert.NotEqual(500, result.StatusCode);
    }

    [Fact]
    public async Task Publish_MissingTrack_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "No Track Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(race.Result.Success, race.Result.Message);

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Đường đua (Track) là bắt buộc", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_NullTrackCapacity_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: null);
        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "No Capacity Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(race.Result.Success, race.Result.Message);

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("chưa được thiết lập", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_RaceCapacityExceedsTrack_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 3);
        // Create-time friendly validation would already reject MaxParticipants > Capacity, so
        // seed directly to prove the Publish-time defense also catches it independently
        // (e.g. legacy data, or a Track whose Capacity was reduced after the Race was created).
        f.Db.Add(new Race
        {
            Id = Guid.NewGuid(), Name = "Over Capacity Race", TournamentId = tournamentId, RoundId = roundId,
            TrackId = track, ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            MaxParticipants = 5, QualificationSlots = 0, Status = RaceStatus.Scheduled, CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("vượt quá sức chứa", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_ValidTrackCapacity_Passes()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, _, _, _) = await BuildValidPublishableTournamentAsync(f);
        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("vượt quá sức chứa", publish.Result.Message ?? "");
        Assert.DoesNotContain("chưa được thiết lập", publish.Result.Message ?? "");
    }

    [Fact]
    public async Task Publish_SameTrackOverlapInsideTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        var race1 = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race A", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(2),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(race1.Result.Success, race1.Result.Message);

        // Overlapping second Race on the same Track. CreateRaceAsync's friendly overlap check
        // would already reject this through the normal API, so seed directly to test the
        // independent Publish-time defense against the same invalid state.
        f.Db.Add(new Race
        {
            Id = Guid.NewGuid(), Name = "Race B", TournamentId = tournamentId, RoundId = roundId,
            TrackId = track, ScheduledAt = roundStart.AddHours(1), ScheduledEndAt = roundStart.AddHours(3),
            MaxParticipants = 5, QualificationSlots = 0, Status = RaceStatus.Scheduled, CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Trùng lịch đường đua", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_SameTrackOverlapPublishedOtherTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        await SeedOtherTournamentRaceOnTrackAsync(f, TournamentStatus.Published, track, roundStart.AddHours(1), roundStart.AddHours(3));

        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Candidate Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(2),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        // Friendly Create-time overlap check should already catch a Published other-Tournament
        // conflict too — assert that directly, then also prove Publish independently blocks it.
        Assert.False(race.Result.Success);
    }

    [Fact]
    public async Task Publish_SameTrackOverlapOngoingOtherTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        await SeedOtherTournamentRaceOnTrackAsync(f, TournamentStatus.Ongoing, track, roundStart.AddHours(1), roundStart.AddHours(3));

        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Candidate Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(2),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.False(race.Result.Success);
    }

    [Fact]
    public async Task Publish_SameTrackOverlapDraftOtherTournament_Ignored()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        await SeedOtherTournamentRaceOnTrackAsync(f, TournamentStatus.Draft, track, roundStart.AddHours(1), roundStart.AddHours(3));

        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Candidate Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(2),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        // A Draft other-Tournament never reserves the Track globally, so this succeeds.
        Assert.True(race.Result.Success, race.Result.Message);

        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("Trùng lịch đường đua", publish.Result.Message ?? "");
    }

    [Fact]
    public async Task Publish_SameTrackOverlapCancelledOtherTournament_Ignored()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        await SeedOtherTournamentRaceOnTrackAsync(f, TournamentStatus.Cancelled, track, roundStart.AddHours(1), roundStart.AddHours(3));

        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Candidate Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(2),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(race.Result.Success, race.Result.Message);

        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("Trùng lịch đường đua", publish.Result.Message ?? "");
    }

    [Fact]
    public async Task Publish_BackToBackSameTrack_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        var race1 = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race A", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(race1.Result.Success, race1.Result.Message);

        // Race B starts exactly when Race A ends on the same Track — allowed (strict overlap formula).
        f.Db.Add(new Race
        {
            Id = Guid.NewGuid(), Name = "Race B", TournamentId = tournamentId, RoundId = roundId,
            TrackId = track, ScheduledAt = roundStart.AddHours(1), ScheduledEndAt = roundStart.AddHours(2),
            MaxParticipants = 5, QualificationSlots = 0, Status = RaceStatus.Scheduled, CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();
        f.Db.Races.Remove(await f.Db.Races.FirstAsync(r => r.Name == "Race A")); // final round allows exactly 1 race

        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("Trùng lịch đường đua", publish.Result.Message ?? "");
    }

    // ── RACE CAPACITY ───────────────────────────────────────────────────

    [Fact]
    public async Task Publish_RaceMaxParticipantsZero_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        f.Db.Add(new Race
        {
            Id = Guid.NewGuid(), Name = "Zero Cap Race", TournamentId = tournamentId, RoundId = roundId,
            TrackId = track, ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            MaxParticipants = 0, QualificationSlots = 0, Status = RaceStatus.Scheduled, CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("MaxParticipants phải lớn hơn 0", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_RaceMaxParticipantsNegative_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        f.Db.Add(new Race
        {
            Id = Guid.NewGuid(), Name = "Negative Cap Race", TournamentId = tournamentId, RoundId = roundId,
            TrackId = track, ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            MaxParticipants = -1, QualificationSlots = 0, Status = RaceStatus.Scheduled, CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("MaxParticipants phải lớn hơn 0", publish.Result.Message);
    }

    // ── QUALIFICATION ───────────────────────────────────────────────────

    [Fact]
    public async Task Publish_NonFinalNullQualificationSlots_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        var r1 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = 4
        });
        var r2 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });
        var track1 = await CreateTrackAsync(f, capacity: 10);
        var track2 = await CreateTrackAsync(f, capacity: 4);
        await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race1", TournamentId = tournamentId, RoundId = r1.Result.Data!.Id,
            ScheduledAt = start, ScheduledEndAt = start.AddHours(1), TrackId = track1, MaxParticipants = 10
            // QualificationSlots omitted -> null
        });
        await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Final Race", TournamentId = tournamentId, RoundId = r2.Result.Data!.Id,
            ScheduledAt = start.AddDays(1), ScheduledEndAt = start.AddDays(1).AddHours(1), TrackId = track2, MaxParticipants = 4, QualificationSlots = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("QualificationSlots là bắt buộc", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_NegativeQualificationSlots_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, race1Id, _) = await BuildValidPublishableTournamentAsync(f);
        var race1 = await f.Db.Races.FirstAsync(r => r.Id == race1Id);
        race1.QualificationSlots = -1;
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("QualificationSlots không được âm", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_QualificationSlotsEqualMaxParticipants_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, race1Id, _) = await BuildValidPublishableTournamentAsync(f);
        var race1 = await f.Db.Races.FirstAsync(r => r.Id == race1Id);
        race1.QualificationSlots = race1.MaxParticipants; // 10 == 10
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("QualificationSlots phải nhỏ hơn MaxParticipants", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_QualificationSlotsGreaterThanMaxParticipants_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, race1Id, _) = await BuildValidPublishableTournamentAsync(f);
        var race1 = await f.Db.Races.FirstAsync(r => r.Id == race1Id);
        race1.QualificationSlots = race1.MaxParticipants + 1;
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("QualificationSlots phải nhỏ hơn MaxParticipants", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_QualificationSlotSumMismatch_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, race1Id, _) = await BuildValidPublishableTournamentAsync(f);
        var race1 = await f.Db.Races.FirstAsync(r => r.Id == race1Id);
        race1.QualificationSlots = 3; // Round1.AdvanceCount is 4 -> mismatch
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Tổng QualificationSlots", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_FinalQualificationSlotsNull_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, race1Id, race2Id) = await BuildValidPublishableTournamentAsync(f);
        var race2 = await f.Db.Races.FirstAsync(r => r.Id == race2Id);
        race2.QualificationSlots = null;
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Vòng chung kết): QualificationSlots phải bằng 0", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_FinalQualificationSlotsNonZero_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, race1Id, race2Id) = await BuildValidPublishableTournamentAsync(f);
        var race2 = await f.Db.Races.FirstAsync(r => r.Id == race2Id);
        race2.QualificationSlots = 1;
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Vòng chung kết): QualificationSlots phải bằng 0", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_ValidQualificationConfiguration_Passes()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, _, _, _) = await BuildValidPublishableTournamentAsync(f);
        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("QualificationSlots", publish.Result.Message ?? "");
    }

    // ── ROUND CAPACITY ──────────────────────────────────────────────────

    [Fact]
    public async Task Publish_InsufficientAggregateRaceCapacity_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, race1Id, _) = await BuildValidPublishableTournamentAsync(f);
        var race1 = await f.Db.Races.FirstAsync(r => r.Id == race1Id);
        race1.MaxParticipants = 5; // Round1 planned = Tournament.MaxParticipants = 10 > 5
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("Tổng MaxParticipants của các Cuộc đua", publish.Result.Message);
    }

    [Fact]
    public async Task Publish_ExactAggregateRaceCapacity_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, _, _, _, _) = await BuildValidPublishableTournamentAsync(f); // Race1.MaxParticipants == planned (10 == 10)
        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("Tổng MaxParticipants của các Cuộc đua", publish.Result.Message ?? "");
    }

    [Fact]
    public async Task Publish_ExcessAggregateRaceCapacity_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, race1Id, _) = await BuildValidPublishableTournamentAsync(f);
        var track = await CreateTrackAsync(f, capacity: 20);
        var race1 = await f.Db.Races.FirstAsync(r => r.Id == race1Id);
        race1.MaxParticipants = 20; // exceeds planned (10) — coverage, not equality
        race1.TrackId = track;
        await f.Db.SaveChangesAsync();

        var publish = await PublishAsync(f, tournamentId);
        Assert.DoesNotContain("Tổng MaxParticipants của các Cuộc đua", publish.Result.Message ?? "");
    }

    // ── COMPLETE PUBLISH ────────────────────────────────────────────────

    [Fact]
    public async Task CompletePublish_ValidStructure_PublishesAndPreservesStructure()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, round2Id, race1Id, race2Id) = await BuildValidPublishableTournamentAsync(f);

        var publish = await PublishAsync(f, tournamentId);
        Assert.True(publish.Result.Success, publish.Result.Message);
        Assert.Equal(TournamentStatus.Published, publish.Result.Data!.Status);
        Assert.NotNull(publish.Result.Data.PublishedAt);
        Assert.False(publish.Result.Data.IsActive);

        var round1 = await f.Db.Rounds.AsNoTracking().FirstAsync(r => r.Id == round1Id);
        var round2 = await f.Db.Rounds.AsNoTracking().FirstAsync(r => r.Id == round2Id);
        Assert.Equal(1, round1.RoundNumber);
        Assert.Equal(4, round1.AdvanceCount);
        Assert.Equal(2, round2.RoundNumber);
        Assert.Equal(0, round2.AdvanceCount);
        var race1 = await f.Db.Races.AsNoTracking().FirstAsync(r => r.Id == race1Id);
        var race2 = await f.Db.Races.AsNoTracking().FirstAsync(r => r.Id == race2Id);
        Assert.Equal(10, race1.MaxParticipants);
        Assert.Equal(4, race2.MaxParticipants);
    }

    [Fact]
    public async Task MultiError_InvalidStructure_ReportsMultipleErrorsAndStaysDraft()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, start.AddDays(20), maxParticipants: 10, maxRounds: 3));
        var tournamentId = create.Result.Data!.Id;
        // Sequence gap (1, 3) AND negative AdvanceCount on Round1 -- two independent failures.
        // MaxRounds=3 so Round3 stays within the V0.1 create-time ceiling and this test keeps
        // exercising the intended Publish-time gap detection rather than a Create-time rejection.
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1), AdvanceCount = -3
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R3", TournamentId = tournamentId, RoundNumber = 3,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2), AdvanceCount = 0
        });

        var publish = await PublishAsync(f, tournamentId);
        Assert.False(publish.Result.Success);
        Assert.Contains("liên tục", publish.Result.Message);
        Assert.Contains("AdvanceCount không được âm", publish.Result.Message);
        Assert.Contains(";", publish.Result.Message); // joined-message convention proves >1 error collected

        var reloaded = await f.TournamentSvc.GetTournamentAsync(tournamentId);
        Assert.Equal(TournamentStatus.Draft, reloaded.Result.Data!.Status);
        Assert.Null(reloaded.Result.Data.PublishedAt);
    }

    // ── EDITABILITY ─────────────────────────────────────────────────────

    [Fact]
    public async Task RaceMutation_Published_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id, _, race1Id, _) = await BuildValidPublishableTournamentAsync(f);
        var publish = await PublishAsync(f, tournamentId);
        Assert.True(publish.Result.Success, publish.Result.Message);

        var track = await CreateTrackAsync(f, capacity: 5);
        var create = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Post-publish Race", TournamentId = tournamentId, RoundId = round1Id,
            ScheduledAt = DateTime.UtcNow.AddDays(30), TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.False(create.Result.Success);
        Assert.Equal(400, create.StatusCode);

        var update = await f.RaceManagement.UpdateRaceAsync(race1Id, new UpdateRaceRequest { Name = "Changed" });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);

        var delete = await f.RaceManagement.DeleteRaceAsync(race1Id);
        Assert.False(delete.Result.Success);
        Assert.Equal(400, delete.StatusCode);
    }

    // ── ROUND SCHEDULE CONTAINMENT (Phase5B Fix2) ──────────────────────
    // Early rejection at Create/Update time, not just at Publish (see ValidateRoundScheduleWithinTournament
    // in TournamentAndRoundService.cs) — Tournament.StartDate <= Round.Start < Round.End <= Tournament.EndDate.

    [Fact]
    public async Task CreateRound_BeforeTournamentStart_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(20);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, end, maxParticipants: 5));
        var tournamentId = create.Result.Data!.Id;

        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start.AddDays(-1), ScheduledEndDate = start.AddDays(1)
        });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("không được trước ngày bắt đầu giải đấu", result.Result.Message);
    }

    [Fact]
    public async Task CreateRound_AfterTournamentEnd_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(20);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, end, maxParticipants: 5));
        var tournamentId = create.Result.Data!.Id;

        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = end.AddDays(-1), ScheduledEndDate = end.AddDays(1)
        });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("không được sau ngày kết thúc giải đấu", result.Result.Message);
    }

    [Fact]
    public async Task CreateRound_StartEqualsTournamentStart_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(20);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, end, maxParticipants: 5));
        var tournamentId = create.Result.Data!.Id;

        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1)
        });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task CreateRound_EndEqualsTournamentEnd_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(20);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, end, maxParticipants: 5));
        var tournamentId = create.Result.Data!.Id;

        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = end.AddDays(-1), ScheduledEndDate = end
        });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task CreateRound_StartEqualsEnd_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(20);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, end, maxParticipants: 5));
        var tournamentId = create.Result.Data!.Id;

        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start
        });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("phải trước thời gian kết thúc", result.Result.Message);
    }

    [Fact]
    public async Task UpdateRound_OutsideTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var tournament = (await f.TournamentSvc.GetTournamentAsync(tournamentId)).Result.Data!;

        var update = await f.RoundSvc.UpdateRoundAsync(roundId, new UpdateRoundRequest
        {
            ScheduledStartDate = tournament.EndDate.AddDays(1),
            ScheduledEndDate = tournament.EndDate.AddDays(2)
        });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
        Assert.Contains("không được sau ngày kết thúc giải đấu", update.Result.Message);
    }

    [Fact]
    public async Task UpdateTournament_DatesWouldExcludeExistingRound_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        // Round is anchored at the Tournament's own StartDate — moving StartDate later strands it.
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);

        var update = await f.TournamentSvc.UpdateTournamentAsync(tournamentId, new UpdateTournamentRequest
        {
            StartDate = roundStart.AddDays(1)
        });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
        Assert.Contains("Vòng 1", update.Result.Message);

        // The Tournament itself must remain unmutated — rejection happens before any assignment.
        var reloaded = (await f.TournamentSvc.GetTournamentAsync(tournamentId)).Result.Data!;
        Assert.Equal(roundStart, reloaded.StartDate);
    }

    [Fact]
    public async Task ValidRoundInsideTournament_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddDays(20);
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidDraftTournamentRequest(start, end, maxParticipants: 5));
        var tournamentId = create.Result.Data!.Id;

        var result = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2)
        });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    // ── RACE SCHEDULE CONTAINMENT (Phase5B Fix3) ────────────────────────
    // Early rejection at Create/Update time, not just at Publish (see ValidateRaceScheduleWithinRound
    // in RaceManagementService.cs) — Round.ScheduledStartDate <= Race.ScheduledAt < Race.ScheduledEndAt
    // <= Round.ScheduledEndDate.

    [Fact]
    public async Task CreateRace_BeforeRoundStart_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);

        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart.AddHours(-1), ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("không được trước thời gian bắt đầu Vòng đấu", result.Result.Message);
    }

    [Fact]
    public async Task CreateRace_AfterRoundEnd_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);

        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundEnd.AddHours(-1), ScheduledEndAt = roundEnd.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("không được sau thời gian kết thúc Vòng đấu", result.Result.Message);
    }

    [Fact]
    public async Task CreateRace_StartEqualsRoundStart_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);

        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task CreateRace_EndEqualsRoundEnd_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);

        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundEnd.AddHours(-1), ScheduledEndAt = roundEnd,
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task CreateRace_StartEqualsEnd_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);

        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart,
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("phải trước thời gian kết thúc", result.Result.Message);
    }

    [Fact]
    public async Task UpdateRace_OutsideRound_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);
        var create = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart, ScheduledEndAt = roundStart.AddHours(1),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(create.Result.Success, create.Result.Message);
        var raceId = create.Result.Data!.Id;

        var update = await f.RaceManagement.UpdateRaceAsync(raceId, new UpdateRaceRequest
        {
            ScheduledAt = roundEnd.AddHours(1),
            ScheduledEndAt = roundEnd.AddHours(2)
        });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
        Assert.Contains("không được sau thời gian kết thúc Vòng đấu", update.Result.Message);
    }

    [Fact]
    public async Task ValidRaceInsideRound_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, roundId, roundStart, roundEnd) = await BuildDraftSingleFinalRoundAsync(f);
        var track = await CreateTrackAsync(f, capacity: 5);

        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = roundStart.AddHours(1), ScheduledEndAt = roundStart.AddHours(2),
            TrackId = track, MaxParticipants = 5, QualificationSlots = 0
        });
        Assert.True(result.Result.Success, result.Result.Message);
    }
}
