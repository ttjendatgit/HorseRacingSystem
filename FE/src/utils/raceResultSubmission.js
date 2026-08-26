// SELECTIVE-REFEREE-VNHNAM-INTEGRATION: Referee Race Report result submission stays full-ranking
// (R0) — every RaceEntry must appear exactly once with a unique position 1..N, matching the
// backend's own validation (LiveResultService.ValidateAndCanonicalizeRankings). A vnhnam commit
// (3084ac9 "Fix submitting result error") added a `winningHorseId` field alongside `rankings` to
// this same payload — that legacy single-winner contract must never come back. Extracting the
// payload-building here (rather than leaving it inline in the page) makes both invariants — full
// ranking, no winningHorseId — directly testable without rendering the component.

export function isFullRankingComplete(resultEntries, resultPositions) {
  const entryCount = Array.isArray(resultEntries) ? resultEntries.length : 0;
  const assigned = Object.values(resultPositions || {}).filter((p) => p !== "" && p != null);
  return entryCount > 0 && assigned.length === entryCount && new Set(assigned).size === entryCount;
}

export function buildFullRankingSubmission(resultEntries, resultPositions) {
  const rankings = (Array.isArray(resultEntries) ? resultEntries : []).map((entry) => ({
    horseId: entry.horseId || entry.HorseId,
    position: resultPositions[entry.horseId || entry.HorseId],
  }));
  return { rankings };
}
