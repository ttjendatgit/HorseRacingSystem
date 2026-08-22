import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { getLatestHealthCheck } from "./healthCheckDisplay.js";

describe("latest health check selection", () => {
  test("older Passed, newer Failed -> latest is Failed", () => {
    const checks = [
      { status: "Passed", checkedAt: "2026-01-01T00:00:00Z" },
      { status: "Failed", checkedAt: "2026-01-02T00:00:00Z" },
    ];
    assert.equal(getLatestHealthCheck(checks).status, "Failed");
  });

  test("older Failed, newer Passed -> latest is Passed", () => {
    const checks = [
      { status: "Failed", checkedAt: "2026-01-01T00:00:00Z" },
      { status: "Passed", checkedAt: "2026-01-02T00:00:00Z" },
    ];
    assert.equal(getLatestHealthCheck(checks).status, "Passed");
  });

  test("input order does not matter", () => {
    const checks = [
      { status: "Passed", checkedAt: "2026-01-05T00:00:00Z" },
      { status: "Failed", checkedAt: "2026-01-01T00:00:00Z" },
      { status: "RequiresRecheck", checkedAt: "2026-01-03T00:00:00Z" },
    ];
    assert.equal(getLatestHealthCheck(checks).status, "Passed");
  });

  test("supports PascalCase CheckedAt as a fallback", () => {
    const checks = [
      { Status: "Failed", CheckedAt: "2026-01-01T00:00:00Z" },
      { Status: "Passed", CheckedAt: "2026-01-02T00:00:00Z" },
    ];
    assert.equal(getLatestHealthCheck(checks).Status, "Passed");
  });

  test("empty/missing input returns null", () => {
    assert.equal(getLatestHealthCheck([]), null);
    assert.equal(getLatestHealthCheck(undefined), null);
  });
});
