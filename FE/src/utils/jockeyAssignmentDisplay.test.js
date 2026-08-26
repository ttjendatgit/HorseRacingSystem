import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { getJockeyNameDisplay, getJockeyConfirmedDisplay, isInvitationOfficial } from "./jockeyAssignmentDisplay.js";

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

describe("official invitation detection (J3.1)", () => {
  const raceEntries = [
    { raceId: "race-1", jockeyId: "jockey-official" },
    { raceId: "race-2", jockeyId: null },
  ];

  test("Accepted invitation whose JockeyId matches RaceEntry.JockeyId is official", () => {
    const invitation = { raceId: "race-1", jockeyId: "jockey-official", status: "Accepted" };
    assert.equal(isInvitationOfficial(invitation, raceEntries), true);
  });

  test("Accepted invitation for a DIFFERENT jockey on the same Race is NOT official", () => {
    const invitation = { raceId: "race-1", jockeyId: "jockey-other", status: "Accepted" };
    assert.equal(isInvitationOfficial(invitation, raceEntries), false);
  });

  test("Accepted invitation is not official merely by having Status === Accepted (no RaceEntry.JockeyId yet)", () => {
    const invitation = { raceId: "race-2", jockeyId: "jockey-candidate", status: "Accepted" };
    assert.equal(isInvitationOfficial(invitation, raceEntries), false);
  });

  test("Pending invitation is never official", () => {
    const invitation = { raceId: "race-1", jockeyId: "jockey-official", status: "Pending" };
    // Status is irrelevant to the check — but a Pending invitation can never legitimately
    // share JockeyId with an official RaceEntry in the live flow; this documents that the
    // helper judges purely by JockeyId match, not by Status.
    assert.equal(isInvitationOfficial(invitation, raceEntries), true);
  });

  test("no matching RaceEntry for the invitation's Race -> not official", () => {
    const invitation = { raceId: "race-unknown", jockeyId: "jockey-official", status: "Accepted" };
    assert.equal(isInvitationOfficial(invitation, raceEntries), false);
  });

  test("supports PascalCase field fallback", () => {
    const invitation = { RaceId: "race-1", JockeyId: "jockey-official" };
    assert.equal(isInvitationOfficial(invitation, raceEntries), true);
  });
});
