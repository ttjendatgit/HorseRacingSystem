import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  groupJockeysByApprovalStatus,
  isRejectReasonValid,
  formatJockeyReviewValue,
  getLicenseDocumentType,
} from "./jockeyAdminReview.js";

describe("groupJockeysByApprovalStatus", () => {
  test("buckets Pending/Approved/Rejected separately, `all` preserves input order", () => {
    const jockeys = [
      { id: "1", approvalStatus: "Pending" },
      { id: "2", approvalStatus: "Approved" },
      { id: "3", approvalStatus: "Rejected" },
      { id: "4", ApprovalStatus: "Pending" }, // PascalCase fallback
      { id: "5", approvalStatus: 1 }, // numeric enum
      { id: "6", approvalStatus: 2 },
      { id: "7", approvalStatus: 3 },
    ];

    const groups = groupJockeysByApprovalStatus(jockeys);

    assert.deepEqual(groups.all.map((j) => j.id), ["1", "2", "3", "4", "5", "6", "7"]);
    assert.deepEqual(groups.pending.map((j) => j.id), ["1", "4", "5"]);
    assert.deepEqual(groups.approved.map((j) => j.id), ["2", "6"]);
    assert.deepEqual(groups.rejected.map((j) => j.id), ["3", "7"]);
    assert.deepEqual(groups.unknown, []);
  });

  test("pending count is the queue badge count", () => {
    const jockeys = [
      { id: "1", approvalStatus: "Pending" },
      { id: "2", approvalStatus: "Pending" },
      { id: "3", approvalStatus: "Approved" },
    ];
    assert.equal(groupJockeysByApprovalStatus(jockeys).pending.length, 2);
  });

  test("unrecognized/missing status falls into `unknown`, never dropped from `all`", () => {
    const jockeys = [{ id: "1" }, { id: "2", approvalStatus: "SomethingNew" }];
    const groups = groupJockeysByApprovalStatus(jockeys);
    assert.equal(groups.pending.length, 0);
    assert.equal(groups.approved.length, 0);
    assert.equal(groups.rejected.length, 0);
    assert.equal(groups.unknown.length, 2);
    assert.equal(groups.all.length, 2);
  });

  test("non-array input is a safe empty-list fallback for every bucket", () => {
    for (const input of [null, undefined, "not-an-array"]) {
      const groups = groupJockeysByApprovalStatus(input);
      assert.deepEqual(groups.all, []);
      assert.deepEqual(groups.pending, []);
      assert.deepEqual(groups.approved, []);
      assert.deepEqual(groups.rejected, []);
      assert.deepEqual(groups.unknown, []);
    }
  });
});

describe("isRejectReasonValid", () => {
  test("valid non-blank reason", () => {
    assert.equal(isRejectReasonValid("Giấy phép thi đấu không hợp lệ."), true);
  });

  test("blank/whitespace-only reason is invalid", () => {
    assert.equal(isRejectReasonValid(""), false);
    assert.equal(isRejectReasonValid("   "), false);
    assert.equal(isRejectReasonValid("\n\t "), false);
  });

  test("null/undefined/non-string is invalid", () => {
    assert.equal(isRejectReasonValid(null), false);
    assert.equal(isRejectReasonValid(undefined), false);
    assert.equal(isRejectReasonValid(42), false);
  });

  test("reason with surrounding whitespace but real content is valid", () => {
    assert.equal(isRejectReasonValid("  reason  "), true);
  });
});

describe("formatJockeyReviewValue", () => {
  test("passes through a real value", () => {
    assert.equal(formatJockeyReviewValue("0900000000"), "0900000000");
  });

  test("null/undefined/blank string fall back to the em-dash placeholder", () => {
    assert.equal(formatJockeyReviewValue(null), "—");
    assert.equal(formatJockeyReviewValue(undefined), "—");
    assert.equal(formatJockeyReviewValue(""), "—");
    assert.equal(formatJockeyReviewValue("   "), "—");
  });

  test("custom fallback text is honored", () => {
    assert.equal(formatJockeyReviewValue(null, "Chưa có tài liệu giấy phép"), "Chưa có tài liệu giấy phép");
  });

  test("never renders literal undefined/null strings", () => {
    const result = formatJockeyReviewValue(undefined);
    assert.notEqual(result, "undefined");
    assert.notEqual(result, "null");
  });

  test("numeric zero is a real value, not treated as missing", () => {
    assert.equal(formatJockeyReviewValue(0), 0);
  });
});

describe("getLicenseDocumentType", () => {
  test("recognizes common image extensions, case-insensitively", () => {
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/documents/license.jpg"), "image");
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/documents/license.JPEG"), "image");
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/documents/license.png"), "image");
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/documents/license.webp"), "image");
  });

  test("recognizes pdf, case-insensitively", () => {
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/documents/license.pdf"), "pdf");
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/documents/license.PDF"), "pdf");
  });

  test("query strings and fragments after the extension are ignored", () => {
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/license.jpg?v=123&x=1"), "image");
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/license.pdf#page=2"), "pdf");
  });

  test("unrecognized extension is 'unknown', not misclassified", () => {
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/license.docx"), "unknown");
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/license"), "unknown");
    assert.equal(getLicenseDocumentType("https://res.cloudinary.com/demo/license."), "unknown");
  });

  test("null/undefined/empty is 'none' (no file uploaded)", () => {
    assert.equal(getLicenseDocumentType(null), "none");
    assert.equal(getLicenseDocumentType(undefined), "none");
    assert.equal(getLicenseDocumentType(""), "none");
    assert.equal(getLicenseDocumentType("   "), "none");
  });
});
