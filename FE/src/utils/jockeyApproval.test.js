import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { normalizeJockeyApprovalStatus, getJockeyApprovalDisplay } from "./jockeyApproval.js";

describe("normalizeJockeyApprovalStatus", () => {
  test("numeric enum", () => {
    assert.equal(normalizeJockeyApprovalStatus(1), "Pending");
    assert.equal(normalizeJockeyApprovalStatus(2), "Approved");
    assert.equal(normalizeJockeyApprovalStatus(3), "Rejected");
  });

  test("string enum name, any case", () => {
    assert.equal(normalizeJockeyApprovalStatus("Pending"), "Pending");
    assert.equal(normalizeJockeyApprovalStatus("approved"), "Approved");
    assert.equal(normalizeJockeyApprovalStatus("REJECTED"), "Rejected");
  });

  test("numeric-as-string", () => {
    assert.equal(normalizeJockeyApprovalStatus("1"), "Pending");
    assert.equal(normalizeJockeyApprovalStatus("2"), "Approved");
    assert.equal(normalizeJockeyApprovalStatus("3"), "Rejected");
  });

  test("unknown/null/undefined/empty is a safe null fallback", () => {
    assert.equal(normalizeJockeyApprovalStatus(null), null);
    assert.equal(normalizeJockeyApprovalStatus(undefined), null);
    assert.equal(normalizeJockeyApprovalStatus(""), null);
    assert.equal(normalizeJockeyApprovalStatus("something-else"), null);
    assert.equal(normalizeJockeyApprovalStatus(99), null);
  });
});

describe("getJockeyApprovalDisplay", () => {
  test("Pending: camelCase field", () => {
    const result = getJockeyApprovalDisplay({ approvalStatus: "Pending" });
    assert.equal(result.status, "Pending");
    assert.equal(result.label, "Đang chờ Admin phê duyệt");
    assert.equal(result.tone, "pending");
    assert.equal(result.isPending, true);
    assert.equal(result.isApproved, false);
    assert.equal(result.isRejected, false);
    assert.equal(result.note, null);
  });

  test("Approved: PascalCase field", () => {
    const result = getJockeyApprovalDisplay({ ApprovalStatus: "Approved" });
    assert.equal(result.status, "Approved");
    assert.equal(result.label, "Đã được Admin phê duyệt");
    assert.equal(result.tone, "approved");
    assert.equal(result.isApproved, true);
  });

  test("Rejected: numeric enum with ApprovalNote shown", () => {
    const result = getJockeyApprovalDisplay({ approvalStatus: 3, approvalNote: "Thiếu giấy phép hợp lệ" });
    assert.equal(result.status, "Rejected");
    assert.equal(result.label, "Hồ sơ đã bị từ chối");
    assert.equal(result.tone, "rejected");
    assert.equal(result.isRejected, true);
    assert.equal(result.note, "Thiếu giấy phép hợp lệ");
  });

  test("Rejected with no ApprovalNote does not invent a reason", () => {
    const result = getJockeyApprovalDisplay({ approvalStatus: "Rejected" });
    assert.equal(result.isRejected, true);
    assert.equal(result.note, null);
  });

  test("Rejected with blank/whitespace-only ApprovalNote is treated as absent", () => {
    const result = getJockeyApprovalDisplay({ approvalStatus: "Rejected", approvalNote: "   " });
    assert.equal(result.note, null);
  });

  test("ApprovalNote on a non-Rejected status is never surfaced", () => {
    const result = getJockeyApprovalDisplay({ approvalStatus: "Approved", approvalNote: "stale note" });
    assert.equal(result.note, null);
  });

  test("missing/unknown profile is a safe fallback, not a crash", () => {
    const missing = getJockeyApprovalDisplay(null);
    assert.equal(missing.status, null);
    assert.equal(missing.tone, "unknown");
    assert.equal(missing.isPending, false);
    assert.equal(missing.isApproved, false);
    assert.equal(missing.isRejected, false);

    const unknownValue = getJockeyApprovalDisplay({ approvalStatus: "SomethingNew" });
    assert.equal(unknownValue.status, null);
    assert.equal(unknownValue.tone, "unknown");
  });

  test("ApprovalStatusName fallback is used when ApprovalStatus is absent", () => {
    const result = getJockeyApprovalDisplay({ approvalStatusName: "Pending" });
    assert.equal(result.status, "Pending");
  });
});
