import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { getRegistrationStatusLabel, getRegistrationStatusTone } from "./registrationStatusDisplay.js";

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

describe("registration status tone", () => {
  test("maps each status to its semantic color tone", () => {
    assert.equal(getRegistrationStatusTone("Approved"), "pass");
    assert.equal(getRegistrationStatusTone("Pending"), "caution");
    assert.equal(getRegistrationStatusTone("Rejected"), "fail");
    assert.equal(getRegistrationStatusTone("Withdrawn"), "neutral");
  });

  test("is case-insensitive and falls back to neutral for unknown values", () => {
    assert.equal(getRegistrationStatusTone("approved"), "pass");
    assert.equal(getRegistrationStatusTone(""), "neutral");
    assert.equal(getRegistrationStatusTone(undefined), "neutral");
  });
});
