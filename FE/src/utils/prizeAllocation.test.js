import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  normalizeTournamentStatus,
  isTournamentDraftEditable,
  computeAllocatedTotal,
  computeRemainingBudget,
  sortPrizesByPosition,
  hasPrizeAllocations,
  isAllocationComplete,
  formatVndCurrency,
  validatePrizeForm,
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
  test("zero pool + zero rows is complete", () => {
    assert.equal(isAllocationComplete(0, []), true);
  });

  test("zero pool + positive rows is incomplete", () => {
    assert.equal(isAllocationComplete(0, [{ amount: 1 }]), false);
  });

  test("positive pool requires exact sum match", () => {
    assert.equal(isAllocationComplete(1000, [{ amount: 600 }, { amount: 400 }]), true);
    assert.equal(isAllocationComplete(1000, [{ amount: 600 }]), false);
    assert.equal(isAllocationComplete(1000, [{ amount: 600 }, { amount: 500 }]), false);
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

describe("validatePrizeForm", () => {
  const prizePool = 1000;

  test("position < 1 is rejected", () => {
    const errors = validatePrizeForm({ position: 0, amount: 100, prizePool, existingPrizes: [] });
    assert.equal(errors.position, "Hạng thưởng phải lớn hơn hoặc bằng 1.");
  });

  test("duplicate position is rejected", () => {
    const existingPrizes = [{ id: "a", position: 1, amount: 100 }];
    const errors = validatePrizeForm({ position: 1, amount: 100, prizePool, existingPrizes });
    assert.equal(errors.position, "Hạng thưởng này đã được cấu hình.");
  });

  test("editing own row does not self-collide on position", () => {
    const existingPrizes = [{ id: "a", position: 1, amount: 100 }];
    const errors = validatePrizeForm({ position: 1, amount: 200, prizePool, existingPrizes, editingPrizeId: "a" });
    assert.equal(errors.position, undefined);
  });

  test("amount <= 0 is rejected", () => {
    const errors = validatePrizeForm({ position: 1, amount: 0, prizePool, existingPrizes: [] });
    assert.equal(errors.amount, "Tiền thưởng phải lớn hơn 0.");
  });

  test("sum exceeding PrizePool is rejected", () => {
    const existingPrizes = [{ id: "a", position: 1, amount: 800 }];
    const errors = validatePrizeForm({ position: 2, amount: 300, prizePool, existingPrizes });
    assert.equal(errors.amount, "Tổng tiền thưởng không được vượt quá quỹ thưởng của giải đấu.");
  });

  test("sum exactly equal to PrizePool is allowed", () => {
    const existingPrizes = [{ id: "a", position: 1, amount: 700 }];
    const errors = validatePrizeForm({ position: 2, amount: 300, prizePool, existingPrizes });
    assert.deepEqual(errors, {});
  });

  test("editing own row excludes its own current amount from the sum check", () => {
    const existingPrizes = [{ id: "a", position: 1, amount: 700 }];
    const errors = validatePrizeForm({ position: 1, amount: 1000, prizePool, existingPrizes, editingPrizeId: "a" });
    assert.deepEqual(errors, {});
  });

  test("valid form has no errors", () => {
    const errors = validatePrizeForm({ position: 1, amount: 500, prizePool, existingPrizes: [] });
    assert.deepEqual(errors, {});
  });
});
