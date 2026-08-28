using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HorseRacing.Models;
using HorseRacing.Services.Interfaces;

namespace HorseRacing.Services;

/// <summary>
/// R0's canonical Official-ranking validator — the SAME contract must govern every consumer that
/// trusts a stored RaceResult.RankingsJson as authoritative. Extracted from
/// AdminService.ApproveRaceResultAsync (formerly a private static method there) so
/// RaceManagementService.GenerateNextRoundEntriesAsync (Q1) shares it rather than re-deriving an
/// independent, potentially-drifting parser.
///
/// Re-derives and re-validates the stored ranking against the Race's CURRENT RaceEntry
/// participants immediately before a caller trusts it — guards against a stale/malformed
/// RankingsJson (e.g. entries changed since Provisional submit, or corrupted after Official
/// approval). Throws InvalidOperationException with a clean Vietnamese message on any problem;
/// callers translate that into their own error response, never a partial apply.
/// </summary>
public static class RaceResultRankingValidator
{
    /// <summary>
    /// Returns the validated ranking ordered by Position ascending. Enforces, in order:
    /// RankingsJson non-empty and parseable; every item's HorseId is a current participant;
    /// no duplicate HorseId; Position &gt; 0; Status is Completed/DNF/DSQ; item count equals
    /// participant count. Only Status=Completed items participate in the finishing order: their
    /// positions must be unique and form exactly the continuous set 1..M (M = Completed count),
    /// and that range is exclusively theirs — a DNF/DSQ item may not reuse any Position a
    /// Completed item occupies (though DNF/DSQ items may freely share a Position among themselves,
    /// e.g. a shared sentinel like 99). Finally, the Position-1 Completed HorseId equals
    /// winningHorseId — unless every item is DNF/DSQ, in which case there is no true position 1 to
    /// check (WinningHorseId is then just LiveResultService's fallback pick).
    /// </summary>
    public static List<RaceResultRankingItemRequest> ParseAndValidate(
        string? rankingsJson, Guid winningHorseId, List<RaceEntry> participants)
    {
        if (string.IsNullOrWhiteSpace(rankingsJson))
            throw new InvalidOperationException("Kết quả này không có bảng xếp hạng hợp lệ để duyệt.");

        List<RaceResultRankingItemRequest>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<RaceResultRankingItemRequest>>(rankingsJson);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Bảng xếp hạng đã lưu không hợp lệ và không thể duyệt.");
        }

        if (items == null || items.Count == 0)
            throw new InvalidOperationException("Bảng xếp hạng đã lưu không hợp lệ và không thể duyệt.");

        var participantIds = participants.Select(p => p.HorseId).ToHashSet();
        var seenHorseIds = new HashSet<Guid>();
        var allowedStatuses = new HashSet<string> { "Completed", "DNF", "DSQ" };

        foreach (var item in items)
        {
            if (!participantIds.Contains(item.HorseId) ||
                item.Position <= 0 ||
                !seenHorseIds.Add(item.HorseId) ||
                !allowedStatuses.Contains(item.Status))
            {
                throw new InvalidOperationException(
                    "Bảng xếp hạng đã lưu không còn khớp với danh sách ngựa tham gia cuộc đua này. Trọng tài phải nộp lại kết quả.");
            }
        }

        if (items.Count != participants.Count)
        {
            throw new InvalidOperationException(
                "Bảng xếp hạng đã lưu không còn khớp với danh sách ngựa tham gia cuộc đua này. Trọng tài phải nộp lại kết quả.");
        }

        // Chỉ ngựa Status=Completed mới tham gia thứ hạng về đích: vị trí của chúng phải duy nhất và
        // liên tục 1..M (M = số ngựa Completed). Ngựa DNF/DSQ không tham gia thứ hạng này — vị trí
        // của chúng chỉ cần > 0 (đã kiểm tra ở trên), được phép trùng nhau (VD dùng chung 1 số hiệu
        // quy ước cho "không về đích"), nhưng KHÔNG được trùng vào bất kỳ vị trí nào đã dùng cho ngựa
        // Completed — dải 1..M là không gian riêng của ngựa Completed, không được lẫn.
        var completedItems = items.Where(i => i.Status == "Completed").ToList();
        var completedPositions = new HashSet<int>();
        foreach (var item in completedItems)
        {
            if (!completedPositions.Add(item.Position))
                throw new InvalidOperationException(
                    "Bảng xếp hạng đã lưu không còn khớp với danh sách ngựa tham gia cuộc đua này. Trọng tài phải nộp lại kết quả.");
        }
        if (!completedPositions.SetEquals(Enumerable.Range(1, completedItems.Count)))
        {
            throw new InvalidOperationException(
                "Bảng xếp hạng đã lưu không còn khớp với danh sách ngựa tham gia cuộc đua này. Trọng tài phải nộp lại kết quả.");
        }
        if (items.Any(i => i.Status != "Completed" && completedPositions.Contains(i.Position)))
        {
            throw new InvalidOperationException(
                "Bảng xếp hạng đã lưu không còn khớp với danh sách ngựa tham gia cuộc đua này. Trọng tài phải nộp lại kết quả.");
        }

        // Ngựa thắng cuộc phải là ngựa Completed ở vị trí 1 — TRỪ trường hợp hiếm: toàn bộ ngựa đều
        // DNF/DSQ (completedItems.Count == 0). Khi đó không có "hạng 1" thật sự để đối chiếu;
        // WinningHorseId là giá trị fallback đã được LiveResultService gán lúc nộp kết quả — chỉ cần
        // là 1 participant hợp lệ, đã được đảm bảo ở vòng lặp coverage phía trên, không cần kiểm tra
        // gì thêm ở đây.
        if (completedItems.Count > 0)
        {
            var winner = completedItems.Single(i => i.Position == 1);
            if (winner.HorseId != winningHorseId)
                throw new InvalidOperationException("Ngựa thắng cuộc không khớp với vị trí 1 trong bảng xếp hạng đã lưu.");
        }

        return items.OrderBy(i => i.Position).ToList();
    }
}
