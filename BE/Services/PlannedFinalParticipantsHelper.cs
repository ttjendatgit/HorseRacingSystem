using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Services;

/// <summary>
/// PRIZE-V1.1: structural planned Final-round capacity — the maximum valid Prize.Position for a
/// Tournament. Prize.Position means rank in the Tournament's eventual FINAL ranking, so its upper
/// bound must come from the STRUCTURAL plan (MaxParticipants / AdvanceCount), never from actual
/// registrations, RaceEntry counts, Race.MaxParticipants, or Track.Capacity — those answer
/// different questions (see PART 13 of the PRIZE-V1.1 report).
///
/// Mirrors TournamentAndRoundService's existing PlannedParticipantsFor / ComputePlannedParticipantsAsync
/// semantics (Round 1 = Tournament.MaxParticipants; Round N&gt;1 = predecessor Round's AdvanceCount),
/// applied specifically to the FINAL round (RoundNumber == MaxRounds) — which is exactly
/// PlannedFinalParticipants. Deliberately does NOT require the Final Round row to already exist:
/// Prize rows may be configured before all Rounds are created, so this only needs the single
/// PRE-final Round (RoundNumber == MaxRounds - 1) to exist when MaxRounds &gt; 1.
/// </summary>
internal static class PlannedFinalParticipantsHelper
{
    /// <summary>
    /// MaxRounds &lt;= 1: returns maxParticipants (possibly null if not yet set on the Tournament).
    /// MaxRounds &gt; 1: returns the AdvanceCount of the single Round with RoundNumber == MaxRounds - 1
    /// in this Tournament; null if zero or more than one such Round exists (duplicates possible
    /// pre-Phase5 RoundNumber-uniqueness enforcement — never guessed) or its AdvanceCount is unset.
    /// </summary>
    public static async Task<int?> ComputeAsync(ApplicationDbContext db, Guid tournamentId, int maxRounds, int? maxParticipants)
    {
        if (maxRounds <= 1)
            return maxParticipants;

        var preFinalRoundNumber = maxRounds - 1;
        var advanceCounts = await db.Rounds
            .Where(r => r.TournamentId == tournamentId && r.RoundNumber == preFinalRoundNumber)
            .Select(r => r.AdvanceCount)
            .ToListAsync();

        return advanceCounts.Count == 1 ? advanceCounts[0] : null;
    }
}
