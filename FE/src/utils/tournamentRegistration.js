import { apiToUtcDate } from "./vnDateTime.js";

const STATUS_NAME_BY_VALUE = {
  0: "Draft",
  1: "Published",
  2: "Ongoing",
  3: "Finished",
  4: "Cancelled",
};

export const normalizeTournamentStatus = (tournament) => {
  const raw = tournament?.statusName ?? tournament?.StatusName ?? tournament?.status ?? tournament?.Status;
  if (raw === null || raw === undefined || raw === "") return "";
  if (typeof raw === "number") return STATUS_NAME_BY_VALUE[raw] ?? String(raw);

  const text = String(raw).trim();
  if (/^\d+$/.test(text)) return STATUS_NAME_BY_VALUE[Number(text)] ?? text;

  const normalized = text.toLowerCase();
  if (normalized === "draft") return "Draft";
  if (normalized === "published") return "Published";
  if (normalized === "ongoing") return "Ongoing";
  if (normalized === "finished") return "Finished";
  if (normalized === "cancelled" || normalized === "canceled") return "Cancelled";
  return text;
};

export const TOURNAMENT_LIFECYCLE_LABELS = {
  Draft: "Bản nháp",
  Published: "Đã công bố",
  Ongoing: "Đang diễn ra",
  Finished: "Đã kết thúc",
  Cancelled: "Đã hủy",
};

export const getTournamentLifecycleLabel = (tournament) =>
  TOURNAMENT_LIFECYCLE_LABELS[normalizeTournamentStatus(tournament)] ?? "Không xác định";

// V0.1: structural Tournament fields (MaxRounds, and per existing rules StartDate/EndDate/
// MinParticipants/MaxParticipants) may only be edited while Draft — Published, Ongoing,
// Finished, and Cancelled must all lock them alike, not just Published. Accepts a raw status
// value (numeric TournamentStatus or its string name) rather than a whole tournament object, so
// callers holding only the status (e.g. FE edit-form state) don't need to fabricate one.
export const canEditTournamentStructure = (status) => normalizeTournamentStatus({ status }) === "Draft";

// Tournament capacity gate: Approved TournamentHorseRegistration count vs Tournament.MaxParticipants.
// Distinct from the deadline-based "closed" state — capacity being full must never be reported as
// "Đã đóng đăng ký" (the deadline may still be open; capacity is the actual reason).
const getCapacityInfo = (tournament) => {
  const maxParticipants = tournament?.maxParticipants ?? tournament?.MaxParticipants;
  const approvedCount =
    tournament?.approvedRegistrationCount ?? tournament?.ApprovedRegistrationCount ?? 0;
  const isFull =
    typeof maxParticipants === "number" && maxParticipants > 0 && approvedCount >= maxParticipants;
  return { maxParticipants, approvedCount, isFull };
};

export const getTournamentRegistrationState = (tournament, now = new Date()) => {
  const status = normalizeTournamentStatus(tournament);
  const deadline = tournament?.registrationDeadline ?? tournament?.RegistrationDeadline;

  if (status === "Published") {
    const deadlineDate = deadline ? apiToUtcDate(deadline) : null;
    const beforeDeadline = !!deadlineDate && deadlineDate > now;
    if (!beforeDeadline) {
      return { key: "closed", label: "Đã đóng đăng ký", canRegister: false };
    }

    const { isFull } = getCapacityInfo(tournament);
    if (isFull) {
      return { key: "full", label: "Đã đủ số lượng tham gia", canRegister: false };
    }

    return { key: "open", label: "Mở đăng ký", canRegister: true };
  }

  if (status === "Draft") return { key: "unpublished", label: "Chưa công bố", canRegister: false };
  if (status === "Ongoing") return { key: "registration-ended", label: "Đã kết thúc đăng ký", canRegister: false };
  if (status === "Finished") return { key: "finished", label: "Giải đã kết thúc", canRegister: false };
  if (status === "Cancelled") return { key: "cancelled", label: "Giải đã hủy", canRegister: false };

  return { key: "unknown", label: "Không xác định", canRegister: false };
};

// Shared Owner-registration-eligibility rule. Never derive this from RaceStatus
// RegistrationOpen/Closed or Tournament.IsActive; both are unrelated concerns.
export const canRegisterTournament = (tournament, now) =>
  getTournamentRegistrationState(tournament, now).canRegister;

// V0/V0.1: Final Round identity is RoundNumber === Tournament.MaxRounds — NEVER AdvanceCount === 0
// (Draft data may temporarily hold AdvanceCount=0 on a non-final Round). Number() on both sides
// guards against API/form state carrying either as a string.
export const isFinalRound = (round, tournament) => {
  const roundNumber = round?.roundNumber ?? round?.RoundNumber;
  const maxRounds = tournament?.maxRounds ?? tournament?.MaxRounds;
  if (roundNumber === null || roundNumber === undefined || maxRounds === null || maxRounds === undefined) return false;
  return Number(roundNumber) === Number(maxRounds);
};

// T-D1: hard delete (DELETE /api/tournaments/{id}) is backend-rejected (409) for anything past
// Draft — once Published/Ongoing/Finished/Cancelled, a Tournament is historical/auditable data.
// This is FE affordance only (hide the "Xóa" button); the backend guard in
// TournamentAndRoundService.DeleteTournamentAsync remains the authoritative check.
export const canHardDeleteTournament = (tournament) =>
  ["Draft", "Cancelled"].includes(normalizeTournamentStatus(tournament));

// "Giải đấu đã đủ X/Y ngựa tham gia.\nHẹn bạn ở giải đấu tiếp theo." — only meaningful once the
// Tournament is actually at/over capacity; returns null otherwise so callers don't need to guard.
export const getCapacityFullMessage = (tournament, now = new Date()) => {
  if (getTournamentRegistrationState(tournament, now).key !== "full") return null;
  const { maxParticipants, approvedCount } = getCapacityInfo(tournament);
  return `Giải đấu đã đủ ${approvedCount}/${maxParticipants} ngựa tham gia.\nHẹn bạn ở giải đấu tiếp theo.`;
};
