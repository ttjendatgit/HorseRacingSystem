import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { getJockeyDisplayStats } from "./jockeyStats.js";

describe("getJockeyDisplayStats", () => {
  test("reads camelCase profile fields", () => {
    assert.deepEqual(
      getJockeyDisplayStats({ totalRaces: 10, totalWins: 4, winRate: 40.5, leaderboardRank: 5 }),
      { totalRaces: 10, totalWins: 4, winRate: 40.5, rank: 5 },
    );
  });

  test("reads PascalCase profile fields", () => {
    assert.deepEqual(
      getJockeyDisplayStats({ TotalRaces: 8, TotalWins: 3, WinRate: 37, Rank: 12 }),
      { totalRaces: 8, totalWins: 3, winRate: 37, rank: 12 },
    );
  });

  test("falls back to computed win rate and null rank when fields are missing", () => {
    assert.deepEqual(
      getJockeyDisplayStats({ totalRaces: 5, totalWins: 2 }),
      { totalRaces: 5, totalWins: 2, winRate: 40, rank: null },
    );
  });
});

