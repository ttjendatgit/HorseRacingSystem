const OWNER_RACE_STATUS_LABELS = Object.freeze({
  scheduled: "Chuẩn bị",
  registrationopen: "Chuẩn bị",
  registrationclosed: "Chuẩn bị",
  inprogress: "Đang diễn ra",
  finished: "Đã kết thúc",
  cancelled: "Đã hủy",
});

export function getOwnerRaceStatusLabel(status) {
  const key = (status ?? "").toString().trim().toLowerCase();
  return OWNER_RACE_STATUS_LABELS[key] ?? "Không xác định";
}

// OWNER-DEMO-POLISH-V1.2 §2: RaceStatus is the ONLY authority for the Owner Schedule lifecycle
// badge — never derived from ScheduledAt vs "now". A Finished race must read "Đã kết thúc" even
// when its ScheduledAt happens to sit in the future (stale/mis-set data), and a race that hasn't
// started yet must never be mislabeled by a separate time-only badge that can disagree with this
// one. Time-based grouping may still exist elsewhere (e.g. filter tabs), just never as this badge.
const OWNER_RACE_STATUS_TONES = Object.freeze({
  scheduled: "caution",
  registrationopen: "caution",
  registrationclosed: "caution",
  inprogress: "live",
  finished: "pass",
  cancelled: "fail",
});

export function getOwnerRaceStatusTone(status) {
  const key = (status ?? "").toString().trim().toLowerCase();
  return OWNER_RACE_STATUS_TONES[key] ?? "neutral";
}