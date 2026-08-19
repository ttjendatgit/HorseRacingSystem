export function getJockeyNameDisplay({ jockeyId, jockeyName }) {
  if (jockeyId == null) return "Chưa phân công";
  return jockeyName || "Chưa phân công";
}

export function getJockeyConfirmedDisplay({ jockeyId, jockeyConfirmed }) {
  if (jockeyId == null) return "Chưa xác nhận";
  return jockeyConfirmed ? "Đã xác nhận" : "Chưa xác nhận";
}
