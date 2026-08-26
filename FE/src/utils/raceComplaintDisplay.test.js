import test from "node:test";
import assert from "node:assert/strict";
import {
  MAX_EVIDENCE_PER_SOURCE,
  buildRuleRaceComplaintPayload,
  canFilerMutateEvidence,
  canFilerWithdraw,
  canRefereeRespond,
  canRefereeUploadEvidence,
  filterRaceComplaintsByTab,
  getAvailableAdminRaceComplaintActions,
  getDefaultRaceComplaintTab,
  getRaceComplaintStatusDetails,
  getRaceComplaintTabCounts,
  getRaceComplaintTypeLabel,
  groupComplaintEvidenceByUploader,
  mapEligibleRacesToOptions,
  normalizeEvidenceMediaType,
  normalizeRaceComplaintStatus,
  normalizeRaceComplaintType,
  validateEvidenceFile,
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

// ── COMPLAINT-EVIDENCE-V1 ──

test("validateEvidenceFile accepts supported image/video types under their size ceiling", () => {
  assert.deepEqual(validateEvidenceFile({ name: "a.jpg", type: "image/jpeg", size: 1024 }), { valid: true, error: null });
  assert.deepEqual(validateEvidenceFile({ name: "b.mp4", type: "video/mp4", size: 1024 }), { valid: true, error: null });
});

test("validateEvidenceFile rejects unsupported file types", () => {
  const result = validateEvidenceFile({ name: "doc.pdf", type: "application/pdf", size: 1024 });
  assert.equal(result.valid, false);
  assert.match(result.error, /doc\.pdf/);
});

test("validateEvidenceFile rejects oversized images (>10MB) and videos (>50MB) with distinct messages", () => {
  const bigImage = validateEvidenceFile({ name: "big.png", type: "image/png", size: 11 * 1024 * 1024 });
  assert.equal(bigImage.valid, false);
  assert.match(bigImage.error, /10MB/);

  const bigVideo = validateEvidenceFile({ name: "big.mp4", type: "video/mp4", size: 51 * 1024 * 1024 });
  assert.equal(bigVideo.valid, false);
  assert.match(bigVideo.error, /50MB/);
});

test("validateEvidenceFile rejects a missing file without throwing", () => {
  assert.equal(validateEvidenceFile(null).valid, false);
  assert.equal(validateEvidenceFile(undefined).valid, false);
});

test("normalizeEvidenceMediaType tolerates casing and always resolves to Image or Video", () => {
  assert.equal(normalizeEvidenceMediaType("Video"), "Video");
  assert.equal(normalizeEvidenceMediaType("video"), "Video");
  assert.equal(normalizeEvidenceMediaType("Image"), "Image");
  assert.equal(normalizeEvidenceMediaType(""), "Image");
  assert.equal(normalizeEvidenceMediaType(undefined), "Image");
});

// COMPLAINT-EVIDENCE-V1.1: grouping now keys off the persisted evidenceSource field, never off
// uploadedByUserId/uploadedByRole — those fields are still present on the DTO for display but are
// no longer read by this function.
test("groupComplaintEvidenceByUploader separates Filer evidence from Referee evidence by evidenceSource — lets Admin review both sides", () => {
  const evidence = [
    { evidenceSource: "Filer", fileName: "filer1.jpg" },
    { evidenceSource: "Referee", fileName: "referee1.jpg" },
    { EvidenceSource: "Filer", FileName: "filer2.jpg" },
  ];

  const grouped = groupComplaintEvidenceByUploader(evidence);
  assert.equal(grouped.filerEvidence.length, 2);
  assert.equal(grouped.refereeEvidence.length, 1);
  assert.equal(grouped.otherEvidence.length, 0);
  assert.deepEqual(grouped.filerEvidence.map((e) => e.fileName ?? e.FileName), ["filer1.jpg", "filer2.jpg"]);
});

test("groupComplaintEvidenceByUploader puts unrecognized/missing evidenceSource in otherEvidence rather than guessing", () => {
  const grouped = groupComplaintEvidenceByUploader([{ fileName: "no-source.jpg" }]);
  assert.equal(grouped.filerEvidence.length, 0);
  assert.equal(grouped.refereeEvidence.length, 0);
  assert.equal(grouped.otherEvidence.length, 1);
});

test("groupComplaintEvidenceByUploader never crashes on missing/non-array evidence", () => {
  assert.deepEqual(groupComplaintEvidenceByUploader(null), { filerEvidence: [], refereeEvidence: [], otherEvidence: [] });
  assert.deepEqual(groupComplaintEvidenceByUploader(undefined), { filerEvidence: [], refereeEvidence: [], otherEvidence: [] });
});

// COMPLAINT-EVIDENCE-V1.1: narrowed from "awaiting response OR under review" — once the referee
// has submitted their response, their evidence set must stay stable for admin review.
test("canRefereeUploadEvidence only before the referee's response is submitted", () => {
  assert.equal(canRefereeUploadEvidence("AwaitingRefereeResponse"), true);
  assert.equal(canRefereeUploadEvidence("UnderReview"), false);
  assert.equal(canRefereeUploadEvidence("Pending"), false);
  assert.equal(canRefereeUploadEvidence("Upheld"), false);
  assert.equal(canRefereeUploadEvidence("Rejected"), false);
});

test("canFilerMutateEvidence matches canFilerWithdraw's active-complaint window", () => {
  assert.equal(canFilerMutateEvidence("Pending"), true);
  assert.equal(canFilerMutateEvidence("AwaitingRefereeResponse"), true);
  assert.equal(canFilerMutateEvidence("UnderReview"), true);
  assert.equal(canFilerMutateEvidence("Upheld"), false);
  assert.equal(canFilerMutateEvidence("Rejected"), false);
  assert.equal(canFilerMutateEvidence("Withdrawn"), false);
});

test("MAX_EVIDENCE_PER_SOURCE mirrors the backend's per-side cap", () => {
  assert.equal(MAX_EVIDENCE_PER_SOURCE, 5);
});
