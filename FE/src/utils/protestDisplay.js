const STATUS_BY_NUMBER = {
  1: "Pending",
  2: "UnderReview",
  3: "Upheld",
  4: "Rejected",
  5: "Withdrawn",
};

const normalizeText = (value) =>
  String(value ?? "")
    .trim()
    .replace(/[\s_-]+/g, "")
    .toLowerCase();

export const normalizeProtestStatus = (value) => {
  if (value === null || value === undefined || value === "") return "Unknown";
  if (typeof value === "number") return STATUS_BY_NUMBER[value] ?? "Unknown";
  const text = String(value).trim();
  if (/^\d+$/.test(text)) return STATUS_BY_NUMBER[Number(text)] ?? "Unknown";

  const key = normalizeText(text);
  if (key === "pending") return "Pending";
  if (key === "underreview") return "UnderReview";
  if (key === "upheld") return "Upheld";
  if (key === "rejected") return "Rejected";
  if (key === "withdrawn") return "Withdrawn";
  return text || "Unknown";
};

export const getProtestStatusDetails = (status) => {
  const normalized = normalizeProtestStatus(status);
  switch (normalized) {
    case "Pending":
      return { status: normalized, label: "Chờ xử lý", variant: "pending", group: "pending" };
    case "UnderReview":
      return { status: normalized, label: "Đang xem xét", variant: "active", group: "underReview" };
    case "Upheld":
      return { status: normalized, label: "Chấp nhận", variant: "approved", group: "resolved" };
    case "Rejected":
      return { status: normalized, label: "Bác khiếu nại", variant: "rejected", group: "resolved" };
    case "Withdrawn":
      return { status: normalized, label: "Đã rút", variant: "inactive", group: "resolved" };
    default:
      return { status: normalized, label: String(status || "Không rõ"), variant: "inactive", group: "resolved" };
  }
};

const readStatus = (item) => item?.status ?? item?.Status;

export const getProtestTabCounts = (items) => {
  const counts = { pending: 0, underReview: 0, resolved: 0, all: 0 };
  if (!Array.isArray(items)) return counts;
  items.forEach((item) => {
    const group = getProtestStatusDetails(readStatus(item)).group;
    counts[group] = (counts[group] ?? 0) + 1;
    counts.all += 1;
  });
  return counts;
};

export const filterProtestsByTab = (items, tab) => {
  if (!Array.isArray(items)) return [];
  if (!tab || tab === "all") return items;
  return items.filter((item) => getProtestStatusDetails(readStatus(item)).group === tab);
};

export const getDefaultProtestTab = (counts) => {
  if ((counts?.pending ?? 0) > 0) return "pending";
  if ((counts?.underReview ?? 0) > 0) return "underReview";
  return "all";
};

export const getAvailableProtestActions = (status) => {
  const normalized = normalizeProtestStatus(status);
  if (normalized === "Pending") return ["underReview", "upheld", "rejected"];
  if (normalized === "UnderReview") return ["upheld", "rejected"];
  return [];
};

export const buildRuleProtestPayload = (outcome, note = "") => {
  const normalized = normalizeProtestStatus(outcome);
  if (normalized !== "Upheld" && normalized !== "Rejected") {
    throw new Error("Outcome must be Upheld or Rejected.");
  }
  const text = String(note || "").trim();
  return {
    outcome: normalized,
    ruling: text || (normalized === "Upheld" ? "Chấp nhận khiếu nại" : "Bác khiếu nại"),
    resolution: text || null,
  };
};
