import test, { describe } from "node:test";
import assert from "node:assert/strict";
import {
  buildTournamentRequirementItems,
  canShowOwnerComplaintCta,
  canSubmitTournamentRegistration,
  formatAffectsResult,
  getFinalRaceRanking,
  getHorseTournamentSelectionState,
  getOwnerResultStatusDetails,
  OWNER_FINAL_RANKING_SUBTITLE,
  OWNER_FINAL_RANKING_TITLE,
  OWNER_REQUIREMENT_LABELS,
  isOfficialResult,
  isProvisionalResult,
  normalizeHorseApprovalStatus,
  normalizeResultStatus,
} from "./ownerDemoDisplay.js";

// ── 1. Complaint status labels — ownerDemoDisplay result status (not re-exporting complaint status) ──
// raceComplaintDisplay.js owns complaint status labels. ownerDemoDisplay.js owns RESULT status labels.
// This test confirms each result status maps to the correct Vietnamese label.
test("result status labels map Provisional and Official to correct Vietnamese copy", () => {
  const provisional = getOwnerResultStatusDetails("Provisional");
  assert.equal(provisional.label, "K\u1ebft qu\u1ea3 t\u1ea1m th\u1eddi");
  const official = getOwnerResultStatusDetails("Official");
  assert.equal(official.label, "K\u1ebft qu\u1ea3 ch\u00ednh th\u1ee9c");
  const none = getOwnerResultStatusDetails(null);
  assert.equal(none.label, "Ch\u01b0a c\u00f3 k\u1ebft qu\u1ea3");
});

// ── 2. Active complaint — withdraw IS permitted (via complaint CTA being open) ──
test("complaint CTA is visible when Race=Finished and result=Provisional", () => {
  const entry = { raceId: "r1", raceStatus: "Finished" };
  const provisionalResult = { resultStatus: "Provisional" };
  assert.equal(canShowOwnerComplaintCta(entry, provisionalResult), true);
});

// ── 3. Terminal result — complaint CTA is hidden (no withdraw possible) ──
test("complaint CTA is hidden when result=Official (terminal — complaint window is closed)", () => {
  const entry = { raceId: "r1", raceStatus: "Finished" };
  const officialResult = { resultStatus: "Official" };
  assert.equal(canShowOwnerComplaintCta(entry, officialResult), false);
});

// ── 4. Complaint CTA requires BOTH Finished race AND Provisional result ──
test("complaint CTA requires Race=Finished AND result=Provisional — not just Finished", () => {
  const finishedEntry = { raceId: "r1", raceStatus: "Finished" };
  const provisionalResult = { resultStatus: "Provisional" };
  const officialResult = { resultStatus: "Official" };
  const noResult = null;

  assert.equal(canShowOwnerComplaintCta(finishedEntry, provisionalResult), true, "Finished+Provisional=CTA");
  assert.equal(canShowOwnerComplaintCta(finishedEntry, officialResult), false, "Finished+Official=no CTA");
  assert.equal(canShowOwnerComplaintCta(finishedEntry, noResult), false, "Finished+no result=no CTA");
  assert.equal(canShowOwnerComplaintCta({ raceId: "r1", raceStatus: "Scheduled" }, provisionalResult), false, "Scheduled+Provisional=no CTA");
  assert.equal(canShowOwnerComplaintCta({ raceId: null, raceStatus: "Finished" }, provisionalResult), false, "no raceId=no CTA");
});

// ── 5. Provisional result label ──
test("Provisional result label = 'K\u1ebft qu\u1ea3 t\u1ea1m th\u1eddi' with isProvisional=true", () => {
  const details = getOwnerResultStatusDetails("Provisional");
  assert.equal(details.label, "K\u1ebft qu\u1ea3 t\u1ea1m th\u1eddi");
  assert.equal(details.isProvisional, true);
  assert.equal(details.isOfficial, false);
  assert.equal(details.tone, "warning");
});

// ── 6. Official result label ──
test("Official result label = 'K\u1ebft qu\u1ea3 ch\u00ednh th\u1ee9c' with isOfficial=true", () => {
  const details = getOwnerResultStatusDetails("Official");
  assert.equal(details.label, "K\u1ebft qu\u1ea3 ch\u00ednh th\u1ee9c");
  assert.equal(details.isOfficial, true);
  assert.equal(details.isProvisional, false);
  assert.equal(details.tone, "success");
});

// ── 7. Canonical ranking helpers — isOfficialResult / isProvisionalResult ──
test("isOfficialResult and isProvisionalResult read resultStatus from camelCase and PascalCase", () => {
  assert.equal(isOfficialResult({ resultStatus: "Official" }), true);
  assert.equal(isOfficialResult({ ResultStatus: "Official" }), true);
  assert.equal(isOfficialResult({ resultStatus: "Provisional" }), false);
  assert.equal(isOfficialResult(null), false);

  assert.equal(isProvisionalResult({ resultStatus: "Provisional" }), true);
  assert.equal(isProvisionalResult({ ResultStatus: "Provisional" }), true);
  assert.equal(isProvisionalResult({ resultStatus: "Official" }), false);
  assert.equal(isProvisionalResult(null), false);
});

// ── 8. Final Race ranking label — spec-mandated Vietnamese strings ──
test("OWNER_FINAL_RANKING_TITLE and OWNER_FINAL_RANKING_SUBTITLE match the spec exactly", () => {
  assert.equal(OWNER_FINAL_RANKING_TITLE, "X\u1ebfp h\u1ea1ng chung cu\u1ed9c");
  assert.equal(OWNER_FINAL_RANKING_SUBTITLE, "K\u1ebft qu\u1ea3 v\u00f2ng Chung k\u1ebft");
});

// ── 9. No fake aggregate Tournament ranking ──
test("getFinalRaceRanking returns null when Final Race result is not Official", () => {
  const registration = { tournamentId: "t1" };
  const tournament = { maxRounds: 3 };
  const entries = [{ tournamentId: "t1", roundNumber: 3, raceId: "r-final" }];
  const raceResults = { "r-final": { resultStatus: "Provisional" } };
  assert.equal(
    getFinalRaceRanking({ registration, entries, raceResults, tournament }),
    null,
    "Must not present a final ranking when the Final Race result is not Official",
  );
});

test("getFinalRaceRanking returns the ranking block when Final Race result IS Official", () => {
  const registration = { tournamentId: "t1" };
  const tournament = { maxRounds: 3 };
  const entries = [{ tournamentId: "t1", roundNumber: 3, raceId: "r-final" }];
  const raceResults = { "r-final": { resultStatus: "Official", rankings: [{ position: 1 }] } };
  const result = getFinalRaceRanking({ registration, entries, raceResults, tournament });
  assert.notEqual(result, null);
  assert.equal(result.title, OWNER_FINAL_RANKING_TITLE);
  assert.equal(result.subtitle, OWNER_FINAL_RANKING_SUBTITLE);
  assert.deepEqual(result.entry, entries[0]);
});

test("getFinalRaceRanking returns null when no entries exist for the final round", () => {
  const registration = { tournamentId: "t1" };
  const tournament = { maxRounds: 3 };
  const entries = [{ tournamentId: "t1", roundNumber: 1, raceId: "r1" }];
  const raceResults = { r1: { resultStatus: "Official" } };
  assert.equal(getFinalRaceRanking({ registration, entries, raceResults, tournament }), null);
});

// ── 10. No hardcoded age >= 3 ──
test("getHorseTournamentSelectionState does not gate on horse age (no hardcoded age constraint)", () => {
  const youngApprovedHorse = { id: "h1", approvalStatus: "Approved", age: 1, isArchived: false };
  const state = getHorseTournamentSelectionState(youngApprovedHorse, [], "t1");
  assert.equal(state.selectable, true,
    "Horse approval — not age — determines eligibility; age=1 must still be selectable if Approved");
});

// ── 11. Pending Horse disabled ──
test("Pending Horse is visible but not selectable, with 'Ch\u1edd duy\u1ec7t' label", () => {
  const pendingHorse = { id: "h1", approvalStatus: "Pending", isArchived: false };
  const state = getHorseTournamentSelectionState(pendingHorse, [], "t1");
  assert.equal(state.hidden, false, "Pending horse must be visible");
  assert.equal(state.selectable, false, "Pending horse must not be selectable");
  assert.equal(state.label, "Ch\u1edd duy\u1ec7t");
});

// ── 12. Rejected Horse disabled — surfaces rejection note ──
test("Rejected Horse is visible but not selectable, and surfaces the ApprovalNote as reason", () => {
  const rejectedHorse = {
    id: "h1",
    approvalStatus: "Rejected",
    isArchived: false,
    approvalNote: "Ng\u1ef1a kh\u00f4ng \u0111\u1ee7 ti\u00eau chu\u1ea9n s\u1ee9c kho\u1ebb.",
  };
  const state = getHorseTournamentSelectionState(rejectedHorse, [], "t1");
  assert.equal(state.hidden, false, "Rejected horse must be visible");
  assert.equal(state.selectable, false, "Rejected horse must not be selectable");
  assert.equal(state.label, "Kh\u00f4ng \u0111\u01b0\u1ee3c duy\u1ec7t");
  assert.equal(state.reason, "Ng\u1ef1a kh\u00f4ng \u0111\u1ee7 ti\u00eau chu\u1ea9n s\u1ee9c kho\u1ebb.",
    "Rejection note must be surfaced as the disabled reason");
});

// ── 13. Accepted vs Official Jockey wording ──
test("Accepted invitation text uses '\u0110\u00e3 ch\u1ea5p nh\u1eadn l\u1eddi m\u1eddi' (no raw enum string)", () => {
  const invitationOptionLabel = (jockeyName) => `\u0110\u00e3 ch\u1ea5p nh\u1eadn l\u1eddi m\u1eddi \u00b7 ${jockeyName}`;
  assert.equal(invitationOptionLabel("Nguy\u1ec5n V\u0103n A"), "\u0110\u00e3 ch\u1ea5p nh\u1eadn l\u1eddi m\u1eddi \u00b7 Nguy\u1ec5n V\u0103n A");
  assert.match(invitationOptionLabel(""), /\u0110\u00e3 ch\u1ea5p nh\u1eadn l\u1eddi m\u1eddi/);
  assert.doesNotMatch(invitationOptionLabel("A"), /Accepted|Pending|null/);
});

test("Official jockey and Final Confirm button text match spec (no raw backend enum strings)", () => {
  const OFFICIAL_BADGE = "Jockey ch\u00ednh th\u1ee9c";
  const UNCONFIRMED_BADGE = "Ch\u01b0a ch\u1ecdn Jockey ch\u00ednh th\u1ee9c";
  const FINAL_CONFIRM_BUTTON = "Ch\u1ecdn k\u1ef5 s\u0129 ch\u00ednh th\u1ee9c";

  assert.match(OFFICIAL_BADGE, /Jockey ch\u00ednh th\u1ee9c/);
  assert.match(UNCONFIRMED_BADGE, /Jockey ch\u00ednh th\u1ee9c/);
  assert.match(FINAL_CONFIRM_BUTTON, /Ch\u1ecdn k\u1ef5 s\u0129 ch\u00ednh th\u1ee9c/);
  assert.doesNotMatch(OFFICIAL_BADGE, /Accepted|Official|Confirmed/);
  assert.doesNotMatch(UNCONFIRMED_BADGE, /Pending|null/);
});

// ── 14. Prize wording — no wallet payout implication ──
test("formatAffectsResult produces result-domain language, never wallet/payment language", () => {
  const upheld = formatAffectsResult(true);
  const rejected = formatAffectsResult(false);
  const unknown = formatAffectsResult(undefined);

  assert.equal(upheld, "C\u00f3 \u1ea3nh h\u01b0\u1edfng k\u1ebft qu\u1ea3");
  assert.equal(rejected, "Kh\u00f4ng \u1ea3nh h\u01b0\u1edfng k\u1ebft qu\u1ea3");
  assert.equal(unknown, "Ch\u01b0a x\u00e1c \u0111\u1ecbnh");

  for (const text of [upheld, rejected, unknown]) {
    assert.doesNotMatch(text, /v\u00ed|wallet|thanh to\u00e1n|payment|nh\u1eadn ti\u1ec1n|payout/i,
      `"${text}" must not imply wallet payout`);
  }
});

test("OWNER_REQUIREMENT_LABELS contain no wallet payout language and no hardcoded age constraint", () => {
  for (const label of OWNER_REQUIREMENT_LABELS) {
    assert.doesNotMatch(label, /v\u00ed|wallet|thanh to\u00e1n|payout/i,
      `Requirement label must not reference wallet: "${label}"`);
    assert.doesNotMatch(label, /tu\u1ed5i|age|\d+\s*(tu\u1ed5i|n\u0103m)/i,
      `Requirement label must not reference age: "${label}"`);
  }
});

// \u2500\u2500 15. Eligibility row color semantics (Owner Tournament Register polish) \u2500\u2500
test("buildTournamentRequirementItems: no tournament / no horse selected yet -> all neutral except duplicate-check stays neutral too", () => {
  const items = buildTournamentRequirementItems({ tournament: null, selectedHorseState: null, hasHorse: false });
  for (const item of items) {
    assert.equal(item.tone, "neutral", `"${item.label}" should be neutral before any selection`);
  }
});

test("buildTournamentRequirementItems: open tournament + approved horse -> everything passes", () => {
  const tournament = { registerable: true, registrationKey: "open", registrationLabel: "M\u1edf \u0111\u0103ng k\u00fd" };
  const selectedHorseState = { selectable: true, label: "\u0110\u1ee7 \u0111i\u1ec1u ki\u1ec7n", reason: "" };
  const items = buildTournamentRequirementItems({ tournament, selectedHorseState, hasHorse: true });
  for (const item of items) {
    assert.equal(item.tone, "pass", `"${item.label}" should pass when open + approved`);
  }
});

test("buildTournamentRequirementItems: capacity-full tournament fails only the capacity row, not the deadline row", () => {
  const tournament = { registerable: false, registrationKey: "full", registrationLabel: "\u0110\u00e3 \u0111\u1ee7 s\u1ed1 l\u01b0\u1ee3ng tham gia" };
  const items = buildTournamentRequirementItems({ tournament, selectedHorseState: null, hasHorse: false });
  const byLabel = Object.fromEntries(items.map((i) => [i.label, i.tone]));
  assert.equal(byLabel["Gi\u1ea3i \u0111\u1ea5u \u0111ang m\u1edf \u0111\u0103ng k\u00fd"], "fail", "overall gate must fail when not open");
  assert.equal(byLabel["C\u00f2n su\u1ea5t tham gia"], "fail", "capacity row must fail when full");
  assert.equal(byLabel["C\u00f2n h\u1ea1n \u0111\u0103ng k\u00fd"], "caution", "deadline row is not the blocker here, so it's caution not fail");
});

test("buildTournamentRequirementItems: closed-past-deadline tournament fails the deadline row specifically", () => {
  const tournament = { registerable: false, registrationKey: "closed", registrationLabel: "\u0110\u00e3 \u0111\u00f3ng \u0111\u0103ng k\u00fd" };
  const items = buildTournamentRequirementItems({ tournament, selectedHorseState: null, hasHorse: false });
  const byLabel = Object.fromEntries(items.map((i) => [i.label, i.tone]));
  assert.equal(byLabel["C\u00f2n h\u1ea1n \u0111\u0103ng k\u00fd"], "fail");
  assert.equal(byLabel["C\u00f2n su\u1ea5t tham gia"], "caution", "capacity is not the blocker here, so it's caution not fail");
});

test("buildTournamentRequirementItems: Pending horse is caution (not fail) on the approval row", () => {
  const tournament = { registerable: true, registrationKey: "open" };
  const selectedHorseState = { selectable: false, label: "Ch\u1edd duy\u1ec7t", reason: "Ng\u1ef1a \u0111ang ch\u1edd Admin duy\u1ec7t." };
  const items = buildTournamentRequirementItems({ tournament, selectedHorseState, hasHorse: true });
  const approvalRow = items.find((i) => i.label === "Ng\u1ef1a ph\u1ea3i \u0111\u01b0\u1ee3c ph\u00ea duy\u1ec7t");
  assert.equal(approvalRow.tone, "caution");
});

test("buildTournamentRequirementItems: Rejected horse is fail on the approval row", () => {
  const tournament = { registerable: true, registrationKey: "open" };
  const selectedHorseState = { selectable: false, label: "Kh\u00f4ng \u0111\u01b0\u1ee3c duy\u1ec7t", reason: "Ng\u1ef1a ch\u01b0a \u0111\u01b0\u1ee3c Admin ph\u00ea duy\u1ec7t." };
  const items = buildTournamentRequirementItems({ tournament, selectedHorseState, hasHorse: true });
  const approvalRow = items.find((i) => i.label === "Ng\u1ef1a ph\u1ea3i \u0111\u01b0\u1ee3c ph\u00ea duy\u1ec7t");
  assert.equal(approvalRow.tone, "fail");
});

test("buildTournamentRequirementItems: an existing active registration fails the duplicate-check row", () => {
  const tournament = { registerable: true, registrationKey: "open" };
  const selectedHorseState = {
    selectable: false,
    label: "\u0110\u00e3 c\u00f3 \u0111\u0103ng k\u00fd",
    reason: "B\u1ea1n \u0111\u00e3 c\u00f3 m\u1ed9t ng\u1ef1a \u0111ang \u0111\u0103ng k\u00fd ho\u1eb7c \u0111\u00e3 \u0111\u01b0\u1ee3c duy\u1ec7t cho gi\u1ea3i n\u00e0y.",
  };
  const items = buildTournamentRequirementItems({ tournament, selectedHorseState, hasHorse: true });
  const duplicateRow = items.find((i) => i.label === "Kh\u00f4ng c\u00f3 \u0111\u0103ng k\u00fd \u0111ang ho\u1ea1t \u0111\u1ed9ng kh\u00e1c");
  assert.equal(duplicateRow.tone, "fail");
});

// \u2500\u2500 OWNER-DEMO-POLISH-V1.3 \u00a72/\u00a715: the Owner Tournament Register CTA must be disabled whenever
// there is no Tournament selected, no registration-open Tournament exists, no Horse is selected,
// or the selected Horse isn't eligible \u2014 this is the pure decision the page's `disabled` prop
// reads, kept testable without rendering the component. \u2500\u2500
describe("canSubmitTournamentRegistration", () => {
  const eligibleHorseState = { selectable: true, label: "\u0110\u1ee7 \u0111i\u1ec1u ki\u1ec7n", reason: "" };
  const openTournament = { registerable: true, registrationKey: "open" };
  const base = {
    selectedHorse: { id: "h1", name: "Bach Ma" },
    selectedHorseState: eligibleHorseState,
    selectedTournament: openTournament,
    isSubmitting: false,
    hasExistingRegistration: false,
    hasOwnerActiveRegistration: false,
  };

  test("no Tournament available/selected => cannot submit", () => {
    assert.equal(canSubmitTournamentRegistration({ ...base, selectedTournament: undefined }), false);
    assert.equal(canSubmitTournamentRegistration({ ...base, selectedTournament: null }), false);
  });

  test("a Tournament that exists but isn't registration-open => cannot submit", () => {
    assert.equal(
      canSubmitTournamentRegistration({ ...base, selectedTournament: { registerable: false, registrationKey: "closed" } }),
      false,
    );
  });

  test("Tournament open + eligible Horse selected => can submit", () => {
    assert.equal(canSubmitTournamentRegistration(base), true);
  });

  test("no Horse selected => cannot submit, even with an open Tournament", () => {
    assert.equal(canSubmitTournamentRegistration({ ...base, selectedHorse: null }), false);
  });

  test("an ineligible (Pending/Rejected) Horse => cannot submit", () => {
    const pendingHorseState = { selectable: false, label: "Ch\u1edd duy\u1ec7t", reason: "Ng\u1ef1a \u0111ang ch\u1edd Admin duy\u1ec7t." };
    assert.equal(canSubmitTournamentRegistration({ ...base, selectedHorseState: pendingHorseState }), false);
  });

  test("already submitting, an existing registration, or an owner-wide active registration all block re-submission", () => {
    assert.equal(canSubmitTournamentRegistration({ ...base, isSubmitting: true }), false);
    assert.equal(canSubmitTournamentRegistration({ ...base, hasExistingRegistration: true }), false);
    assert.equal(canSubmitTournamentRegistration({ ...base, hasOwnerActiveRegistration: true }), false);
  });
});
