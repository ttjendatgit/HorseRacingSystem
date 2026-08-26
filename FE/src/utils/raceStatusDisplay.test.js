import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { getOwnerRaceStatusLabel, getOwnerRaceStatusTone } from "./raceStatusDisplay.js";

describe("owner race status display", () => {
  test("maps registration race statuses to neutral owner schedule label", () => {
    assert.equal(getOwnerRaceStatusLabel("RegistrationOpen"), "Chuẩn bị");
    assert.equal(getOwnerRaceStatusLabel("RegistrationClosed"), "Chuẩn bị");
  });

  test("does not expose raw registration enum values", () => {
    assert.notEqual(getOwnerRaceStatusLabel("RegistrationOpen"), "RegistrationOpen");
    assert.notEqual(getOwnerRaceStatusLabel("RegistrationClosed"), "RegistrationClosed");
  });

  // ── OWNER-DEMO-POLISH-V1.2 §2/§15: RaceStatus is authoritative for the lifecycle badge. A race
  // must never be labeled "Sắp diễn ra" (time-based) once it's Finished/Cancelled/InProgress. ──
  test("Finished -> Đã kết thúc, regardless of ScheduledAt", () => {
    assert.equal(getOwnerRaceStatusLabel("Finished"), "Đã kết thúc");
    assert.equal(getOwnerRaceStatusLabel("finished"), "Đã kết thúc");
  });

  test("Cancelled -> Đã hủy", () => {
    assert.equal(getOwnerRaceStatusLabel("Cancelled"), "Đã hủy");
  });

  test("InProgress -> Đang diễn ra", () => {
    assert.equal(getOwnerRaceStatusLabel("InProgress"), "Đang diễn ra");
  });

  test("case is irrelevant — PascalCase and lowercase both resolve the same", () => {
    assert.equal(getOwnerRaceStatusLabel("InProgress"), getOwnerRaceStatusLabel("inprogress"));
    assert.equal(getOwnerRaceStatusLabel("Cancelled"), getOwnerRaceStatusLabel("cancelled"));
  });
});

describe("owner race status tone", () => {
  test("Finished is 'pass' (green), Cancelled is 'fail' (red), InProgress is 'live'", () => {
    assert.equal(getOwnerRaceStatusTone("Finished"), "pass");
    assert.equal(getOwnerRaceStatusTone("Cancelled"), "fail");
    assert.equal(getOwnerRaceStatusTone("InProgress"), "live");
  });

  test("pre-start statuses (Scheduled/RegistrationOpen/RegistrationClosed) are 'caution'", () => {
    assert.equal(getOwnerRaceStatusTone("Scheduled"), "caution");
    assert.equal(getOwnerRaceStatusTone("RegistrationOpen"), "caution");
    assert.equal(getOwnerRaceStatusTone("RegistrationClosed"), "caution");
  });

  test("unknown/missing status falls back to neutral", () => {
    assert.equal(getOwnerRaceStatusTone(""), "neutral");
    assert.equal(getOwnerRaceStatusTone(undefined), "neutral");
    assert.equal(getOwnerRaceStatusTone("SomethingElse"), "neutral");
  });
});