// GATE-V1: pure helpers for the Referee starting-gate assignment page. GateNumber assignment is
// Referee-only (a Confirmed RefereeAssignment for the exact Race); these helpers only compute
// display/validation state — the actual authorization and mutation happen server-side
// (BE/Controllers/RefereesController.cs AssignGateNumber).

const PRE_START_RACE_STATUSES = new Set(["Scheduled", "RegistrationOpen", "RegistrationClosed"]);

// Mirrors RaceManagementService.AssignGateNumberAsync's own status gate — gates are editable only
// before the Race actually starts; once InProgress/Finished/Cancelled, assignments are locked.
export function isRaceGateEditable(raceStatus) {
  if (raceStatus === undefined || raceStatus === null) return false;
  return PRE_START_RACE_STATUSES.has(String(raceStatus).trim());
}

const toGate = (entry) => {
  const raw = entry?.gateNumber ?? entry?.GateNumber;
  return raw === undefined || raw === null || raw === "" ? null : Number(raw);
};

// Integer, >= 1, <= Race.MaxParticipants — the same range PrizeService/RaceManagementService
// enforce server-side. Returns true/false only; use getGateValidationError for a user-facing
// message.
export function isValidGateNumber(value, maxParticipants) {
  const n = Number(value);
  return Number.isInteger(n) && n >= 1 && n <= Number(maxParticipants);
}

// Returns a Vietnamese error message, or null when the value is valid — mirrors the backend's own
// range message so the UI can show the same wording before the round-trip.
export function getGateValidationError(value, maxParticipants) {
  if (value === "" || value === null || value === undefined) return "Vui lòng nhập số cổng.";
  const n = Number(value);
  if (!Number.isInteger(n)) return "Số cổng phải là số nguyên.";
  if (n < 1 || n > Number(maxParticipants)) return `Cổng xuất phát phải từ 1 đến ${maxParticipants}.`;
  return null;
}

// Ascending by GateNumber; entries with no gate yet (null) sort last, so a Referee working through
// the list sees already-assigned entries in gate order first, unassigned ones grouped at the end.
export function sortEntriesByGate(entries) {
  if (!Array.isArray(entries)) return [];
  return [...entries].sort((a, b) => {
    const ga = toGate(a);
    const gb = toGate(b);
    if (ga === null && gb === null) return 0;
    if (ga === null) return 1;
    if (gb === null) return -1;
    return ga - gb;
  });
}

// The one established null-gate label in this codebase (see OwnerRaceConfirmationPage.jsx) —
// reused verbatim here for consistency across Owner (read-only) and Referee (editable) surfaces.
export function formatGateLabel(gateNumber) {
  const n = gateNumber === undefined || gateNumber === null || gateNumber === "" ? null : gateNumber;
  return n === null ? "Chưa xếp" : String(n);
}

// A RaceEntry is only gate-assignable when it is still participating — same filter as
// R1a/GATE-V1 StartRace readiness (Status != Rejected && ScratchedAt == null).
export function isEntryGateAssignable(entry) {
  const status = entry?.status ?? entry?.Status;
  const scratchedAt = entry?.scratchedAt ?? entry?.ScratchedAt ?? null;
  return status !== "Rejected" && !scratchedAt;
}

// Summarizes how many participating entries still need a gate — drives the page's readiness
// banner ("2/3 đã xếp cổng xuất phát" style copy), mirroring the StartRace gate-readiness rule
// without duplicating its actual enforcement (that stays server-side).
export function getGateReadinessSummary(entries) {
  const participating = (Array.isArray(entries) ? entries : []).filter(isEntryGateAssignable);
  const assigned = participating.filter((e) => toGate(e) !== null).length;
  return {
    total: participating.length,
    assigned,
    missing: participating.length - assigned,
    isComplete: participating.length > 0 && assigned === participating.length,
  };
}
