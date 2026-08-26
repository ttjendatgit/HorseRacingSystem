import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { buildRankingDisplayList, getFinishPosition, getPlacementLabel, getRankedEntries } from "./raceResultDisplay.js";

describe("getFinishPosition", () => {
  test("reads camelCase position", () => {
    assert.equal(getFinishPosition({ position: 2 }), 2);
  });

  test("reads PascalCase Position fallback", () => {
    assert.equal(getFinishPosition({ Position: 3 }), 3);
  });

  test("also reads finishPosition/FinishPosition (legacy RaceEntry shape)", () => {
    assert.equal(getFinishPosition({ finishPosition: 1 }), 1);
    assert.equal(getFinishPosition({ FinishPosition: 4 }), 4);
  });

  test("null/undefined/empty => null", () => {
    assert.equal(getFinishPosition({ position: null }), null);
    assert.equal(getFinishPosition({ position: undefined }), null);
    assert.equal(getFinishPosition({ position: "" }), null);
    assert.equal(getFinishPosition({}), null);
  });

  test("only positive integers are valid", () => {
    assert.equal(getFinishPosition({ position: 0 }), null);
    assert.equal(getFinishPosition({ position: -1 }), null);
    assert.equal(getFinishPosition({ position: 1.5 }), null);
  });

  test("numeric strings are accepted (raw API/form state)", () => {
    assert.equal(getFinishPosition({ position: "2" }), 2);
  });
});

describe("getRankedEntries", () => {
  test("sorts 3,1,2 -> 1,2,3", () => {
    const entries = [{ position: 3, horseId: "c" }, { position: 1, horseId: "a" }, { position: 2, horseId: "b" }];
    const ranked = getRankedEntries(entries);
    assert.deepEqual(ranked.map((e) => e.horseId), ["a", "b", "c"]);
  });

  test("entries with missing/invalid FinishPosition are excluded", () => {
    const entries = [
      { position: 2, horseId: "b" },
      { position: null, horseId: "x" },
      { horseId: "y" },
      { position: 1, horseId: "a" },
    ];
    const ranked = getRankedEntries(entries);
    assert.deepEqual(ranked.map((e) => e.horseId), ["a", "b"]);
  });

  test("does not mutate the input array", () => {
    const entries = [{ position: 2 }, { position: 1 }];
    const original = [...entries];
    getRankedEntries(entries);
    assert.deepEqual(entries, original);
  });

  test("non-array input returns empty array", () => {
    assert.deepEqual(getRankedEntries(null), []);
    assert.deepEqual(getRankedEntries(undefined), []);
  });
});

describe("buildRankingDisplayList (RESULT-APPROVAL-REVIEW-UX)", () => {
  const rankings = [
    { position: 1, horseId: "bravo", horseName: "Horse Bravo" },
    { position: 2, horseId: "charlie", horseName: "Horse Charlie" },
    { position: 3, horseId: "alpha", horseName: "Horse Alpha" },
  ];
  const entries = [
    { horseId: "alpha", jockeyName: "Other Jockey 1" },
    { horseId: "bravo", jockeyName: "Jockey" },
    { horseId: "charlie", jockeyName: "Other Jockey 2" },
  ];

  test("a full Provisional RankingsJson produces every position, not just the winner", () => {
    const list = buildRankingDisplayList(rankings, entries);
    assert.equal(list.length, 3);
  });

  test("rows are ordered by Position ascending regardless of input order", () => {
    const shuffled = [rankings[2], rankings[0], rankings[1]];
    const list = buildRankingDisplayList(shuffled, entries);
    assert.deepEqual(list.map((r) => r.position), [1, 2, 3]);
    assert.deepEqual(list.map((r) => r.horseId), ["bravo", "charlie", "alpha"]);
  });

  test("Position 1 is flagged as the canonical winner, and only Position 1", () => {
    const list = buildRankingDisplayList(rankings, entries);
    assert.deepEqual(list.map((r) => r.isWinner), [true, false, false]);
  });

  test("jockey name is looked up by HorseId from the separate entries list, never from FinishPosition", () => {
    const list = buildRankingDisplayList(rankings, entries);
    assert.equal(list.find((r) => r.horseId === "bravo").jockeyName, "Jockey");
    assert.equal(list.find((r) => r.horseId === "charlie").jockeyName, "Other Jockey 2");
    assert.equal(list.find((r) => r.horseId === "alpha").jockeyName, "Other Jockey 1");
  });

  test("output does not depend on any Official/Provisional flag — same rankings always produce the same list", () => {
    // The helper never takes a resultStatus/isOfficial argument at all: callers must stop gating
    // on it before calling in, which is the actual fix for the "winner-only until Official" bug.
    const a = buildRankingDisplayList(rankings, entries);
    const b = buildRankingDisplayList(rankings, entries);
    assert.deepEqual(a, b);
  });

  test("empty/malformed RankingsJson-derived input fails safe to an empty list, no crash", () => {
    assert.deepEqual(buildRankingDisplayList([], entries), []);
    assert.deepEqual(buildRankingDisplayList(null, entries), []);
    assert.deepEqual(buildRankingDisplayList(undefined, entries), []);
    assert.deepEqual(buildRankingDisplayList([{ horseId: "x" }], entries), []); // no Position
    assert.deepEqual(buildRankingDisplayList(rankings, null), buildRankingDisplayList(rankings, undefined));
  });

  test("a corrected resubmission's new rankings fully replace the previous list (no stale merge)", () => {
    const before = buildRankingDisplayList(rankings, entries);
    const corrected = [
      { position: 1, horseId: "alpha", horseName: "Horse Alpha" },
      { position: 2, horseId: "bravo", horseName: "Horse Bravo" },
      { position: 3, horseId: "charlie", horseName: "Horse Charlie" },
    ];
    const after = buildRankingDisplayList(corrected, entries);

    assert.equal(before[0].horseId, "bravo");
    assert.equal(after[0].horseId, "alpha");
    assert.deepEqual(after.map((r) => r.horseId), ["alpha", "bravo", "charlie"]);
  });

  // ADMIN-TOURNAMENTS-REGRESSION-FIX #8/#9: RaceRankingPanel (AdminPage.jsx / TournamentDetail.jsx)
  // gates its Duyệt KQ/Từ chối `actions` slot purely on resultStatus === "provisional" — never on
  // whether a ranking happens to be present. Pin that the ranking data itself is available
  // whenever a Provisional result has valid RankingsJson, so the review data an Admin needs is
  // never accidentally empty right when the approval buttons are showing.
  test("a Provisional result with valid rankings always has non-empty rows for the same panel that will offer Duyệt KQ/Từ chối", () => {
    const provisionalRankings = [
      { Position: 1, HorseId: "bravo", HorseName: "Horse Bravo" },
      { Position: 2, HorseId: "charlie", HorseName: "Horse Charlie" },
      { Position: 3, HorseId: "alpha", HorseName: "Horse Alpha" },
    ];
    const rows = buildRankingDisplayList(provisionalRankings, entries);
    assert.equal(rows.length, 3);
    assert.equal(rows[0].isWinner, true);
  });

  // ── OWNER-DEMO-POLISH-V1.2 §1/§15: Owner Finished Tournament ranking was showing missing
  // Jockeys for Horses outside the logged-in Owner's own participation data. Root cause was the
  // CALLER passing an Owner-scoped RaceEntry list (their own Horses only) into this function —
  // buildRankingDisplayList itself was always correct, it just needs the FULL, race-wide RaceEntry
  // list (every Horse in that Race) to resolve every ranked Horse's official Jockey. These tests
  // pin that requirement so a future caller can't silently regress back to a scoped/partial list. ──
  test("full race-wide entries resolve the Jockey for every ranked Horse, including ones the viewing Owner does not own", () => {
    const officialRankings = [
      { position: 1, horseId: "bach-ma", horseName: "Bach Ma" },
      { position: 2, horseId: "alain", horseName: "Alain" },
      { position: 3, horseId: "anassar", horseName: "Anassar" },
    ];
    // Simulates GET /api/referees/race/{raceId}/entries — every Approved entry in the Race,
    // across every Owner, not filtered to any single viewer.
    const fullRaceEntries = [
      { horseId: "bach-ma", jockeyName: "Ky Si Mot" },
      { horseId: "alain", jockeyName: "Ky Si Hai" },
      { horseId: "anassar", jockeyName: "Ky Si Ba" },
    ];
    const rows = buildRankingDisplayList(officialRankings, fullRaceEntries);
    assert.equal(rows.find((r) => r.horseId === "bach-ma").jockeyName, "Ky Si Mot");
    assert.equal(rows.find((r) => r.horseId === "alain").jockeyName, "Ky Si Hai");
    assert.equal(rows.find((r) => r.horseId === "anassar").jockeyName, "Ky Si Ba");
  });

  test("an Owner-scoped (partial) entries list reproduces the original bug — other Owners' Horses show no Jockey", () => {
    const officialRankings = [
      { position: 1, horseId: "bach-ma", horseName: "Bach Ma" },
      { position: 2, horseId: "alain", horseName: "Alain" },
      { position: 3, horseId: "anassar", horseName: "Anassar" },
    ];
    // The viewing Owner only owns "Bach Ma" — this is what the old, buggy caller passed in.
    const ownerScopedEntries = [{ horseId: "bach-ma", jockeyName: "Ky Si Mot" }];
    const rows = buildRankingDisplayList(officialRankings, ownerScopedEntries);
    assert.equal(rows.find((r) => r.horseId === "bach-ma").jockeyName, "Ky Si Mot");
    assert.equal(rows.find((r) => r.horseId === "alain").jockeyName, null, "documents the bug: a scoped entries list loses this Jockey");
    assert.equal(rows.find((r) => r.horseId === "anassar").jockeyName, null, "documents the bug: a scoped entries list loses this Jockey");
  });
});

describe("getPlacementLabel", () => {
  test("non-final: position <= slots => Đi tiếp", () => {
    assert.equal(getPlacementLabel({ position: 1, isFinal: false, qualificationSlots: 3 }), "Đi tiếp");
    assert.equal(getPlacementLabel({ position: 3, isFinal: false, qualificationSlots: 3 }), "Đi tiếp");
  });

  test("non-final: position > slots => Bị loại", () => {
    assert.equal(getPlacementLabel({ position: 4, isFinal: false, qualificationSlots: 3 }), "Bị loại");
  });

  test("final: position 1 => Vô địch", () => {
    assert.equal(getPlacementLabel({ position: 1, isFinal: true, qualificationSlots: 0 }), "Vô địch");
  });

  test("final: position 2 => Hạng 2", () => {
    assert.equal(getPlacementLabel({ position: 2, isFinal: true, qualificationSlots: 0 }), "Hạng 2");
  });

  test("final never returns Đi tiếp/Bị loại regardless of qualificationSlots", () => {
    assert.equal(getPlacementLabel({ position: 4, isFinal: true, qualificationSlots: 3 }), "Hạng 4");
  });

  test("invalid/missing qualificationSlots on a non-final race does not falsely mark qualified/eliminated", () => {
    assert.equal(getPlacementLabel({ position: 1, isFinal: false, qualificationSlots: null }), "Hạng 1");
    assert.equal(getPlacementLabel({ position: 1, isFinal: false, qualificationSlots: undefined }), "Hạng 1");
    assert.equal(getPlacementLabel({ position: 1, isFinal: false, qualificationSlots: "not-a-number" }), "Hạng 1");
    assert.equal(getPlacementLabel({ position: 1, isFinal: false, qualificationSlots: -1 }), "Hạng 1");
  });

  test("missing position returns empty string", () => {
    assert.equal(getPlacementLabel({ isFinal: false, qualificationSlots: 3 }), "");
  });
});
