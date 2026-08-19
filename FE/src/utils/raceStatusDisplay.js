const OWNER_RACE_STATUS_LABELS = Object.freeze({
  scheduled: "Chuẩn bị",
  registrationopen: "Chuẩn bị",
  registrationclosed: "Chuẩn bị",
  inprogress: "Đang đua",
  finished: "Đã kết thúc",
  cancelled: "Đã hủy",
});

export function getOwnerRaceStatusLabel(status) {
  const key = (status ?? "").toString().trim().toLowerCase();
  return OWNER_RACE_STATUS_LABELS[key] ?? "Không xác định";
}