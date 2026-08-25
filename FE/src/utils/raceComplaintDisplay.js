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
      return { status: normalized, label: "Chấp nhận khiếu nại", variant: "approved", group: "resolved" };
    case "Rejected":
      return { status: normalized, label: "Bác khiếu nại", variant: "rejected", group: "resolved" };
    case "Withdrawn":
      return { status: normalized, label: "Đã rút", variant: "inactive", group: "resolved" };
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
