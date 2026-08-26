import test from "node:test";
import assert from "node:assert/strict";
import {
  filterAssignmentsByTab,
  getAssignmentStatusDetails,
  getAssignmentTabCounts,
  getDefaultAssignmentTab,
  isPendingAssignment,
} from "./refereeAssignmentDisplay.js";

test("referee assignment status details preserve existing business meanings", () => {
  assert.deepEqual(getAssignmentStatusDetails("Assigned"), {
    label: "Chờ xử lý",
    variant: "warning",
    group: "pending",
  });
  assert.deepEqual(getAssignmentStatusDetails("Confirmed"), {
    label: "Đã xác nhận",
    variant: "success",
    group: "confirmed",
  });
  assert.deepEqual(getAssignmentStatusDetails("Completed"), {
    label: "Hoàn thành",
    variant: "success",
    group: "confirmed",
  });
  assert.deepEqual(getAssignmentStatusDetails("Cancelled"), {
    label: "Đã từ chối",
    variant: "danger",
    group: "rejected",
  });
});

test("referee assignment tabs count camelCase and PascalCase status fields", () => {
  const assignments = [
    { id: "a1", status: "Assigned" },
    { id: "a2", Status: "Pending" },
    { id: "a3", status: "Confirmed" },
    { id: "a4", Status: "Completed" },
    { id: "a5", status: "Rejected" },
    { id: "a6", status: "Cancelled" },
  ];

  assert.deepEqual(getAssignmentTabCounts(assignments), {
    pending: 2,
    confirmed: 2,
    rejected: 2,
    all: 6,
  });
});

test("default assignment tab prefers pending only when pending rows exist", () => {
  assert.equal(getDefaultAssignmentTab({ pending: 1, confirmed: 0, rejected: 0, all: 1 }), "pending");
  assert.equal(getDefaultAssignmentTab({ pending: 0, confirmed: 1, rejected: 0, all: 1 }), "all");
});

test("filterAssignmentsByTab only hides rows outside the selected group", () => {
  const assignments = [
    { id: "pending", status: "Assigned" },
    { id: "confirmed", status: "Confirmed" },
    { id: "rejected", status: "Cancelled" },
  ];

  assert.equal(isPendingAssignment("Assigned"), true);
  assert.deepEqual(filterAssignmentsByTab(assignments, "pending"), [assignments[0]]);
  assert.deepEqual(filterAssignmentsByTab(assignments, "confirmed"), [assignments[1]]);
  assert.deepEqual(filterAssignmentsByTab(assignments, "rejected"), [assignments[2]]);
  assert.deepEqual(filterAssignmentsByTab(assignments, "all"), assignments);
});
