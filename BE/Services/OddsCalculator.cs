using System;
using System.Collections.Generic;
using System.Linq;
using HorseRacing.Models;

namespace HorseRacing.Services;

public static class OddsCalculator
{
    private const decimal HouseEdge = 1.10m; // Lợi thế nhà cái (House Edge 10%)
    private const decimal MinOdds = 1.01m; // Tỷ lệ cược tối thiểu
    private const decimal MaxOdds = 99.00m; // Tỷ lệ cược tối đa
    private const decimal DefaultWinRate = 10.0m; // Tỷ lệ thắng mặc định 10% nếu chưa có dữ liệu hoặc chưa đua trận nào

    /// <summary>
    /// Tính tỷ lệ cược (Odds) cho danh sách các chiến mã trong cùng một trận đua theo thuật toán chuẩn hóa 4 bước:
    /// BƯỚC 1: Điểm Score = (Tỷ lệ thắng Ngựa * 0.70) + (Tỷ lệ thắng Kỵ sĩ * 0.30)
    /// BƯỚC 2: Chuẩn hóa Xác suất Probability = Score / Tổng Score của tất cả ngựa trong trận -> Tổng Xác suất = 100%
    /// BƯỚC 3 & BƯỚC 4: Tỷ lệ cược Odds = 1 / (Probability * 1.10) [Đã tính House Edge 10%]
    /// </summary>
    public static void Recalculate(IList<RaceEntry> entries)
    {
        if (entries == null || !entries.Any()) return;

        var scores = new List<(Guid entryId, decimal score)>();

        foreach (var e in entries)
        {
            var horse = e.Horse;
            var jockey = e.Jockey;

            // 1. Tỷ lệ thắng của Ngựa (%)
            decimal horseWinRate = DefaultWinRate;
            if (horse != null && horse.TotalRaces > 0)
            {
                horseWinRate = ((decimal)horse.TotalWins / horse.TotalRaces) * 100m;
            }

            // 2. Tỷ lệ thắng của Kỵ sĩ (%)
            decimal jockeyWinRate = DefaultWinRate;
            if (jockey != null && jockey.WinRate > 0)
            {
                jockeyWinRate = jockey.WinRate; // Lưu dạng phần trăm %, ví dụ 24.12
            }

            // Kiểm tra phòng ngừa số âm
            if (horseWinRate < 0m) horseWinRate = 0m;
            if (jockeyWinRate < 0m) jockeyWinRate = 0m;

            // BƯỚC 1: Tính điểm Score trọng số (70% Ngựa + 30% Kỵ sĩ)
            decimal score = (horseWinRate * 0.70m) + (jockeyWinRate * 0.30m);

            // Đảm bảo điểm score luôn dương để tránh lỗi chia cho 0
            if (score <= 0m) score = 0.01m;

            scores.Add((e.Id, score));
        }

        decimal totalScore = scores.Sum(s => s.score);

        foreach (var (entryId, score) in scores)
        {
            // BƯỚC 2: Chuẩn hóa Xác suất chiến thắng (Probability)
            decimal probability = totalScore > 0m
                ? score / totalScore
                : 1.0m / entries.Count;

            if (probability <= 0m) probability = 0.0001m;

            // BƯỚC 3 & BƯỚC 4: Tính Tỷ lệ cược (Odds) có tính thêm 10% House Edge
            decimal odds = Math.Round(1.0m / (probability * HouseEdge), 2);

            // Kiểm tra giới hạn biên tối thiểu và tối đa
            if (odds < MinOdds) odds = MinOdds;
            if (odds > MaxOdds) odds = MaxOdds;

            var entry = entries.First(e => e.Id == entryId);
            entry.Odds = odds;
        }
    }
}
