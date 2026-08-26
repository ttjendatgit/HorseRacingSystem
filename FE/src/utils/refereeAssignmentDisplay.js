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
