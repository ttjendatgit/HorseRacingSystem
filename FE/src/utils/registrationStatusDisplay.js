// Shared FE label for BE.Models.Enums.RegistrationStatus (Pending/Approved/Rejected/Withdrawn).
// This exact enum backs both TournamentRegistration.Status and RaceEntry.Status — callers for
// either object share this one mapping instead of duplicating the switch per page.
const REGISTRATION_STATUS_LABELS = Object.freeze({
  pending: "Chờ duyệt",
  approved: "Đã duyệt",
  rejected: "Từ chối",
  withdrawn: "Đã rút",
});

export function getRegistrationStatusLabel(status) {
  const key = (status ?? "").toString().trim().toLowerCase();
  return REGISTRATION_STATUS_LABELS[key] ?? "Chờ duyệt";
}

// Semantic color tone for the same enum — shared across Owner pages so a given status always
// reads the same way: pass=green (Approved), caution=gold (Pending), fail=red (Rejected),
// neutral=muted gray (Withdrawn / unknown).
const REGISTRATION_STATUS_TONES = Object.freeze({
  pending: "caution",
  approved: "pass",
  rejected: "fail",
  withdrawn: "neutral",
});

export function getRegistrationStatusTone(status) {
  const key = (status ?? "").toString().trim().toLowerCase();
  return REGISTRATION_STATUS_TONES[key] ?? "neutral";
}
