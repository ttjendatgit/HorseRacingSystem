using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Phase4B: Tournament Draft-save validation, the temporary (Phase5-pending) Publish block,
/// field-editability-after-Publish, cancellation, and the Race cancellation cascade. Wired
/// against a real Sqlite in-memory DB and the actual production services (not mocks), reusing
/// RaceLifecycleTests.LifecycleFixture.
/// </summary>
public class TournamentValidationTests
{
    private static CreateTournamentRequest ValidCreateRequest(
        string name = "Giải hợp lệ",
        DateTime? startDate = null,
        DateTime? endDate = null,
        DateTime? registrationDeadline = null,
        int? minParticipants = 3,
        int? maxParticipants = 10,
        decimal prizePool = 0,
        int maxRounds = 1)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(10);
        var end = endDate ?? start.AddDays(5);
        var deadline = registrationDeadline ?? start.AddDays(-1);
        return new CreateTournamentRequest
        {
            Name = name,
            StartDate = start,
            EndDate = end,
            RegistrationDeadline = deadline,
            MinParticipants = minParticipants,
            MaxParticipants = maxParticipants,
            PrizePool = prizePool,
            MaxRounds = maxRounds
        };
    }

    // ── CREATE / DRAFT ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_BlankName_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(name: "   "));
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Create_NameOver200Chars_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(name: new string('A', 201)));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_MaxParticipantsNull_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(maxParticipants: null));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_MaxParticipantsZeroOrLess_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(maxParticipants: 0, minParticipants: null));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_MinParticipantsNull_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(minParticipants: null));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_MinParticipantsBelowThree_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(minParticipants: 2));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_MinParticipantsGreaterThanMaxParticipants_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(minParticipants: 12, maxParticipants: 10));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_StartDateEqualsEndDate_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(startDate: start, endDate: start));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_StartDateAfterEndDate_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(startDate: start, endDate: start.AddDays(-1)));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_RegistrationDeadlineEqualsStartDate_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(startDate: start, registrationDeadline: start));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_RegistrationDeadlineAfterStartDate_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(startDate: start, registrationDeadline: start.AddDays(1)));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_PrizePoolNegative_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(prizePool: -1m));
        Assert.False(result.Result.Success);
    }

    [Fact]
    public async Task Create_ValidDraft_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(TournamentStatus.Draft, result.Result.Data!.Status);
    }

    [Fact]
    public async Task Create_NullRegistrationDeadline_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var req = ValidCreateRequest();
        req.RegistrationDeadline = null;
        var result = await f.TournamentSvc.CreateTournamentAsync(req);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    // ── NAME UPDATE SEMANTICS ───────────────────────────────────────────

    [Fact]
    public async Task DraftUpdate_EmptyName_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        Assert.True(create.Result.Success, create.Result.Message);
        var id = create.Result.Data!.Id;

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest { Name = "" });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task DraftUpdate_WhitespaceName_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        Assert.True(create.Result.Success, create.Result.Message);
        var id = create.Result.Data!.Id;

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest { Name = "   " });
        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    // ── PUBLISH (Phase5-pending block) ──────────────────────────────────

    [Fact]
    public async Task Publish_PastRegistrationDeadline_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var start = DateTime.UtcNow.AddDays(10);
        var create = await f.TournamentSvc.CreateTournamentAsync(
            ValidCreateRequest(startDate: start, registrationDeadline: DateTime.UtcNow.AddDays(-1)));
        Assert.True(create.Result.Success, create.Result.Message);

        var publish = await f.TournamentSvc.ChangeStatusAsync(create.Result.Data!.Id,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());

        Assert.False(publish.Result.Success);
        Assert.Equal(400, publish.StatusCode);
    }

    [Fact]
    public async Task Publish_NoRound_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        Assert.True(create.Result.Success, create.Result.Message);

        var publish = await f.TournamentSvc.ChangeStatusAsync(create.Result.Data!.Id,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());

        Assert.False(publish.Result.Success);
    }

    [Fact]
    public async Task Publish_ValidTournamentAndRound_StillDoesNotTransition_Phase5Pending()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        var tournamentId = create.Result.Data!.Id;
        var tournamentStart = create.Result.Data.StartDate;

        // Phase5B Fix2: the Round's schedule must fall inside the Tournament's own window
        // (Tournament.StartDate <= Round.Start < Round.End <= Tournament.EndDate) or CreateRoundAsync
        // now friendly-rejects it up front — this test is about the (still-missing) Race/Track
        // structural readiness, not Round/Tournament containment, so keep the Round in-window.
        var round = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = tournamentStart, ScheduledEndDate = tournamentStart.AddDays(1)
        });
        Assert.True(round.Result.Success, round.Result.Message);

        var publish = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());

        // Tournament-level checks all pass, but the transition must still be blocked: Phase5's
        // Round/Race structural readiness does not exist yet, and V1.1 Publish is all-or-nothing.
        Assert.False(publish.Result.Success);
        Assert.Equal(400, publish.StatusCode);

        var reloaded = await f.TournamentSvc.GetTournamentAsync(tournamentId);
        Assert.Equal(TournamentStatus.Draft, reloaded.Result.Data!.Status);
        Assert.Null(reloaded.Result.Data.PublishedAt);
        Assert.False(reloaded.Result.Data.IsActive);
    }

    [Fact]
    public async Task Publish_ErrorMessage_DoesNotContainPhase5()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        var tournamentId = create.Result.Data!.Id;

        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = DateTime.UtcNow, ScheduledEndDate = DateTime.UtcNow.AddDays(1)
        });

        var publish = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Published }, Guid.NewGuid());

        Assert.False(publish.Result.Success);
        // User-facing error message must NOT contain internal roadmap wording.
        Assert.DoesNotContain("Phase5", publish.Result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Phase", publish.Result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── EDITABILITY ─────────────────────────────────────────────────────

    private static async Task<Guid> SeedPublishedTournamentAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        // Publish can never actually complete in Phase4B (by design), so directly arrange a
        // Published tournament via the DbContext to test the post-Publish editability guard —
        // mirrors the existing direct-status-seeding pattern already used in RaceLifecycleTests
        // (Cancellation_AllowedBeforeFinished).
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        var id = create.Result.Data!.Id;
        var tournament = await f.Db.Tournaments.FirstAsync(t => t.Id == id);
        tournament.Status = TournamentStatus.Published;
        tournament.PublishedAt = DateTime.UtcNow;
        tournament.IsActive = true;
        await f.Db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Published_ChangedImmutableValue_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var id = await SeedPublishedTournamentAsync(f);

        // Attempt to change StartDate — should be rejected
        var persisted = await f.Db.Tournaments.AsNoTracking().FirstAsync(t => t.Id == id);
        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest
        {
            StartDate = persisted.StartDate.AddDays(5)  // different value
        });

        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task Published_NameOnlyEdit_Succeeds_EvenWhenLegacyClientResendsSameImmutableValues()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var id = await SeedPublishedTournamentAsync(f);

        // Read persisted values so we can resend them unchanged (legacy FE behavior).
        var persisted = await f.Db.Tournaments.AsNoTracking().FirstAsync(t => t.Id == id);

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest
        {
            Name = "Tên mới sau công bố",
            // Legacy FE resends these unchanged:
            StartDate = persisted.StartDate,
            EndDate = persisted.EndDate,
            MinParticipants = persisted.MinParticipants,
            MaxParticipants = persisted.MaxParticipants,
        });

        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal("Tên mới sau công bố", update.Result.Data!.Name);
    }

    [Fact]
    public async Task Published_EmptyName_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var id = await SeedPublishedTournamentAsync(f);

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest { Name = "" });

        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task Published_WhitespaceName_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var id = await SeedPublishedTournamentAsync(f);

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest { Name = "   " });

        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task Published_NameOver200_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var id = await SeedPublishedTournamentAsync(f);

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest { Name = new string('A', 201) });

        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task Published_NegativePrizePool_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var id = await SeedPublishedTournamentAsync(f);

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest { PrizePool = -1m });

        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task Published_ValidName_UpdateSucceeds()
    {
        // PRIZE-V1 Part 4: PrizePool became immutable after Publish (see PrizeV1Tests'
        // UpdateTournament_PrizePool_Published_* cases for that rule's own coverage) — this test
        // now isolates Name, which remains the one Published-mutable field.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var id = await SeedPublishedTournamentAsync(f);

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest
        {
            Name = "Tên hợp lệ sau công bố"
        });

        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal("Tên hợp lệ sau công bố", update.Result.Data!.Name);
    }

    [Fact]
    public async Task Published_MaxRoundsUpdate_Rejected()
    {
        // V0.1: MaxRounds is structural (drives V0 Final identity, RoundNumber == MaxRounds) and
        // now follows the same Draft-only structural-lock convention as StartDate/EndDate/
        // MinParticipants/MaxParticipants — superseding the old "remains allowed for
        // compatibility" behavior this test used to assert.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var id = await SeedPublishedTournamentAsync(f);
        var before = (await f.TournamentSvc.GetTournamentAsync(id)).Result.Data!.MaxRounds;

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest { MaxRounds = 7 });

        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
        var reloaded = await f.TournamentSvc.GetTournamentAsync(id);
        Assert.Equal(before, reloaded.Result.Data!.MaxRounds);
    }

    [Fact]
    public async Task Published_MaxRoundsResentUnchanged_Allowed()
    {
        // Phase4B convention (shared by every other immutable-after-publish field): resending the
        // CURRENT value is not a "change" and must not be rejected.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var id = await SeedPublishedTournamentAsync(f);
        var current = (await f.TournamentSvc.GetTournamentAsync(id)).Result.Data!.MaxRounds;

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest { MaxRounds = current });

        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal(current, update.Result.Data!.MaxRounds);
    }

    // ── V0.1: MaxRounds create/edit + Round-ceiling consistency ──────────

    [Fact]
    public async Task Create_MaxRoundsTwo_PersistsAndReturnsTwo()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(maxRounds: 2));
        Assert.True(create.Result.Success, create.Result.Message);
        Assert.Equal(2, create.Result.Data!.MaxRounds);

        var reloaded = await f.TournamentSvc.GetTournamentAsync(create.Result.Data.Id);
        Assert.Equal(2, reloaded.Result.Data!.MaxRounds);
    }

    [Fact]
    public async Task CreateRound_WithinMaxRounds_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        var start = create.Result.Data.StartDate;

        var round1 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1)
        });
        var round2 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2)
        });

        Assert.True(round1.Result.Success, round1.Result.Message);
        Assert.True(round2.Result.Success, round2.Result.Message);
    }

    [Fact]
    public async Task CreateRound_ExceedsMaxRounds_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        var start = create.Result.Data.StartDate;

        var round3 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 3", TournamentId = tournamentId, RoundNumber = 3,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1)
        });

        Assert.False(round3.Result.Success);
        Assert.Equal(400, round3.StatusCode);
    }

    [Fact]
    public async Task UpdateMaxRounds_BelowExistingRoundNumber_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        var start = create.Result.Data.StartDate;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1)
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2)
        });

        var update = await f.TournamentSvc.UpdateTournamentAsync(tournamentId, new UpdateTournamentRequest { MaxRounds = 1 });

        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
        var reloaded = await f.TournamentSvc.GetTournamentAsync(tournamentId);
        Assert.Equal(2, reloaded.Result.Data!.MaxRounds); // unchanged
    }

    [Fact]
    public async Task UpdateMaxRounds_AboveExistingRoundNumber_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest(maxRounds: 2));
        var tournamentId = create.Result.Data!.Id;
        var start = create.Result.Data.StartDate;
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1)
        });
        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = start.AddDays(1), ScheduledEndDate = start.AddDays(2)
        });

        var update = await f.TournamentSvc.UpdateTournamentAsync(tournamentId, new UpdateTournamentRequest { MaxRounds = 3 });

        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal(3, update.Result.Data!.MaxRounds);

        // Repairs a previously-broken Draft: Round 3 is now creatable to actually fill the
        // expanded structure — existing Round1/Round2 rows were never touched/renumbered.
        var round3 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 3", TournamentId = tournamentId, RoundNumber = 3,
            ScheduledStartDate = start.AddDays(2), ScheduledEndDate = start.AddDays(3)
        });
        Assert.True(round3.Result.Success, round3.Result.Message);
    }

    [Theory]
    [InlineData("StartDate")]
    [InlineData("EndDate")]
    [InlineData("RegistrationDeadline")]
    [InlineData("MinParticipants")]
    [InlineData("MaxParticipants")]
    public async Task Update_ImmutableFieldAfterPublish_Rejected(string field)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var id = await SeedPublishedTournamentAsync(f);

        // Attempt to send a DIFFERENT value for each immutable field — must be rejected.
        var persisted = await f.Db.Tournaments.AsNoTracking().FirstAsync(t => t.Id == id);
        var request = new UpdateTournamentRequest();
        switch (field)
        {
            case "StartDate": request.StartDate = persisted.StartDate.AddDays(5); break;
            case "EndDate": request.EndDate = persisted.EndDate.AddDays(5); break;
            case "RegistrationDeadline": request.RegistrationDeadline = (persisted.RegistrationDeadline ?? DateTime.UtcNow).AddDays(-5); break;
            case "MinParticipants": request.MinParticipants = (persisted.MinParticipants ?? 3) + 1; break;
            case "MaxParticipants": request.MaxParticipants = (persisted.MaxParticipants ?? 10) + 2; break;
        }

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, request);

        Assert.False(update.Result.Success);
        Assert.Equal(400, update.StatusCode);
    }

    [Fact]
    public async Task Update_ImmutableFields_AllowedWhileDraft()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        var id = create.Result.Data!.Id;

        var newStart = DateTime.UtcNow.AddDays(30);
        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest
        {
            StartDate = newStart,
            EndDate = newStart.AddDays(5),
            RegistrationDeadline = newStart.AddDays(-1),
            MinParticipants = 4,
            MaxParticipants = 12
        });

        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal(newStart, update.Result.Data!.StartDate);
        Assert.Equal(4, update.Result.Data.MinParticipants);
        Assert.Equal(12, update.Result.Data.MaxParticipants);
    }

    [Fact]
    public void UpdateTournamentRequest_HasNoIsActiveProperty()
    {
        // Regression proof (Phase4B §6): IsActive is not on the DTO at all, so no client
        // payload — old or new — can reach UpdateTournamentAsync and mutate it. An old client
        // sending an extra "isActive" JSON property is silently ignored by normal model binding.
        Assert.Null(typeof(UpdateTournamentRequest).GetProperty("IsActive"));
    }

    // ── CANCEL ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_NullReason_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());

        var cancel = await f.TournamentSvc.ChangeStatusAsync(create.Result.Data!.Id,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = null },
            Guid.NewGuid());

        Assert.False(cancel.Result.Success);
        Assert.Equal(400, cancel.StatusCode);
    }

    [Fact]
    public async Task Cancel_BlankReason_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());

        var cancel = await f.TournamentSvc.ChangeStatusAsync(create.Result.Data!.Id,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "   " },
            Guid.NewGuid());

        Assert.False(cancel.Result.Success);
    }

    [Fact]
    public async Task Cancel_ValidReason_PersistsReasonActorTimestampAndDeactivates()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        var id = create.Result.Data!.Id;
        var actorId = Guid.NewGuid();

        var cancel = await f.TournamentSvc.ChangeStatusAsync(id,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "  Không đủ số lượng ngựa tham gia  " },
            actorId);

        Assert.True(cancel.Result.Success, cancel.Result.Message);
        var t = cancel.Result.Data!;
        Assert.Equal(TournamentStatus.Cancelled, t.Status);
        Assert.Equal("Không đủ số lượng ngựa tham gia", t.CancellationReason);
        Assert.Equal(actorId, t.CancelledBy);
        Assert.NotNull(t.CancelledAt);
        Assert.False(t.IsActive);
    }

    // ── RACE CASCADE ────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_CascadesToChildRaces_ByCurrentRaceStatus()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        var tournamentId = create.Result.Data!.Id;
        var tournamentStart = create.Result.Data.StartDate;

        // Phase5B Fix2: Round schedule must fall inside the Tournament's own window or
        // CreateRoundAsync now friendly-rejects it up front — unrelated to what this test covers
        // (the cancellation cascade), so keep the Round in-window.
        var round = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = tournamentStart, ScheduledEndDate = tournamentStart.AddDays(1)
        });
        var roundId = round.Result.Data!.Id;

        Race MakeRace(RaceStatus status) => new Race
        {
            Id = Guid.NewGuid(),
            Name = $"Race-{status}",
            TournamentId = tournamentId,
            RoundId = roundId,
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        var scheduled = MakeRace(RaceStatus.Scheduled);
        var registrationOpen = MakeRace(RaceStatus.RegistrationOpen);
        var registrationClosed = MakeRace(RaceStatus.RegistrationClosed);
        var inProgress = MakeRace(RaceStatus.InProgress);
        var finished = MakeRace(RaceStatus.Finished);
        var alreadyCancelled = MakeRace(RaceStatus.Cancelled);
        f.Db.AddRange(scheduled, registrationOpen, registrationClosed, inProgress, finished, alreadyCancelled);
        await f.Db.SaveChangesAsync();

        var cancel = await f.TournamentSvc.ChangeStatusAsync(tournamentId,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "Hủy để kiểm tra cascade" },
            Guid.NewGuid());
        Assert.True(cancel.Result.Success, cancel.Result.Message);

        async Task<RaceStatus> StatusOfAsync(Guid raceId) =>
            (await f.Db.Races.AsNoTracking().FirstAsync(r => r.Id == raceId)).Status;

        Assert.Equal(RaceStatus.Cancelled, await StatusOfAsync(scheduled.Id));
        Assert.Equal(RaceStatus.Cancelled, await StatusOfAsync(registrationOpen.Id));
        Assert.Equal(RaceStatus.Cancelled, await StatusOfAsync(registrationClosed.Id));
        Assert.Equal(RaceStatus.Cancelled, await StatusOfAsync(inProgress.Id));
        Assert.Equal(RaceStatus.Finished, await StatusOfAsync(finished.Id));
        Assert.Equal(RaceStatus.Cancelled, await StatusOfAsync(alreadyCancelled.Id));
    }

    // ── TRANSITION REGRESSION ───────────────────────────────────────────

    [Theory]
    [InlineData(TournamentStatus.Draft, TournamentStatus.Finished)]
    [InlineData(TournamentStatus.Draft, TournamentStatus.Ongoing)]
    public async Task ForbiddenTransitions_StillRejected(TournamentStatus from, TournamentStatus to)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        var id = create.Result.Data!.Id;

        if (from != TournamentStatus.Draft)
        {
            var tournament = await f.Db.Tournaments.FirstAsync(t => t.Id == id);
            tournament.Status = from;
            await f.Db.SaveChangesAsync();
        }

        var result = await f.TournamentSvc.ChangeStatusAsync(id,
            new ChangeTournamentStatusRequest { NewStatus = to }, Guid.NewGuid());

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Cancel_NotAllowedFromFinished()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        var id = create.Result.Data!.Id;

        var tournament = await f.Db.Tournaments.FirstAsync(t => t.Id == id);
        tournament.Status = TournamentStatus.Finished;
        await f.Db.SaveChangesAsync();

        var cancel = await f.TournamentSvc.ChangeStatusAsync(id,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "Thử hủy giải đã kết thúc" },
            Guid.NewGuid());

        Assert.False(cancel.Result.Success);
        Assert.Equal(400, cancel.StatusCode);
    }

    [Theory]
    [InlineData(TournamentStatus.Draft)]
    [InlineData(TournamentStatus.Published)]
    [InlineData(TournamentStatus.Ongoing)]
    public async Task Cancel_AllowedFromDraftPublishedOngoing(TournamentStatus from)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var create = await f.TournamentSvc.CreateTournamentAsync(ValidCreateRequest());
        var id = create.Result.Data!.Id;

        if (from != TournamentStatus.Draft)
        {
            var tournament = await f.Db.Tournaments.FirstAsync(t => t.Id == id);
            tournament.Status = from;
            await f.Db.SaveChangesAsync();
        }

        var cancel = await f.TournamentSvc.ChangeStatusAsync(id,
            new ChangeTournamentStatusRequest { NewStatus = TournamentStatus.Cancelled, Reason = "Hợp lệ" },
            Guid.NewGuid());

        Assert.True(cancel.Result.Success, cancel.Result.Message);
    }
}
