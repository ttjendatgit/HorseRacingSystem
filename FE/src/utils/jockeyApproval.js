// J-UX: pure Jockey competitive-approval display helper. Account authorization (login, dashboard
// access) is governed by User.IsActive + role and is untouched by this file — this only maps
// Jockey.ApprovalStatus (competitive eligibility) to display copy, so every page that needs an
// approval banner/status reads from one place instead of re-deriving the enum mapping.

const STATUS_BY_NUMBER = { 1: "Pending", 2: "Approved", 3: "Rejected" };

// Handles numeric enum (1/2/3), string enum name ("Pending"/"Approved"/"Rejected", any case),
// and the numeric-as-string form ("1"/"2"/"3") — matches the range of shapes the backend already
// sends across different endpoints/DTOs in this project (see jockeyApi.js normalizeJockey).
export function normalizeJockeyApprovalStatus(rawStatus) {
  if (rawStatus === undefined || rawStatus === null || rawStatus === "") return null;

  if (typeof rawStatus === "number") {
    return STATUS_BY_NUMBER[rawStatus] ?? null;
  }

  const normalized = String(rawStatus).trim().toLowerCase();
  if (normalized === "pending" || normalized === "1") return "Pending";
  if (normalized === "approved" || normalized === "2") return "Approved";
  if (normalized === "rejected" || normalized === "3") return "Rejected";
  return null;
}

const STATUS_META = {
  Pending: { label: "Đang chờ Admin phê duyệt", tone: "pending" },
  Approved: { label: "Đã được Admin phê duyệt", tone: "approved" },
  Rejected: { label: "Hồ sơ đã bị từ chối", tone: "rejected" },
};

// Accepts the raw /api/jockeys/me payload (camelCase or PascalCase) and returns a small display
// object every page can render directly. ApprovalNote is surfaced only when the Jockey is
// Rejected AND the backend actually sent a note — never invented.
export function getJockeyApprovalDisplay(jockeyProfile) {
  const rawStatus =
    jockeyProfile?.approvalStatus ??
    jockeyProfile?.ApprovalStatus ??
    jockeyProfile?.approvalStatusName ??
    jockeyProfile?.ApprovalStatusName ??
    null;
  const status = normalizeJockeyApprovalStatus(rawStatus);
  const meta = STATUS_META[status] ?? { label: "Không rõ trạng thái phê duyệt", tone: "unknown" };

  const rawNote = jockeyProfile?.approvalNote ?? jockeyProfile?.ApprovalNote ?? null;
  const note = status === "Rejected" && rawNote ? String(rawNote).trim() || null : null;

  return {
    status,
    label: meta.label,
    tone: meta.tone,
    note,
    isPending: status === "Pending",
    isApproved: status === "Approved",
    isRejected: status === "Rejected",
  };
}
