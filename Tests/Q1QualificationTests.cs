using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
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

    // Renamed/updated from the old "Q1's own defensive count check must catch the mismatch — always
    // Rejected" framing: a shortfall no longer always hard-fails. With eligible(2) >= 2 and
    // confirmShortfall left at its default (false), this is now specifically the "needs Admin
    // confirmation before continuing with fewer than planned" branch — still a 409, but the response
    // is no longer a bare rejection: it carries RequiresShortfallConfirmation/EligibleCount/
    // RequiredAdvanceCount so the caller can decide whether to retry with confirmShortfall=true. See
    // GenerateNextRound_Shortfall_ConfirmTrue_AdvancesWithEligibleCount below for that confirmed path.
    [Fact]
    public async Task GenerateNextRound_QualifierCountMismatchAdvanceCount_WithoutConfirm_RequiresShortfallConfirmation()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        // Round1.AdvanceCount=3 but Race A only awards 2 slots — CreateRaceAsync allows this
        // (only rejects a SUM exceeding AdvanceCount, not falling short of it), so eligible(2) ends
        // up short of expectedAdvance(3).
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
        Assert.Equal(409, generate.StatusCode);
        Assert.True(generate.Result.Data!.RequiresShortfallConfirmation);
        Assert.False(generate.Result.Data.RequiresTournamentLevelAction);
        Assert.Equal(2, generate.Result.Data.EligibleCount);
        Assert.Equal(3, generate.Result.Data.RequiredAdvanceCount);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    // eligible == AdvanceCount (the fully-matched, "auto-advance" branch) is already covered by
    // GenerateNextRound_TopThreeOfFour_QualifyExcludingFourth and
    // GenerateNextRound_MultipleSourceRaces_DeterministicRoundRobin above — no separate test needed.

    [Fact]
    public async Task GenerateNextRound_EligibleExceedsAdvanceCount_DefensivelyRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        // Race A's slots are validated against Round.AdvanceCount at Publish/CreateRace time, so this
        // scenario (eligible > expectedAdvance) should never arise from the normal flow — simulate it
        // via a direct data corruption (QualificationSlots bumped after the race is already Official)
        // to prove the defensive guard fires instead of silently truncating qualifiers.
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        var (h3, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h3");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2, h3 });

        var raceAEntity = await f.Db.Races.SingleAsync(r => r.Id == raceA);
        raceAEntity.QualificationSlots = 3; // corrupted post-Official — now exceeds Round.AdvanceCount
        await f.Db.SaveChangesAsync();

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(409, generate.StatusCode);
        Assert.Contains("VƯỢT QUÁ", generate.Result.Message);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));
    }

    [Fact]
    public async Task GenerateNextRound_Shortfall_ConfirmTrue_AdvancesWithExactlyEligibleCount_NoPadding()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 3);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        var (h3, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h3");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2, h3 });

        // Without confirmation: still blocked (mirrors the WithoutConfirm test above).
        var withoutConfirm = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id, confirmShortfall: false);
        Assert.False(withoutConfirm.Result.Success);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));

        // With confirmation: proceeds with exactly the 2 eligible horses (h1, h2 — slots=2 caps Race
        // A's contribution regardless of confirmShortfall) — never padded up toward AdvanceCount=3.
        var confirmed = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id, confirmShortfall: true);
        Assert.True(confirmed.Result.Success, confirmed.Result.Message);
        Assert.Equal(2, confirmed.Result.Data!.GeneratedEntries);

        var finalHorseIds = (await f.Db.RaceEntries.AsNoTracking().Where(e => e.RaceId == raceFinal).Select(e => e.HorseId).ToListAsync()).ToHashSet();
        Assert.Equal(new HashSet<Guid> { h1, h2 }, finalHorseIds);
        Assert.DoesNotContain(h3, finalHorseIds);
    }

    // eligible == 0: no horse actually finished — nothing to walkover with either. Renamed/rewritten
    // from GenerateNextRound_EligibleEqualsZero_AlwaysRejected_RegardlessOfConfirmShortfall: eligible==0
    // is no longer a hard 409 block — it now auto-voids the Tournament (Cancelled) the same way
    // eligible==1 auto-finishes it (walkover), since ChangeStatusAsync's Cancelled branch already has
    // its own cascade-cancel + Prediction-refund built in (see GenerateNextRoundEntriesAsync).
    [Fact]
    public async Task GenerateNextRound_EligibleEqualsZero_AutoVoidsTournament_RegardlessOfConfirmShortfall()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        // Auto-void mutates state (Tournament -> Cancelled) on the very first call, so proving
        // confirmShortfall=true leads to the identical outcome needs a SECOND, independent Tournament
        // — calling GenerateNextRoundEntriesAsync twice against the same (now-Cancelled) Tournament
        // would hit a different guard the second time, not re-exercise eligible==0.
        async Task<(TwoRoundSetup Setup, Guid RaceA, Guid RaceFinal, Guid H1)> BuildEligibleZeroScenarioAsync(string tag)
        {
            var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
            var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: $"Race A {tag}");
            var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: $"Final {tag}");
            await AssignRefereeAsync(f, raceA, setup.RefereeId);
            var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, $"h1-{tag}");
            var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, $"h2-{tag}");

            // Both horses DNF/DSQ — nobody actually finishes, so eligible=0 even though the Race
            // awarded 2 slots and 2 horses ran.
            await SetTournamentOngoingAsync(f, raceA);
            await f.RaceManagement.OpenRegistrationAsync(raceA);
            await f.RaceManagement.CloseRegistrationAsync(raceA);
            await f.RaceManagement.StartRaceAsync(raceA);
            await f.RaceManagement.EndRaceAsync(raceA);
            var submit = await f.LiveResult.UpdateRaceResultAsync(raceA, new SubmitRaceResultRequest
            {
                Rankings = new List<SubmitRankingEntry>
                {
                    new() { HorseId = h1, Position = 99, Status = "DNF" },
                    new() { HorseId = h2, Position = 99, Status = "DSQ" },
                }
            });
            Assert.True(submit.Result.Success, submit.Result.Message);
            f.Db.Add(new RaceReport { Id = Guid.NewGuid(), RaceId = raceA, RefereeId = setup.RefereeId, CompletedAt = DateTime.UtcNow, Details = "All DNF/DSQ.", CreatedAt = DateTime.UtcNow });
            await f.Db.SaveChangesAsync();
            var approve = await f.Admin.ApproveRaceResultAsync(raceA);
            Assert.True(approve.Result.Success, approve.Result.Message);

            return (setup, raceA, raceFinal, h1);
        }

        // ── confirmShortfall=false: full assertion suite ──
        var scenarioA = await BuildEligibleZeroScenarioAsync("A");

        // A Pending Prediction on the not-yet-run Final Race — must be refunded by the Cancelled
        // cascade inside ChangeStatusAsync, same convention asserted by
        // RaceLifecycleTests.Cancellation_RefundsPendingPredictions.
        var (spectatorId, walletBefore) = await f.CreateSpectatorWithWalletAsync(0m);
        await f.AddPendingPredictionAsync(scenarioA.RaceFinal, spectatorId, scenarioA.H1, betAmount: 50m, odds: 2m);

        var prizesBefore = await f.Db.Prizes.AsNoTracking().ToListAsync();

        var withoutConfirm = await f.RaceManagement.GenerateNextRoundEntriesAsync(scenarioA.Setup.Round1Id, confirmShortfall: false);
        Assert.True(withoutConfirm.Result.Success, withoutConfirm.Result.Message);
        Assert.Equal(200, withoutConfirm.StatusCode);
        Assert.True(withoutConfirm.Result.Data!.IsVoided);

        // Tournament auto-voided (Cancelled) — not Finished. CancellationReason must be populated
        // (ChangeStatusAsync's Cancelled branch requires it); FinishedAt stays null (only walkover sets it).
        var tournamentAAfter = await f.Db.Tournaments.AsNoTracking().SingleAsync(t => t.Id == scenarioA.Setup.TournamentId);
        Assert.Equal(TournamentStatus.Cancelled, tournamentAAfter.Status);
        Assert.NotNull(tournamentAAfter.CancelledAt);
        Assert.False(string.IsNullOrWhiteSpace(tournamentAAfter.CancellationReason));
        Assert.Null(tournamentAAfter.FinishedAt);

        // Round 2's not-yet-run Race is cancelled — never populated with RaceEntries.
        var raceFinalAAfter = await f.Db.Races.AsNoTracking().SingleAsync(r => r.Id == scenarioA.RaceFinal);
        Assert.Equal(RaceStatus.Cancelled, raceFinalAAfter.Status);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == scenarioA.RaceFinal));

        // Round 1's already-Finished Race is left untouched by the void cascade.
        var raceAAAfter = await f.Db.Races.AsNoTracking().SingleAsync(r => r.Id == scenarioA.RaceA);
        Assert.Equal(RaceStatus.Finished, raceAAAfter.Status);

        // Pending Prediction on the cancelled Final Race was refunded.
        var predictionAfter = (await f.GetPredictionsFreshAsync(scenarioA.RaceFinal)).Single();
        Assert.Equal(PredictionStatus.Lost, predictionAfter.Status); // refund marker, per existing convention
        Assert.Equal(walletBefore + 50m, await f.GetWalletBalanceAsync(spectatorId));

        // No Prize row created/modified by the void.
        var prizesAfter = await f.Db.Prizes.AsNoTracking().ToListAsync();
        Assert.Empty(prizesBefore);
        Assert.Empty(prizesAfter);
        Assert.Equal(prizesBefore.Count, prizesAfter.Count);

        // ── confirmShortfall=true, independent Tournament: proves the flag is irrelevant for this
        // branch (both lead to the identical auto-void outcome), same as it does for walkover. ──
        var scenarioB = await BuildEligibleZeroScenarioAsync("B");
        var withConfirm = await f.RaceManagement.GenerateNextRoundEntriesAsync(scenarioB.Setup.Round1Id, confirmShortfall: true);
        Assert.True(withConfirm.Result.Success, withConfirm.Result.Message);
        Assert.Equal(200, withConfirm.StatusCode);
        Assert.True(withConfirm.Result.Data!.IsVoided);

        var tournamentBAfter = await f.Db.Tournaments.AsNoTracking().SingleAsync(t => t.Id == scenarioB.Setup.TournamentId);
        Assert.Equal(TournamentStatus.Cancelled, tournamentBAfter.Status);
    }

    [Fact]
    public async Task GenerateNextRound_EligibleEqualsOne_Walkover_FinishesTournamentWithoutTouchingPrizes()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");

        // h1 finishes Completed, h2 is a DNF — only 1 horse is actually eligible even though the
        // Race awarded 2 slots and 2 horses ran. This is the walkover scenario: the tournament is
        // effectively decided (exactly 1 contender remains), so Q1 finishes the tournament outright
        // instead of asking for shortfall confirmation like a 2+ eligible shortfall would.
        await SetTournamentOngoingAsync(f, raceA);
        await f.RaceManagement.OpenRegistrationAsync(raceA);
        await f.RaceManagement.CloseRegistrationAsync(raceA);
        await f.RaceManagement.StartRaceAsync(raceA);
        await f.RaceManagement.EndRaceAsync(raceA);
        var submit = await f.LiveResult.UpdateRaceResultAsync(raceA, new SubmitRaceResultRequest
        {
            Rankings = new List<SubmitRankingEntry>
            {
                new() { HorseId = h1, Position = 1, Status = "Completed" },
                new() { HorseId = h2, Position = 99, Status = "DNF" },
            }
        });
        Assert.True(submit.Result.Success, submit.Result.Message);
        f.Db.Add(new RaceReport { Id = Guid.NewGuid(), RaceId = raceA, RefereeId = setup.RefereeId, CompletedAt = DateTime.UtcNow, Details = "One DNF.", CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        var approve = await f.Admin.ApproveRaceResultAsync(raceA);
        Assert.True(approve.Result.Success, approve.Result.Message);

        var prizesBefore = await f.Db.Prizes.AsNoTracking().ToListAsync();

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(generate.Result.Success, generate.Result.Message);
        Assert.Equal(200, generate.StatusCode);
        Assert.True(generate.Result.Data!.IsWalkover);
        Assert.Equal(h1, generate.Result.Data.WalkoverWinnerHorseId);
        Assert.Equal(0, generate.Result.Data.GeneratedEntries);

        // Tournament finishes — not Cancelled. Must go through the real ChangeStatusAsync transition
        // (FinishedAt stamped, CancelledAt/CancellationReason stay null — those only ever get set by
        // the Cancelled branch, never by Finished).
        var tournamentAfter = await f.Db.Tournaments.AsNoTracking().SingleAsync(t => t.Id == setup.TournamentId);
        Assert.Equal(TournamentStatus.Finished, tournamentAfter.Status);
        Assert.NotNull(tournamentAfter.FinishedAt);
        Assert.Null(tournamentAfter.CancelledAt);
        Assert.Null(tournamentAfter.CancellationReason);

        // Round 2's not-yet-run Race is skipped (Cancelled) — never populated with RaceEntries.
        var raceFinalAfter = await f.Db.Races.AsNoTracking().SingleAsync(r => r.Id == raceFinal);
        Assert.Equal(RaceStatus.Cancelled, raceFinalAfter.Status);
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));

        // Round 1's already-Finished Race is left untouched by the walkover cascade.
        var raceAAfter = await f.Db.Races.AsNoTracking().SingleAsync(r => r.Id == raceA);
        Assert.Equal(RaceStatus.Finished, raceAAfter.Status);

        // No Prize row created/modified by the walkover.
        var prizesAfter = await f.Db.Prizes.AsNoTracking().ToListAsync();
        Assert.Empty(prizesBefore);
        Assert.Empty(prizesAfter);
        Assert.Equal(prizesBefore.Count, prizesAfter.Count);
    }

    [Fact]
    public async Task GenerateNextRound_RoundRobinSpreadsEnoughTotalTooThin_TargetRaceWouldHaveOnlyOneHorse_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        // 2 total qualifiers (enough to satisfy AdvanceCount exactly) but 3 target Races in the next
        // Round — deterministic round-robin gives Race C=1, Race D=1, Race E=0, none of which can
        // hold a meaningful race even though the round-level total looked fine.
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceC = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Race C");
        var raceD = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5).AddHours(3), maxParticipants: 10, qualificationSlots: 0, name: "Race D");
        var raceE = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5).AddHours(6), maxParticipants: 10, qualificationSlots: 0, name: "Race E");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2 });

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(generate.Result.Success);
        Assert.Equal(409, generate.StatusCode);
        Assert.True(generate.Result.Data!.RequiresTournamentLevelAction);
        Assert.Equal(2, generate.Result.Data.EligibleCount);
        Assert.Equal(2, generate.Result.Data.RequiredAdvanceCount);

        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceC));
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceD));
        Assert.Equal(0, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceE));
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
        // AdvanceCount=2/slots=2 (not 1/1 as originally written) — the new "every target Race must
        // receive >= 2 horses to be a meaningful race" rule (see the underfilled-race check in
        // GenerateNextRoundEntriesAsync) now rejects a single qualifier landing alone in the sole
        // Final race, which the old 1/1 setup here would have hit. Bumped to 2/2 so this test can
        // keep exercising what it's actually about — retry-after-success idempotency — independent
        // of that unrelated policy.
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2 });

        var first = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(first.Result.Success, first.Result.Message);
        Assert.Equal(2, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal));

        var second = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.False(second.Result.Success);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(2, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceFinal)); // still exactly two, no duplicate
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

    // ── P: GetFinalStandingsAsync (Q2 Phase 2, read-only "kết quả chung cuộc") ────────────────

    private sealed record ThreeRoundSetup(Guid TournamentId, Guid Round1Id, Guid Round2Id, Guid Round3Id, Guid RefereeId, DateTime Start);

    /// <summary>Tournament(MaxRounds=3, Draft) + Round1(non-final) + Round2(non-final) + Round3(Final) + one Referee. No Races yet.</summary>
    private static async Task<ThreeRoundSetup> BuildThreeRoundTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, int round1AdvanceCount, int round2AdvanceCount, int tournamentMaxParticipants = 20)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = $"Q2-3R-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(30),
            RegistrationDeadline = start.AddDays(-1), MinParticipants = 3, MaxParticipants = tournamentMaxParticipants, MaxRounds = 3
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
            Name = "Round 2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(5), ScheduledEndDate = start.AddDays(10), AdvanceCount = round2AdvanceCount
        });
        Assert.True(r2.Result.Success, r2.Result.Message);
        var r3 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Final", TournamentId = tournamentId, RoundNumber = 3,
            ScheduledStartDate = start.AddDays(10), ScheduledEndDate = start.AddDays(15), AdvanceCount = 0
        });
        Assert.True(r3.Result.Success, r3.Result.Message);

        var refereeUserId = Guid.NewGuid();
        f.Db.Add(new User { Id = refereeUserId, Email = $"ref-{Guid.NewGuid():N}@test.com", PasswordHash = "x", FullName = "Referee", Role = UserRole.Referee });
        var referee = new Referee { Id = Guid.NewGuid(), UserId = refereeUserId, LicenseNumber = $"LIC-{Guid.NewGuid():N}", IsActive = true };
        f.Db.Add(referee);
        await f.Db.SaveChangesAsync();

        return new ThreeRoundSetup(tournamentId, r1.Result.Data!.Id, r2.Result.Data!.Id, r3.Result.Data!.Id, referee.Id, start);
    }

    /// <summary>Sets GateNumber + a Passed HorseHealthCheck for entries that were carried over by
    /// GenerateNextRoundEntriesAsync (GateNumber=null, no HealthCheck) — needed before StartRaceAsync
    /// will accept the Race they now belong to.</summary>
    private static async Task PrepareCarriedOverEntriesForStartAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid raceId, Guid refereeId, List<Guid> horseIds)
    {
        var gate = 1;
        foreach (var horseId in horseIds)
        {
            var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
            entry.GateNumber = gate++;
            f.Db.Add(new HorseHealthCheck
            {
                Id = Guid.NewGuid(), HorseId = horseId, RaceId = raceId, RefereeId = refereeId,
                Status = HealthCheckStatus.Passed, ApprovedToRace = true, CheckedAt = DateTime.UtcNow
            });
        }
        await f.Db.SaveChangesAsync();
    }

    /// <summary>Drives a Race with its already-seeded entries from Scheduled through Official, with
    /// an explicit per-entry (Position, Status) — unlike FinishRaceOfficialAsync's Ranking() helper,
    /// this allows a mix of Completed/DNF/DSQ instead of "everyone Completed in the given order".</summary>
    private static async Task FinishRaceWithMixedResultsAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid raceId, Guid refereeId, List<(Guid HorseId, int Position, string Status)> results)
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

        var submit = await f.LiveResult.UpdateRaceResultAsync(raceId, new SubmitRaceResultRequest
        {
            Rankings = results.Select(r => new SubmitRankingEntry { HorseId = r.HorseId, Position = r.Position, Status = r.Status }).ToList()
        });
        Assert.True(submit.Result.Success, submit.Result.Message);

        f.Db.Add(new RaceReport
        {
            Id = Guid.NewGuid(), RaceId = raceId, RefereeId = refereeId,
            CompletedAt = DateTime.UtcNow, Details = "Test fixture race report.", CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        var approve = await f.Admin.ApproveRaceResultAsync(raceId);
        Assert.True(approve.Result.Success, approve.Result.Message);
    }

    [Fact]
    public async Task GetFinalStandings_NormalFinish_ReturnsFinalRoundRankingInOrder()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, j2) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2 });

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(generate.Result.Success, generate.Result.Message);

        await AssignRefereeAsync(f, raceFinal, setup.RefereeId);
        await PrepareCarriedOverEntriesForStartAsync(f, raceFinal, setup.RefereeId, new List<Guid> { h1, h2 });
        // h2 wins the Final, h1 second — proves Standings reflects the Final's OWN order, not Round 1's.
        await FinishRaceOfficialAsync(f, raceFinal, setup.RefereeId, new List<Guid> { h2, h1 });

        var finish = await f.TournamentSvc.ChangeStatusAsync(setup.TournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Finished }, Guid.NewGuid());
        Assert.True(finish.Result.Success, finish.Result.Message);

        var prizesBefore = await f.Db.Prizes.AsNoTracking().ToListAsync();

        var standings = await f.RaceSvc.GetFinalStandingsAsync(setup.TournamentId);
        Assert.True(standings.Result.Success, standings.Result.Message);
        var dto = standings.Result.Data!;
        Assert.True(dto.IsFinal);
        Assert.False(dto.IsVoid);
        Assert.False(dto.IsWalkover);
        Assert.False(dto.RequiresManualReview);
        Assert.Equal(2, dto.DecidingRoundNumber);
        Assert.NotNull(dto.Standings);
        Assert.Equal(2, dto.Standings!.Count);
        Assert.Equal(1, dto.Standings[0].Position);
        Assert.Equal(h2, dto.Standings[0].HorseId);
        Assert.Equal(2, dto.Standings[1].Position);
        Assert.Equal(h1, dto.Standings[1].HorseId);

        // Owner/JockeyId/OwnerName are NOT provided by the reused GetRaceResultAsync (it only has
        // HorseId/HorseName/JockeyName) — this is the newly-written join (RaceEntry -> Horse ->
        // Owner -> User) inside BuildFinishedStandingsAsync, so assert its actual values, not just
        // that Position/HorseId came through.
        var horse2 = await f.Db.Horses.AsNoTracking()
            .Include(h => h.Owner!).ThenInclude(o => o.User)
            .SingleAsync(h => h.Id == h2);
        Assert.Equal(j2, dto.Standings[0].JockeyId);
        Assert.Equal(horse2.OwnerId, dto.Standings[0].OwnerId);
        Assert.Equal(horse2.Owner!.User!.FullName, dto.Standings[0].OwnerName);

        var prizesAfter = await f.Db.Prizes.AsNoTracking().ToListAsync();
        Assert.Equal(prizesBefore.Count, prizesAfter.Count);
        Assert.Empty(prizesAfter);
    }

    [Fact]
    public async Task GetFinalStandings_Walkover_DecidingRoundIsThePreFinalRound()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        var raceFinal = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");

        // Same walkover scenario as GenerateNextRound_EligibleEqualsOne_Walkover_FinishesTournamentWithoutTouchingPrizes:
        // h1 Completed, h2 DNF -> exactly 1 eligible horse -> Q1 auto-finishes the Tournament at Round 1.
        await SetTournamentOngoingAsync(f, raceA);
        await f.RaceManagement.OpenRegistrationAsync(raceA);
        await f.RaceManagement.CloseRegistrationAsync(raceA);
        await f.RaceManagement.StartRaceAsync(raceA);
        await f.RaceManagement.EndRaceAsync(raceA);
        var submit = await f.LiveResult.UpdateRaceResultAsync(raceA, new SubmitRaceResultRequest
        {
            Rankings = new List<SubmitRankingEntry>
            {
                new() { HorseId = h1, Position = 1, Status = "Completed" },
                new() { HorseId = h2, Position = 99, Status = "DNF" },
            }
        });
        Assert.True(submit.Result.Success, submit.Result.Message);
        f.Db.Add(new RaceReport { Id = Guid.NewGuid(), RaceId = raceA, RefereeId = setup.RefereeId, CompletedAt = DateTime.UtcNow, Details = "One DNF.", CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        var approve = await f.Admin.ApproveRaceResultAsync(raceA);
        Assert.True(approve.Result.Success, approve.Result.Message);

        var prizesBefore = await f.Db.Prizes.AsNoTracking().ToListAsync();

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(generate.Result.Success, generate.Result.Message);
        Assert.True(generate.Result.Data!.IsWalkover);

        var standings = await f.RaceSvc.GetFinalStandingsAsync(setup.TournamentId);
        Assert.True(standings.Result.Success, standings.Result.Message);
        var dto = standings.Result.Data!;
        Assert.True(dto.IsFinal);
        Assert.False(dto.IsVoid);
        Assert.True(dto.IsWalkover);
        Assert.False(dto.RequiresManualReview);
        Assert.Equal(1, dto.DecidingRoundNumber); // walkover Round, NOT MaxRounds (2)
        Assert.NotNull(dto.Standings);
        var h1Entry = dto.Standings!.Single(s => s.HorseId == h1);
        Assert.Equal(1, h1Entry.Position);

        var prizesAfter = await f.Db.Prizes.AsNoTracking().ToListAsync();
        Assert.Equal(prizesBefore.Count, prizesAfter.Count);
        Assert.Empty(prizesAfter);
    }

    [Fact]
    public async Task GetFinalStandings_Void_ReturnsCancellationReasonAsVoidReason_NoStandings()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");

        // Same void scenario as GenerateNextRound_EligibleEqualsZero_AutoVoidsTournament: both
        // horses DNF/DSQ -> 0 eligible -> Q1 auto-cancels the Tournament.
        await SetTournamentOngoingAsync(f, raceA);
        await f.RaceManagement.OpenRegistrationAsync(raceA);
        await f.RaceManagement.CloseRegistrationAsync(raceA);
        await f.RaceManagement.StartRaceAsync(raceA);
        await f.RaceManagement.EndRaceAsync(raceA);
        var submit = await f.LiveResult.UpdateRaceResultAsync(raceA, new SubmitRaceResultRequest
        {
            Rankings = new List<SubmitRankingEntry>
            {
                new() { HorseId = h1, Position = 99, Status = "DNF" },
                new() { HorseId = h2, Position = 99, Status = "DSQ" },
            }
        });
        Assert.True(submit.Result.Success, submit.Result.Message);
        f.Db.Add(new RaceReport { Id = Guid.NewGuid(), RaceId = raceA, RefereeId = setup.RefereeId, CompletedAt = DateTime.UtcNow, Details = "All DNF/DSQ.", CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        var approve = await f.Admin.ApproveRaceResultAsync(raceA);
        Assert.True(approve.Result.Success, approve.Result.Message);

        var prizesBefore = await f.Db.Prizes.AsNoTracking().ToListAsync();

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(generate.Result.Success, generate.Result.Message);
        Assert.True(generate.Result.Data!.IsVoided);

        var tournamentAfter = await f.Db.Tournaments.AsNoTracking().SingleAsync(t => t.Id == setup.TournamentId);
        Assert.Equal(TournamentStatus.Cancelled, tournamentAfter.Status);
        Assert.False(string.IsNullOrWhiteSpace(tournamentAfter.CancellationReason));

        var standings = await f.RaceSvc.GetFinalStandingsAsync(setup.TournamentId);
        Assert.True(standings.Result.Success, standings.Result.Message);
        var dto = standings.Result.Data!;
        Assert.True(dto.IsFinal);
        Assert.True(dto.IsVoid);
        Assert.Equal(tournamentAfter.CancellationReason, dto.VoidReason);
        Assert.Null(dto.Standings);
        Assert.False(dto.RequiresManualReview);

        var prizesAfter = await f.Db.Prizes.AsNoTracking().ToListAsync();
        Assert.Equal(prizesBefore.Count, prizesAfter.Count);
        Assert.Empty(prizesAfter);
    }

    [Fact]
    public async Task GetFinalStandings_TournamentStillOngoing_NotFinal_NoStandings()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");

        // Tournament is left Draft (never Published/Ongoing) — one of the 3 "chưa kết thúc" statuses.
        var standings = await f.RaceSvc.GetFinalStandingsAsync(setup.TournamentId);
        Assert.True(standings.Result.Success, standings.Result.Message);
        var dto = standings.Result.Data!;
        Assert.False(dto.IsFinal);
        Assert.Null(dto.Standings);
        Assert.False(string.IsNullOrWhiteSpace(dto.Message));
    }

    [Fact]
    public async Task GetFinalStandings_DecidingRoundHasMultipleRaces_RequiresManualReview_NoStandings()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        // MaxRounds=3: Round1(non-final) feeds Round2(non-final, 2 parallel Races) feeds Round3(Final).
        // Round2's combined eligible resolves to 1 (walkover) BEFORE Round3 ever runs — so the Round
        // that actually decided the Tournament (Round2) has 2 Races Finished+Official, not 1. This is
        // the real "multi-Race deciding Round" scenario confirmed possible in GenerateNextRoundEntriesAsync
        // (eligible is summed across ALL Races of a Round) — unlike Q1Test-E in SEED_Q1_SCENARIOS, this
        // never touches the Final Round's Race count, so it never conflicts with the Publish-time rule
        // that the Final must have exactly 1 Race.
        var setup = await BuildThreeRoundTournamentAsync(f, round1AdvanceCount: 4, round2AdvanceCount: 2);

        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 5, qualificationSlots: 4, name: "Round1 Race");
        var raceB1 = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 4, qualificationSlots: 1, name: "Round2 Race B1");
        var raceB2 = await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5).AddHours(3), maxParticipants: 4, qualificationSlots: 1, name: "Round2 Race B2");
        await CreateRaceAsync(f, setup.TournamentId, setup.Round3Id, setup.Start.AddDays(10), maxParticipants: 4, qualificationSlots: 0, name: "Final");

        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");
        var (h3, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h3");
        var (h4, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h4");
        await FinishRaceOfficialAsync(f, raceA, setup.RefereeId, new List<Guid> { h1, h2, h3, h4 });

        var generate1 = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(generate1.Result.Success, generate1.Result.Message);
        Assert.Equal(4, generate1.Result.Data!.GeneratedEntries);

        // Deterministic round-robin: sourceRaces=[raceA] flattened h1,h2,h3,h4 against
        // targetRaces=[raceB1,raceB2] (ordered by ScheduledAt) -> raceB1={h1,h3}, raceB2={h2,h4}.
        await AssignRefereeAsync(f, raceB1, setup.RefereeId);
        await AssignRefereeAsync(f, raceB2, setup.RefereeId);
        await PrepareCarriedOverEntriesForStartAsync(f, raceB1, setup.RefereeId, new List<Guid> { h1, h3 });
        await PrepareCarriedOverEntriesForStartAsync(f, raceB2, setup.RefereeId, new List<Guid> { h2, h4 });

        // Only h1 finishes Completed across BOTH Round 2 Races combined -> eligible == 1 (walkover),
        // computed by summing qualifiers over every Race of Round 2 — exactly the aggregation
        // GenerateNextRoundEntriesAsync itself performs.
        await FinishRaceWithMixedResultsAsync(f, raceB1, setup.RefereeId, new List<(Guid, int, string)>
        {
            (h1, 1, "Completed"),
            (h3, 99, "DNF"),
        });
        await FinishRaceWithMixedResultsAsync(f, raceB2, setup.RefereeId, new List<(Guid, int, string)>
        {
            (h2, 99, "DSQ"),
            (h4, 99, "DSQ"),
        });

        var prizesBefore = await f.Db.Prizes.AsNoTracking().ToListAsync();

        var generate2 = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round2Id);
        Assert.True(generate2.Result.Success, generate2.Result.Message);
        Assert.True(generate2.Result.Data!.IsWalkover);
        Assert.Equal(h1, generate2.Result.Data.WalkoverWinnerHorseId);

        var tournamentAfter = await f.Db.Tournaments.AsNoTracking().SingleAsync(t => t.Id == setup.TournamentId);
        Assert.Equal(TournamentStatus.Finished, tournamentAfter.Status);

        var standings = await f.RaceSvc.GetFinalStandingsAsync(setup.TournamentId);
        Assert.True(standings.Result.Success, standings.Result.Message);
        var dto = standings.Result.Data!;
        Assert.True(dto.IsFinal);
        Assert.False(dto.IsVoid);
        Assert.True(dto.IsWalkover); // Round 2 != MaxRounds (3)
        Assert.Equal(2, dto.DecidingRoundNumber);
        Assert.True(dto.RequiresManualReview);
        Assert.Null(dto.Standings);
        Assert.False(string.IsNullOrWhiteSpace(dto.Message));

        var prizesAfter = await f.Db.Prizes.AsNoTracking().ToListAsync();
        Assert.Equal(prizesBefore.Count, prizesAfter.Count);
        Assert.Empty(prizesAfter);
    }

    [Fact]
    public async Task GetFinalStandings_TournamentNotFound_Returns404()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.RaceSvc.GetFinalStandingsAsync(Guid.NewGuid());
        Assert.False(result.Result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    // ── Q: PrizeService.DistributeAsync (Phase 4, manual Admin-triggered prize payout) ──────────

    private static PrizeService MakeDistributionPrizeService(RaceLifecycleTests.LifecycleFixture f)
        => new PrizeService(new PrizeRepository(f.Db), f.TournamentRepo, f.UnitOfWork, f.Db, f.RaceSvc, f.FaultWallet);

    /// <summary>Single-round Tournament (Round1 IS Final) with a real PrizePool — BuildSingleRoundTournamentAsync
    /// above always leaves PrizePool at its 0 default, which is unusable for a distribution test.</summary>
    private static async Task<(Guid tournamentId, Guid round1Id)> BuildSingleRoundTournamentWithPrizePoolAsync(
        RaceLifecycleTests.LifecycleFixture f, decimal prizePool)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = $"PrizeDist-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(10),
            RegistrationDeadline = start.AddDays(-1), MinParticipants = 3, MaxParticipants = 10, MaxRounds = 1,
            PrizePool = prizePool
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

    private static async Task<Guid> CreateStandaloneRefereeAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var refereeUserId = Guid.NewGuid();
        f.Db.Add(new User { Id = refereeUserId, Email = $"ref-{Guid.NewGuid():N}@test.com", PasswordHash = "x", FullName = "Referee", Role = UserRole.Referee });
        var referee = new Referee { Id = Guid.NewGuid(), UserId = refereeUserId, LicenseNumber = $"LIC-{Guid.NewGuid():N}", IsActive = true };
        f.Db.Add(referee);
        await f.Db.SaveChangesAsync();
        return referee.Id;
    }

    private static async Task<Guid> GetOwnerUserIdForHorseAsync(RaceLifecycleTests.LifecycleFixture f, Guid horseId)
    {
        var horse = await f.Db.Horses.AsNoTracking().Include(h => h.Owner).SingleAsync(h => h.Id == horseId);
        return horse.Owner!.UserId;
    }

    /// <summary>Owner-role Users have no Wallet auto-created anywhere in production
    /// (WalletService.GetOrCreateWalletAsync is restricted to UserRole.Spectator, and
    /// AddPointsAsync never auto-creates one at all) — seeded by hand here, same as this file
    /// already hand-seeds Referee/HealthCheck rows for other prerequisites.</summary>
    private static async Task AddWalletAsync(RaceLifecycleTests.LifecycleFixture f, Guid userId, decimal balance = 0m)
    {
        f.Db.Add(new Wallet { Id = Guid.NewGuid(), UserId = userId, Balance = balance, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task DistributeAsync_NormalFinish_CreditsEachOwnerWalletByPrizeAmount()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var prizeSvc = MakeDistributionPrizeService(f);
        var (tournamentId, round1Id) = await BuildSingleRoundTournamentWithPrizePoolAsync(f, prizePool: 1_000_000m);

        var p1 = await prizeSvc.CreateAsync(new CreatePrizeRequest { TournamentId = tournamentId, Position = 1, PercentageOfPool = 50, Name = "Vô địch" });
        Assert.True(p1.Result.Success, p1.Result.Message);
        var p2 = await prizeSvc.CreateAsync(new CreatePrizeRequest { TournamentId = tournamentId, Position = 2, PercentageOfPool = 30, Name = "Á quân" });
        Assert.True(p2.Result.Success, p2.Result.Message);
        var p3 = await prizeSvc.CreateAsync(new CreatePrizeRequest { TournamentId = tournamentId, Position = 3, PercentageOfPool = 20, Name = "Quý quân" });
        Assert.True(p3.Result.Success, p3.Result.Message);

        var refereeId = await CreateStandaloneRefereeAsync(f);
        var raceId = await CreateRaceAsync(f, tournamentId, round1Id, DateTime.UtcNow.AddDays(10), maxParticipants: 5, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceId, refereeId);

        var (h1, _) = await AddQualifiableEntryAsync(f, raceId, refereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceId, refereeId, "h2");
        var (h3, _) = await AddQualifiableEntryAsync(f, raceId, refereeId, "h3");
        await FinishRaceOfficialAsync(f, raceId, refereeId, new List<Guid> { h1, h2, h3 });

        var finish = await f.TournamentSvc.ChangeStatusAsync(tournamentId, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Finished }, Guid.NewGuid());
        Assert.True(finish.Result.Success, finish.Result.Message);

        var owner1UserId = await GetOwnerUserIdForHorseAsync(f, h1);
        var owner2UserId = await GetOwnerUserIdForHorseAsync(f, h2);
        var owner3UserId = await GetOwnerUserIdForHorseAsync(f, h3);
        await AddWalletAsync(f, owner1UserId);
        await AddWalletAsync(f, owner2UserId);
        await AddWalletAsync(f, owner3UserId);

        var distribute = await prizeSvc.DistributeAsync(tournamentId);
        Assert.True(distribute.Result.Success, distribute.Result.Message);
        var dto = distribute.Result.Data!;

        Assert.Equal(3, dto.Distributed.Count);
        Assert.Empty(dto.Skipped);
        Assert.Empty(dto.Errors);

        Assert.Equal(500_000m, dto.Distributed.Single(d => d.Position == 1).Amount);
        Assert.Equal(300_000m, dto.Distributed.Single(d => d.Position == 2).Amount);
        Assert.Equal(200_000m, dto.Distributed.Single(d => d.Position == 3).Amount);

        Assert.Equal(500_000m, await f.GetWalletBalanceAsync(owner1UserId));
        Assert.Equal(300_000m, await f.GetWalletBalanceAsync(owner2UserId));
        Assert.Equal(200_000m, await f.GetWalletBalanceAsync(owner3UserId));

        var prizesAfter = await f.Db.Prizes.AsNoTracking().Where(p => p.TournamentId == tournamentId).ToListAsync();
        Assert.All(prizesAfter, p => Assert.True(p.IsDistributed));
        Assert.All(prizesAfter, p => Assert.NotNull(p.DistributedAt));
    }

    [Fact]
    public async Task DistributeAsync_CalledTwice_SecondCallDoesNotCreditWalletAgain()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var prizeSvc = MakeDistributionPrizeService(f);
        var (tournamentId, round1Id) = await BuildSingleRoundTournamentWithPrizePoolAsync(f, prizePool: 1_000_000m);

        var p1 = await prizeSvc.CreateAsync(new CreatePrizeRequest { TournamentId = tournamentId, Position = 1, PercentageOfPool = 100, Name = "Vô địch" });
        Assert.True(p1.Result.Success, p1.Result.Message);

        var refereeId = await CreateStandaloneRefereeAsync(f);
        var raceId = await CreateRaceAsync(f, tournamentId, round1Id, DateTime.UtcNow.AddDays(10), maxParticipants: 5, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceId, refereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceId, refereeId, "h1");
        await FinishRaceOfficialAsync(f, raceId, refereeId, new List<Guid> { h1 });

        var finish = await f.TournamentSvc.ChangeStatusAsync(tournamentId, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Finished }, Guid.NewGuid());
        Assert.True(finish.Result.Success, finish.Result.Message);

        var ownerUserId = await GetOwnerUserIdForHorseAsync(f, h1);
        await AddWalletAsync(f, ownerUserId);

        var first = await prizeSvc.DistributeAsync(tournamentId);
        Assert.True(first.Result.Success, first.Result.Message);
        Assert.Single(first.Result.Data!.Distributed);
        var balanceAfterFirst = await f.GetWalletBalanceAsync(ownerUserId);
        Assert.Equal(1_000_000m, balanceAfterFirst);

        var second = await prizeSvc.DistributeAsync(tournamentId);
        Assert.True(second.Result.Success, second.Result.Message);
        // Prize.IsDistributed already true after the first call -> filtered out of `pending`
        // before the loop even runs, so nothing new to distribute or skip.
        Assert.Empty(second.Result.Data!.Distributed);
        Assert.Empty(second.Result.Data!.Skipped);

        Assert.Equal(balanceAfterFirst, await f.GetWalletBalanceAsync(ownerUserId));
    }

    [Fact]
    public async Task DistributeAsync_VoidTournament_FailsWithoutTouchingWalletOrPrize()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var prizeSvc = MakeDistributionPrizeService(f);
        var setup = await BuildTwoRoundTournamentAsync(f, round1AdvanceCount: 2);
        var p1 = await prizeSvc.CreateAsync(new CreatePrizeRequest { TournamentId = setup.TournamentId, Position = 1, PercentageOfPool = 100, Name = "Vô địch" });
        Assert.True(p1.Result.Success, p1.Result.Message);

        var raceA = await CreateRaceAsync(f, setup.TournamentId, setup.Round1Id, setup.Start, maxParticipants: 4, qualificationSlots: 2, name: "Race A");
        await CreateRaceAsync(f, setup.TournamentId, setup.Round2Id, setup.Start.AddDays(5), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceA, setup.RefereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceA, setup.RefereeId, "h2");

        // Same void scenario as GetFinalStandings_Void_...: both horses DNF/DSQ -> eligible == 0.
        await SetTournamentOngoingAsync(f, raceA);
        await f.RaceManagement.OpenRegistrationAsync(raceA);
        await f.RaceManagement.CloseRegistrationAsync(raceA);
        await f.RaceManagement.StartRaceAsync(raceA);
        await f.RaceManagement.EndRaceAsync(raceA);
        var submit = await f.LiveResult.UpdateRaceResultAsync(raceA, new SubmitRaceResultRequest
        {
            Rankings = new List<SubmitRankingEntry>
            {
                new() { HorseId = h1, Position = 99, Status = "DNF" },
                new() { HorseId = h2, Position = 99, Status = "DSQ" },
            }
        });
        Assert.True(submit.Result.Success, submit.Result.Message);
        f.Db.Add(new RaceReport { Id = Guid.NewGuid(), RaceId = raceA, RefereeId = setup.RefereeId, CompletedAt = DateTime.UtcNow, Details = "All DNF/DSQ.", CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        var approve = await f.Admin.ApproveRaceResultAsync(raceA);
        Assert.True(approve.Result.Success, approve.Result.Message);

        var generate = await f.RaceManagement.GenerateNextRoundEntriesAsync(setup.Round1Id);
        Assert.True(generate.Result.Success, generate.Result.Message);
        Assert.True(generate.Result.Data!.IsVoided);

        var owner1UserId = await GetOwnerUserIdForHorseAsync(f, h1);
        await AddWalletAsync(f, owner1UserId, 100m);
        var before = await f.GetWalletBalanceAsync(owner1UserId);

        var distribute = await prizeSvc.DistributeAsync(setup.TournamentId);
        Assert.False(distribute.Result.Success);
        Assert.Equal(400, distribute.StatusCode);

        Assert.Equal(before, await f.GetWalletBalanceAsync(owner1UserId));
        var prizesAfter = await f.Db.Prizes.AsNoTracking().Where(p => p.TournamentId == setup.TournamentId).ToListAsync();
        Assert.All(prizesAfter, p => Assert.False(p.IsDistributed));
    }

    [Fact]
    public async Task DistributeAsync_TournamentStillOngoing_FailsWithoutTouchingWalletOrPrize()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var prizeSvc = MakeDistributionPrizeService(f);
        var (tournamentId, round1Id) = await BuildSingleRoundTournamentWithPrizePoolAsync(f, prizePool: 1_000_000m);
        var p1 = await prizeSvc.CreateAsync(new CreatePrizeRequest { TournamentId = tournamentId, Position = 1, PercentageOfPool = 100, Name = "Vô địch" });
        Assert.True(p1.Result.Success, p1.Result.Message);

        var refereeId = await CreateStandaloneRefereeAsync(f);
        var raceId = await CreateRaceAsync(f, tournamentId, round1Id, DateTime.UtcNow.AddDays(10), maxParticipants: 5, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceId, refereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceId, refereeId, "h1");
        // Tournament driven to Ongoing but the Race is never finished/approved -> not Finished.
        await SetTournamentOngoingAsync(f, raceId);

        var ownerUserId = await GetOwnerUserIdForHorseAsync(f, h1);
        await AddWalletAsync(f, ownerUserId);

        var distribute = await prizeSvc.DistributeAsync(tournamentId);
        Assert.False(distribute.Result.Success);
        Assert.Equal(400, distribute.StatusCode);

        Assert.Equal(0m, await f.GetWalletBalanceAsync(ownerUserId));
        var prizeAfter = await f.Db.Prizes.AsNoTracking().SingleAsync(p => p.TournamentId == tournamentId);
        Assert.False(prizeAfter.IsDistributed);
    }

    [Fact]
    public async Task DistributeAsync_PositionWithNoFinisher_SkipsThatPrizeButDistributesOthers()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var prizeSvc = MakeDistributionPrizeService(f);
        var (tournamentId, round1Id) = await BuildSingleRoundTournamentWithPrizePoolAsync(f, prizePool: 1_000_000m);

        var p1 = await prizeSvc.CreateAsync(new CreatePrizeRequest { TournamentId = tournamentId, Position = 1, PercentageOfPool = 50, Name = "Vô địch" });
        Assert.True(p1.Result.Success, p1.Result.Message);
        // Position 5 configured, but only 3 horses will ever run -> nobody finishes at Position 5.
        var p5 = await prizeSvc.CreateAsync(new CreatePrizeRequest { TournamentId = tournamentId, Position = 5, PercentageOfPool = 10, Name = "Hạng 5" });
        Assert.True(p5.Result.Success, p5.Result.Message);

        var refereeId = await CreateStandaloneRefereeAsync(f);
        var raceId = await CreateRaceAsync(f, tournamentId, round1Id, DateTime.UtcNow.AddDays(10), maxParticipants: 10, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceId, refereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceId, refereeId, "h1");
        var (h2, _) = await AddQualifiableEntryAsync(f, raceId, refereeId, "h2");
        var (h3, _) = await AddQualifiableEntryAsync(f, raceId, refereeId, "h3");
        await FinishRaceOfficialAsync(f, raceId, refereeId, new List<Guid> { h1, h2, h3 });

        var finish = await f.TournamentSvc.ChangeStatusAsync(tournamentId, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Finished }, Guid.NewGuid());
        Assert.True(finish.Result.Success, finish.Result.Message);

        var owner1UserId = await GetOwnerUserIdForHorseAsync(f, h1);
        await AddWalletAsync(f, owner1UserId);

        var distribute = await prizeSvc.DistributeAsync(tournamentId);
        Assert.True(distribute.Result.Success, distribute.Result.Message);
        var dto = distribute.Result.Data!;

        Assert.Single(dto.Distributed);
        Assert.Equal(1, dto.Distributed[0].Position);
        Assert.Single(dto.Skipped);
        Assert.Equal(5, dto.Skipped[0].Position);
        Assert.Contains("Hạng 5", dto.Skipped[0].Reason);

        Assert.Equal(dto.Distributed[0].Amount, await f.GetWalletBalanceAsync(owner1UserId));

        var prizesAfter = await f.Db.Prizes.AsNoTracking().Where(p => p.TournamentId == tournamentId).ToListAsync();
        Assert.True(prizesAfter.Single(p => p.Position == 1).IsDistributed);
        Assert.False(prizesAfter.Single(p => p.Position == 5).IsDistributed);
    }

    [Fact]
    public async Task DistributeAsync_WalletCreditFails_RollsBackClaimAndSucceedsOnRetry()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var prizeSvc = MakeDistributionPrizeService(f);
        var (tournamentId, round1Id) = await BuildSingleRoundTournamentWithPrizePoolAsync(f, prizePool: 1_000_000m);

        var p1 = await prizeSvc.CreateAsync(new CreatePrizeRequest { TournamentId = tournamentId, Position = 1, PercentageOfPool = 100, Name = "Vô địch" });
        Assert.True(p1.Result.Success, p1.Result.Message);

        var refereeId = await CreateStandaloneRefereeAsync(f);
        var raceId = await CreateRaceAsync(f, tournamentId, round1Id, DateTime.UtcNow.AddDays(10), maxParticipants: 5, qualificationSlots: 0, name: "Final");
        await AssignRefereeAsync(f, raceId, refereeId);
        var (h1, _) = await AddQualifiableEntryAsync(f, raceId, refereeId, "h1");
        await FinishRaceOfficialAsync(f, raceId, refereeId, new List<Guid> { h1 });

        var finish = await f.TournamentSvc.ChangeStatusAsync(tournamentId, new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Finished }, Guid.NewGuid());
        Assert.True(finish.Result.Success, finish.Result.Message);

        var ownerUserId = await GetOwnerUserIdForHorseAsync(f, h1);
        await AddWalletAsync(f, ownerUserId);

        // Real, thrown wallet-credit failure — f.FaultWallet wraps the production WalletService
        // used by MakeDistributionPrizeService above, so this exercises DistributeAsync's actual
        // catch/rollback branch, not a simulated one.
        f.FaultWallet.FailForUserIds.Add(ownerUserId);

        var first = await prizeSvc.DistributeAsync(tournamentId);
        Assert.True(first.Result.Success, first.Result.Message); // one Prize erroring never fails the whole call
        var dto1 = first.Result.Data!;
        Assert.Empty(dto1.Distributed);
        Assert.Single(dto1.Errors);
        Assert.Equal(1, dto1.Errors[0].Position);
        Assert.Contains("Injected payout failure", dto1.Errors[0].Reason);

        // Wallet must be untouched — the credit never actually happened.
        Assert.Equal(0m, await f.GetWalletBalanceAsync(ownerUserId));

        // Claim must have been rolled back to false, not left stuck true with no money moved.
        var prizeAfterFirst = await f.Db.Prizes.AsNoTracking().SingleAsync(p => p.TournamentId == tournamentId);
        Assert.False(prizeAfterFirst.IsDistributed);
        Assert.Null(prizeAfterFirst.DistributedAt);

        // Remove the fault and retry — the rolled-back claim must allow a clean, successful retry.
        f.FaultWallet.FailForUserIds.Remove(ownerUserId);

        var second = await prizeSvc.DistributeAsync(tournamentId);
        Assert.True(second.Result.Success, second.Result.Message);
        var dto2 = second.Result.Data!;
        Assert.Single(dto2.Distributed);
        Assert.Empty(dto2.Errors);
        Assert.Equal(1_000_000m, dto2.Distributed[0].Amount);

        Assert.Equal(1_000_000m, await f.GetWalletBalanceAsync(ownerUserId));
        var prizeAfterSecond = await f.Db.Prizes.AsNoTracking().SingleAsync(p => p.TournamentId == tournamentId);
        Assert.True(prizeAfterSecond.IsDistributed);
        Assert.NotNull(prizeAfterSecond.DistributedAt);
    }
}
