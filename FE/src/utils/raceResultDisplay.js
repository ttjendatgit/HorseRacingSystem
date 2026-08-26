// Pure display helpers for Official/Provisional Race results.
//
// Qualification authority stays RaceResult.RankingsJson — the backend already parses it
// server-side into RaceResultResponse.Rankings ({position, horseId, horseName}, pre-sorted
// ascending). RaceEntry.FinishPosition is denormalized display/stat data only, written by R0
// approval, and must never be treated as a second source of qualification truth. These helpers
// are shape-agnostic (they read whichever position-like field is present) so callers can run them
// over the canonical Rankings array (the normal path) or, as a legacy-safety fallback, over raw
// RaceEntries when an old Official result has no usable Rankings at all.

const RESULT_STATUS_LABELS = Object.freeze({
  provisional: "Kết quả tạm thời",
  official: "Kết quả chính thức",
});

// Raw ResultStatus ("Provisional"/"Official") must never leak into the UI verbatim —
// this is the single label mapping other pages should reuse instead of an inline map.
export const getResultStatusLabel = (resultStatus) => {
  const key = String(resultStatus ?? "").trim().toLowerCase();
  return RESULT_STATUS_LABELS[key] ?? null;
};

// null/undefined/empty => null. Only a positive integer position is valid.
export const getFinishPosition = (entry) => {
  const raw = entry?.position ?? entry?.Position ?? entry?.finishPosition ?? entry?.FinishPosition;
  if (raw === null || raw === undefined || raw === "") return null;
  const num = Number(raw);
  if (!Number.isInteger(num) || num <= 0) return null;
  return num;
};

// Keeps only entries with a valid position, sorted ascending. Never mutates the input array.
export const getRankedEntries = (entries) => {
  if (!Array.isArray(entries)) return [];
  return entries
    .filter((entry) => getFinishPosition(entry) !== null)
    .slice()
    .sort((a, b) => getFinishPosition(a) - getFinishPosition(b));
};

// RESULT-APPROVAL-REVIEW-UX: builds the full, ordered ranking display list an Admin needs to
// review BEFORE approving a Provisional result — never gated on Official/Provisional, since the
// backend already parses RankingsJson into `rankings` at both lifecycle stages (see
// RaceService.GetRaceResultAsync/LiveResultService). `entries` (RaceEntry-shaped, from the
// separate /entries endpoint) is used ONLY to look up JockeyName by HorseId — never for
// FinishPosition, which stays Official-only per R0. Malformed/legacy rankings safely degrade to [].
export const buildRankingDisplayList = (rankings, entries) => {
  const ranked = getRankedEntries(rankings);
  const entryList = Array.isArray(entries) ? entries : [];
  return ranked.map((r) => {
    const horseId = r.horseId ?? r.HorseId;
    const position = getFinishPosition(r);
    const entry = entryList.find((e) => (e.horseId ?? e.HorseId) === horseId);
    return {
      position,
      horseId,
      horseName: r.horseName ?? r.HorseName ?? entry?.horseName ?? entry?.HorseName ?? null,
      jockeyName: entry?.jockeyName ?? entry?.JockeyName ?? null,
      isWinner: position === 1,
    };
  });
};

// isFinal: Round.RoundNumber === Tournament.MaxRounds (V0/V0.1) — never AdvanceCount/QualificationSlots.
// qualificationSlots: only meaningful for a non-final Race; missing/invalid never guesses a label.
export const getPlacementLabel = ({ position, isFinal, qualificationSlots }) => {
  if (position === null || position === undefined) return "";

  if (isFinal) {
    return position === 1 ? "Vô địch" : `Hạng ${position}`;
  }

  // Number(null) === 0 and Number("") === 0 in JS — an explicit presence check is required first,
  // or a genuinely-missing slots value would silently coerce to a valid "0 slots" and misreport.
  if (qualificationSlots === null || qualificationSlots === undefined || qualificationSlots === "") {
    return `Hạng ${position}`;
  }
  const slots = Number(qualificationSlots);
  if (!Number.isFinite(slots) || slots < 0) {
    return `Hạng ${position}`;
  }
  return position <= slots ? "Đi tiếp" : "Bị loại";
};
