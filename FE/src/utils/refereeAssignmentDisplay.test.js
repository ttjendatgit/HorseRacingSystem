import test from "node:test";
import assert from "node:assert/strict";
import {
  filterAssignmentsByTab,
  formatDateTime,
  getAssignmentStatusDetails,
  getAssignmentTabCounts,
  getDefaultAssignmentTab,
  getScheduledAt,
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

test("getScheduledAt reads Race.ScheduledAt (camelCase/PascalCase) now exposed by the API", () => {
  assert.equal(getScheduledAt({ scheduledAt: "2026-08-19T05:45:00" }), "2026-08-19T05:45:00");
  assert.equal(getScheduledAt({ ScheduledAt: "2026-08-19T05:45:00" }), "2026-08-19T05:45:00");
});

test("getScheduledAt is undefined when no schedule-like field is present (null ScheduledAt from API)", () => {
  assert.equal(getScheduledAt({ scheduledAt: null }), undefined);
  assert.equal(getScheduledAt({}), undefined);
});

test("formatDateTime renders a populated ScheduledAt as a real Vietnamese date/time, not 'Chưa xác định'", () => {
  const formatted = formatDateTime("2026-08-19T05:45:00");
  assert.notEqual(formatted, "Chưa xác định");
  assert.match(formatted, /2026/);
});

test("formatDateTime falls back to 'Chưa xác định' for null/undefined, never DateTime.MinValue", () => {
  assert.equal(formatDateTime(null), "Chưa xác định");
  assert.equal(formatDateTime(undefined), "Chưa xác định");
  assert.doesNotMatch(formatDateTime(null), /0001|year 1/i);
});

test("end-to-end: a populated Race.ScheduledAt no longer resolves to 'Chưa xác định' via getScheduledAt+formatDateTime", () => {
  const assignment = { scheduledAt: "2026-08-19T05:45:00" };
  assert.notEqual(formatDateTime(getScheduledAt(assignment)), "Chưa xác định");
});
