export function getJockeyNameDisplay({ jockeyId, jockeyName }) {
  if (jockeyId == null) return "Chưa phân công";
  return jockeyName || "Chưa phân công";
}

export function getJockeyConfirmedDisplay({ jockeyId, jockeyConfirmed }) {
  if (jockeyId == null) return "Chưa xác nhận";
  return jockeyConfirmed ? "Đã xác nhận" : "Chưa xác nhận";
}

// J3.1: official-ness is Tournament-wide truth that lives on RaceEntry.JockeyId, never on
// invitation.Status — an Accepted invitation stays Accepted forever even after Owner Final
// Confirm makes it official (Final Confirm never touches invitation.Status). So "is this
// invitation the official one" must be computed by matching JockeyId against the RaceEntry
// for the same Race, not by checking Status === "Accepted" alone.
export function isInvitationOfficial(invitation, raceEntries) {
  const invitationRaceId = invitation?.raceId ?? invitation?.RaceId;
  const invitationJockeyId = invitation?.jockeyId ?? invitation?.JockeyId;
  if (!invitationRaceId || !invitationJockeyId) return false;

  const entry = (raceEntries ?? []).find((e) => (e.raceId ?? e.RaceId) === invitationRaceId);
  const entryJockeyId = entry?.jockeyId ?? entry?.JockeyId;
  return entryJockeyId != null && entryJockeyId === invitationJockeyId;
}
