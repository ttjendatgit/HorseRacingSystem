using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HorseRacing.Models;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Xunit;

namespace Tests;

/// <summary>
/// Direct unit coverage for RaceResultRankingValidator.ParseAndValidate's Status-aware rules: only
/// Status=Completed items occupy the continuous 1..M finishing-order range; DNF/DSQ items are
/// excluded from that range (and may freely share a sentinel Position among themselves, but must
/// never reuse a Position a Completed item holds); pre-Status legacy RankingsJson still parses
/// (defaulting every item to Completed); and an unrecognized Status string is rejected. No DB
/// involved — RaceEntry instances here only need HorseId populated, which is all the validator reads.
/// </summary>
public class RaceResultRankingValidatorTests
{
    private static List<RaceEntry> Participants(params Guid[] horseIds) =>
        horseIds.Select(id => new RaceEntry { HorseId = id }).ToList();

    [Fact]
    public void ParseAndValidate_FourCompletedContinuousPlusFourDsqAtSentinel_Succeeds_OnlyCompletedCountToward1ToM()
    {
        var horseIds = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        var completed = horseIds.Take(4).ToArray();
        var dsq = horseIds.Skip(4).Take(4).ToArray();

        var items = new List<RaceResultRankingItemRequest>();
        for (var i = 0; i < completed.Length; i++)
            items.Add(new RaceResultRankingItemRequest { HorseId = completed[i], Position = i + 1, Status = "Completed" });
        foreach (var h in dsq)
            items.Add(new RaceResultRankingItemRequest { HorseId = h, Position = 99, Status = "DSQ" });

        var json = JsonSerializer.Serialize(items);
        var result = RaceResultRankingValidator.ParseAndValidate(json, completed[0], Participants(horseIds));

        Assert.Equal(8, result.Count);
        var completedResult = result.Where(r => r.Status == "Completed").OrderBy(r => r.Position).ToList();
        Assert.Equal(4, completedResult.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, completedResult.Select(r => r.Position));
        Assert.Equal(completed, completedResult.Select(r => r.HorseId));
        Assert.All(result.Where(r => r.Status == "DSQ"), r => Assert.Equal(99, r.Position));
    }

    [Fact]
    public void ParseAndValidate_DsqItemsShareSameSentinelPosition_Succeeds()
    {
        var h1 = Guid.NewGuid(); var h2 = Guid.NewGuid(); var h3 = Guid.NewGuid(); var h4 = Guid.NewGuid();
        var items = new List<RaceResultRankingItemRequest>
        {
            new() { HorseId = h1, Position = 1, Status = "Completed" },
            new() { HorseId = h2, Position = 2, Status = "Completed" },
            new() { HorseId = h3, Position = 99, Status = "DSQ" },
            new() { HorseId = h4, Position = 99, Status = "DSQ" }, // shares 99 with h3 — allowed, both non-Completed
        };
        var json = JsonSerializer.Serialize(items);

        var result = RaceResultRankingValidator.ParseAndValidate(json, h1, Participants(h1, h2, h3, h4));

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void ParseAndValidate_DnfReusesACompletedPosition_Throws()
    {
        var h1 = Guid.NewGuid(); var h2 = Guid.NewGuid(); var h3 = Guid.NewGuid();
        var items = new List<RaceResultRankingItemRequest>
        {
            new() { HorseId = h1, Position = 1, Status = "Completed" },
            new() { HorseId = h2, Position = 2, Status = "Completed" },
            new() { HorseId = h3, Position = 2, Status = "DNF" }, // reuses Completed's position 2 — the
                                                                    // 1..M range is exclusively theirs
        };
        var json = JsonSerializer.Serialize(items);

        Assert.Throws<InvalidOperationException>(() =>
            RaceResultRankingValidator.ParseAndValidate(json, h1, Participants(h1, h2, h3)));
    }

    [Fact]
    public void ParseAndValidate_AllDnfOrDsq_NoCompletedAtAll_SkipsWinnerCheck_Succeeds()
    {
        var h1 = Guid.NewGuid(); var h2 = Guid.NewGuid();
        var items = new List<RaceResultRankingItemRequest>
        {
            new() { HorseId = h1, Position = 99, Status = "DNF" },
            new() { HorseId = h2, Position = 99, Status = "DSQ" },
        };
        var json = JsonSerializer.Serialize(items);

        // winningHorseId is LiveResultService's fallback pick (first participant) when nobody
        // finished — it does not need to match any Position here since there is no real Position 1.
        var result = RaceResultRankingValidator.ParseAndValidate(json, h1, Participants(h1, h2));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseAndValidate_LegacyRankingsJsonWithoutStatusField_DefaultsEveryItemToCompleted()
    {
        var h1 = Guid.NewGuid(); var h2 = Guid.NewGuid();
        // Deliberately hand-built JSON predating the Status field — no "Status" key at all.
        var legacyJson = $"[{{\"HorseId\":\"{h1}\",\"Position\":1}},{{\"HorseId\":\"{h2}\",\"Position\":2}}]";

        var result = RaceResultRankingValidator.ParseAndValidate(legacyJson, h1, Participants(h1, h2));

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("Completed", r.Status));
    }

    [Fact]
    public void ParseAndValidate_UnrecognizedStatus_Throws()
    {
        var h1 = Guid.NewGuid(); var h2 = Guid.NewGuid();
        var items = new List<RaceResultRankingItemRequest>
        {
            new() { HorseId = h1, Position = 1, Status = "Completed" },
            new() { HorseId = h2, Position = 2, Status = "completed" }, // wrong casing — not one of the 3 allowed literals
        };
        var json = JsonSerializer.Serialize(items);

        Assert.Throws<InvalidOperationException>(() =>
            RaceResultRankingValidator.ParseAndValidate(json, h1, Participants(h1, h2)));
    }
}
