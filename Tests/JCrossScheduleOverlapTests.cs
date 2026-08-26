using System;
using System.Linq;
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
/// J-CROSS: replaces the old global "one-active-Tournament-per-Jockey" lock (any Published/Ongoing
/// other-Tournament official assignment blocks Final Confirm) with a schedule-overlap lock (only a
/// colliding Race window across the two Tournaments' FULL immutable schedule sets blocks Final
/// Confirm). Same-Tournament Horse/Jockey immutability (J3 §5/§6) and Finished/Cancelled-never-locks
/// (J3 §7 baseline) are unchanged and are re-proven here alongside the new schedule semantics.
/// </summary>
public class JCrossScheduleOverlapTests
{
    // ── DIFFERENT TOURNAMENT — SCHEDULE OVERLAP SEMANTICS ──────────────────────────────────────

    [Fact]
    public async Task DifferentTournament_NonOverlappingSchedules_FinalConfirmSucceeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var dayA = DateTime.UtcNow.AddDays(22);
        var dayB = DateTime.UtcNow.AddDays(60); // far apart, no overlap possible

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-nooverlap-a", scheduledAt: dayA, scheduledEndAt: dayA.AddHours(1));
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-nooverlap-b", scheduledAt: dayB, scheduledEndAt: dayB.AddHours(1));
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-nooverlap-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceBId && e.HorseId == horseBId);
        Assert.Equal(jockeyId, entryB.JockeyId);
    }

    [Fact]
    public async Task DifferentTournament_CurrentRaceNonOverlapButFutureFinalOverlap_Blocked()
    {
        // CRITICAL: Tournament A Round1=Aug22, Final=Aug30. Tournament B Round1=Aug25, Final=Aug30.
        // Round1 races (the ones actually being Final-Confirmed) do NOT overlap with each other, but
        // both Tournaments' FINAL races land on the same day. This must still be rejected, because
        // official pairing persists for the Horse's entire Tournament journey and Q1 will silently
        // carry the same JockeyId into each Final RaceEntry without ever re-running Final Confirm.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var round1AStart = DateTime.UtcNow.AddDays(22);
        var finalStart = DateTime.UtcNow.AddDays(30);
        var round1BStart = DateTime.UtcNow.AddDays(25);

        var (tournamentAId, raceA1Id, raceAFinalId) = await CreateTwoRoundTournamentAsync(
            f, "jx-futurefinal-a", round1AStart, round1AStart.AddHours(1), finalStart, finalStart.AddHours(1));
        var (tournamentBId, raceB1Id, raceBFinalId) = await CreateTwoRoundTournamentAsync(
            f, "jx-futurefinal-b", round1BStart, round1BStart.AddHours(1), finalStart, finalStart.AddHours(1));

        // Sanity: the two Round1 races genuinely do not overlap.
        Assert.True(round1AStart.AddHours(1) <= round1BStart || round1BStart.AddHours(1) <= round1AStart);

        var (ownerAUserId, ownerAId, horseAId) = await CreateApprovedOwnerHorseAsync(f, "jx-futurefinal-horsea");
        await RegisterTournamentAsync(f, tournamentAId, ownerAId, horseAId, RegistrationStatus.Approved);
        var assignA = await f.RaceManagement.AssignHorseToRaceAsync(raceA1Id, new AssignHorseToRaceRequest { HorseId = horseAId });
        Assert.True(assignA.Result.Success, assignA.Result.Message);

        var (ownerBUserId, ownerBId, horseBId) = await CreateApprovedOwnerHorseAsync(f, "jx-futurefinal-horseb");
        await RegisterTournamentAsync(f, tournamentBId, ownerBId, horseBId, RegistrationStatus.Approved);
        var assignB = await f.RaceManagement.AssignHorseToRaceAsync(raceB1Id, new AssignHorseToRaceRequest { HorseId = horseBId });
        Assert.True(assignB.Result.Success, assignB.Result.Message);

        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-futurefinal-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceA1Id, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceB1Id, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceA1Id,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceB1Id,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(confirmB.Result.Success);
        Assert.Equal(409, confirmB.StatusCode);
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceB1Id && e.HorseId == horseBId);
        Assert.Null(entryB.JockeyId);
    }

    [Fact]
    public async Task DifferentTournament_AnyFutureRoundOverlap_Blocked()
    {
        // Overlap on a MIDDLE round (not the Final) must also block — the rule compares the entire
        // schedule set, not just Round1 or just the Final.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var aRound1 = DateTime.UtcNow.AddDays(10);
        var aRound2 = DateTime.UtcNow.AddDays(20); // this is the one that will collide
        var aFinal = DateTime.UtcNow.AddDays(30);
        var bRound1 = DateTime.UtcNow.AddDays(15);
        var bRound2 = aRound2; // exact collision with A's Round2
        var bFinal = DateTime.UtcNow.AddDays(45);

        var tournamentAId = await CreateThreeRoundTournamentAsync(f, "jx-midround-a", aRound1, aRound2, aFinal);
        var tournamentBId = await CreateThreeRoundTournamentAsync(f, "jx-midround-b", bRound1, bRound2, bFinal);

        var raceA1Id = await GetRoundRaceIdAsync(f, tournamentAId, roundNumber: 1);
        var raceB1Id = await GetRoundRaceIdAsync(f, tournamentBId, roundNumber: 1);

        var (ownerAUserId, ownerAId, horseAId) = await CreateApprovedOwnerHorseAsync(f, "jx-midround-horsea");
        await RegisterTournamentAsync(f, tournamentAId, ownerAId, horseAId, RegistrationStatus.Approved);
        var assignA = await f.RaceManagement.AssignHorseToRaceAsync(raceA1Id, new AssignHorseToRaceRequest { HorseId = horseAId });
        Assert.True(assignA.Result.Success, assignA.Result.Message);

        var (ownerBUserId, ownerBId, horseBId) = await CreateApprovedOwnerHorseAsync(f, "jx-midround-horseb");
        await RegisterTournamentAsync(f, tournamentBId, ownerBId, horseBId, RegistrationStatus.Approved);
        var assignB = await f.RaceManagement.AssignHorseToRaceAsync(raceB1Id, new AssignHorseToRaceRequest { HorseId = horseBId });
        Assert.True(assignB.Result.Success, assignB.Result.Message);

        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-midround-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceA1Id, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceB1Id, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceA1Id,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceB1Id,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(confirmB.Result.Success);
        Assert.Equal(409, confirmB.StatusCode);
    }

    [Fact]
    public async Task DifferentTournament_BackToBackSchedules_Succeeds()
    {
        // A ends exactly 11:00, B starts exactly 11:00 -> NOT an overlap (existing.Start < candidate.End
        // AND candidate.Start < existing.End; when o.Start == t.End neither strict inequality holds
        // for both, so this must succeed).
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var aStart = DateTime.UtcNow.AddDays(22).Date.AddHours(10); // 10:00
        var aEnd = aStart.AddHours(1); // 11:00
        var bStart = aEnd; // 11:00, back-to-back
        var bEnd = bStart.AddHours(1);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-backtoback-a", scheduledAt: aStart, scheduledEndAt: aEnd);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-backtoback-b", scheduledAt: bStart, scheduledEndAt: bEnd);
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-backtoback-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
    }

    [Fact]
    public async Task DifferentTournament_ExactOverlap_Blocked()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(22);
        var end = start.AddHours(1);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-exact-a", scheduledAt: start, scheduledEndAt: end);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-exact-b", scheduledAt: start, scheduledEndAt: end);
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-exact-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(confirmB.Result.Success);
        Assert.Equal(409, confirmB.StatusCode);
        Assert.Contains("lịch thi đấu trùng", confirmB.Result.Message);
    }

    [Fact]
    public async Task DifferentTournament_PartialOverlap_Blocked()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var aStart = DateTime.UtcNow.AddDays(22).Date.AddHours(10); // 10:00-11:00
        var aEnd = aStart.AddHours(1);
        var bStart = aStart.AddMinutes(30); // 10:30-11:30, partial overlap
        var bEnd = bStart.AddHours(1);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-partial-a", scheduledAt: aStart, scheduledEndAt: aEnd);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-partial-b", scheduledAt: bStart, scheduledEndAt: bEnd);
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-partial-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(confirmB.Result.Success);
        Assert.Equal(409, confirmB.StatusCode);
    }

    [Fact]
    public async Task DifferentTournament_OtherTournamentFinished_DoesNotBlock()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(22);
        var end = start.AddHours(1);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-finished-a", scheduledAt: start, scheduledEndAt: end);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-finished-b", scheduledAt: start, scheduledEndAt: end);
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-finished-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var tournamentAId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var tournamentA = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentAId);
        tournamentA.Status = TournamentStatus.Finished;
        tournamentA.IsActive = false; // project invariant: IsActive == true only when Status == Ongoing
        await f.Db.SaveChangesAsync();

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
    }

    [Fact]
    public async Task DifferentTournament_OtherTournamentCancelled_DoesNotBlock()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(22);
        var end = start.AddHours(1);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-cancelled-a", scheduledAt: start, scheduledEndAt: end);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-cancelled-b", scheduledAt: start, scheduledEndAt: end);
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-cancelled-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var tournamentAId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var tournamentA = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentAId);
        tournamentA.Status = TournamentStatus.Cancelled;
        tournamentA.IsActive = false; // project invariant: IsActive == true only when Status == Ongoing
        await f.Db.SaveChangesAsync();

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
    }

    [Fact]
    public async Task DifferentTournament_FinishedRaceInOngoingTournament_DoesNotBlock()
    {
        // Review micro-fix: a Finished Race no longer represents a live/future jockey schedule
        // obligation, even though its parent Tournament remains Ongoing and the Jockey's pairing
        // there is still official — the overlapping window must be excluded from the schedule
        // comparison entirely, not just from a Tournament-status filter.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(22);
        var end = start.AddHours(1);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-finishedrace-a", scheduledAt: start, scheduledEndAt: end);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-finishedrace-b", scheduledAt: start, scheduledEndAt: end);
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-finishedrace-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        // Tournament A moves to Ongoing (still an active lock candidate) and its Race concludes.
        var tournamentAId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var tournamentA = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentAId);
        tournamentA.Status = TournamentStatus.Ongoing;
        tournamentA.IsActive = true; // project invariant: IsActive == true only when Status == Ongoing
        var raceA = await f.Db.Races.SingleAsync(r => r.Id == raceAId);
        raceA.Status = RaceStatus.Finished;
        await f.Db.SaveChangesAsync();

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
    }

    [Fact]
    public async Task DifferentTournament_CancelledRaceInOngoingTournament_DoesNotBlock()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(22);
        var end = start.AddHours(1);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-cancelledrace-a", scheduledAt: start, scheduledEndAt: end);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-cancelledrace-b", scheduledAt: start, scheduledEndAt: end);
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-cancelledrace-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var tournamentAId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var tournamentA = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentAId);
        tournamentA.Status = TournamentStatus.Ongoing;
        tournamentA.IsActive = true; // project invariant: IsActive == true only when Status == Ongoing
        var raceA = await f.Db.Races.SingleAsync(r => r.Id == raceAId);
        raceA.Status = RaceStatus.Cancelled;
        await f.Db.SaveChangesAsync();

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
    }

    [Fact]
    public async Task DifferentTournament_FutureScheduledRaceStillBlocks()
    {
        // Proves the Finished/Cancelled filter did NOT accidentally exclude legitimate future/live
        // races: Tournament A is Ongoing with a still-Scheduled Race overlapping Tournament B — this
        // must still block.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(22);
        var end = start.AddHours(1);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-futurescheduled-a", scheduledAt: start, scheduledEndAt: end);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-futurescheduled-b", scheduledAt: start, scheduledEndAt: end);
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-futurescheduled-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var tournamentAId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var tournamentA = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentAId);
        tournamentA.Status = TournamentStatus.Ongoing;
        tournamentA.IsActive = true; // project invariant: IsActive == true only when Status == Ongoing
        // Race A deliberately stays Scheduled — still a live schedule obligation.
        await f.Db.SaveChangesAsync();

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(confirmB.Result.Success);
        Assert.Equal(409, confirmB.StatusCode);
    }

    [Fact]
    public async Task DifferentTournament_AcceptedInvitationOnly_DoesNotBlock()
    {
        // Accepted invitation alone (no official RaceEntry.JockeyId yet) in Tournament A must never
        // lock Tournament B, even with a fully overlapping schedule.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(22);
        var end = start.AddHours(1);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-acceptonly-a", scheduledAt: start, scheduledEndAt: end);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-acceptonly-b", scheduledAt: start, scheduledEndAt: end);
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-acceptonly-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        // Deliberately do NOT confirm A — only an Accepted invitation exists there.
        var horseService = BuildHorseService(f);
        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
        var invitationA = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationAId);
        Assert.Equal(JockeyInvitationStatus.Accepted, invitationA.Status);
    }

    // ── SAME TOURNAMENT — J3/J3.1 IMMUTABILITY PRESERVED ───────────────────────────────────────

    [Fact]
    public async Task SameTournament_JockeyDifferentHorse_StillBlocked()
    {
        // One Jockey, one Horse per Tournament — must still reject even with a clean, non-overlapping
        // gap between the two Races, proving schedule never overrides same-Tournament identity rules.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var raceAStart = DateTime.UtcNow.AddDays(22);
        var raceAEnd = raceAStart.AddMinutes(45);
        var raceBStart = raceAEnd.AddMinutes(30); // non-overlapping gap

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-samehorse-a", scheduledAt: raceAStart, scheduledEndAt: raceAEnd);
        var raceBId = await CreateAdditionalRaceInSameTournamentAsync(f, raceAId, "jx-samehorse-b", raceBStart, raceBStart.AddMinutes(45));
        var (ownerBUserId, horseBId) = await CreateAndAssignAdditionalHorseAsync(f, raceBId, "jx-samehorse-horseb");
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-samehorse-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(confirmB.Result.Success);
        Assert.Equal(409, confirmB.StatusCode);
        Assert.Contains("một ngựa khác", confirmB.Result.Message);
    }

    [Fact]
    public async Task SameTournament_HorseCannotSwitchJockey_StillBlocked()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "jx-noswitch");
        var (_, firstJockeyId, firstInvitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "jx-noswitch-a");
        var (_, _, secondInvitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "jx-noswitch-b");

        var horseService = BuildHorseService(f);
        var first = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = firstInvitationId });
        Assert.True(first.Result.Success, first.Result.Message);

        var second = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = secondInvitationId });

        Assert.False(second.Result.Success);
        Assert.Equal(409, second.StatusCode);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Equal(firstJockeyId, entry.JockeyId);
    }

    // ── DEDUPLICATION / NULL-CONVENTION / DRAFT ─────────────────────────────────────────────────

    [Fact]
    public async Task MultipleRaceEntriesSameOtherTournament_DeduplicatedCorrectly()
    {
        // Simulates the Q1 shape: the SAME Horse+Jockey has TWO official RaceEntries in Tournament A
        // (Round1 + a Round2 entry the same Jockey was already carried into). This must count as ONE
        // Tournament-level lock, not two, and must not throw or double-evaluate the overlap check.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var round1Start = DateTime.UtcNow.AddDays(10);
        var finalStart = DateTime.UtcNow.AddDays(20);
        var (tournamentAId, raceA1Id, raceAFinalId) = await CreateTwoRoundTournamentAsync(
            f, "jx-dedup-a", round1Start, round1Start.AddHours(1), finalStart, finalStart.AddHours(1));

        var (ownerAUserId, ownerAId, horseAId) = await CreateApprovedOwnerHorseAsync(f, "jx-dedup-horsea");
        await RegisterTournamentAsync(f, tournamentAId, ownerAId, horseAId, RegistrationStatus.Approved);
        var assignA = await f.RaceManagement.AssignHorseToRaceAsync(raceA1Id, new AssignHorseToRaceRequest { HorseId = horseAId });
        Assert.True(assignA.Result.Success, assignA.Result.Message);

        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-dedup-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceA1Id, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceA1Id,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        // Simulate Q1 propagation: a second official RaceEntry for the SAME Horse+Jockey in the
        // Final of Tournament A (Q1 itself is not exercised here — only its resulting data shape).
        var propagatedEntry = new RaceEntry
        {
            Id = Guid.NewGuid(),
            RaceId = raceAFinalId,
            HorseId = horseAId,
            JockeyId = jockeyId,
            Status = RegistrationStatus.Approved,
            OwnerConfirmed = true,
            JockeyConfirmed = true
        };
        f.Db.RaceEntries.Add(propagatedEntry);
        await f.Db.SaveChangesAsync();

        // Tournament B does not overlap Tournament A's schedule at all.
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(
            f, "jx-dedup-b", scheduledAt: DateTime.UtcNow.AddDays(90), scheduledEndAt: DateTime.UtcNow.AddDays(90).AddHours(1));
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
    }

    [Fact]
    public async Task MissingScheduledEndAt_WithinDefaultThirtyMinuteWindow_Blocked()
    {
        // Preserves the existing +30 minute convention (ScheduledEndAt ?? ScheduledAt.AddMinutes(30)):
        // Race A has no ScheduledEndAt, so its effective window is [Start, Start+30min). Race B starts
        // 15 minutes after Race A's Start, landing inside that default window -> must block.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var aStart = DateTime.UtcNow.AddDays(22);
        var bStart = aStart.AddMinutes(15);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-nullend-block-a", scheduledAt: aStart, scheduledEndAt: null);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-nullend-block-b", scheduledAt: bStart, scheduledEndAt: bStart.AddMinutes(30));
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-nullend-block-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(confirmB.Result.Success);
        Assert.Equal(409, confirmB.StatusCode);
    }

    [Fact]
    public async Task MissingScheduledEndAt_BackToBackAtDefaultThirtyMinutes_Succeeds()
    {
        // Race A has no ScheduledEndAt -> effective window [Start, Start+30min). Race B starts exactly
        // at Start+30min -> back-to-back, must NOT block.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var aStart = DateTime.UtcNow.AddDays(22);
        var bStart = aStart.AddMinutes(30);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-nullend-ok-a", scheduledAt: aStart, scheduledEndAt: null);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-nullend-ok-b", scheduledAt: bStart, scheduledEndAt: bStart.AddMinutes(30));
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-nullend-ok-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
    }

    [Fact]
    public async Task DraftOtherTournament_DoesNotBlock()
    {
        // Only Published/Ongoing other-Tournament pairings are relevant — a Draft Tournament must
        // never be treated as an active lock, even if it happens to already carry an official
        // RaceEntry.JockeyId (this cannot arise via Final Confirm itself, since Final Confirm requires
        // the target Tournament to be Published/Ongoing — so this simulates the defensive case).
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(22);
        var end = start.AddHours(1);

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "jx-draft-a", scheduledAt: start, scheduledEndAt: end);
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "jx-draft-b", scheduledAt: start, scheduledEndAt: end);
        var (_, jockeyId) = await CreateJockeyAsync(f, "jx-draft-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        // Tournament A regresses to Draft after the official assignment already exists.
        var tournamentAId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var tournamentA = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentAId);
        tournamentA.Status = TournamentStatus.Draft;
        tournamentA.IsActive = false; // project invariant: IsActive == true only when Status == Ongoing
        await f.Db.SaveChangesAsync();

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
    }

    // ── SHARED BUILDERS (duplicated per-file per repo convention; see J3OwnerFinalConfirmTests) ──

    private static HorseService BuildHorseService(RaceLifecycleTests.LifecycleFixture f)
        => new(
            new HorseRepository(f.Db),
            new OwnerRepository(f.Db),
            new JockeyRepository(f.Db),
            new RaceRepository(f.Db),
            new RaceEntryRepository(f.Db),
            new JockeyInvitationRepository(f.Db),
            f.UnitOfWork,
            new NoopNotificationService(),
            f.Db);

    private static async Task<(Guid ownerUserId, Guid ownerId, Guid horseId)> CreateApprovedOwnerHorseAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var ownerUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"owner-{tag}-{suffix}@test.local",
            PasswordHash = "x",
            FullName = $"Owner {tag}",
            Role = UserRole.HorseOwner,
            IsActive = true
        };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUser.Id, OwnerCode = $"OWN-{suffix.Substring(0, 8)}" };
        var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse {tag}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };

        f.Db.AddRange(ownerUser, owner, horse);
        await f.Db.SaveChangesAsync();
        return (ownerUser.Id, owner.Id, horse.Id);
    }

    private static async Task<(Guid jockeyUserId, Guid jockeyId)> CreateJockeyAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag, ApprovalStatus approvalStatus, bool userActive = true)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var jockeyUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"jockey-{tag}-{suffix}@test.local",
            PasswordHash = "x",
            FullName = $"Jockey {tag}",
            Role = UserRole.Jockey,
            IsActive = userActive
        };
        var jockey = new Jockey
        {
            Id = Guid.NewGuid(),
            UserId = jockeyUser.Id,
            LicenseNumber = $"LIC-{suffix.Substring(0, 8)}",
            ApprovalStatus = approvalStatus
        };

        f.Db.AddRange(jockeyUser, jockey);
        await f.Db.SaveChangesAsync();
        return (jockeyUser.Id, jockey.Id);
    }

    private static async Task RegisterTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid ownerId, Guid horseId, RegistrationStatus status)
    {
        f.Db.TournamentHorseRegistrations.Add(new TournamentHorseRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            OwnerId = ownerId,
            HorseId = horseId,
            Status = status,
            ApprovedAt = status == RegistrationStatus.Approved ? DateTime.UtcNow : null
        });
        await f.Db.SaveChangesAsync();
    }

    private static async Task<(Guid tournamentId, Guid raceId)> CreateRaceAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag,
        TournamentStatus tournamentStatus = TournamentStatus.Published,
        RaceStatus raceStatus = RaceStatus.Scheduled,
        DateTime? scheduledAt = null, DateTime? scheduledEndAt = null)
    {
        var start = scheduledAt ?? DateTime.UtcNow.AddDays(10);
        var end = scheduledEndAt ?? start.AddMinutes(60);
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = $"Tournament {tag}",
            StartDate = start.Date.AddDays(-1),
            EndDate = start.Date.AddDays(3),
            Status = tournamentStatus,
            IsActive = tournamentStatus == TournamentStatus.Ongoing, // project invariant: IsActive == true only when Status == Ongoing
            MaxRounds = 1,
            MaxParticipants = 8,
            RegistrationDeadline = start.Date.AddDays(-2)
        };
        var round = new Round
        {
            Id = Guid.NewGuid(),
            Name = $"Round {tag}",
            TournamentId = tournament.Id,
            RoundNumber = 1,
            AdvanceCount = 1,
            ScheduledStartDate = start,
            ScheduledEndDate = end
        };
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = $"Race {tag}",
            TournamentId = tournament.Id,
            RoundId = round.Id,
            ScheduledAt = start,
            ScheduledEndAt = scheduledEndAt, // preserved as-is (may be null) — caller controls the convention under test
            Status = raceStatus,
            MaxParticipants = 8,
            Distance = 1200
        };

        f.Db.AddRange(tournament, round, race);
        await f.Db.SaveChangesAsync();
        return (tournament.Id, race.Id);
    }

    private static async Task<(Guid ownerUserId, Guid ownerId, Guid horseId, Guid raceId)> CreateAssignedHorseAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag,
        TournamentStatus tournamentStatus = TournamentStatus.Published,
        RaceStatus raceStatus = RaceStatus.Scheduled,
        DateTime? scheduledAt = null, DateTime? scheduledEndAt = null)
    {
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, tag);
        var (tournamentId, raceId) = await CreateRaceAsync(f, tag, scheduledAt: scheduledAt, scheduledEndAt: scheduledEndAt);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);

        if (tournamentStatus != TournamentStatus.Published || raceStatus != RaceStatus.Scheduled)
        {
            var tournament = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentId);
            tournament.Status = tournamentStatus;
            tournament.IsActive = tournamentStatus == TournamentStatus.Ongoing; // project invariant
            var race = await f.Db.Races.SingleAsync(r => r.Id == raceId);
            race.Status = raceStatus;
            await f.Db.SaveChangesAsync();
        }

        return (ownerUserId, ownerId, horseId, raceId);
    }

    private static async Task<(Guid ownerUserId, Guid horseId)> CreateAndAssignAdditionalHorseAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid raceId, string tag)
    {
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, tag);
        var tournamentId = (await f.Db.Races.SingleAsync(r => r.Id == raceId)).TournamentId;
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);
        return (ownerUserId, horseId);
    }

    private static async Task<Guid> CreateAdditionalRaceInSameTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid existingRaceId, string tag,
        DateTime scheduledAt, DateTime scheduledEndAt, RaceStatus raceStatus = RaceStatus.Scheduled)
    {
        var existingRace = await f.Db.Races.SingleAsync(r => r.Id == existingRaceId);
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = $"Race {tag}",
            TournamentId = existingRace.TournamentId,
            RoundId = existingRace.RoundId,
            ScheduledAt = scheduledAt,
            ScheduledEndAt = scheduledEndAt,
            Status = raceStatus,
            MaxParticipants = 8,
            Distance = 1200
        };
        f.Db.Races.Add(race);
        await f.Db.SaveChangesAsync();
        return race.Id;
    }

    /// <summary>Builds a Tournament with Round1 (AdvanceCount=1) + Final/Round2 (AdvanceCount=0), each
    /// carrying one Race with the given schedule — the minimum shape needed to prove a J-CROSS
    /// overlap against a FUTURE round the target Race itself is not part of. Structure is created
    /// directly (bypassing CreateRoundAsync/CreateRaceAsync's Draft-only+Publish-readiness plumbing,
    /// matching J3OwnerFinalConfirmTests' own convention) since these tests need Tournaments that are
    /// already Published without going through the full multi-round Publish-readiness gate.</summary>
    private static async Task<(Guid tournamentId, Guid round1RaceId, Guid finalRaceId)> CreateTwoRoundTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag,
        DateTime round1Start, DateTime round1End, DateTime finalStart, DateTime finalEnd)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = $"Tournament {tag}",
            StartDate = round1Start.Date.AddDays(-1),
            EndDate = finalEnd.Date.AddDays(3),
            Status = TournamentStatus.Published,
            IsActive = false, // project invariant: IsActive == true only when Status == Ongoing
            MaxRounds = 2,
            MaxParticipants = 8,
            RegistrationDeadline = round1Start.Date.AddDays(-2)
        };
        var round1 = new Round
        {
            Id = Guid.NewGuid(), Name = $"Round1 {tag}", TournamentId = tournament.Id, RoundNumber = 1,
            AdvanceCount = 1, ScheduledStartDate = round1Start, ScheduledEndDate = round1End
        };
        var round2 = new Round
        {
            Id = Guid.NewGuid(), Name = $"Final {tag}", TournamentId = tournament.Id, RoundNumber = 2,
            AdvanceCount = 0, ScheduledStartDate = finalStart, ScheduledEndDate = finalEnd
        };
        var race1 = new Race
        {
            Id = Guid.NewGuid(), Name = $"Race1 {tag}", TournamentId = tournament.Id, RoundId = round1.Id,
            ScheduledAt = round1Start, ScheduledEndAt = round1End, Status = RaceStatus.Scheduled,
            MaxParticipants = 8, Distance = 1200
        };
        var raceFinal = new Race
        {
            Id = Guid.NewGuid(), Name = $"Final {tag}", TournamentId = tournament.Id, RoundId = round2.Id,
            ScheduledAt = finalStart, ScheduledEndAt = finalEnd, Status = RaceStatus.Scheduled,
            MaxParticipants = 8, Distance = 1200
        };

        f.Db.AddRange(tournament, round1, round2, race1, raceFinal);
        await f.Db.SaveChangesAsync();
        return (tournament.Id, race1.Id, raceFinal.Id);
    }

    /// <summary>Builds a 3-Round Tournament (Round1/Round2/Final) — used for the mid-round overlap
    /// scenario where the collision is neither the currently-confirmed Race nor the Final.</summary>
    private static async Task<Guid> CreateThreeRoundTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag,
        DateTime round1Start, DateTime round2Start, DateTime finalStart)
    {
        var round1End = round1Start.AddHours(1);
        var round2End = round2Start.AddHours(1);
        var finalEnd = finalStart.AddHours(1);

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = $"Tournament {tag}",
            StartDate = round1Start.Date.AddDays(-1),
            EndDate = finalEnd.Date.AddDays(3),
            Status = TournamentStatus.Published,
            IsActive = false, // project invariant: IsActive == true only when Status == Ongoing
            MaxRounds = 3,
            MaxParticipants = 8,
            RegistrationDeadline = round1Start.Date.AddDays(-2)
        };
        var round1 = new Round { Id = Guid.NewGuid(), Name = $"Round1 {tag}", TournamentId = tournament.Id, RoundNumber = 1, AdvanceCount = 2, ScheduledStartDate = round1Start, ScheduledEndDate = round1End };
        var round2 = new Round { Id = Guid.NewGuid(), Name = $"Round2 {tag}", TournamentId = tournament.Id, RoundNumber = 2, AdvanceCount = 1, ScheduledStartDate = round2Start, ScheduledEndDate = round2End };
        var round3 = new Round { Id = Guid.NewGuid(), Name = $"Final {tag}", TournamentId = tournament.Id, RoundNumber = 3, AdvanceCount = 0, ScheduledStartDate = finalStart, ScheduledEndDate = finalEnd };
        var race1 = new Race { Id = Guid.NewGuid(), Name = $"Race1 {tag}", TournamentId = tournament.Id, RoundId = round1.Id, ScheduledAt = round1Start, ScheduledEndAt = round1End, Status = RaceStatus.Scheduled, MaxParticipants = 8, Distance = 1200 };
        var race2 = new Race { Id = Guid.NewGuid(), Name = $"Race2 {tag}", TournamentId = tournament.Id, RoundId = round2.Id, ScheduledAt = round2Start, ScheduledEndAt = round2End, Status = RaceStatus.Scheduled, MaxParticipants = 8, Distance = 1200 };
        var race3 = new Race { Id = Guid.NewGuid(), Name = $"Final {tag}", TournamentId = tournament.Id, RoundId = round3.Id, ScheduledAt = finalStart, ScheduledEndAt = finalEnd, Status = RaceStatus.Scheduled, MaxParticipants = 8, Distance = 1200 };

        f.Db.AddRange(tournament, round1, round2, round3, race1, race2, race3);
        await f.Db.SaveChangesAsync();
        return tournament.Id;
    }

    private static async Task<Guid> GetRoundRaceIdAsync(RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, int roundNumber)
    {
        return (await f.Db.Races
                .Include(r => r.Round)
                .Where(r => r.TournamentId == tournamentId && r.Round!.RoundNumber == roundNumber)
                .SingleAsync())
            .Id;
    }

    private static async Task<(Guid jockeyUserId, Guid jockeyId, Guid invitationId)> InviteAndAcceptAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid ownerUserId, Guid horseId, Guid raceId, string tag)
    {
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, tag, ApprovalStatus.Approved);
        var invitationId = await InviteAndAcceptExistingJockeyAsync(f, ownerUserId, horseId, raceId, jockeyId, jockeyUserId);
        return (jockeyUserId, jockeyId, invitationId);
    }

    private static async Task<Guid> InviteAndAcceptExistingJockeyAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid ownerUserId, Guid horseId, Guid raceId, Guid jockeyId, Guid? jockeyUserIdOverride = null)
    {
        var jockeyUserId = jockeyUserIdOverride ?? await f.Db.Jockeys.Where(j => j.Id == jockeyId).Select(j => j.UserId).SingleAsync();
        var invite = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId,
            new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        var accept = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(accept.Result.Success, accept.Result.Message);
        return invitation.Id;
    }

    private static JockeyService BuildJockeyService(RaceLifecycleTests.LifecycleFixture f)
        => new(
            new UserRepository(f.Db),
            new JockeyRepository(f.Db),
            new JockeyInvitationRepository(f.Db),
            new RaceEntryRepository(f.Db),
            new RaceRepository(f.Db),
            f.UnitOfWork,
            new NoopNotificationService());

    private sealed class NoopNotificationService : INotificationService
    {
        public Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto)
            => Task.FromResult(ServiceResult<NotificationDto>.Ok(new NotificationDto()));

        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetUserNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));

        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));

        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));

        public Task<ServiceResult<NotificationDetailDto>> GetNotificationByIdAsync(Guid id)
            => Task.FromResult(ServiceResult<NotificationDetailDto>.Ok(new NotificationDetailDto()));

        public Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId)
            => Task.FromResult(ServiceResult<bool>.Ok(true));

        public Task<ServiceResult<bool>> MarkMultipleAsReadAsync(MarkNotificationsAsReadDto dto)
            => Task.FromResult(ServiceResult<bool>.Ok(true));

        public Task<ServiceResult<bool>> DeleteNotificationAsync(Guid notificationId)
            => Task.FromResult(ServiceResult<bool>.Ok(true));

        public Task<ServiceResult<bool>> DeleteAllNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<bool>.Ok(true));

        public Task<ServiceResult<int>> GetUnreadCountAsync(Guid userId)
            => Task.FromResult(ServiceResult<int>.Ok(0));

        public Task<ServiceResult<NotificationStatsDto>> GetNotificationStatsAsync(Guid userId)
            => Task.FromResult(ServiceResult<NotificationStatsDto>.Ok(new NotificationStatsDto()));

        public Task<ServiceResult<bool>> SendBulkNotificationsAsync(BulkNotificationDto dto)
            => Task.FromResult(ServiceResult<bool>.Ok(true));

        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));

        public Task ProcessUnsentNotificationsAsync()
            => Task.CompletedTask;
    }
}
