import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  normalizeTournamentStatus,
  isTournamentDraftEditable,
  computeAllocatedTotal,
  computeRemainingBudget,
  computeAllocatedPercentage,
  computeRemainingPercentage,
  sortPrizesByPosition,
  hasPrizeAllocations,
  isAllocationComplete,
  formatVndCurrency,
  formatPercentage,
  formatPrizeFigure,
  computePreviewAmount,
  isValidPercentage,
  validatePrizeForm,
  computePlannedFinalParticipants,
  getMaxRankLabel,
  getDefaultPrizeName,
  PRESET_PERCENTAGES,
} from "./prizeAllocation.js";

describe("normalizeTournamentStatus", () => {
  test("numeric enum", () => {
    assert.equal(normalizeTournamentStatus(0), "Draft");
    assert.equal(normalizeTournamentStatus(1), "Published");
    assert.equal(normalizeTournamentStatus(4), "Cancelled");
  });

  test("string enum name, any case", () => {
    assert.equal(normalizeTournamentStatus("draft"), "Draft");
    assert.equal(normalizeTournamentStatus("ONGOING"), "Ongoing");
  });

  test("numeric-as-string", () => {
    assert.equal(normalizeTournamentStatus("0"), "Draft");
    assert.equal(normalizeTournamentStatus("3"), "Finished");
  });

  test("unknown/null/undefined/empty is a safe null fallback", () => {
    assert.equal(normalizeTournamentStatus(null), null);
    assert.equal(normalizeTournamentStatus(undefined), null);
    assert.equal(normalizeTournamentStatus(""), null);
    assert.equal(normalizeTournamentStatus("bogus"), null);
    assert.equal(normalizeTournamentStatus(99), null);
  });
});

describe("isTournamentDraftEditable", () => {
  test("Draft (camelCase status) is editable", () => {
    assert.equal(isTournamentDraftEditable({ status: 0 }), true);
  });

  test("Draft (PascalCase Status) is editable", () => {
    assert.equal(isTournamentDraftEditable({ Status: 0 }), true);
  });

  test("Published/Ongoing/Finished/Cancelled are not editable", () => {
    assert.equal(isTournamentDraftEditable({ status: 1 }), false);
    assert.equal(isTournamentDraftEditable({ status: 2 }), false);
    assert.equal(isTournamentDraftEditable({ status: 3 }), false);
    assert.equal(isTournamentDraftEditable({ status: 4 }), false);
  });

  test("missing/unknown status is not editable", () => {
    assert.equal(isTournamentDraftEditable({}), false);
    assert.equal(isTournamentDraftEditable(null), false);
  });
});

describe("computeAllocatedTotal / computeRemainingBudget", () => {
  test("sums Amount across camelCase rows", () => {
    const prizes = [{ amount: 600000 }, { amount: 300000 }, { amount: 100000 }];
    assert.equal(computeAllocatedTotal(prizes), 1000000);
    assert.equal(computeRemainingBudget(1000000, prizes), 0);
  });

  test("sums Amount across PascalCase rows", () => {
    const prizes = [{ Amount: 500 }, { Amount: 500 }];
    assert.equal(computeAllocatedTotal(prizes), 1000);
  });

  test("empty/non-array is zero", () => {
    assert.equal(computeAllocatedTotal([]), 0);
    assert.equal(computeAllocatedTotal(null), 0);
    assert.equal(computeAllocatedTotal(undefined), 0);
  });

  test("remaining budget can go negative (over-allocated legacy data)", () => {
    assert.equal(computeRemainingBudget(100, [{ amount: 150 }]), -50);
  });
});

describe("computeAllocatedPercentage / computeRemainingPercentage", () => {
  test("sums PercentageOfPool across camelCase rows", () => {
    const prizes = [{ percentageOfPool: 50 }, { percentageOfPool: 30 }];
    assert.equal(computeAllocatedPercentage(prizes), 80);
    assert.equal(computeRemainingPercentage(prizes), 20);
  });

  test("sums PercentageOfPool across PascalCase rows", () => {
    const prizes = [{ PercentageOfPool: 60 }, { PercentageOfPool: 40 }];
    assert.equal(computeAllocatedPercentage(prizes), 100);
    assert.equal(computeRemainingPercentage(prizes), 0);
  });

  test("empty/non-array is zero allocated, 100 remaining", () => {
    assert.equal(computeAllocatedPercentage([]), 0);
    assert.equal(computeRemainingPercentage(null), 100);
  });

  test("handles decimal percentages without float drift", () => {
    const prizes = [{ percentageOfPool: 33.33 }, { percentageOfPool: 33.33 }, { percentageOfPool: 33.34 }];
    assert.equal(computeAllocatedPercentage(prizes), 100);
    assert.equal(computeRemainingPercentage(prizes), 0);
  });
});

describe("sortPrizesByPosition", () => {
  test("sorts ascending by Position regardless of input order", () => {
    const prizes = [{ position: 3 }, { position: 1 }, { position: 2 }];
    assert.deepEqual(
      sortPrizesByPosition(prizes).map((p) => p.position),
      [1, 2, 3]
    );
  });

  test("does not mutate the input array", () => {
    const prizes = [{ position: 2 }, { position: 1 }];
    const sorted = sortPrizesByPosition(prizes);
    assert.notEqual(sorted, prizes);
    assert.equal(prizes[0].position, 2);
  });

  test("non-array input returns empty array", () => {
    assert.deepEqual(sortPrizesByPosition(null), []);
  });
});

describe("hasPrizeAllocations", () => {
  test("true only for a non-empty array", () => {
    assert.equal(hasPrizeAllocations([{ position: 1 }]), true);
    assert.equal(hasPrizeAllocations([]), false);
    assert.equal(hasPrizeAllocations(null), false);
  });
});

describe("isAllocationComplete", () => {
  test("no rows is never complete", () => {
    assert.equal(isAllocationComplete([]), false);
  });

  test("percentage total below 100 is incomplete", () => {
    assert.equal(isAllocationComplete([{ percentageOfPool: 50 }, { percentageOfPool: 30 }]), false);
  });

  test("percentage total exactly 100 is complete", () => {
    assert.equal(isAllocationComplete([{ percentageOfPool: 60 }, { percentageOfPool: 40 }]), true);
  });

  test("percentage total above 100 is not reported complete (defensive)", () => {
    assert.equal(isAllocationComplete([{ percentageOfPool: 70 }, { percentageOfPool: 40 }]), false);
  });
});

describe("formatVndCurrency", () => {
  test("formats with vi-VN grouping and dong sign", () => {
    assert.equal(formatVndCurrency(10000000), "10.000.000 ₫");
  });

  test("non-numeric input formats as zero", () => {
    assert.equal(formatVndCurrency(undefined), "0 ₫");
    assert.equal(formatVndCurrency(null), "0 ₫");
  });
});

describe("formatPercentage", () => {
  test("whole numbers show no decimal places", () => {
    assert.equal(formatPercentage(30), "30%");
    assert.equal(formatPercentage(100), "100%");
  });

  test("half-percent shows one decimal place", () => {
    assert.equal(formatPercentage(27.5), "27.5%");
    assert.equal(formatPercentage(12.5), "12.5%");
  });

  test("two-decimal percentages are preserved exactly", () => {
    assert.equal(formatPercentage(33.33), "33.33%");
  });

  test("never shows trailing zeros like 30.0000%", () => {
    assert.equal(formatPercentage(30.0), "30%");
  });

  test("non-numeric input formats as 0%", () => {
    assert.equal(formatPercentage(undefined), "0%");
    assert.equal(formatPercentage(null), "0%");
  });
});

describe("formatPrizeFigure", () => {
  test("positive percentage shows both percentage and amount", () => {
    assert.equal(formatPrizeFigure(50, 1000000000), "50% · 1.000.000.000 ₫");
  });

  test("legacy PercentageOfPool <= 0 falls back to amount only, never '0% · amount'", () => {
    assert.equal(formatPrizeFigure(0, 1000000000), "1.000.000.000 ₫");
  });

  test("missing/undefined percentage also falls back to amount only", () => {
    assert.equal(formatPrizeFigure(undefined, 1000000000), "1.000.000.000 ₫");
    assert.equal(formatPrizeFigure(null, 1000000000), "1.000.000.000 ₫");
  });

  test("negative percentage (defensive) also falls back to amount only", () => {
    assert.equal(formatPrizeFigure(-5, 1000000000), "1.000.000.000 ₫");
  });
});

describe("computePreviewAmount", () => {
  test("computes a rounded preview from PrizePool and percentage", () => {
    assert.equal(computePreviewAmount(2000000000, 30), 600000000);
  });

  test("zero or invalid percentage previews as 0", () => {
    assert.equal(computePreviewAmount(1000, 0), 0);
    assert.equal(computePreviewAmount(1000, -5), 0);
    assert.equal(computePreviewAmount(1000, undefined), 0);
  });

  test("rounds to the nearest whole VND", () => {
    assert.equal(computePreviewAmount(100, 33.33), 33);
  });
});

describe("isValidPercentage", () => {
  test("accepts decimals within (0, 100]", () => {
    assert.equal(isValidPercentage(50), true);
    assert.equal(isValidPercentage(12.5), true);
    assert.equal(isValidPercentage(33.33), true);
    assert.equal(isValidPercentage(100), true);
  });

  test("rejects zero, negative, above 100, and non-numeric", () => {
    assert.equal(isValidPercentage(0), false);
    assert.equal(isValidPercentage(-1), false);
    assert.equal(isValidPercentage(100.01), false);
    assert.equal(isValidPercentage("abc"), false);
  });
});

describe("getMaxRankLabel", () => {
  test("wraps a numeric max rank as Hạng N", () => {
    assert.equal(getMaxRankLabel(10), "Hạng 10");
  });

  test("null/undefined stays null (indeterminate)", () => {
    assert.equal(getMaxRankLabel(null), null);
    assert.equal(getMaxRankLabel(undefined), null);
  });
});

describe("getDefaultPrizeName", () => {
  test("Position 1-3 get named defaults", () => {
    assert.equal(getDefaultPrizeName(1), "Vô địch");
    assert.equal(getDefaultPrizeName(2), "Á quân");
    assert.equal(getDefaultPrizeName(3), "Quý quân");
  });

  test("Position 4+ falls back to Hạng N", () => {
    assert.equal(getDefaultPrizeName(4), "Hạng 4");
    assert.equal(getDefaultPrizeName(10), "Hạng 10");
  });
});

describe("PRESET_PERCENTAGES", () => {
  test("is a non-empty array of numbers used as quick-fill shortcuts only", () => {
    assert.ok(Array.isArray(PRESET_PERCENTAGES));
    assert.ok(PRESET_PERCENTAGES.length > 0);
    assert.ok(PRESET_PERCENTAGES.every((n) => typeof n === "number" && n > 0 && n <= 100));
  });
});

describe("validatePrizeForm", () => {
  const prizePool = 1000;

  test("position < 1 is rejected", () => {
    const errors = validatePrizeForm({ position: 0, percentage: 10, prizePool, existingPrizes: [] });
    assert.equal(errors.position, "Hạng thưởng phải lớn hơn hoặc bằng 1.");
  });

  test("duplicate position is rejected", () => {
    const existingPrizes = [{ id: "a", position: 1, percentageOfPool: 10 }];
    const errors = validatePrizeForm({ position: 1, percentage: 10, prizePool, existingPrizes });
    assert.equal(errors.position, "Hạng thưởng này đã được cấu hình.");
  });

  test("editing own row does not self-collide on position", () => {
    const existingPrizes = [{ id: "a", position: 1, percentageOfPool: 10 }];
    const errors = validatePrizeForm({ position: 1, percentage: 20, prizePool, existingPrizes, editingPrizeId: "a" });
    assert.equal(errors.position, undefined);
  });

  test("percentage <= 0 is rejected", () => {
    const errors = validatePrizeForm({ position: 1, percentage: 0, prizePool, existingPrizes: [] });
    assert.equal(errors.percentage, "Tỷ lệ phân bổ phải lớn hơn 0.");
  });

  test("percentage above 100 is rejected", () => {
    const errors = validatePrizeForm({ position: 1, percentage: 101, prizePool, existingPrizes: [] });
    assert.equal(errors.percentage, "Tỷ lệ phân bổ không được vượt quá 100%.");
  });

  test("total percentage exceeding 100 is rejected", () => {
    const existingPrizes = [{ id: "a", position: 1, percentageOfPool: 80 }];
    const errors = validatePrizeForm({ position: 2, percentage: 30, prizePool, existingPrizes });
    assert.equal(errors.percentage, "Tổng tỷ lệ phân bổ không được vượt quá 100%.");
  });

  test("total percentage exactly 100 is allowed", () => {
    const existingPrizes = [{ id: "a", position: 1, percentageOfPool: 70 }];
    const errors = validatePrizeForm({ position: 2, percentage: 30, prizePool, existingPrizes });
    assert.deepEqual(errors, {});
  });

  test("editing own row excludes its own current percentage from the total check", () => {
    const existingPrizes = [{ id: "a", position: 1, percentageOfPool: 70 }];
    const errors = validatePrizeForm({ position: 1, percentage: 100, prizePool, existingPrizes, editingPrizeId: "a" });
    assert.deepEqual(errors, {});
  });

  test("valid form has no errors", () => {
    const errors = validatePrizeForm({ position: 1, percentage: 50, prizePool, existingPrizes: [] });
    assert.deepEqual(errors, {});
  });

  test("position above maxRank is rejected with the exact message", () => {
    const errors = validatePrizeForm({ position: 4, percentage: 50, prizePool, existingPrizes: [], maxRank: 3 });
    assert.equal(errors.position, "Hạng thưởng chỉ được từ 1 đến 3.");
  });

  test("position equal to maxRank is allowed", () => {
    const errors = validatePrizeForm({ position: 3, percentage: 50, prizePool, existingPrizes: [], maxRank: 3 });
    assert.equal(errors.position, undefined);
  });

  test("maxRank omitted skips the check entirely", () => {
    const errors = validatePrizeForm({ position: 999, percentage: 50, prizePool: 100000, existingPrizes: [] });
    assert.equal(errors.position, undefined);
  });
});

describe("computePlannedFinalParticipants", () => {
  test("single-round (MaxRounds omitted/1) uses Tournament.MaxParticipants", () => {
    assert.equal(computePlannedFinalParticipants({ maxParticipants: 3 }, []), 3);
    assert.equal(computePlannedFinalParticipants({ maxRounds: 1, maxParticipants: 5 }, []), 5);
  });

  test("single-round with MaxParticipants unset is indeterminate (null)", () => {
    assert.equal(computePlannedFinalParticipants({ maxRounds: 1, maxParticipants: null }, []), null);
  });

  test("multi-round uses the pre-Final Round's AdvanceCount", () => {
    // Tournament.MaxParticipants=16, MaxRounds=3 -> pre-Final is Round 2, AdvanceCount=4.
    const tournament = { maxRounds: 3, maxParticipants: 16 };
    const rounds = [
      { roundNumber: 1, advanceCount: 8 },
      { roundNumber: 2, advanceCount: 4 },
      { roundNumber: 3, advanceCount: 0 },
    ];
    assert.equal(computePlannedFinalParticipants(tournament, rounds), 4);
  });

  test("multi-round with PascalCase round fields", () => {
    const tournament = { MaxRounds: 2, MaxParticipants: 10 };
    const rounds = [{ RoundNumber: 1, AdvanceCount: 4 }];
    assert.equal(computePlannedFinalParticipants(tournament, rounds), 4);
  });

  test("multi-round with missing pre-Final Round is indeterminate (null), never guessed", () => {
    const tournament = { maxRounds: 3, maxParticipants: 16 };
    const rounds = [{ roundNumber: 1, advanceCount: 8 }]; // Round 2 (pre-Final) missing
    assert.equal(computePlannedFinalParticipants(tournament, rounds), null);
  });

  test("multi-round with duplicate pre-Final Round rows is indeterminate (null)", () => {
    const tournament = { maxRounds: 2, maxParticipants: 10 };
    const rounds = [{ roundNumber: 1, advanceCount: 4 }, { roundNumber: 1, advanceCount: 5 }];
    assert.equal(computePlannedFinalParticipants(tournament, rounds), null);
  });

  test("multi-round with unset AdvanceCount on the pre-Final Round is indeterminate (null)", () => {
    const tournament = { maxRounds: 2, maxParticipants: 10 };
    const rounds = [{ roundNumber: 1, advanceCount: null }];
    assert.equal(computePlannedFinalParticipants(tournament, rounds), null);
  });
});
