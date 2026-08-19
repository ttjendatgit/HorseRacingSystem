import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { getOwnerRaceStatusLabel } from "./raceStatusDisplay.js";

describe("owner race status display", () => {
  test("maps registration race statuses to neutral owner schedule label", () => {
    assert.equal(getOwnerRaceStatusLabel("RegistrationOpen"), "Chuẩn bị");
    assert.equal(getOwnerRaceStatusLabel("RegistrationClosed"), "Chuẩn bị");
  });

  test("does not expose raw registration enum values", () => {
    assert.notEqual(getOwnerRaceStatusLabel("RegistrationOpen"), "RegistrationOpen");
    assert.notEqual(getOwnerRaceStatusLabel("RegistrationClosed"), "RegistrationClosed");
  });
});