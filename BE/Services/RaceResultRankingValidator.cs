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
    /// no duplicate HorseId; no duplicate Position; Position &gt; 0; item count equals participant
    /// count; positions form exactly the continuous set 1..N; and the Position-1 HorseId equals
    /// winningHorseId.
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
        var seenPositions = new HashSet<int>();
        foreach (var item in items)
        {
            if (!participantIds.Contains(item.HorseId) ||
                item.Position <= 0 ||
                !seenHorseIds.Add(item.HorseId) ||
                !seenPositions.Add(item.Position))
            {
                throw new InvalidOperationException(
                    "Bảng xếp hạng đã lưu không còn khớp với danh sách ngựa tham gia cuộc đua này. Trọng tài phải nộp lại kết quả.");
            }
        }

        if (items.Count != participants.Count ||
            !seenPositions.SetEquals(Enumerable.Range(1, items.Count)))
        {
            throw new InvalidOperationException(
                "Bảng xếp hạng đã lưu không còn khớp với danh sách ngựa tham gia cuộc đua này. Trọng tài phải nộp lại kết quả.");
        }

        var winner = items.Single(i => i.Position == 1);
        if (winner.HorseId != winningHorseId)
        {
            throw new InvalidOperationException("Ngựa thắng cuộc không khớp với vị trí 1 trong bảng xếp hạng đã lưu.");
        }

        return items.OrderBy(i => i.Position).ToList();
    }
}
