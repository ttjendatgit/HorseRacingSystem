using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// R1a: three StartRace safety fixes on top of R0.1 — (1) parent Tournament must be Ongoing,
/// (2) Jockey.ApprovalStatus/User.IsActive are re-checked as defense-in-depth (Invite/Accept/
/// FinalConfirm already enforce this at pairing time, but approval can be revoked afterward),
/// (3) Rejected/Scratched RaceEntry rows are excluded from start-readiness instead of
/// permanently blocking the Race. Reuses RaceLifecycleTests.LifecycleFixture, same convention
/// as R01ResultRefereeSafetyTests.
/// </summary>
public class R1aStartRaceSafetyTests
{
    private static async Task ProgressToPreStartAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId)
    {
        var open = await f.RaceManagement.OpenRegistrationAsync(raceId);
        Assert.True(open.Result.Success, open.Result.Message);
        var close = await f.RaceManagement.CloseRegistrationAsync(raceId);
        Assert.True(close.Result.Success, close.Result.Message);
    }

    private static Task<RaceEntry> GetEntryAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId, Guid horseId)
        => f.Db.RaceEntries.Include(e => e.Jockey)!.ThenInclude(j => j!.User)
            .SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);

    private static async Task SetTournamentStatusAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId, TournamentStatus status)
    {
        var tournamentId = (await f.Db.Races.SingleAsync(r => r.Id == raceId)).TournamentId;
        var tournament = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentId);
        tournament.Status = status;
        tournament.IsActive = status == TournamentStatus.Ongoing;
        await f.Db.SaveChangesAsync();
    }

    /// <summary>Adds a third RaceEntry (new Horse+Jockey, no HealthCheck, no confirmations —
    /// deliberately unready in every other way) already Rejected or Scratched, to prove it is
    /// excluded from readiness rather than blocking the Race.</summary>
    private static async Task AddNonParticipatingEntryAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid raceId, bool rejected, bool scratched)
    {
        var ownerUserId = Guid.NewGuid();
        f.Db.Add(new User { Id = ownerUserId, Email = $"owner-np-{Guid.NewGuid():N}@test.com", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner });
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUserId, OwnerCode = $"OWN-NP-{Guid.NewGuid():N}" };
        f.Db.Add(owner);
        var horse = new Horse { Id = Guid.NewGuid(), Name = "NonParticipant", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.Add(horse);

        f.Db.Add(new RaceEntry
        {
            Id = Guid.NewGuid(),
            RaceId = raceId,
            HorseId = horse.Id,
            JockeyId = null,
            Status = rejected ? RegistrationStatus.Rejected : RegistrationStatus.Approved,
            OwnerConfirmed = false,
            JockeyConfirmed = false,
            ScratchedAt = scratched ? DateTime.UtcNow : null,
            ScratchReason = scratched ? "Withdrawn" : null,
        });
        await f.Db.SaveChangesAsync();
    }

    // ── Part 1: Tournament must be Ongoing ──────────────────────────────────

    [Fact]
    public async Task StartRace_TournamentPublished_Blocked()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToPreStartAsync(f, race.Id);
        await SetTournamentStatusAsync(f, race.Id, TournamentStatus.Published);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.False(start.Result.Success);
        Assert.Equal(409, start.StatusCode);
        Assert.Equal(RaceStatus.RegistrationClosed, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    [Fact]
    public async Task StartRace_TournamentOngoing_AllReady_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToPreStartAsync(f, race.Id);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.True(start.Result.Success, start.Result.Message);
        Assert.Equal(RaceStatus.InProgress, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    // ── Part 2: Jockey PERSON eligibility re-checked at start ──────────────

    [Fact]
    public async Task StartRace_JockeyRejectedAfterFinalConfirm_Blocked()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entry = await GetEntryAsync(f, race.Id, race.WinnerHorseId);
        Assert.True(entry.JockeyConfirmed);
        Assert.True(entry.OwnerConfirmed);
        entry.Jockey!.ApprovalStatus = ApprovalStatus.Rejected;
        await f.Db.SaveChangesAsync();
        await ProgressToPreStartAsync(f, race.Id);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.False(start.Result.Success);
        Assert.Equal(RaceStatus.RegistrationClosed, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    [Fact]
    public async Task StartRace_JockeyPending_Blocked()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entry = await GetEntryAsync(f, race.Id, race.WinnerHorseId);
        entry.Jockey!.ApprovalStatus = ApprovalStatus.Pending;
        await f.Db.SaveChangesAsync();
        await ProgressToPreStartAsync(f, race.Id);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.False(start.Result.Success);
    }

    [Fact]
    public async Task StartRace_JockeyUserInactive_Blocked()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entry = await GetEntryAsync(f, race.Id, race.WinnerHorseId);
        Assert.Equal(ApprovalStatus.Approved, entry.Jockey!.ApprovalStatus);
        entry.Jockey!.User!.IsActive = false;
        await f.Db.SaveChangesAsync();
        await ProgressToPreStartAsync(f, race.Id);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.False(start.Result.Success);
    }

    [Fact]
    public async Task StartRace_ApprovedActiveJockey_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entry = await GetEntryAsync(f, race.Id, race.WinnerHorseId);
        Assert.Equal(ApprovalStatus.Approved, entry.Jockey!.ApprovalStatus);
        Assert.True(entry.Jockey!.User!.IsActive);
        await ProgressToPreStartAsync(f, race.Id);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.True(start.Result.Success, start.Result.Message);
    }

    // ── Part 3: Rejected/Scratched entries excluded from readiness ─────────

    [Fact]
    public async Task StartRace_RejectedHistoricalEntry_DoesNotBlock()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await AddNonParticipatingEntryAsync(f, race.Id, rejected: true, scratched: false);
        await ProgressToPreStartAsync(f, race.Id);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.True(start.Result.Success, start.Result.Message);
        Assert.Equal(RaceStatus.InProgress, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    [Fact]
    public async Task StartRace_ScratchedHistoricalEntry_DoesNotBlock()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await AddNonParticipatingEntryAsync(f, race.Id, rejected: false, scratched: true);
        await ProgressToPreStartAsync(f, race.Id);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.True(start.Result.Success, start.Result.Message);
        Assert.Equal(RaceStatus.InProgress, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    [Fact]
    public async Task StartRace_AllEntriesRejectedOrScratched_Blocked()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var winnerEntry = await GetEntryAsync(f, race.Id, race.WinnerHorseId);
        var loserEntry = await GetEntryAsync(f, race.Id, race.LoserHorseId);
        winnerEntry.Status = RegistrationStatus.Rejected;
        winnerEntry.ScratchedAt = DateTime.UtcNow;
        loserEntry.ScratchedAt = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();
        await ProgressToPreStartAsync(f, race.Id);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.False(start.Result.Success);
        Assert.Equal(RaceStatus.RegistrationClosed, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    [Fact]
    public async Task StartRace_IncompleteApprovedEntry_StillBlocks()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        var entry = await GetEntryAsync(f, race.Id, race.WinnerHorseId);
        Assert.Equal(RegistrationStatus.Approved, entry.Status);
        Assert.Null(entry.ScratchedAt);
        entry.JockeyId = null;
        entry.JockeyConfirmed = false;
        await f.Db.SaveChangesAsync();
        await ProgressToPreStartAsync(f, race.Id);

        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.False(start.Result.Success);
        Assert.Equal(RaceStatus.RegistrationClosed, (await f.RaceRepo.GetByIdAsync(race.Id))!.Status);
    }

    // ── Part 5/7.12: Q1-generated-shaped entry starts without any invitation lookup ─────────

    [Fact]
    public async Task StartRace_Q1StyleEntry_SucceedsWithoutInvitationLookup()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();

        // CreateReadyToStartRaceAsync seeds RaceEntry rows directly (Status=Approved,
        // OwnerConfirmed=true, JockeyConfirmed=true, JockeyId=official Jockey) exactly like Q1's
        // generated Round2+ entries — and never creates a JockeyInvitation row for either Horse,
        // proving StartRace readiness depends only on the RaceEntry's own fields.
        var invitationCount = await f.Db.JockeyInvitations
            .CountAsync(i => i.HorseId == race.WinnerHorseId || i.HorseId == race.LoserHorseId);
        Assert.Equal(0, invitationCount);

        await ProgressToPreStartAsync(f, race.Id);
        var start = await f.RaceManagement.StartRaceAsync(race.Id);

        Assert.True(start.Result.Success, start.Result.Message);
    }
}
