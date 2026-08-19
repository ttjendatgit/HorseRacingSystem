using System;
using System.Collections.Generic;
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

public class J1JockeyEligibilityTests
{
    [Fact]
    public async Task AdminAssignHorse_RoundOneCreatesEntryWithoutJockeyOrConfirmations()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "admin-null");
        var (tournamentId, raceId) = await CreateRaceAsync(f, "admin-null", roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest
        {
            HorseId = horseId
        });

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(201, result.StatusCode);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.OwnerConfirmed);
        Assert.False(entry.JockeyConfirmed);
        Assert.Equal(RegistrationStatus.Approved, entry.Status);
        Assert.NotEqual(Guid.Empty, ownerUserId);
    }

    [Fact]
    public async Task AdminAssignHorse_LegacyJockeyIdIsIgnored()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "legacy-jockey");
        var (_, jockeyId) = await CreateJockeyAsync(f, "legacy-jockey", ApprovalStatus.Approved);
        var (tournamentId, raceId) = await CreateRaceAsync(f, "legacy-jockey", roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest
        {
            HorseId = horseId,
            JockeyId = jockeyId
        });

        Assert.True(result.Result.Success, result.Result.Message);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.OwnerConfirmed);
        Assert.False(entry.JockeyConfirmed);
    }

    [Fact]
    public async Task AdminAssignHorse_DuplicateRaceEntryStillRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "duplicate");
        var (tournamentId, raceId) = await CreateRaceAsync(f, "duplicate", roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var first = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(first.Result.Success, first.Result.Message);

        var duplicate = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.False(duplicate.Result.Success);
        Assert.Equal(400, duplicate.StatusCode);
        Assert.Equal(1, await f.Db.RaceEntries.CountAsync(e => e.RaceId == raceId && e.HorseId == horseId));
    }

    [Fact]
    public async Task AdminAssignHorse_RoundGreaterThanOneStillRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "round-two");
        var (tournamentId, raceId) = await CreateRaceAsync(f, "round-two", roundNumber: 2);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await f.Db.RaceEntries.Where(e => e.RaceId == raceId && e.HorseId == horseId).ToListAsync());
    }

    [Fact]
    public async Task AvailableJockeys_OnlyApprovedJockeyAppears()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, _) = await CreateApprovedOwnerHorseAsync(f, "availability-owner");
        var (_, approvedJockeyId) = await CreateJockeyAsync(f, "availability-approved", ApprovalStatus.Approved);
        var (_, pendingJockeyId) = await CreateJockeyAsync(f, "availability-pending", ApprovalStatus.Pending);
        var (_, rejectedJockeyId) = await CreateJockeyAsync(f, "availability-rejected", ApprovalStatus.Rejected);

        var result = await BuildJockeyService(f).GetAvailableJockeysAsync(ownerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        var jockeys = Assert.IsAssignableFrom<IEnumerable<JockeyListResponse>>(result.Result.Data);
        var ids = jockeys.Select(jockey => jockey.Id).ToHashSet();
        Assert.Contains(approvedJockeyId, ids);
        Assert.DoesNotContain(pendingJockeyId, ids);
        Assert.DoesNotContain(rejectedJockeyId, ids);
    }

    [Theory]
    [InlineData(ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task OwnerInviteJockey_UnapprovedJockeyRejected(ApprovalStatus approvalStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, $"invite-{approvalStatus}");
        var (_, jockeyId) = await CreateJockeyAsync(f, $"invite-{approvalStatus}", approvalStatus);
        var (tournamentId, raceId) = await CreateRaceAsync(f, $"invite-{approvalStatus}", roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await f.Db.JockeyInvitations.Where(i => i.HorseId == horseId && i.JockeyId == jockeyId).ToListAsync());
    }

    [Fact]
    public async Task OwnerInviteJockey_InactiveApprovedJockeyRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, "invite-inactive-approved");
        var (_, jockeyId) = await CreateJockeyAsync(f, "invite-inactive-approved", ApprovalStatus.Approved, userActive: false);
        var (tournamentId, raceId) = await CreateRaceAsync(f, "invite-inactive-approved", roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);

        var result = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await f.Db.JockeyInvitations.Where(i => i.HorseId == horseId && i.JockeyId == jockeyId).ToListAsync());
    }
    [Theory]
    [InlineData(ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task JockeyRespondInvitation_UnapprovedJockeyCannotAcceptNewInvitation(ApprovalStatus approvalStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, $"respond-{approvalStatus}");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, $"respond-{approvalStatus}", approvalStatus);
        var invitation = new JockeyInvitation
        {
            Id = Guid.NewGuid(),
            HorseId = horseId,
            JockeyId = jockeyId,
            RaceId = raceId,
            Status = JockeyInvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        f.Db.JockeyInvitations.Add(invitation);
        await f.Db.SaveChangesAsync();

        var result = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest
        {
            Accept = true
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        var storedInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Pending, storedInvitation.Status);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.JockeyConfirmed);
    }

    [Fact]
    public async Task JockeyRespondInvitation_InactiveApprovedJockeyCannotAccept()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "respond-inactive-approved");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "respond-inactive-approved", ApprovalStatus.Approved, userActive: false);
        var invitation = new JockeyInvitation
        {
            Id = Guid.NewGuid(),
            HorseId = horseId,
            JockeyId = jockeyId,
            RaceId = raceId,
            Status = JockeyInvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        f.Db.JockeyInvitations.Add(invitation);
        await f.Db.SaveChangesAsync();

        var result = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest
        {
            Accept = true
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        var storedInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Pending, storedInvitation.Status);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.JockeyConfirmed);
    }
    [Fact]
    public async Task JockeyAccept_DoesNotAssignOfficialRaceEntry()
    {
        // J2: accepting an invitation means "willing to ride", not final assignment.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "approved-flow");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "approved-flow", ApprovalStatus.Approved);

        // Prove Accept leaves OwnerConfirmed untouched in either direction, not just at its default.
        var entryBefore = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        entryBefore.OwnerConfirmed = true;
        await f.Db.SaveChangesAsync();

        var invite = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId,
            Message = "Ready to race"
        });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        var accept = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest
        {
            Accept = true
        });

        Assert.True(accept.Result.Success, accept.Result.Message);
        var storedInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Accepted, storedInvitation.Status);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.JockeyConfirmed);
        Assert.True(entry.OwnerConfirmed);
    }

    [Fact]
    public async Task JockeyReject_OnlyChangesInvitationStatus()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "reject-flow");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "reject-flow", ApprovalStatus.Approved);

        var entryBefore = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        entryBefore.OwnerConfirmed = true;
        await f.Db.SaveChangesAsync();

        var invite = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        var reject = await BuildJockeyService(f).RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest
        {
            Accept = false
        });

        Assert.True(reject.Result.Success, reject.Result.Message);
        var storedInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Declined, storedInvitation.Status);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.JockeyConfirmed);
        Assert.True(entry.OwnerConfirmed);
    }

    [Fact]
    public async Task OwnerInviteJockey_MultipleDifferentJockeysForSameHorseRace_BothCoexistAndBothCanAccept()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "multi-jockey");
        var (jockeyAUserId, jockeyAId) = await CreateJockeyAsync(f, "multi-jockey-a", ApprovalStatus.Approved);
        var (jockeyBUserId, jockeyBId) = await CreateJockeyAsync(f, "multi-jockey-b", ApprovalStatus.Approved);
        var horseService = BuildHorseService(f);
        var jockeyService = BuildJockeyService(f);

        var inviteA = await horseService.InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyAId,
            RaceId = raceId
        });
        Assert.True(inviteA.Result.Success, inviteA.Result.Message);

        var inviteB = await horseService.InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyBId,
            RaceId = raceId
        });
        Assert.True(inviteB.Result.Success, inviteB.Result.Message);

        var invitationA = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyAId && i.RaceId == raceId);
        var invitationB = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyBId && i.RaceId == raceId);
        Assert.NotEqual(invitationA.Id, invitationB.Id);

        // Jockey A accepts first — this must not block Jockey B from also accepting for the same Horse+Race.
        var acceptA = await jockeyService.RespondInvitationAsync(jockeyAUserId, invitationA.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(acceptA.Result.Success, acceptA.Result.Message);

        var acceptB = await jockeyService.RespondInvitationAsync(jockeyBUserId, invitationB.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(acceptB.Result.Success, acceptB.Result.Message);

        var storedA = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationA.Id);
        var storedB = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationB.Id);
        Assert.Equal(JockeyInvitationStatus.Accepted, storedA.Status);
        Assert.Equal(JockeyInvitationStatus.Accepted, storedB.Status);

        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
        Assert.False(entry.JockeyConfirmed);
    }

    [Fact]
    public async Task OwnerInviteJockey_DuplicateActiveInvitationSameJockeySameRace_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "duplicate-invite");
        var (_, jockeyId) = await CreateJockeyAsync(f, "duplicate-invite", ApprovalStatus.Approved);
        var horseService = BuildHorseService(f);

        var first = await horseService.InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });
        Assert.True(first.Result.Success, first.Result.Message);

        var duplicate = await horseService.InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });

        Assert.False(duplicate.Result.Success);
        Assert.Equal(409, duplicate.StatusCode);
        Assert.Equal(1, await f.Db.JockeyInvitations.CountAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId));
    }

    [Fact]
    public async Task SameJockey_CanAcceptOverlappingInvitationsFromDifferentOwners()
    {
        // J2: acceptance is not final assignment, so exclusivity/schedule-conflict checks
        // must not block a jockey from holding multiple accepted invitations at once.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerAUserId, _, horseAId, raceAId) = await CreateAssignedHorseForInvitationAsync(f, "cross-owner-a");
        var (ownerBUserId, _, horseBId, raceBId) = await CreateAssignedHorseForInvitationAsync(f, "cross-owner-b");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "cross-owner-shared", ApprovalStatus.Approved);
        var horseService = BuildHorseService(f);
        var jockeyService = BuildJockeyService(f);

        var inviteFromOwnerA = await horseService.InviteJockeyAsync(ownerAUserId, horseAId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceAId
        });
        Assert.True(inviteFromOwnerA.Result.Success, inviteFromOwnerA.Result.Message);
        var invitationA = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseAId && i.JockeyId == jockeyId && i.RaceId == raceAId);

        var acceptA = await jockeyService.RespondInvitationAsync(jockeyUserId, invitationA.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(acceptA.Result.Success, acceptA.Result.Message);

        // Owner B's race is scheduled at (effectively) the same time as Owner A's — J1's
        // CreateRaceAsync derives ScheduledAt purely from roundNumber, so two round-1 races
        // created moments apart overlap. Owner B must still be able to invite this jockey.
        var inviteFromOwnerB = await horseService.InviteJockeyAsync(ownerBUserId, horseBId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceBId
        });
        Assert.True(inviteFromOwnerB.Result.Success, inviteFromOwnerB.Result.Message);
        var invitationB = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseBId && i.JockeyId == jockeyId && i.RaceId == raceBId);

        var acceptB = await jockeyService.RespondInvitationAsync(jockeyUserId, invitationB.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(acceptB.Result.Success, acceptB.Result.Message);

        var storedA = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationA.Id);
        var storedB = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationB.Id);
        Assert.Equal(JockeyInvitationStatus.Accepted, storedA.Status);
        Assert.Equal(JockeyInvitationStatus.Accepted, storedB.Status);

        var entryA = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceAId && e.HorseId == horseAId);
        var entryB = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceBId && e.HorseId == horseBId);
        Assert.Null(entryA.JockeyId);
        Assert.Null(entryB.JockeyId);
    }

    [Fact]
    public async Task RemoveJockey_CancelsOnlyTheTargetedInvitation()
    {
        // J2 follow-up: multiple active invitations per Horse+Race means cancel must be
        // pinned by InvitationId — never picked arbitrarily with FirstOrDefault.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "cancel-target");
        var (jockeyAUserId, jockeyAId) = await CreateJockeyAsync(f, "cancel-target-a", ApprovalStatus.Approved);
        var (_, jockeyBId) = await CreateJockeyAsync(f, "cancel-target-b", ApprovalStatus.Approved);
        var horseService = BuildHorseService(f);
        var jockeyService = BuildJockeyService(f);

        var inviteA = await horseService.InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest { JockeyId = jockeyAId, RaceId = raceId });
        Assert.True(inviteA.Result.Success, inviteA.Result.Message);
        var inviteB = await horseService.InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest { JockeyId = jockeyBId, RaceId = raceId });
        Assert.True(inviteB.Result.Success, inviteB.Result.Message);

        var invitationA = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyAId && i.RaceId == raceId);
        var invitationB = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyBId && i.RaceId == raceId);

        var acceptA = await jockeyService.RespondInvitationAsync(jockeyAUserId, invitationA.Id, new JockeyInvitationRespondRequest { Accept = true });
        Assert.True(acceptA.Result.Success, acceptA.Result.Message);
        // Jockey B is left Pending — never responded to.

        var cancelB = await horseService.RemoveJockeyAsync(ownerUserId, horseId, raceId, new JockeyRemovalRequest
        {
            InvitationId = invitationB.Id,
            Reason = "Không phù hợp lịch"
        });
        Assert.True(cancelB.Result.Success, cancelB.Result.Message);

        var storedA = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationA.Id);
        var storedB = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitationB.Id);
        Assert.Equal(JockeyInvitationStatus.Accepted, storedA.Status);
        Assert.Equal(JockeyInvitationStatus.Declined, storedB.Status);
        Assert.Equal("Không phù hợp lịch", storedB.ResponseNote);
    }

    [Fact]
    public async Task RemoveJockey_OtherOwnerCannotCancelInvitation()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "cancel-other-owner");
        var (_, jockeyId) = await CreateJockeyAsync(f, "cancel-other-owner", ApprovalStatus.Approved);
        var horseService = BuildHorseService(f);

        var invite = await horseService.InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        var (otherOwnerUserId, _, _) = await CreateApprovedOwnerHorseAsync(f, "cancel-other-owner-intruder");

        var cancel = await horseService.RemoveJockeyAsync(otherOwnerUserId, horseId, raceId, new JockeyRemovalRequest
        {
            InvitationId = invitation.Id,
            Reason = "Not my horse"
        });

        Assert.False(cancel.Result.Success);
        Assert.Equal(404, cancel.StatusCode);
        var stored = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task RemoveJockey_UnknownInvitationIdRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "cancel-unknown-invite");
        var (_, jockeyId) = await CreateJockeyAsync(f, "cancel-unknown-invite", ApprovalStatus.Approved);
        var horseService = BuildHorseService(f);

        var invite = await horseService.InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        var cancel = await horseService.RemoveJockeyAsync(ownerUserId, horseId, raceId, new JockeyRemovalRequest
        {
            InvitationId = Guid.NewGuid(),
            Reason = "Does not exist"
        });

        Assert.False(cancel.Result.Success);
        Assert.Equal(404, cancel.StatusCode);
        var stored = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Pending, stored.Status);
    }

    [Theory]
    [InlineData(TournamentStatus.Published, RaceStatus.Scheduled)]
    [InlineData(TournamentStatus.Ongoing, RaceStatus.Scheduled)]
    [InlineData(TournamentStatus.Published, RaceStatus.RegistrationOpen)]
    [InlineData(TournamentStatus.Published, RaceStatus.RegistrationClosed)]
    public async Task OwnerInviteJockey_AllowedLifecycleCombination_Succeeds(TournamentStatus tournamentStatus, RaceStatus raceStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(
            f, $"lifecycle-ok-{tournamentStatus}-{raceStatus}", tournamentStatus, raceStatus);
        var (_, jockeyId) = await CreateJockeyAsync(f, $"lifecycle-ok-{tournamentStatus}-{raceStatus}", ApprovalStatus.Approved);

        var result = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });

        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Theory]
    [InlineData(TournamentStatus.Draft)]
    [InlineData(TournamentStatus.Finished)]
    [InlineData(TournamentStatus.Cancelled)]
    public async Task OwnerInviteJockey_ClosedTournamentStatus_Rejected(TournamentStatus tournamentStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(
            f, $"lifecycle-tournament-{tournamentStatus}", tournamentStatus, RaceStatus.Scheduled);
        var (_, jockeyId) = await CreateJockeyAsync(f, $"lifecycle-tournament-{tournamentStatus}", ApprovalStatus.Approved);

        var result = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await f.Db.JockeyInvitations.Where(i => i.HorseId == horseId && i.JockeyId == jockeyId).ToListAsync());
    }

    [Theory]
    [InlineData(RaceStatus.InProgress)]
    [InlineData(RaceStatus.Finished)]
    [InlineData(RaceStatus.Cancelled)]
    public async Task OwnerInviteJockey_ClosedRaceStatus_Rejected(RaceStatus raceStatus)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(
            f, $"lifecycle-race-{raceStatus}", TournamentStatus.Published, raceStatus);
        var (_, jockeyId) = await CreateJockeyAsync(f, $"lifecycle-race-{raceStatus}", ApprovalStatus.Approved);

        var result = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await f.Db.JockeyInvitations.Where(i => i.HorseId == horseId && i.JockeyId == jockeyId).ToListAsync());
    }

    [Fact]
    public async Task OwnerInviteJockey_ScheduledAtInPastButStatusStillScheduled_Allowed()
    {
        // ScheduledAt is a planned time, not authoritative — a delayed Race that is still
        // Status=Scheduled must remain invitable even after its planned start time has passed.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(
            f, "lifecycle-past-scheduled", TournamentStatus.Published, RaceStatus.Scheduled,
            scheduledAt: DateTime.UtcNow.AddMinutes(-30));
        var (_, jockeyId) = await CreateJockeyAsync(f, "lifecycle-past-scheduled", ApprovalStatus.Approved);

        var result = await BuildHorseService(f).InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest
        {
            JockeyId = jockeyId,
            RaceId = raceId
        });

        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task JockeyAccept_RejectedWhenRaceStartsAfterInvitationWasCreated()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "lifecycle-race-started");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "lifecycle-race-started", ApprovalStatus.Approved);
        var horseService = BuildHorseService(f);
        var jockeyService = BuildJockeyService(f);

        var invite = await horseService.InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        // The Race actually starts after the invitation was sent — a stale Pending invitation.
        var race = await f.Db.Races.SingleAsync(r => r.Id == raceId);
        race.Status = RaceStatus.InProgress;
        await f.Db.SaveChangesAsync();

        var accept = await jockeyService.RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest { Accept = true });

        Assert.False(accept.Result.Success);
        Assert.Equal(409, accept.StatusCode);
        var storedInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Pending, storedInvitation.Status);
        var entry = await f.Db.RaceEntries.SingleAsync(e => e.RaceId == raceId && e.HorseId == horseId);
        Assert.Null(entry.JockeyId);
    }

    [Fact]
    public async Task JockeyAccept_RejectedWhenTournamentFinishesAfterInvitationWasCreated()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerUserId, _, horseId, raceId) = await CreateAssignedHorseForInvitationAsync(f, "lifecycle-tournament-finished");
        var (jockeyUserId, jockeyId) = await CreateJockeyAsync(f, "lifecycle-tournament-finished", ApprovalStatus.Approved);
        var horseService = BuildHorseService(f);
        var jockeyService = BuildJockeyService(f);

        var invite = await horseService.InviteJockeyAsync(ownerUserId, horseId, new JockeyInvitationCreateRequest { JockeyId = jockeyId, RaceId = raceId });
        Assert.True(invite.Result.Success, invite.Result.Message);
        var invitation = await f.Db.JockeyInvitations.SingleAsync(i => i.HorseId == horseId && i.JockeyId == jockeyId && i.RaceId == raceId);

        // The Tournament wraps up after the invitation was sent — a stale Pending invitation.
        var tournamentId = (await f.Db.Races.SingleAsync(r => r.Id == raceId)).TournamentId;
        var tournament = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentId);
        tournament.Status = TournamentStatus.Finished;
        await f.Db.SaveChangesAsync();

        var accept = await jockeyService.RespondInvitationAsync(jockeyUserId, invitation.Id, new JockeyInvitationRespondRequest { Accept = true });

        Assert.False(accept.Result.Success);
        Assert.Equal(409, accept.StatusCode);
        var storedInvitation = await f.Db.JockeyInvitations.SingleAsync(i => i.Id == invitation.Id);
        Assert.Equal(JockeyInvitationStatus.Pending, storedInvitation.Status);
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
        RaceLifecycleTests.LifecycleFixture f,
        string tag)
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
        var owner = new Owner
        {
            Id = Guid.NewGuid(),
            UserId = ownerUser.Id,
            OwnerCode = $"OWN-{suffix.Substring(0, 8)}"
        };
        var horse = new Horse
        {
            Id = Guid.NewGuid(),
            Name = $"Horse {tag}",
            OwnerId = owner.Id,
            ApprovalStatus = ApprovalStatus.Approved
        };

        f.Db.AddRange(ownerUser, owner, horse);
        await f.Db.SaveChangesAsync();
        return (ownerUser.Id, owner.Id, horse.Id);
    }

    private static async Task<(Guid jockeyUserId, Guid jockeyId)> CreateJockeyAsync(
        RaceLifecycleTests.LifecycleFixture f,
        string tag,
        ApprovalStatus approvalStatus,
        bool userActive = true)
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

    private static async Task<(Guid tournamentId, Guid raceId)> CreateRaceAsync(
        RaceLifecycleTests.LifecycleFixture f,
        string tag,
        int roundNumber,
        TournamentStatus tournamentStatus = TournamentStatus.Published,
        RaceStatus raceStatus = RaceStatus.Scheduled,
        DateTime? scheduledAt = null)
    {
        var start = scheduledAt ?? DateTime.UtcNow.AddDays(10 + roundNumber);
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = $"Tournament {tag}",
            StartDate = start.Date,
            EndDate = start.Date.AddDays(3),
            Status = tournamentStatus,
            IsActive = true,
            MaxRounds = Math.Max(1, roundNumber),
            MaxParticipants = 8,
            RegistrationDeadline = start.Date.AddDays(-1)
        };
        var round = new Round
        {
            Id = Guid.NewGuid(),
            Name = $"Round {roundNumber}",
            TournamentId = tournament.Id,
            RoundNumber = roundNumber,
            AdvanceCount = roundNumber == 1 ? 1 : 0,
            ScheduledStartDate = start,
            ScheduledEndDate = start.AddHours(2)
        };
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = $"Race {tag}",
            TournamentId = tournament.Id,
            RoundId = round.Id,
            ScheduledAt = start.AddMinutes(10),
            ScheduledEndAt = start.AddMinutes(70),
            Status = raceStatus,
            MaxParticipants = 8,
            Distance = 1200
        };

        f.Db.AddRange(tournament, round, race);
        await f.Db.SaveChangesAsync();
        return (tournament.Id, race.Id);
    }

    private static async Task RegisterTournamentAsync(
        RaceLifecycleTests.LifecycleFixture f,
        Guid tournamentId,
        Guid ownerId,
        Guid horseId,
        RegistrationStatus status)
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

    private static async Task<(Guid ownerUserId, Guid ownerId, Guid horseId, Guid raceId)> CreateAssignedHorseForInvitationAsync(
        RaceLifecycleTests.LifecycleFixture f,
        string tag,
        TournamentStatus tournamentStatus = TournamentStatus.Published,
        RaceStatus raceStatus = RaceStatus.Scheduled,
        DateTime? scheduledAt = null)
    {
        var (ownerUserId, ownerId, horseId) = await CreateApprovedOwnerHorseAsync(f, tag);
        // Always assign while Published/Scheduled first — AssignHorseToRaceAsync has its own
        // validation independent of the invite-time lifecycle guard under test here. The
        // caller's desired lifecycle state is applied afterward, once the RaceEntry exists,
        // to simulate "RaceEntry already existed, then the Tournament/Race moved on".
        var (tournamentId, raceId) = await CreateRaceAsync(f, tag, roundNumber: 1);
        await RegisterTournamentAsync(f, tournamentId, ownerId, horseId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);

        if (tournamentStatus != TournamentStatus.Published || raceStatus != RaceStatus.Scheduled || scheduledAt.HasValue)
        {
            var tournament = await f.Db.Tournaments.SingleAsync(t => t.Id == tournamentId);
            tournament.Status = tournamentStatus;
            var race = await f.Db.Races.SingleAsync(r => r.Id == raceId);
            race.Status = raceStatus;
            if (scheduledAt.HasValue)
            {
                race.ScheduledAt = scheduledAt.Value;
            }
            await f.Db.SaveChangesAsync();
        }

        return (ownerUserId, ownerId, horseId, raceId);
    }

    private sealed class NoopNotificationService : INotificationService
    {
        public Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto)
            => Task.FromResult(ServiceResult<NotificationDto>.Ok(new NotificationDto()));

        public Task<ServiceResult<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));

        public Task<ServiceResult<List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));

        public Task<ServiceResult<List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter)
            => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));

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

        public Task<ServiceResult<List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId)
            => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));

        public Task ProcessUnsentNotificationsAsync()
            => Task.CompletedTask;
    }
}