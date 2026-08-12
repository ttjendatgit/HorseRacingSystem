using System;
using System.Collections.Generic;
using System.Linq;
using HorseRacing.Models;

namespace HorseRacing.Services;

public static class OddsCalculator
{
    /// <summary>
    /// Tính tỉ lệ cược cho danh sách race entries.
    /// Cập nhật trực tiếp Odds trên mỗi entry.
    /// </summary>
    public static void Recalculate(IList<RaceEntry> entries)
    {
        if (!entries.Any()) return;

        var scores = new List<(Guid entryId, decimal score)>();
        foreach (var e in entries)
        {
            var horse = e.Horse;
            var jockey = e.Jockey;

            decimal winRate = horse != null && horse.TotalRaces > 0
                ? (decimal)horse.TotalWins / horse.TotalRaces
                : 0.10m;

            decimal jockeyRate = jockey != null && jockey.WinRate > 0
                ? jockey.WinRate / 100m
                : 0.10m;

            decimal experienceBonus = horse != null
                ? Math.Min(horse.TotalRaces * 0.002m, 0.10m)
                : 0m;

            decimal score = winRate * 0.50m + jockeyRate * 0.30m + experienceBonus + 0.05m;
            scores.Add((e.Id, score));
        }

        decimal totalScore = scores.Sum(s => s.score);

        foreach (var (entryId, score) in scores)
        {
            decimal probability = score / totalScore;
            decimal odds = Math.Round(1m / probability, 2);
            if (odds < 1.01m) odds = 1.01m;
            if (odds > 99m) odds = 99m;

            var entry = entries.First(e => e.Id == entryId);
            entry.Odds = odds;
        }
    }
}
