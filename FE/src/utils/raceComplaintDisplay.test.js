import test from "node:test";
import assert from "node:assert/strict";
import {
  buildRuleRaceComplaintPayload,
  canFilerWithdraw,
  canRefereeRespond,
  filterRaceComplaintsByTab,
  getAvailableAdminRaceComplaintActions,
  getDefaultRaceComplaintTab,
  getRaceComplaintStatusDetails,
  getRaceComplaintTabCounts,
  getRaceComplaintTypeLabel,
  mapEligibleRacesToOptions,
  normalizeRaceComplaintStatus,
  normalizeRaceComplaintType,
} from "./raceComplaintDisplay.js";

test("status labels map every lifecycle status to its admin tab group", () => {
  assert.deepEqual(getRaceComplaintStatusDetails("Pending"), {
    status: "Pending",
    label: "Chờ tiếp nhận",
    variant: "pending",
    group: "intake",
  });
  assert.deepEqual(getRaceComplaintStatusDetails("AwaitingRefereeResponse"), {
    status: "AwaitingRefereeResponse",
    label: "Chờ trọng tài giải trình",
    variant: "active",
    group: "awaiting",
  });
  assert.deepEqual(getRaceComplaintStatusDetails("UnderReview"), {
    status: "UnderReview",
    label: "Đang xem xét",
    variant: "active",
    group: "underReview",
  });
  assert.deepEqual(getRaceComplaintStatusDetails("Upheld"), {
    status: "Upheld",
    label: "Chấp nhận khiếu nại",
    variant: "approved",
    group: "resolved",
  });
  assert.deepEqual(getRaceComplaintStatusDetails("Rejected"), {
    status: "Rejected",
    label: "Bác khiếu nại",
    variant: "rejected",
    group: "resolved",
  });
  assert.deepEqual(getRaceComplaintStatusDetails("Withdrawn"), {
    status: "Withdrawn",
    label: "Đã rút",
    variant: "inactive",
    group: "resolved",
  });
});

test("normalize functions tolerate raw enum casing/spacing from the API", () => {
  assert.equal(normalizeRaceComplaintStatus("awaiting_referee_response"), "AwaitingRefereeResponse");
  assert.equal(normalizeRaceComplaintStatus("under review"), "UnderReview");
  assert.equal(normalizeRaceComplaintType("race-operation"), "RaceOperation");
  assert.equal(normalizeRaceComplaintType("resultjudging"), "ResultJudging");
});

test("complaint type labels use the required Vietnamese copy", () => {
  assert.equal(getRaceComplaintTypeLabel("ResultJudging"), "Chấm kết quả không công bằng");
  assert.equal(getRaceComplaintTypeLabel("RaceOperation"), "Điều hành cuộc đua không đúng");
});

test("admin tabs count and filter complaints by lifecycle group", () => {
  const complaints = [
    { id: "c1", status: "Pending" },
    { id: "c2", Status: "AwaitingRefereeResponse" },
    { id: "c3", status: "UnderReview" },
    { id: "c4", status: "Upheld" },
    { id: "c5", status: "Rejected" },
    { id: "c6", status: "Withdrawn" },
  ];

  assert.deepEqual(getRaceComplaintTabCounts(complaints), {
    intake: 1,
    awaiting: 1,
    underReview: 1,
    resolved: 3,
    all: 6,
  });
  assert.deepEqual(filterRaceComplaintsByTab(complaints, "intake"), [complaints[0]]);
  assert.deepEqual(filterRaceComplaintsByTab(complaints, "awaiting"), [complaints[1]]);
  assert.deepEqual(filterRaceComplaintsByTab(complaints, "underReview"), [complaints[2]]);
  assert.deepEqual(filterRaceComplaintsByTab(complaints, "resolved"), complaints.slice(3));
});

test("default admin tab prefers the earliest open queue with work in it", () => {
  assert.equal(getDefaultRaceComplaintTab({ intake: 1, awaiting: 0, underReview: 0, resolved: 0 }), "intake");
  assert.equal(getDefaultRaceComplaintTab({ intake: 0, awaiting: 2, underReview: 0, resolved: 0 }), "awaiting");
  assert.equal(getDefaultRaceComplaintTab({ intake: 0, awaiting: 0, underReview: 3, resolved: 0 }), "underReview");
  assert.equal(getDefaultRaceComplaintTab({ intake: 0, awaiting: 0, underReview: 0, resolved: 4 }), "resolved");
});

test("admin action visibility is locked to Pending (reject/route) and UnderReview (rule)", () => {
  assert.deepEqual(getAvailableAdminRaceComplaintActions("Pending"), ["reject", "route"]);
  assert.deepEqual(getAvailableAdminRaceComplaintActions("AwaitingRefereeResponse"), []);
  assert.deepEqual(getAvailableAdminRaceComplaintActions("UnderReview"), ["upheld", "rejected"]);
  assert.deepEqual(getAvailableAdminRaceComplaintActions("Upheld"), []);
  assert.deepEqual(getAvailableAdminRaceComplaintActions("Rejected"), []);
  assert.deepEqual(getAvailableAdminRaceComplaintActions("Withdrawn"), []);
});

test("referee may only respond while a complaint awaits their explanation", () => {
  assert.equal(canRefereeRespond("AwaitingRefereeResponse"), true);
  assert.equal(canRefereeRespond("Pending"), false);
  assert.equal(canRefereeRespond("UnderReview"), false);
  assert.equal(canRefereeRespond("Upheld"), false);
});

test("filer may withdraw only while a complaint is still active", () => {
  assert.equal(canFilerWithdraw("Pending"), true);
  assert.equal(canFilerWithdraw("AwaitingRefereeResponse"), true);
  assert.equal(canFilerWithdraw("UnderReview"), true);
  assert.equal(canFilerWithdraw("Upheld"), false);
  assert.equal(canFilerWithdraw("Rejected"), false);
  assert.equal(canFilerWithdraw("Withdrawn"), false);
});

test("upheld ruling payload requires an explicit boolean AffectsResult", () => {
  assert.deepEqual(
    buildRuleRaceComplaintPayload("Upheld", "Chấp nhận, ảnh hưởng kết quả.", true),
    { outcome: "Upheld", ruling: "Chấp nhận, ảnh hưởng kết quả.", affectsResult: true },
  );
  assert.deepEqual(
    buildRuleRaceComplaintPayload("Upheld", "Chấp nhận, không ảnh hưởng.", false),
    { outcome: "Upheld", ruling: "Chấp nhận, không ảnh hưởng.", affectsResult: false },
  );
  assert.throws(
    () => buildRuleRaceComplaintPayload("Upheld", "Thiếu AffectsResult"),
    /AffectsResult must be explicitly true or false/,
  );
});

test("rejected ruling payload never carries AffectsResult", () => {
  assert.deepEqual(
    buildRuleRaceComplaintPayload("Rejected", "Không đủ căn cứ.", true),
    { outcome: "Rejected", ruling: "Không đủ căn cứ.", affectsResult: null },
  );
});

test("ruling payload rejects a blank note and a non-terminal outcome", () => {
  assert.throws(() => buildRuleRaceComplaintPayload("Upheld", "   ", true), /Ruling is required/);
  assert.throws(() => buildRuleRaceComplaintPayload("Pending", "note"), /Outcome must be Upheld or Rejected/);
});

test("eligible races map to picker options without exposing RaceEntry selection", () => {
  const races = [
    { raceId: "r1", raceName: "Vòng loại 1", horseName: "Bão Tố", tournamentName: "Giải Mùa Xuân", scheduledAt: "2026-08-01T00:00:00Z" },
    { RaceId: "r2", RaceName: "Vòng loại 2", HorseName: "Sấm Sét" },
  ];

  assert.deepEqual(mapEligibleRacesToOptions(races), [
    { value: "r1", label: "Vòng loại 1 — Bão Tố", tournamentName: "Giải Mùa Xuân", scheduledAt: "2026-08-01T00:00:00Z" },
    { value: "r2", label: "Vòng loại 2 — Sấm Sét", tournamentName: null, scheduledAt: null },
  ]);
  assert.deepEqual(mapEligibleRacesToOptions(null), []);
});
