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

public class J3OwnerFinalConfirmTests
{
    [Fact]
    public async Task FinalConfirm_AcceptedInvitation_Succeeds_SetsJockeyId_JockeyConfirmed_KeepsOwnerConfirmed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j3-success");
        var (_, jockeyId, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j3-success");

        // OwnerConfirmed is a legacy, unrelated flag — flip it first to prove Final Confirm never touches it.
        var entryBefore = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        entryBefore.OwnerConfirmed = true;
        await f.Db.SaveChangesAsync();

        var result = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });

        Assert.True(result.Result.Success, result.Result.Message);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Equal(jockeyId, entry.JockeyId);
        Assert.True(entry.JockeyConfirmed);
        Assert.True(entry.OwnerConfirmed);
    }

    [Fact]
    public async Task FinalConfirm_PendingInvitation_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j3-pending");
        var (_, jockeyId) = await CreateJockeyAsync(f, "j3-pending", ApprovalStatus.Approved);
        var invite = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId,
            new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        var result = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitation.Id });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
    }

    [Fact]
    public async Task FinalConfirm_DeclinedInvitation_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j3-declined");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "j3-declined", ApprovalStatus.Approved);
        var horseService = BuildHorseService(f);
        var jockeyService = BuildJockeyService(f);

        var invite = await horseService.InviteJockeyAsync(ownerUserId, horseId,
            new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        var reject = await jockeyService.RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest { Accept = false });
        Assert.True(reject.Result.Success, reject.Result.Message);

        var result = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitation.Id });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task FinalConfirm_InvitationForDifferentHorseOrRace_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-mismatch-a");
        var (_, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "j3-mismatch-b");
        var (_, jockeyId, invitationIdForA) = await InviteAndAcceptAsync(f, ownerAUserId, horseAId, raceAId, "j3-mismatch");

        // Wrong Race for the same Horse.
        var wrongRace = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationIdForA });
        Assert.False(wrongRace.Result.Success);
        Assert.Equal(404, wrongRace.StatusCode);

        // Wrong Horse for the same Race.
        var wrongHorse = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerAUserId, horseBId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationIdForA });
        Assert.False(wrongHorse.Result.Success);
        Assert.Equal(404, wrongHorse.StatusCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task FinalConfirm_JockeyBecameIneligibleAfterAccept_Rejected(bool unapprove, bool deactivate)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, $"j3-ineligible-{unapprove}-{deactivate}");
        var (_, jockeyId, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, $"j3-ineligible-{unapprove}-{deactivate}");

        var jockey = await f.Db.Jockeys.Include(j => j.User).SingleAsync(j => j.Id == jockeyId);
        if (unapprove) jockey.ApprovalStatus = ApprovalStatus.Rejected;
        if (deactivate) jockey.User!.IsActive = false;
        await f.Db.SaveChangesAsync();

        var result = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
    }

    [Fact]
    public async Task FinalConfirm_RaceEntryAlreadyHasJockey_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j3-already-has-jockey");
        var (_, firstJockeyId, firstInvitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j3-already-a");
        var (_, _, secondInvitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j3-already-b");

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

    [Fact]
    public async Task FinalConfirm_SameJockeyAlreadyOfficialForAnotherHorse_SameRace_Returns409()
    {
        // Same Race implies same Tournament — this is the special case of the general
        // one-Jockey-one-Horse-per-Tournament rule (§4), not a Race-level exclusivity rule anymore.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceId) = await CreateAssignedHorseAsync(f, "j3-samerace-a");
        var (ownerBUserId, horseBId) = await CreateAndAssignAdditionalHorseAsync(f, raceId, "j3-samerace-b");

        var (_, jockeyId) = await CreateJockeyAsync(f, "j3-samerace-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceId, jockeyId);

        var horseService = BuildHorseService(f);
        var first = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(first.Result.Success, first.Result.Message);

        var second = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(second.Result.Success);
        Assert.Equal(409, second.StatusCode);
        Assert.Contains("ngựa khác", second.Result.Message);
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseBId);
        Assert.Null(entryB.JockeyId);
    }

    // ── One Horse — one Jockey per Tournament (J3 BUSINESS PIVOT §3) ───────────────────────────

    [Fact]
    public async Task FinalConfirm_HorseAlreadyPairedViaDifferentRaceEntryInSameTournament_Rejected()
    {
        // Simulates the forward-looking §7 shape: Horse A already has a Tournament-long pairing
        // established via raceA; a second RaceEntry for the SAME Horse exists in the same
        // Tournament (as a future-Qualification Round would create) with JockeyId still null.
        // Owner must never be allowed to pick a DIFFERENT Jockey for it — the Horse's pairing for
        // this Tournament is already decided.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceAId) = await CreateAssignedHorseAsync(f, "j3-horsepair-existing-a");
        var (_, _, invitationAId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceAId, "j3-horsepair-existing-jockeya");

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var raceBId = await CreateAdditionalRaceInSameTournamentAsync(f, raceAId, "j3-horsepair-existing-b",
            DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(30).AddMinutes(60));
        await CreateRawRaceEntryAsync(f, raceBId, horseId); // same Horse, JockeyId still null on THIS entry
        var (_, _, invitationBId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceBId, "j3-horsepair-existing-jockeyb");

        var second = await horseService.FinalConfirmJockeyAsync(ownerUserId, horseId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(second.Result.Success);
        Assert.Equal(409, second.StatusCode);
        Assert.Contains("giải đấu", second.Result.Message);
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceBId && e.HorseId == horseId);
        Assert.Null(entryB.JockeyId); // still not overwritten — Jockey A's pairing stands
    }

    // ── One Jockey — one Horse per Tournament (J3 BUSINESS PIVOT §4) ───────────────────────────

    [Fact]
    public async Task FinalConfirm_JockeyAlreadyPairedWithDifferentHorse_SameTournament_NonOverlappingRace_StillRejected()
    {
        // Schedule no longer matters at all for this rule — even a clean, non-overlapping gap
        // between the two Races must still reject, because one Jockey can only ever hold ONE
        // official Horse per Tournament now.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var raceAStart = DateTime.UtcNow.AddDays(20);
        var raceAEnd = raceAStart.AddMinutes(45);
        var raceBStart = raceAEnd.AddMinutes(30); // clean, non-overlapping gap

        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-jockeypair-nooverlap-a", scheduledAt: raceAStart, scheduledEndAt: raceAEnd);
        var raceBId = await CreateAdditionalRaceInSameTournamentAsync(f, raceAId, "j3-jockeypair-nooverlap-b", raceBStart, raceBStart.AddMinutes(45));
        var (ownerBUserId, horseBId) = await CreateAndAssignAdditionalHorseAsync(f, raceBId, "j3-jockeypair-nooverlap-horseb");
        var (_, jockeyId) = await CreateJockeyAsync(f, "j3-jockeypair-nooverlap-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var first = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(first.Result.Success, first.Result.Message);

        var second = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(second.Result.Success);
        Assert.Equal(409, second.StatusCode);
        Assert.Contains("một ngựa khác", second.Result.Message);
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceBId && e.HorseId == horseBId);
        Assert.Null(entryB.JockeyId);
    }

    [Fact]
    public async Task FinalConfirm_JockeyAlreadyPairedWithDifferentHorse_SameTournament_DifferentRound_StillRejected()
    {
        // Round identity is irrelevant to the rule — a pairing in Round 1 still blocks a different
        // Horse in Round 2 of the SAME Tournament for that Jockey.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-jockeypair-round-a");
        var (_, jockeyId) = await CreateJockeyAsync(f, "j3-jockeypair-round-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var tournamentId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var raceBId = await CreateRaceInRoundAsync(f, tournamentId, roundNumber: 2, "j3-jockeypair-round-b",
            DateTime.UtcNow.AddDays(31), DateTime.UtcNow.AddDays(31).AddMinutes(60));
        var (ownerBUserId, ownerBId, horseBId) = await CreateApprovedOwnerHorseAsync(f, "j3-jockeypair-round-horseb");
        await RegisterTournamentAsync(f, tournamentId, ownerBId, horseBId, RegistrationStatus.Approved);
        await CreateRawRaceEntryAsync(f, raceBId, horseBId); // future-Qualification-shaped Round 2 entry
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var second = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(second.Result.Success);
        Assert.Equal(409, second.StatusCode);
        Assert.Contains("một ngựa khác", second.Result.Message);
    }

    // ── Schedule completeness no longer blocks Final Confirm (J3 BUSINESS PIVOT §6) ─────────────

    [Fact]
    public async Task FinalConfirm_MissingScheduledEndAt_DoesNotIndependentlyBlockInitialPairing_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j3-noschedule-check");
        var (_, jockeyId, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j3-noschedule-check");

        var race = await f.Db.Races.SingleAsync(r => r.Id == raceId);
        race.ScheduledEndAt = null;
        await f.Db.SaveChangesAsync();

        var result = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });

        Assert.True(result.Result.Success, result.Result.Message);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Equal(jockeyId, entry.JockeyId);
    }

    // ── One-active-Tournament-per-Jockey lock (J3 BUSINESS PIVOT §5, preserved) ────────────────

    [Fact]
    public async Task FinalConfirm_AcceptedInvitationsInMultipleTournaments_NoOfficialAssignmentsYet_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-multi-accept-a");
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-multi-accept-b");
        var (ownerCUserId, _, horseCId, raceCId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-multi-accept-c");
        var (_, jockeyId) = await CreateJockeyAsync(f, "j3-crosstourn-multi-accept-shared", ApprovalStatus.Approved);

        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);
        var invitationCId = await InviteAndAcceptExistingJockeyAsync(f, ownerCUserId, horseCId, raceCId, jockeyId);

        // No RaceEntry.JockeyId is set anywhere yet — three Accepted invitations across three
        // different (Published) Tournaments do not lock the Jockey to any of them.
        var confirmB = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceBId && e.HorseId == horseBId);
        Assert.Equal(jockeyId, entryB.JockeyId);

        var invitationA = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationAId);
        var invitationC = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationCId);
        Assert.Equal(JockeyInvitationStatus.Accepted, invitationA.Status);
        Assert.Equal(JockeyInvitationStatus.Accepted, invitationC.Status);
    }

    [Fact]
    public async Task FinalConfirm_OfficialInPublishedTournamentA_BlocksDifferentPublishedTournamentB()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-pub-a");
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-pub-b");
        var (_, jockeyId) = await CreateJockeyAsync(f, "j3-crosstourn-pub-shared", ApprovalStatus.Approved);
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
        Assert.Contains("giải đấu khác", confirmB.Result.Message);
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceBId && e.HorseId == horseBId);
        Assert.Null(entryB.JockeyId);

        // Tournament B's invitation stays Accepted — the lock never rewrites J2 invitation history.
        var invitationB = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationBId);
        Assert.Equal(JockeyInvitationStatus.Accepted, invitationB.Status);
    }

    [Fact]
    public async Task FinalConfirm_OfficialInOngoingTournamentA_BlocksDifferentPublishedTournamentB()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-ongoing-a");
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-ongoing-b");
        var (_, jockeyId) = await CreateJockeyAsync(f, "j3-crosstourn-ongoing-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        // Tournament A moves from Published to Ongoing after the official assignment already
        // exists — Ongoing still locks the Jockey just as Published does.
        var tournamentAId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var tournamentA = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentAId);
        tournamentA.Status = TournamentStatus.Ongoing;
        await f.Db.SaveChangesAsync();

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.False(confirmB.Result.Success);
        Assert.Equal(409, confirmB.StatusCode);
    }

    [Fact]
    public async Task FinalConfirm_PreviousTournamentAFinished_DifferentPublishedTournamentB_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-finished-a");
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-finished-b");
        var (_, jockeyId) = await CreateJockeyAsync(f, "j3-crosstourn-finished-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        // Tournament A concludes — its historical RaceEntry.JockeyId is left untouched, but a
        // Finished Tournament no longer locks the Jockey for a NEW Final Confirm elsewhere.
        var tournamentAId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var tournamentA = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentAId);
        tournamentA.Status = TournamentStatus.Finished;
        await f.Db.SaveChangesAsync();

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceBId && e.HorseId == horseBId);
        Assert.Equal(jockeyId, entryB.JockeyId);

        // Historical assignment in the now-Finished Tournament A is untouched.
        var entryA = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceAId && e.HorseId == horseAId);
        Assert.Equal(jockeyId, entryA.JockeyId);
    }

    [Fact]
    public async Task FinalConfirm_PreviousTournamentACancelled_DifferentPublishedTournamentB_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-cancelled-a");
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseAsync(f, "j3-crosstourn-cancelled-b");
        var (_, jockeyId) = await CreateJockeyAsync(f, "j3-crosstourn-cancelled-shared", ApprovalStatus.Approved);
        var invitationAId = await InviteAndAcceptExistingJockeyAsync(f, ownerAUserId, horseAId, raceAId, jockeyId);
        var invitationBId = await InviteAndAcceptExistingJockeyAsync(f, ownerBUserId, horseBId, raceBId, jockeyId);

        var horseService = BuildHorseService(f);
        var confirmA = await horseService.FinalConfirmJockeyAsync(ownerAUserId, horseAId, raceAId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationAId });
        Assert.True(confirmA.Result.Success, confirmA.Result.Message);

        var tournamentAId = (await f.Db.Races.SingleAsync(r => r.Id == raceAId)).TournamentId;
        var tournamentA = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentAId);
        tournamentA.Status = TournamentStatus.Cancelled;
        await f.Db.SaveChangesAsync();

        var confirmB = await horseService.FinalConfirmJockeyAsync(ownerBUserId, horseBId, raceBId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationBId });

        Assert.True(confirmB.Result.Success, confirmB.Result.Message);
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceBId && e.HorseId == horseBId);
        Assert.Equal(jockeyId, entryB.JockeyId);
    }

    [Theory]
    [InlineData(TournamentStatus.Draft)]
    [InlineData(TournamentStatus.Finished)]
    [InlineData(TournamentStatus.Cancelled)]
    public async Task FinalConfirm_ClosedTournamentStatus_Rejected(TournamentStatus tournamentStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, $"j3-tstatus-{tournamentStatus}");
        var (_, _, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, $"j3-tstatus-{tournamentStatus}");

        var tournamentId = (await f.Db.Races.SingleAsync(r => r.Id == raceId)).TournamentId;
        var tournament = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentId);
        tournament.Status = tournamentStatus;
        await f.Db.SaveChangesAsync();

        var result = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Theory]
    [InlineData(RaceStatus.InProgress)]
    [InlineData(RaceStatus.Finished)]
    [InlineData(RaceStatus.Cancelled)]
    public async Task FinalConfirm_ClosedRaceStatus_Rejected(RaceStatus raceStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, $"j3-rstatus-{raceStatus}");
        var (_, _, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, $"j3-rstatus-{raceStatus}");

        var race = await f.Db.Races.SingleAsync(r => r.Id == raceId);
        race.Status = raceStatus;
        await f.Db.SaveChangesAsync();

        var result = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Theory]
    [InlineData(RaceStatus.Scheduled)]
    [InlineData(RaceStatus.RegistrationOpen)]
    [InlineData(RaceStatus.RegistrationClosed)]
    public async Task FinalConfirm_PreStartRaceStatus_Allowed(RaceStatus raceStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, $"j3-allowed-{raceStatus}");
        var (_, _, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, $"j3-allowed-{raceStatus}");

        var race = await f.Db.Races.SingleAsync(r => r.Id == raceId);
        race.Status = raceStatus;
        await f.Db.SaveChangesAsync();

        var result = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });

        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task FinalConfirm_DoesNotMutateOtherInvitations()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j3-no-side-effects");
        var (_, chosenJockeyId, chosenInvitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j3-side-chosen");
        var (_, otherAcceptedJockeyId, otherAcceptedInvitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j3-side-other-accepted");
        var (_, pendingJockeyId) = await CreateJockeyAsync(f, "j3-side-pending", ApprovalStatus.Approved);
        var pendingInvite = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId,
            new JockeyInvitationCreateRequest { JockeyId = pendingJockeyId, RaceId = raceId });
        Assert.True(pendingInvite.Result.Success, pendingInvite.Result.Message);
        var pendingInvitationId = (await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == pendingJockeyId && i.RaceId == raceId)).Id;

        var result = await BuildHorseService(f).FinalConfirmJockeyAsync(ownerUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = chosenInvitationId });
        Assert.True(result.Result.Success, result.Result.Message);

        var otherAccepted = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == otherAcceptedInvitationId);
        var pending = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == pendingInvitationId);
        Assert.Equal(JockeyInvitationStatus.Accepted, otherAccepted.Status);
        Assert.Equal(JockeyInvitationStatus.Pending, pending.Status);
    }

    [Fact]
    public async Task FinalConfirm_HorseNotOwnedByCaller_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseAsync(f, "j3-wrong-owner");
        var (_, _, invitationId) = await InviteAndAcceptAsync(f, ownerUserId, horseId, raceId, "j3-wrong-owner");
        var (intruderUserId, _, _) = await CreateApprovedOwnerHorseAsync(f, "j3-wrong-owner-intruder");

        var result = await BuildHorseService(f).FinalConfirmJockeyAsync(intruderUserId, horseId, raceId,
            new OwnerFinalConfirmJockeyRequest { InvitationId = invitationId });

        Assert.False(result.Result.Success);
        Assert.Equal(404, result.StatusCode);
    }

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

    private static JockeyService BuildJockeyService(RaceLifecycleTests.LifecycleFixture f)
        => new(
            new UserRepository(f.Db),
            new JockeyRepository(f.Db),
            new JockeyInvitationRepository(f.Db),
            new RaceEntryRepository(f.Db),
            new RaceRepository(f.Db),
            f.UnitOfWork,
            new NoopNotificationService());

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
            StartDate = start.Date,
            EndDate = start.Date.AddDays(3),
            Status = tournamentStatus,
            IsActive = true,
            MaxRounds = 1,
            MaxParticipants = 8,
            RegistrationDeadline = start.Date.AddDays(-1)
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
            ScheduledEndAt = end,
            Status = raceStatus,
            MaxParticipants = 8,
            Distance = 1200
        };

        f.Db.AddRange(tournament, round, race);
        await f.Db.SaveChangesAsync();
        return (tournament.Id, race.Id);
    }

    /// <summary>
    /// Creates an Owner + Horse, assigns the Horse to a fresh Round1 Race (via the same
    /// AssignHorseToRaceAsync path Admin uses), then applies the caller's desired lifecycle state —
    /// mirrors J1JockeyEligibilityTests' CreateAssignedHorseForInvitationAsync but also allows an
    /// explicit Race schedule for the J3 overlap tests.
    /// </summary>
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
            var race = await f.Db.Races.SingleAsync(r => r.Id == raceId);
            race.Status = raceStatus;
            await f.Db.SaveChangesAsync();
        }

        return (ownerUserId, ownerId, horseId, raceId);
    }

    /// <summary>Assigns an additional Horse, owned by a NEW Owner (one Owner may hold only one active
    /// TournamentHorseRegistration per Tournament — IX_TournamentHorseRegistrations_OwnerId_TournamentId_Active —
    /// so a second horse in the same Tournament/Race needs its own Owner), into an already-existing Race.</summary>
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

    /// <summary>Creates an additional Race inside the SAME Tournament + Round1 as an existing Race — a
    /// Round may hold multiple Races (heats), and only Round1 is eligible for direct
    /// AssignHorseToRaceAsync, so this is how "different Race, same Tournament" scenarios are built
    /// without accidentally exercising the one-active-Tournament-per-Jockey lock instead.</summary>
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

    /// <summary>Creates a Race under a brand-new Round with the given RoundNumber in an existing
    /// Tournament — used to build "different Round, same Tournament" scenarios that
    /// AssignHorseToRaceAsync itself would reject (it only accepts Round 1), simulating the
    /// RaceEntry shape a future Qualification Round would produce.</summary>
    private static async Task<Guid> CreateRaceInRoundAsync(
        RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, int roundNumber, string tag,
        DateTime scheduledAt, DateTime scheduledEndAt)
    {
        var round = new Round
        {
            Id = Guid.NewGuid(),
            Name = $"Round {roundNumber} {tag}",
            TournamentId = tournamentId,
            RoundNumber = roundNumber,
            AdvanceCount = 1,
            ScheduledStartDate = scheduledAt,
            ScheduledEndDate = scheduledEndAt
        };
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = $"Race {tag}",
            TournamentId = tournamentId,
            RoundId = round.Id,
            ScheduledAt = scheduledAt,
            ScheduledEndAt = scheduledEndAt,
            Status = RaceStatus.Scheduled,
            MaxParticipants = 8,
            Distance = 1200
        };
        f.Db.AddRange(round, race);
        await f.Db.SaveChangesAsync();
        return race.Id;
    }

    /// <summary>Inserts a RaceEntry directly (bypassing AssignHorseToRaceAsync's business rules,
    /// e.g. "Horse already in an active Race elsewhere") to simulate the shape a future
    /// Qualification Round would produce for a Horse that already has a Tournament pairing —
    /// JockeyId starts null on this specific entry.</summary>
    private static async Task<Guid> CreateRawRaceEntryAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId, Guid horseId)
    {
        // O1: any valid Admin-lineage RaceEntry (including a future-Qualification-created one)
        // has OwnerConfirmed = true — Owner consent already happened at Tournament registration.
        var entry = new RaceEntry
        {
            Id = Guid.NewGuid(),
            RaceId = raceId,
            HorseId = horseId,
            JockeyId = null,
            Status = RegistrationStatus.Approved,
            OwnerConfirmed = true,
            JockeyConfirmed = false
        };
        f.Db.RaceEntries.Add(entry);
        await f.Db.SaveChangesAsync();
        return entry.Id;
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
