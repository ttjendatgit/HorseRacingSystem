import { normalizeJockeyApprovalStatus } from "./jockeyApproval.js";

// J-ADMIN-REVIEW: pure helpers for the Admin Jockey approval queue/review UI. Kept separate from
// jockeyApproval.js (J-UX's Jockey-facing display helper) since this is Admin-facing logic over a
// list of jockeys, not a single self-profile's display copy — but both share the same underlying
// status normalization so "Pending"/"Approved"/"Rejected" bucketing never drifts between the two.

// Groups a raw jockey list (as returned by GET /api/jockeys, camelCase or PascalCase) into the
// four buckets the /admin/roles tabs need. `all` preserves input order; `unknown` catches any
// status this app doesn't recognize so a jockey is never silently dropped from every tab.
export function groupJockeysByApprovalStatus(jockeys) {
  const all = Array.isArray(jockeys) ? jockeys : [];
  const pending = [];
  const approved = [];
  const rejected = [];
  const unknown = [];

  for (const jockey of all) {
    const rawStatus =
      jockey?.approvalStatus ??
      jockey?.ApprovalStatus ??
      jockey?.approvalStatusName ??
      jockey?.ApprovalStatusName ??
      null;
    switch (normalizeJockeyApprovalStatus(rawStatus)) {
      case "Pending": pending.push(jockey); break;
      case "Approved": approved.push(jockey); break;
      case "Rejected": rejected.push(jockey); break;
      default: unknown.push(jockey);
    }
  }

  return { all, pending, approved, rejected, unknown };
}

// A rejection reason must be a real, non-blank string — mirrors the backend's
// string.IsNullOrWhiteSpace(reason) check in AdminService.RejectJockeyAsync so the FE can disable
// the Reject button before ever calling the API, without duplicating a looser rule.
export function isRejectReasonValid(reason) {
  return typeof reason === "string" && reason.trim().length > 0;
}

// Safe display formatting for optional review fields — never renders "undefined"/"null"/empty
// string, falls back to a clear placeholder instead.
export function formatJockeyReviewValue(value, fallback = "—") {
  if (value === null || value === undefined) return fallback;
  if (typeof value === "string" && value.trim() === "") return fallback;
  return value;
}

const IMAGE_EXTENSIONS = new Set(["jpg", "jpeg", "png", "gif", "webp"]);

// Classifies a LicenseFile URL for the review-modal document preview: "image" gets an inline
// thumbnail + lightbox, "pdf" gets an embedded preview, "unknown" falls back to a plain
// open-original card, "none" means no file was uploaded at all. Pure string inspection only — no
// network call, no new backend endpoint, works directly off the existing Cloudinary URL.
export function getLicenseDocumentType(url) {
  if (typeof url !== "string" || url.trim() === "") return "none";

  const withoutQuery = url.split("?")[0].split("#")[0];
  const lastDot = withoutQuery.lastIndexOf(".");
  if (lastDot === -1 || lastDot === withoutQuery.length - 1) return "unknown";

  const ext = withoutQuery.slice(lastDot + 1).toLowerCase();
  if (IMAGE_EXTENSIONS.has(ext)) return "image";
  if (ext === "pdf") return "pdf";
  return "unknown";
}
