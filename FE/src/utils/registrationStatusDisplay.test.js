import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { getRegistrationStatusLabel } from "./registrationStatusDisplay.js";

describe("registration status display", () => {
  test("maps RegistrationStatus values to Vietnamese labels", () => {
    assert.equal(getRegistrationStatusLabel("Approved"), "Đã duyệt");
    assert.equal(getRegistrationStatusLabel("Pending"), "Chờ duyệt");
    assert.equal(getRegistrationStatusLabel("Rejected"), "Từ chối");
    assert.equal(getRegistrationStatusLabel("Withdrawn"), "Đã rút");
  });

  test("is case-insensitive and falls back to Chờ duyệt for unknown values", () => {
    assert.equal(getRegistrationStatusLabel("approved"), "Đã duyệt");
    assert.equal(getRegistrationStatusLabel(""), "Chờ duyệt");
    assert.equal(getRegistrationStatusLabel(undefined), "Chờ duyệt");
  });

  test("does not expose raw enum values", () => {
    assert.notEqual(getRegistrationStatusLabel("Approved"), "Approved");
  });
});
