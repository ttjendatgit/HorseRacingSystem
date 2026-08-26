import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  isRaceGateEditable,
  isValidGateNumber,
  getGateValidationError,
  sortEntriesByGate,
  formatGateLabel,
  isEntryGateAssignable,
  getGateReadinessSummary,
} from "./gateAssignment.js";

describe("isRaceGateEditable", () => {
  test("pre-start statuses are editable", () => {
    assert.equal(isRaceGateEditable("Scheduled"), true);
    assert.equal(isRaceGateEditable("RegistrationOpen"), true);
    assert.equal(isRaceGateEditable("RegistrationClosed"), true);
  });

  test("post-start statuses are locked", () => {
    assert.equal(isRaceGateEditable("InProgress"), false);
    assert.equal(isRaceGateEditable("Finished"), false);
    assert.equal(isRaceGateEditable("Cancelled"), false);
  });

  test("missing/unknown status is not editable", () => {
    assert.equal(isRaceGateEditable(null), false);
    assert.equal(isRaceGateEditable(undefined), false);
    assert.equal(isRaceGateEditable(""), false);
    assert.equal(isRaceGateEditable("Bogus"), false);
  });
});

describe("isValidGateNumber", () => {
  test("integer within [1, maxParticipants] is valid", () => {
    assert.equal(isValidGateNumber(1, 12), true);
    assert.equal(isValidGateNumber(12, 12), true);
    assert.equal(isValidGateNumber(6, 12), true);
  });

  test("zero/negative/above-max are invalid", () => {
    assert.equal(isValidGateNumber(0, 12), false);
    assert.equal(isValidGateNumber(-1, 12), false);
    assert.equal(isValidGateNumber(13, 12), false);
  });

  test("non-integer is invalid", () => {
    assert.equal(isValidGateNumber(1.5, 12), false);
    assert.equal(isValidGateNumber("abc", 12), false);
  });
});

describe("getGateValidationError", () => {
  test("empty value asks for input", () => {
    assert.equal(getGateValidationError("", 12), "Vui lòng nhập số cổng.");
    assert.equal(getGateValidationError(null, 12), "Vui lòng nhập số cổng.");
  });

  test("out-of-range value returns the range message", () => {
    assert.equal(getGateValidationError(0, 12), "Cổng xuất phát phải từ 1 đến 12.");
    assert.equal(getGateValidationError(13, 12), "Cổng xuất phát phải từ 1 đến 12.");
  });

  test("valid value returns null", () => {
    assert.equal(getGateValidationError(5, 12), null);
    assert.equal(getGateValidationError(12, 12), null);
  });
});

describe("sortEntriesByGate", () => {
  test("sorts ascending by gate, nulls last", () => {
    const entries = [{ gateNumber: 3 }, { gateNumber: null }, { gateNumber: 1 }];
    const sorted = sortEntriesByGate(entries).map((e) => e.gateNumber);
    assert.deepEqual(sorted, [1, 3, null]);
  });

  test("handles PascalCase field", () => {
    const entries = [{ GateNumber: 2 }, { GateNumber: 1 }];
    assert.deepEqual(sortEntriesByGate(entries).map((e) => e.GateNumber), [1, 2]);
  });

  test("does not mutate the input array", () => {
    const entries = [{ gateNumber: 2 }, { gateNumber: 1 }];
    const sorted = sortEntriesByGate(entries);
    assert.notEqual(sorted, entries);
    assert.equal(entries[0].gateNumber, 2);
  });

  test("non-array input returns empty array", () => {
    assert.deepEqual(sortEntriesByGate(null), []);
  });
});

describe("formatGateLabel", () => {
  test("null/undefined/empty format as Chưa xếp", () => {
    assert.equal(formatGateLabel(null), "Chưa xếp");
    assert.equal(formatGateLabel(undefined), "Chưa xếp");
    assert.equal(formatGateLabel(""), "Chưa xếp");
  });

  test("a number formats as its string value", () => {
    assert.equal(formatGateLabel(5), "5");
  });
});

describe("isEntryGateAssignable", () => {
  test("Approved, not scratched is assignable", () => {
    assert.equal(isEntryGateAssignable({ status: "Approved", scratchedAt: null }), true);
  });

  test("Rejected is not assignable", () => {
    assert.equal(isEntryGateAssignable({ status: "Rejected" }), false);
  });

  test("scratched (any status) is not assignable", () => {
    assert.equal(isEntryGateAssignable({ status: "Approved", scratchedAt: "2026-01-01T00:00:00Z" }), false);
  });

  test("handles PascalCase fields", () => {
    assert.equal(isEntryGateAssignable({ Status: "Rejected" }), false);
    assert.equal(isEntryGateAssignable({ Status: "Approved", ScratchedAt: null }), true);
  });
});

describe("getGateReadinessSummary", () => {
  test("counts only participating entries", () => {
    const entries = [
      { status: "Approved", gateNumber: 1 },
      { status: "Approved", gateNumber: null },
      { status: "Rejected", gateNumber: null },
      { status: "Approved", scratchedAt: "2026-01-01", gateNumber: null },
    ];
    const summary = getGateReadinessSummary(entries);
    assert.equal(summary.total, 2);
    assert.equal(summary.assigned, 1);
    assert.equal(summary.missing, 1);
    assert.equal(summary.isComplete, false);
  });

  test("complete when every participating entry has a gate", () => {
    const entries = [
      { status: "Approved", gateNumber: 1 },
      { status: "Approved", gateNumber: 2 },
    ];
    assert.equal(getGateReadinessSummary(entries).isComplete, true);
  });

  test("empty list is not complete (nothing to be complete about)", () => {
    assert.equal(getGateReadinessSummary([]).isComplete, false);
    assert.equal(getGateReadinessSummary([]).total, 0);
  });
});
