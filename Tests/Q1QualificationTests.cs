using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// Task Q1: Admin-triggered generation of Round N+1 RaceEntries from Round N's Official rankings.
/// Wired against a real Sqlite in-memory DB and the actual production services, reusing
/// RaceLifecycleTests.LifecycleFixture (same convention as other structural test files).
/// Round/Race rows are always pre-created here via the real CreateRoundAsync/CreateRaceAsync —
/// Q1 must never create them itself.
/// </summary>
public class Q1QualificationTests
{
    private static SubmitRaceResultRequest Ranking(IEnumerable<Guid> orderedHorseIds) => new()
    {
        Rankings = orderedHorseIds.Select((h, i) => new SubmitRankingEntry { HorseId = h, Position = i + 1, Status = "Completed" }).ToList()
    };

    /// <summary>Overwrites the stored Official ranking directly — simulates corruption/legacy
    /// drift discovered after approval, independent of whatever RaceEntry.FinishPosition holds.</summary>
    private static async Task SetRankingsJsonAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId, string? rankingsJson)
    {
        var result = await f.Db.RaceResults.FirstAsync(r => r.RaceId == raceId);
        result.RankingsJson = rankingsJson;
        await f.Db.SaveChangesAsync();
    }

    private static async Task<Guid> CreateTrackAsync(RaceLifecycleTests.LifecycleFixture f, int capacity)
    {
        var track = new Track { Id = Guid.NewGuid(), Name = $"Track-{Guid.NewGuid():N}", Capacity = capacity, CreatedAt = DateTime.UtcNow };
        f.Db.Add(track);
        await f.Db.SaveChangesAsync();
        return track.Id;
    }

    private sealed record TwoRoundSetup(Guid TournamentId, Guid Round1Id, Guid Round2Id, Guid RefereeId, DateTime Start);

    /// <summary>Tournament(MaxRounds=2, Draft) + Round1(non-final) + Round2(Final) + one Referee. No Races yet.</summary>
    private static async Task<TwoRoundSetup> BuildTwoRoundTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, int round1AdvanceCount, int tournamentMaxParticipants = 20)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = $"Q1-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(20),
            RegistrationDeadline = start.AddDays(-1), MinParticipants = 3, MaxParticipants = tournamentMaxParticipants, MaxRounds = 2
        });
        Assert.True(create.Result.Success, create.Result.Message);
        var tournamentId = create.Result.Data!.Id;

        var r1 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(5), AdvanceCount = round1AdvanceCount
        });
        Assert.True(r1.Result.Success, r1.Result.Message);
        var r2 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Final", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(5), ScheduledEndDate = start.AddDays(10), AdvanceCount = 0
        });
        Assert.True(r2.Result.Success, r2.Result.Message);

        var refereeUserId = Guid.NewGuid();
        f.Db.Add(new User { Id = refereeUserId, Email = $"ref-{Guid.NewGuid():N}@test.com", PasswordHash = "x", FullName = "Referee", Role = UserRole.Referee });
        var referee = new Referee { Id = Guid.NewGuid(), UserId = refereeUserId, LicenseNumber = $"LIC-{Guid.NewGuid():N}", IsActive = true };
        f.Db.Add(referee);
        await f.Db.SaveChangesAsync();

        return new TwoRoundSetup(tournamentId, r1.Result.Data!.Id, r2.Result.Data!.Id, referee.Id, start);
    }

    /// <summary>Single-round Tournament (Round1 IS Final). No Races yet.</summary>
    private static async Task<(Guid tournamentId, Guid round1Id)> BuildSingleRoundTournamentAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = $"Q1-single-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(10),
            RegistrationDeadline = start.AddDays(-1), MinParticipants = 3, MaxParticipants = 10, MaxRounds = 1
        });
        Assert.True(create.Result.Success, create.Result.Message);
        var tournamentId = create.Result.Data!.Id;

        var r1 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Final", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(5), AdvanceCount = 0
        });
        Assert.True(r1.Result.Success, r1.Result.Message);
        return (tournamentId, r1.Result.Data!.Id);
    }

    private static async Task<Guid> CreateRaceAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid roundId, DateTime scheduledAt,
        int maxParticipants, int? qualificationSlots, string name)
    {
        var track = await CreateTrackAsync(f, capacity: maxParticipants);
        var result = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = name, TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = scheduledAt, ScheduledEndAt = scheduledAt.AddHours(2),
            TrackId = track, MaxParticipants = maxParticipants, QualificationSlots = qualificationSlots
        });
        Assert.True(result.Result.Success, result.Result.Message);
        return result.Result.Data!.Id;
    }

    private static async Task AssignRefereeAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId, Guid refereeId)
    {
        f.Db.Add(new RefereeAssignment
        {
            Id = Guid.NewGuid(), RaceId = raceId, RefereeId = refereeId, Role = "Chief Referee",
            Status = RefereeAssignmentStatus.Confirmed, AssignedAt = DateTime.UtcNow, ConfirmedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();
    }

    /// <summary>One Horse (Approved) + one Jockey (Approved), entered into raceId with JockeyId set
    /// (this RaceEntry.JockeyId IS the "official Tournament pairing" Q1 looks up), plus a Passed
    /// health check so the Race can Start. Returns (horseId, jockeyId).</summary>
    private static async Task<(Guid horseId, Guid jockeyId)> AddQualifiableEntryAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid raceId, Guid refereeId, string tag)
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

        // GATE-V1: StartRace (used by FinishRaceOfficialAsync below) now requires every
        // participating entry to have a unique, in-range gate — assign the next free one
        // dynamically so any number of calls for the same race stay collision-free.
        var nextGate = await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceId) + 1;
        f.Db.Add(new RaceEntry
        {
            Id = Guid.NewGuid(), RaceId = raceId, HorseId = horse.Id, JockeyId = jockey.Id,
            Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = true, GateNumber = nextGate
        });
        f.Db.Add(new HorseHealthCheck
        {
            Id = Guid.NewGuid(), HorseId = horse.Id, RaceId = raceId, RefereeId = refereeId,
            Status = HealthCheckStatus.Passed, ApprovedToRace = true, CheckedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();
        return (horse.Id, jockey.Id);
    }

    /// <summary>R1a: StartRace now requires the parent Tournament to be Ongoing. CreateRaceAsync
    /// requires the Tournament to still be Draft, so races/rounds are always built first (via
    /// BuildTwoRoundTournamentAsync/CreateRaceAsync above) and only transitioned to Ongoing here,
    /// right before a Race is actually driven through StartRace.</summary>
    private static async Task SetTournamentOngoingAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId)
    {
        var tournamentId = (await f.Db.Races.SingleAsync(r => r.Id == raceId)).TournamentId;
        var tournament = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentId);
        if (tournament.Status != TournamentStatus.Ongoing)
        {
            tournament.Status = TournamentStatus.Ongoing;
            tournament.IsActive = true;
            await f.Db.SaveChangesAsync();
        }
    }

    /// <summary>Drives a Race with its already-seeded entries from Scheduled through Official, with the given full ranking.</summary>
    private static async Task FinishRaceOfficialAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId, Guid refereeId, List<Guid> orderedHorseIds)
    {
        await SetTournamentOngoingAsync(f, raceId);
        var open = await f.RaceManagement.OpenRegistrationAsync(raceId);
        Assert.True(open.Result.Success, open.Result.Message);
        var close = await f.RaceManagement.CloseRegistrationAsync(raceId);
        Assert.True(close.Result.Success, close.Result.Message);
        var start = await f.RaceManagement.StartRaceAsync(raceId);
        Assert.True(start.Result.Success, start.Result.Message);
        var end = await f.RaceManagement.EndRaceAsync(raceId);
        Assert.True(end.Result.Success, end.Result.Message);

        var submit = await f.LiveResult.UpdateRaceResultAsync(raceId, Ranking(orderedHorseIds));
        Assert.True(submit.Result.Success, submit.Result.Message);

        f.Db.Add(new RaceReport
        {
            Id = Guid.NewGuid(), RaceId = raceId, RefereeId = refereeId,
            CompletedAt = DateTime.UtcNow, Details = "Clean race, no incidents.", CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        var approve = await f.Admin.ApproveRaceResultAsync(raceId);
        Assert.True(approve.Result.Success, approve.Result.Message);
    }

    // ── A: top-N qualify, rest excluded ──────────────────────────────────

    [Fact]
    public async Task GenerateNextRound_TopThreeOfFour_QualifyExcludingFourth()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 3);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 3, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);

        var (h1, j1) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, j2) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        var (h3, j3) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h3");
        var (h4, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h4");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2, h3, h4 });

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(generate.Result.Success, generate.Result.Message);
        Assert.Equal(3, generate.Result.Data!.GeneratedEntries);
        Assert.Equal(1, generate.Result.Data.SourceRoundNumber);
        Assert.Equal(2, generate.Result.Data.TargetRoundNumber);

        var finalEntries = await f.Db.RaceEntries.AsNoTracking().Where(e => e.RaceId == raceFinal).ToListAsync();
        var finalHorseIds = finalEntries.Select(e => e.HorseId).ToHashSet();
        Assert.Equal(3, finalEntries.Count);
        Assert.Contains(h1, finalHorseIds);
        Assert.Contains(h2, finalHorseIds);
        Assert.Contains(h3, finalHorseIds);
        Assert.DoesNotContain(h4, finalHorseIds); // position 4 excluded

        // ── I: carried-forward entry shape ──
        var e1 = finalEntries.Single(e => e.HorseId == h1);
        Assert.Equal(j1, e1.JockeyId);
        Assert.True(e1.OwnerConfirmed);
        Assert.True(e1.JockeyConfirmed);
        Assert.Equal(RegistrationStatus.Approved, e1.Status);
        Assert.Null(e1.GateNumber);
        Assert.Null(e1.FinishPosition);
    }

    // ── B: multiple source Races, deterministic round-robin ─────────────

    [Fact]
    public async Task GenerateNextRound_MultipleSourceRaces_DeterministicRoundRobin()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 4);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceB = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start.AddHours(3), maxParticipants: 4, qualificationSlots: 2, name: "Race B");
        var raceC = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Race C");
        var raceD = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5).AddHours(3), maxParticipants: 10, qualificationSlots: 0, name: "Race D");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        await AssignRefereeAsync(f, raceB, setup.RefereeId);

        var (a1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "a1");
        var (a2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "a2");
        var (b1, _) = await AddQualifiableEntryAsync(f, raceB, setup.RefereeId, "b1");
        var (b2, _) = await AddQualifiableEntryAsync(f, raceB, setup.RefereeId, "b2");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { a1, a2 });
        await FinishRaceOfficialAsync(f, raceB, setup.RefereeId, new List<Guid> { b1, b2 });

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(generate.Result.Success, generate.Result.Message);

        var cHorses = (await f.Db.RaceEntries.AsNoTracking().Where(e => e.RaceId == raceC).Select(e => e.HorseId).ToListAsync()).ToHashSet();
        var dHorses = (await f.Db.RaceEntries.AsNoTracking().Where(e => e.RaceId == raceD).Select(e => e.HorseId).ToListAsync()).ToHashSet();

        // Deterministic: source order (Race A by ScheduledAt, then Race B), flattened as A1,A2,B1,B2,
        // round-robin against target order (Race C, then Race D) -> C={A1,B1}, D={A2,B2}.
        Assert.Equal(new HashSet<Guid> { a1, b1 }, cHorses);
        Assert.Equal(new HashSet<Guid> { a2, b2 }, dHorses);
    }

    // ── C: not Finished ───────────────────────────────────────────────────

    [Fact]
    public async Task GenerateNextRound_SourceRaceNotFinished_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 1);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 1, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        // Race A stays Scheduled — never driven through the lifecycle.

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    // ── D: Finished but Provisional result ───────────────────────────────

    [Fact]
    public async Task GenerateNextRound_ResultProvisional_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 1);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 1, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");

        await SetTournamentOngoingAsync(f, raceA);
        await f.RaceManagement.OpenRegistrationAsync(raceA);
        await f.RaceManagement.CloseRegistrationAsync(raceA);
        await f.RaceManagement.StartRaceAsync(raceA);
        await f.RaceManagement.EndRaceAsync(raceA);
        var submit = await f.LiveResult.UpdateRaceResultAsync(raceA, Ranking(new[] { h1, h2 }));
        Assert.True(submit.Result.Success, submit.Result.Message);
        // Never approved -> stays Provisional.

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    // ── E: Cancelled ──────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateNextRound_SourceRaceCancelled_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 1);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 1, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        var cancel = await f.RaceManagement.CancelRaceAsync(raceA);
        Assert.True(cancel.Result.Success, cancel.Result.Message);

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    // ── F (rewritten): RankingsJson is the qualification authority, not RaceEntry.FinishPosition —
    //    corrupting FinishPosition alone must NOT affect generation while RankingsJson stays valid ──

    [Fact]
    public async Task GenerateNextRound_FinishPositionCorrupted_RankingsJsonRemainsAuthority_GenerationStillSucceeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2 });

        // Corrupt ONLY the denormalized display/stat field — RaceResult.RankingsJson (the
        // canonical Official ranking) is untouched and still fully valid.
        var entry2 = await f.Db.RaceEntries.FirstAsync(e => e.HorseId == h2);
        entry2.FinishPosition = null;
        await f.Db.SaveChangesAsync();

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(generate.Result.Success, generate.Result.Message);
        var finalHorseIds = (await f.Db.RaceEntries.AsNoTracking().Where(e => e.RaceId == raceFinal).Select(e => e.HorseId).ToListAsync()).ToHashSet();
        Assert.Equal(new HashSet<Guid> { h1, h2 }, finalHorseIds); // both still correctly qualify via RankingsJson
    }

    // ── §3: corrupt Official RankingsJson must fail generation with ZERO target entries ──

    private static async Task<(TwoRoundSetup setup, Guid raceA, Guid raceFinal, Guid h1, Guid h2, Guid h3, Guid h4)>
        BuildFourEntryOfficialRaceAsync(RaceLifecycleTests.LifecycleFixture f, int advanceCount)
    {
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: advanceCount);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: advanceCount, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        var (h3, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h3");
        var (h4, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h4");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2, h3, h4 });
        return (setup, raceA, raceFinal, h1, h2, h3, h4);
    }

    [Fact]
    public async Task GenerateNextRound_RankingsJsonNull_RejectedEvenWithCompleteFinishPositions()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (setup, raceA, raceFinal, h1, h2, _, _) = await BuildFourEntryOfficialRaceAsync(f, advanceCount: 2);

        // FinishPosition stays fully complete/valid for all 4 entries — only RankingsJson is corrupted.
        await SetRankingsJsonAsync(f, raceA, null);
        Assert.NotNull((await f.Db.RaceEntries.AsNoTracking().FirstAsync(e => e.HorseId == h1)).FinishPosition);
        Assert.NotNull((await f.Db.RaceEntries.AsNoTracking().FirstAsync(e => e.HorseId == h2)).FinishPosition);

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    [Fact]
    public async Task GenerateNextRound_RankingsJsonMalformed_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (setup, raceA, raceFinal, _, _, _, _) = await BuildFourEntryOfficialRaceAsync(f, advanceCount: 2);

        await SetRankingsJsonAsync(f, raceA, "{ not valid json ]");

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    [Fact]
    public async Task GenerateNextRound_RankingsJsonIncomplete_ThreeRowsForFourEntries_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (setup, raceA, raceFinal, h1, h2, h3, _) = await BuildFourEntryOfficialRaceAsync(f, advanceCount: 2);

        var incomplete = JsonSerializer.Serialize(new List<RaceResultRankingItemRequest>
        {
            new() { HorseId = h1, Position = 1 },
            new() { HorseId = h2, Position = 2 },
            new() { HorseId = h3, Position = 3 },
        });
        await SetRankingsJsonAsync(f, raceA, incomplete);

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    [Fact]
    public async Task GenerateNextRound_RankingsJsonDuplicatePosition_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (setup, raceA, raceFinal, h1, h2, h3, h4) = await BuildFourEntryOfficialRaceAsync(f, advanceCount: 2);

        var duplicatePosition = JsonSerializer.Serialize(new List<RaceResultRankingItemRequest>
        {
            new() { HorseId = h1, Position = 1 },
            new() { HorseId = h2, Position = 1 }, // duplicate position — e.g. 1,1,2,3
            new() { HorseId = h3, Position = 2 },
            new() { HorseId = h4, Position = 3 },
        });
        await SetRankingsJsonAsync(f, raceA, duplicatePosition);

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    [Fact]
    public async Task GenerateNextRound_RankingsJsonGappedPosition_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (setup, raceA, raceFinal, h1, h2, h3, h4) = await BuildFourEntryOfficialRaceAsync(f, advanceCount: 2);

        var gapped = JsonSerializer.Serialize(new List<RaceResultRankingItemRequest>
        {
            new() { HorseId = h1, Position = 1 },
            new() { HorseId = h2, Position = 2 },
            new() { HorseId = h3, Position = 4 }, // 1,2,4,5 — missing 3, out of range for 4 items
            new() { HorseId = h4, Position = 5 },
        });
        await SetRankingsJsonAsync(f, raceA, gapped);

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    [Fact]
    public async Task GenerateNextRound_RankingsJsonDuplicateHorseId_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (setup, raceA, raceFinal, h1, _, h3, h4) = await BuildFourEntryOfficialRaceAsync(f, advanceCount: 2);

        var duplicateHorse = JsonSerializer.Serialize(new List<RaceResultRankingItemRequest>
        {
            new() { HorseId = h1, Position = 1 },
            new() { HorseId = h1, Position = 2 }, // same Horse listed twice
            new() { HorseId = h3, Position = 3 },
            new() { HorseId = h4, Position = 4 },
        });
        await SetRankingsJsonAsync(f, raceA, duplicateHorse);

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    [Fact]
    public async Task GenerateNextRound_RankingsJsonWinnerMismatch_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (setup, raceA, raceFinal, h1, h2, h3, h4) = await BuildFourEntryOfficialRaceAsync(f, advanceCount: 2);

        // Otherwise-fully-valid 1..4 ranking, but Position 1 is h2 while RaceResult.WinningHorseId
        // (set when the race was originally finished with h1 in position 1) still says h1.
        var winnerMismatch = JsonSerializer.Serialize(new List<RaceResultRankingItemRequest>
        {
            new() { HorseId = h2, Position = 1 },
            new() { HorseId = h1, Position = 2 },
            new() { HorseId = h3, Position = 3 },
            new() { HorseId = h4, Position = 4 },
        });
        await SetRankingsJsonAsync(f, raceA, winnerMismatch);

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    // ── G: selected count != AdvanceCount ─────────────────────────────────

    [Fact]
    public async Task GenerateNextRound_QualifierCountMismatchAdvanceCount_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        // Round1.AdvanceCount=3 but Race A only awards 2 slots — CreateRaceAsync allows this
        // (only rejects a SUM exceeding AdvanceCount, not falling short of it), so Q1's own
        // defensive count check must catch the mismatch.
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 3);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        var (h3, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h3");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2, h3 });

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    // ── H: qualified Horse missing official Jockey ────────────────────────

    [Fact]
    public async Task GenerateNextRound_QualifiedHorseMissingOfficialJockey_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2 });

        // Corrupt/legacy data: the qualified Horse's RaceEntry lost its JockeyId after the race
        // (must never happen via a normal flow — StartRaceAsync requires JockeyId to be set —
        // but Q1 must defend against it rather than create a RaceEntry with JockeyId=null).
        var entry1 = await f.Db.RaceEntries.FirstAsync(e => e.HorseId == h1);
        entry1.JockeyId = null;
        await f.Db.SaveChangesAsync();

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    // ── J: target capacity insufficient ───────────────────────────────────

    [Fact]
    public async Task GenerateNextRound_TargetRaceCapacityInsufficient_RejectedAtomically()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        // Final Race can only hold 1, but 2 qualifiers must land there.
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 1, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2 });

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    // ── K: idempotency — retry after success ──────────────────────────────

    [Fact]
    public async Task GenerateNextRound_RetryAfterSuccess_Returns409_NoDuplicateEntries()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 1);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 1, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2 });

        var first = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(first.Result.Success, first.Result.Message);
        Assert.Equal(1, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));

        var second = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(second.Result.Success);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(1, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal)); // still exactly one, no duplicate
    }

    // ── L: partial pre-existing next-round entries ────────────────────────

    [Fact]
    public async Task GenerateNextRound_PartialPreExistingNextRoundEntries_RejectedNotMerged()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2 });

        // A stray, unrelated RaceEntry already sitting in the target Race (e.g. manual admin
        // meddling, or a previous partial attempt) — must block generation entirely, not be
        // merged with / topped-up by the new generation.
        var strayHorse = new Horse { Id = Guid.NewGuid(), Name = "Stray", OwnerId = Guid.NewGuid(), ApprovalStatus = ApprovalStatus.Approved };
        f.Db.Add(strayHorse);
        f.Db.Add(new RaceEntry { Id = Guid.NewGuid(), RaceId = raceFinal, HorseId = strayHorse.Id, Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = true });
        await f.Db.SaveChangesAsync();

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(409, generate.StatusCode);
        Assert.Equal(1, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal)); // only the stray entry — nothing merged in
    }

    // ── M: Final Round cannot generate ────────────────────────────────────

    [Fact]
    public async Task GenerateNextRound_FromFinalRound_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 1);
        await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 1, name: "Race A");
        await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round2Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(400, generate.StatusCode);
    }

    // ── N: single-round tournament — no next round to generate ───────────

    [Fact]
    public async Task GenerateNextRound_SingleRoundTournament_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, round1Id) = await BuildSingleRoundTournamentAsync(f);
        await CreateRaceAsync(f, tournamentId, round1Id, DateTime.UtcNow.AddDays(10), maxParticipants: 4, qualificationSlots: 0, name: "Final");

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(400, generate.StatusCode);
    }

    // ── O: manual Admin assignment to Round 2+ still rejected ────────────

    [Fact]
    public async Task ManualAssignHorseToRace_RoundTwo_StillRejected_EvenWhenQualificationReady()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 1);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 1, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2 });

        // Even with the source Round fully ready for Q1, the pre-existing manual assignment path
        // must still reject any Round with RoundNumber > 1 — Q1 remains the only mechanism.
        var manualAssign = await f.RaceManagement.AssignHorseToRaceAsync(raceFinal, new AssignHorseToRaceRequest { HorseId = h1 });
        Assert.False(manualAssign.Result.Success);

        var manualBulk = await f.RaceManagement.BulkAssignHorsesToRaceAsync(raceFinal, new BulkAssignHorsesToRaceRequest { HorseIds = new[] { h1 } });
        Assert.False(manualBulk.Result.Success);

        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }
}
