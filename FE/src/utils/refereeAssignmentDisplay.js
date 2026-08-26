export const ASSIGNMENT_TABS = [
  { value: "pending", label: "Chờ xử lý" },
  { value: "confirmed", label: "Đã xác nhận" },
  { value: "rejected", label: "Đã từ chối" },
  { value: "all", label: "Tất cả" },
];

const STATUS_DETAILS = {
  Assigned: { label: "Chờ xử lý", variant: "warning", group: "pending" },
  Pending: { label: "Chờ xử lý", variant: "warning", group: "pending" },
  Confirmed: { label: "Đã xác nhận", variant: "success", group: "confirmed" },
  Accepted: { label: "Đã xác nhận", variant: "success", group: "confirmed" },
  Completed: { label: "Hoàn thành", variant: "success", group: "confirmed" },
  Rejected: { label: "Đã từ chối", variant: "danger", group: "rejected" },
  Declined: { label: "Đã từ chối", variant: "danger", group: "rejected" },
  Cancelled: { label: "Đã từ chối", variant: "danger", group: "rejected" },
};

function readAssignmentValue(assignment, camelKey, pascalKey) {
  return assignment?.[camelKey] ?? assignment?.[pascalKey];
}

// Race.ScheduledAt (added to RefereeAssignmentResponse alongside ScheduledEndAt) is the
// canonical source; RaceDate/ScheduledStartDate are legacy/alternate key fallbacks only.
export function getScheduledAt(assignment) {
  return (
    readAssignmentValue(assignment, "raceDate", "RaceDate") ??
    readAssignmentValue(assignment, "scheduledAt", "ScheduledAt") ??
    readAssignmentValue(assignment, "scheduledStartDate", "ScheduledStartDate")
  );
}

// null/undefined/unparseable => "Chưa xác định". Never renders DateTime.MinValue as if it
// were a real schedule — the backend now sends null instead of a MinValue placeholder.
export function formatDateTime(value) {
  if (!value) return "Chưa xác định";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Chưa xác định";
  return date.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function getAssignmentId(assignment) {
  return readAssignmentValue(assignment, "id", "Id");
}

export function getAssignmentStatus(assignment) {
  return readAssignmentValue(assignment, "status", "Status") || "";
}

export function getAssignmentStatusDetails(status) {
  const raw = String(status || "").trim();
  const canonical = Object.keys(STATUS_DETAILS).find(
    (key) => key.toLowerCase() === raw.toLowerCase()
  );

  return canonical
    ? STATUS_DETAILS[canonical]
    : { label: raw || "Không xác định", variant: "neutral", group: "all" };
}

export function isPendingAssignment(status) {
  return getAssignmentStatusDetails(status).group === "pending";
}

export function getAssignmentTabCounts(assignments) {
  return assignments.reduce(
    (counts, assignment) => {
      const group = getAssignmentStatusDetails(getAssignmentStatus(assignment)).group;
      if (group !== "all") counts[group] += 1;
      counts.all += 1;
      return counts;
    },
    { pending: 0, confirmed: 0, rejected: 0, all: 0 }
  );
}

export function getDefaultAssignmentTab(counts) {
  return counts.pending > 0 ? "pending" : "all";
}

export function filterAssignmentsByTab(assignments, tab) {
  if (tab === "all") return assignments;
  return assignments.filter(
    (assignment) => getAssignmentStatusDetails(getAssignmentStatus(assignment)).group === tab
  );
}
