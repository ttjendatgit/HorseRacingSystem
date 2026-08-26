import test from "node:test";
import assert from "node:assert/strict";
import { isViolationResolved } from "./violationResolution.js";

test("null penalty is unresolved", () => {
  assert.equal(isViolationResolved({ penalty: null }), false);
});

test("undefined penalty is unresolved", () => {
  assert.equal(isViolationResolved({}), false);
});

test("empty string penalty is unresolved", () => {
  assert.equal(isViolationResolved({ penalty: "" }), false);
});

test("whitespace-only penalty is unresolved", () => {
  assert.equal(isViolationResolved({ penalty: "   " }), false);
});

test("non-whitespace penalty is resolved", () => {
  assert.equal(isViolationResolved({ penalty: "Warning" }), true);
});

test("PascalCase Penalty is read as a fallback", () => {
  assert.equal(isViolationResolved({ Penalty: "Warning" }), true);
  assert.equal(isViolationResolved({ Penalty: "   " }), false);
  assert.equal(isViolationResolved({ Penalty: null }), false);
});

test("camelCase penalty takes precedence over PascalCase Penalty", () => {
  assert.equal(isViolationResolved({ penalty: "Warning", Penalty: "" }), true);
  assert.equal(isViolationResolved({ penalty: "", Penalty: "Warning" }), false);
});

test("KPI resolved count and row status agree on the same list", () => {
  const violations = [
    { id: 1, penalty: null },
    { id: 2, penalty: undefined },
    { id: 3, penalty: "" },
    { id: 4, penalty: "   " },
    { id: 5, penalty: "Warning" },
    { id: 6, Penalty: "Disqualified" },
  ];

  const resolvedCount = violations.filter(isViolationResolved).length;
  assert.equal(resolvedCount, 2);

  const rowStatuses = violations.map((v) =>
    isViolationResolved(v) ? "Resolved" : "Pending"
  );
  assert.deepEqual(rowStatuses, [
    "Pending",
    "Pending",
    "Pending",
    "Pending",
    "Resolved",
    "Resolved",
  ]);
});
