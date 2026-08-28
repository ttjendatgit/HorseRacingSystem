using System;
using System.Collections.Generic;
using System.Linq;
using HorseRacing.Models;

namespace HorseRacing.Services;

public static class OddsCalculator
{
    private const decimal HouseEdge = 1.10m; // 10% House Edge
    private const decimal MinOdds = 1.01m;
    private const decimal MaxOdds = 99.00m;
    private const decimal DefaultWinRate = 10.0m; // 10% default for 0 races or missing data

    /// <summary>
    /// Tính tỉ lệ cược cho danh sách race entries trong cùng một cuộc đua theo thuật toán chuẩn hóa:
    /// STEP 1: Score_i = (HorseWinRate_i * 0.70) + (JockeyWinRate_i * 0.30)
    /// STEP 2: Probability_i = Score_i / SUM(All Score) -> Total Probability = 100%
    /// STEP 3 & STEP 4: Odds_i = 1 / (Probability_i * 1.10) [House Edge = 10%]
    /// </summary>
    public static void Recalculate(IList<RaceEntry> entries)
    {
        if (entries == null || !entries.Any()) return;

        var scores = new List<(Guid entryId, decimal score)>();

        foreach (var e in entries)
        {
            var horse = e.Horse;
            var jockey = e.Jockey;

            // 1. Horse Win Rate (%)
            decimal horseWinRate = DefaultWinRate;
            if (horse != null && horse.TotalRaces > 0)
            {
                horseWinRate = ((decimal)horse.TotalWins / horse.TotalRaces) * 100m;
            }

            // 2. Jockey Win Rate (%)
            decimal jockeyWinRate = DefaultWinRate;
            if (jockey != null && jockey.WinRate > 0)
            {
                jockeyWinRate = jockey.WinRate; // Stored as percentage e.g. 24.12
            }

            // Defensive checks for negative values
            if (horseWinRate < 0m) horseWinRate = 0m;
            if (jockeyWinRate < 0m) jockeyWinRate = 0m;

            // STEP 1: Calculate Score (70% Horse Win Rate + 30% Jockey Win Rate)
            decimal score = (horseWinRate * 0.70m) + (jockeyWinRate * 0.30m);

            // Ensure score is strictly positive to prevent divide-by-zero
            if (score <= 0m) score = 0.01m;

            scores.Add((e.Id, score));
        }

        decimal totalScore = scores.Sum(s => s.score);

        foreach (var (entryId, score) in scores)
        {
            // STEP 2: Normalize Probability
            decimal probability = totalScore > 0m
                ? score / totalScore
                : 1.0m / entries.Count;

            if (probability <= 0m) probability = 0.0001m;

            // STEP 3 & STEP 4: Calculate Odds with 10% House Edge
            decimal odds = Math.Round(1.0m / (probability * HouseEdge), 2);

            // Bound checks
            if (odds < MinOdds) odds = MinOdds;
            if (odds > MaxOdds) odds = MaxOdds;

            var entry = entries.First(e => e.Id == entryId);
            entry.Odds = odds;
        }
    }
}
