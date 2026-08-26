import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { buildFullRankingSubmission, isFullRankingComplete } from "./raceResultSubmission.js";

describe("buildFullRankingSubmission (SELECTIVE-REFEREE-VNHNAM-INTEGRATION)", () => {
  test("submits the full ranking — every RaceEntry appears exactly once", () => {
    const resultEntries = [
      { horseId: "a", horseName: "Alpha" },
      { horseId: "b", horseName: "Bravo" },
      { horseId: "c", horseName: "Charlie" },
    ];
    const resultPositions = { a: 2, b: 1, c: 3 };

    const payload = buildFullRankingSubmission(resultEntries, resultPositions);

    assert.deepEqual(payload.rankings, [
      { horseId: "a", position: 2 },
      { horseId: "b", position: 1 },
      { horseId: "c", position: 3 },
    ]);
  });

  // ── The core regression guard: a vnhnam commit (3084ac9) added a top-level
  // `winningHorseId` field alongside `rankings` — that legacy single-winner
  // contract must never reappear in the submission payload. ──
  test("never includes a winningHorseId field, regardless of who is in position 1", () => {
    const resultEntries = [{ horseId: "a" }, { horseId: "b" }];
    const resultPositions = { a: 1, b: 2 };

    const payload = buildFullRankingSubmission(resultEntries, resultPositions);

    assert.equal("winningHorseId" in payload, false);
    assert.equal("winnerHorseId" in payload, false);
    assert.deepEqual(Object.keys(payload), ["rankings"]);
  });

  test("supports PascalCase HorseId as a fallback", () => {
    const resultEntries = [{ HorseId: "a" }, { HorseId: "b" }];
    const resultPositions = { a: 1, b: 2 };

    const payload = buildFullRankingSubmission(resultEntries, resultPositions);

    assert.deepEqual(payload.rankings, [
      { horseId: "a", position: 1 },
      { horseId: "b", position: 2 },
    ]);
  });

  test("an empty entry list still submits a well-formed (empty) rankings array, never throws", () => {
    assert.deepEqual(buildFullRankingSubmission([], {}), { rankings: [] });
    assert.deepEqual(buildFullRankingSubmission(null, {}), { rankings: [] });
  });
});

describe("isFullRankingComplete", () => {
  test("true only when every entry has a position and no two entries share one", () => {
    const entries = [{ horseId: "a" }, { horseId: "b" }, { horseId: "c" }];
    assert.equal(isFullRankingComplete(entries, { a: 1, b: 2, c: 3 }), true);
  });

  test("false when a position is missing", () => {
    const entries = [{ horseId: "a" }, { horseId: "b" }];
    assert.equal(isFullRankingComplete(entries, { a: 1, b: "" }), false);
    assert.equal(isFullRankingComplete(entries, { a: 1 }), false);
  });

  test("false when two entries share the same position (not a valid full ranking)", () => {
    const entries = [{ horseId: "a" }, { horseId: "b" }, { horseId: "c" }];
    assert.equal(isFullRankingComplete(entries, { a: 1, b: 1, c: 2 }), false);
  });

  test("false when there are zero entries — nothing to rank yet", () => {
    assert.equal(isFullRankingComplete([], {}), false);
  });
});
