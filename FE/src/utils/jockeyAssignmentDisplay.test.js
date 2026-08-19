import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { getJockeyNameDisplay, getJockeyConfirmedDisplay } from "./jockeyAssignmentDisplay.js";

describe("owner schedule jockey assignment display", () => {
  test("no jockey assigned shows unassigned name and unconfirmed status", () => {
    const entry = { jockeyId: null, jockeyName: "", jockeyConfirmed: true };
    assert.equal(getJockeyNameDisplay(entry), "Chưa phân công");
    assert.equal(getJockeyConfirmedDisplay(entry), "Chưa xác nhận");
  });

  test("jockey assigned but not yet confirmed shows unconfirmed status", () => {
    const entry = { jockeyId: "jockey-1", jockeyName: "Nguyen Van A", jockeyConfirmed: false };
    assert.equal(getJockeyNameDisplay(entry), "Nguyen Van A");
    assert.equal(getJockeyConfirmedDisplay(entry), "Chưa xác nhận");
  });

  test("jockey assigned and confirmed shows confirmed status", () => {
    const entry = { jockeyId: "jockey-1", jockeyName: "Nguyen Van A", jockeyConfirmed: true };
    assert.equal(getJockeyNameDisplay(entry), "Nguyen Van A");
    assert.equal(getJockeyConfirmedDisplay(entry), "Đã xác nhận");
  });
});
