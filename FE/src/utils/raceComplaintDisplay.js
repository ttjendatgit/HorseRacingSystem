const normalizeText = (value) =>
  String(value ?? "")
    .trim()
    .replace(/[\s_-]+/g, "")
    .toLowerCase();

const STATUS_KEYS = {
  pending: "Pending",
  awaitingrefereeresponse: "AwaitingRefereeResponse",
  underreview: "UnderReview",
  upheld: "Upheld",
  rejected: "Rejected",
  withdrawn: "Withdrawn",
};

const TYPE_KEYS = {
  resultjudging: "ResultJudging",
  raceoperation: "RaceOperation",
};

export const normalizeRaceComplaintStatus = (value) => {
  if (value === null || value === undefined || value === "") return "Unknown";
  const key = normalizeText(value);
  return STATUS_KEYS[key] ?? String(value).trim();
};

export const normalizeRaceComplaintType = (value) => {
  if (value === null || value === undefined || value === "") return "Unknown";
  const key = normalizeText(value);
  return TYPE_KEYS[key] ?? String(value).trim();
};

export const getRaceComplaintTypeLabel = (type) => {
  const normalized = normalizeRaceComplaintType(type);
  switch (normalized) {
    case "ResultJudging":
      return "Chấm kết quả không công bằng";
    case "RaceOperation":
      return "Điều hành cuộc đua không đúng";
    default:
      return String(type || "Không rõ");
  }
};

export const RACE_COMPLAINT_TYPE_OPTIONS = [
  { value: "ResultJudging", label: "Chấm kết quả không công bằng" },
  { value: "RaceOperation", label: "Điều hành cuộc đua không đúng" },
];

// group is the Admin tab this status lives under:
//   intake      -> "Chờ tiếp nhận"
//   awaiting    -> "Chờ giải trình"
//   underReview -> "Đang xem xét"
//   resolved    -> "Đã xử lý"
export const getRaceComplaintStatusDetails = (status) => {
  const normalized = normalizeRaceComplaintStatus(status);
  switch (normalized) {
    case "Pending":
      return { status: normalized, label: "Chờ tiếp nhận", variant: "pending", group: "intake" };
    case "AwaitingRefereeResponse":
      return { status: normalized, label: "Chờ trọng tài giải trình", variant: "active", group: "awaiting" };
    case "UnderReview":
      return { status: normalized, label: "Đang xem xét", variant: "active", group: "underReview" };
    case "Upheld":
      return { status: normalized, label: "Khiếu nại được chấp nhận", variant: "approved", group: "resolved" };
    case "Rejected":
      return { status: normalized, label: "Khiếu nại bị bác", variant: "rejected", group: "resolved" };
    case "Withdrawn":
      return { status: normalized, label: "Đã rút khiếu nại", variant: "inactive", group: "resolved" };
    default:
      return { status: normalized, label: String(status || "Không rõ"), variant: "inactive", group: "resolved" };
  }
};

const readStatus = (item) => item?.status ?? item?.Status;

export const ADMIN_RACE_COMPLAINT_TABS = [
  { value: "intake", label: "Chờ tiếp nhận" },
  { value: "awaiting", label: "Chờ giải trình" },
  { value: "underReview", label: "Đang xem xét" },
  { value: "resolved", label: "Đã xử lý" },
];

export const getRaceComplaintTabCounts = (items) => {
  const counts = { intake: 0, awaiting: 0, underReview: 0, resolved: 0, all: 0 };
  if (!Array.isArray(items)) return counts;
  items.forEach((item) => {
    const group = getRaceComplaintStatusDetails(readStatus(item)).group;
    counts[group] = (counts[group] ?? 0) + 1;
    counts.all += 1;
  });
  return counts;
};

export const filterRaceComplaintsByTab = (items, tab) => {
  if (!Array.isArray(items)) return [];
  if (!tab || tab === "all") return items;
  return items.filter((item) => getRaceComplaintStatusDetails(readStatus(item)).group === tab);
};

export const getDefaultRaceComplaintTab = (counts) => {
  if ((counts?.intake ?? 0) > 0) return "intake";
  if ((counts?.awaiting ?? 0) > 0) return "awaiting";
  if ((counts?.underReview ?? 0) > 0) return "underReview";
  return "resolved";
};

// Which admin action buttons are valid for a complaint's current status.
export const getAvailableAdminRaceComplaintActions = (status) => {
  const normalized = normalizeRaceComplaintStatus(status);
  if (normalized === "Pending") return ["reject", "route"];
  if (normalized === "UnderReview") return ["upheld", "rejected"];
  return [];
};

// Whether the assigned referee may still submit a response for this complaint.
export const canRefereeRespond = (status) =>
  normalizeRaceComplaintStatus(status) === "AwaitingRefereeResponse";

// Whether the original filer may withdraw a complaint at its current status.
export const canFilerWithdraw = (status) => {
  const normalized = normalizeRaceComplaintStatus(status);
  return normalized === "Pending" || normalized === "AwaitingRefereeResponse" || normalized === "UnderReview";
};

// OWNER-DEMO-POLISH-V1.2 §3: a complaint only has an actual Admin ruling once it's Upheld or
// Rejected — Withdrawn is a terminal state too (canFilerWithdraw is false), but it was never ruled
// on, so its "expand" CTA must read "Xem chi tiết" like the still-open statuses, never
// "Mở kết luận". Do not derive this from !canFilerWithdraw — that conflates "no longer
// withdrawable" with "has a ruling", which is exactly the Withdrawn bug this fixes.
export const hasFinalComplaintRuling = (status) => {
  const normalized = normalizeRaceComplaintStatus(status);
  return normalized === "Upheld" || normalized === "Rejected";
};

export const buildRuleRaceComplaintPayload = (outcome, ruling, affectsResult = null) => {
  const normalized = normalizeRaceComplaintStatus(outcome);
  if (normalized !== "Upheld" && normalized !== "Rejected") {
    throw new Error("Outcome must be Upheld or Rejected.");
  }
  const text = String(ruling || "").trim();
  if (!text) {
    throw new Error("Ruling is required.");
  }
  if (normalized === "Upheld" && typeof affectsResult !== "boolean") {
    throw new Error("AffectsResult must be explicitly true or false when a complaint is upheld.");
  }
  return {
    outcome: normalized,
    ruling: text,
    affectsResult: normalized === "Upheld" ? affectsResult : null,
  };
};

const raceLabel = (race) => {
  const name = race?.raceName ?? race?.RaceName ?? "";
  const horse = race?.horseName ?? race?.HorseName ?? "";
  return horse ? `${name} — ${horse}` : name;
};

// Maps the /race-complaints/eligible-races payload into {value,label} options
// for a race picker, without exposing RaceEntry selection to the filer.
export const mapEligibleRacesToOptions = (races) => {
  if (!Array.isArray(races)) return [];
  return races.map((race) => ({
    value: race.raceId ?? race.RaceId,
    label: raceLabel(race),
    tournamentName: race.tournamentName ?? race.TournamentName ?? null,
    scheduledAt: race.scheduledAt ?? race.ScheduledAt ?? null,
  }));
};

// ── COMPLAINT-EVIDENCE-V1 ──

// Mirrors CloudinaryStorageService.UploadMediaAsync's accepted types/size ceilings — client-side
// pre-check only, never a substitute for the backend's own re-validation.
const EVIDENCE_IMAGE_TYPES = ["image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp"];
const EVIDENCE_VIDEO_TYPES = ["video/mp4", "video/quicktime", "video/webm", "video/x-msvideo", "video/3gpp"];
const EVIDENCE_MAX_IMAGE_BYTES = 10 * 1024 * 1024;
const EVIDENCE_MAX_VIDEO_BYTES = 50 * 1024 * 1024;

export const EVIDENCE_ACCEPT_ATTR = [...EVIDENCE_IMAGE_TYPES, ...EVIDENCE_VIDEO_TYPES].join(",");

// COMPLAINT-EVIDENCE-V1.1: backend-authoritative cap (RaceComplaintService.MaxEvidencePerSource) —
// mirrored here only so the uploader UI can show "N/5" and pre-disable the picker at the limit.
export const MAX_EVIDENCE_PER_SOURCE = 5;

// Returns { valid, error } — never throws, so callers can validate a whole FileList in a loop
// and report one message per rejected file without a try/catch per file.
export const validateEvidenceFile = (file) => {
  if (!file) return { valid: false, error: "Không có file nào được chọn." };
  const type = String(file.type || "").toLowerCase();
  const isImage = EVIDENCE_IMAGE_TYPES.includes(type);
  const isVideo = EVIDENCE_VIDEO_TYPES.includes(type);
  if (!isImage && !isVideo) {
    return { valid: false, error: `Định dạng không được hỗ trợ: ${file.name}` };
  }
  const maxBytes = isVideo ? EVIDENCE_MAX_VIDEO_BYTES : EVIDENCE_MAX_IMAGE_BYTES;
  if (file.size > maxBytes) {
    return { valid: false, error: `${file.name} quá lớn (tối đa ${isVideo ? "50MB cho video" : "10MB cho ảnh"}).` };
  }
  return { valid: true, error: null };
};

export const normalizeEvidenceMediaType = (mediaType) => {
  const key = normalizeText(mediaType);
  return key === "video" ? "Video" : "Image";
};

// Groups one complaint's Evidence list into who it belongs to, so the UI can show "Bằng chứng
// của người khiếu nại" and "Bằng chứng của trọng tài" as separate galleries — this is exactly
// what lets Admin "xem hai phía" (review both sides) instead of one flat mixed list.
//
// COMPLAINT-EVIDENCE-V1.1: keys off the persisted evidenceSource field only — never inferred from
// uploadedByUserId/uploadedByRole, which used to require the caller to also pass filedByUserId.
export const groupComplaintEvidenceByUploader = (evidence) => {
  const list = Array.isArray(evidence) ? evidence : [];
  const filerEvidence = [];
  const refereeEvidence = [];
  const otherEvidence = [];
  list.forEach((item) => {
    const source = item.evidenceSource ?? item.EvidenceSource;
    if (source === "Filer") {
      filerEvidence.push(item);
    } else if (source === "Referee") {
      refereeEvidence.push(item);
    } else {
      otherEvidence.push(item);
    }
  });
  return { filerEvidence, refereeEvidence, otherEvidence };
};

// COMPLAINT-EVIDENCE-V1.1: narrowed from "AwaitingRefereeResponse OR UnderReview" — once the
// referee has submitted their response, their evidence set must stay stable for admin review, so
// this is now also the single source of truth for the Referee-side mutation (upload/delete) window.
export const canRefereeUploadEvidence = (status) =>
  normalizeRaceComplaintStatus(status) === "AwaitingRefereeResponse";

// The filer may add/remove their own evidence for as long as the complaint is still active —
// identical window to canFilerWithdraw, kept as its own named predicate for readability at call
// sites that are about evidence, not withdrawal.
export const canFilerMutateEvidence = (status) => canFilerWithdraw(status);
