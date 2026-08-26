const normalizeText = (value) =>
  String(value ?? "")
    .trim()
    .replace(/[\s_-]+/g, "")
    .toLowerCase();

const RESULT_STATUS_KEYS = {
  provisional: "Provisional",
  official: "Official",
};

const RACE_STATUS_KEYS = {
  finished: "Finished",
  3: "Finished",
};

const APPROVAL_STATUS_KEYS = {
  1: "Pending",
  pending: "Pending",
  2: "Approved",
  approved: "Approved",
  3: "Rejected",
  rejected: "Rejected",
};

const ACTIVE_REGISTRATION_STATUSES = new Set(["Pending", "Approved"]);

export const OWNER_REQUIREMENT_LABELS = [
  "Giải đấu đang mở đăng ký",
  "Còn hạn đăng ký",
  "Còn suất tham gia",
  "Ngựa phải được phê duyệt",
  "Mỗi chủ ngựa chỉ có một đăng ký đang hoạt động trong giải",
];

export const OWNER_FINAL_RANKING_TITLE = "Xếp hạng chung cuộc";
export const OWNER_FINAL_RANKING_SUBTITLE = "Kết quả vòng Chung kết";

export const normalizeResultStatus = (value) => {
  const key = normalizeText(value);
  return RESULT_STATUS_KEYS[key] ?? String(value || "");
};

export const getOwnerResultStatusDetails = (value) => {
  const status = normalizeResultStatus(value);
  if (status === "Provisional") {
    return { status, label: "Kết quả tạm thời", tone: "warning", isProvisional: true, isOfficial: false };
  }
  if (status === "Official") {
    return { status, label: "Kết quả chính thức", tone: "success", isProvisional: false, isOfficial: true };
  }
  return { status: status || "None", label: "Chưa có kết quả", tone: "neutral", isProvisional: false, isOfficial: false };
};

export const isProvisionalResult = (result) =>
  getOwnerResultStatusDetails(result?.resultStatus ?? result?.ResultStatus).isProvisional;

export const isOfficialResult = (result) =>
  getOwnerResultStatusDetails(result?.resultStatus ?? result?.ResultStatus).isOfficial;

export const normalizeRaceStatus = (value) => {
  const key = normalizeText(value);
  return RACE_STATUS_KEYS[key] ?? RACE_STATUS_KEYS[String(value)] ?? String(value || "");
};

export const canShowOwnerComplaintCta = (entry, result) =>
  normalizeRaceStatus(entry?.raceStatus ?? entry?.RaceStatus ?? entry?.raceStatusKey) === "Finished" &&
  isProvisionalResult(result) &&
  Boolean(entry?.raceId ?? entry?.RaceId);

export const normalizeHorseApprovalStatus = (value) => {
  const key = normalizeText(value);
  return APPROVAL_STATUS_KEYS[key] ?? APPROVAL_STATUS_KEYS[String(value)] ?? "Unknown";
};

export const getHorseApprovalLabel = (value) => {
  const status = normalizeHorseApprovalStatus(value);
  if (status === "Pending") return "Chờ duyệt";
  if (status === "Approved") return "Đủ điều kiện";
  if (status === "Rejected") return "Không được duyệt";
  return "Chưa xác định";
};

export const isActiveRegistrationStatus = (value) =>
  ACTIVE_REGISTRATION_STATUSES.has(String(value || ""));

export const ownerHasActiveTournamentRegistration = (registrations, tournamentId) => {
  if (!tournamentId || !Array.isArray(registrations)) return false;
  return registrations.some(
    (registration) =>
      String(registration.tournamentId ?? registration.TournamentId) === String(tournamentId) &&
      isActiveRegistrationStatus(registration.statusRaw ?? registration.status ?? registration.Status),
  );
};

export const getHorseTournamentSelectionState = (horse, registrations, tournamentId) => {
  const status = normalizeHorseApprovalStatus(horse?.approvalStatus ?? horse?.ApprovalStatus);
  const archived = Boolean(horse?.isArchived ?? horse?.IsArchived);
  if (archived) {
    return { hidden: true, selectable: false, label: "Đã lưu trữ", reason: "Ngựa đã lưu trữ không thể đăng ký giải mới." };
  }
  if (status === "Pending") {
    return { hidden: false, selectable: false, label: "Chờ duyệt", reason: "Ngựa đang chờ Admin duyệt." };
  }
  if (status === "Rejected") {
    return { hidden: false, selectable: false, label: "Không được duyệt", reason: horse?.approvalNote ?? horse?.ApprovalNote ?? "Ngựa chưa được Admin phê duyệt." };
  }
  if (status !== "Approved") {
    return { hidden: false, selectable: false, label: "Chưa xác định", reason: "Trạng thái duyệt của ngựa chưa hợp lệ." };
  }
  const ownerAlreadyRegistered = ownerHasActiveTournamentRegistration(registrations, tournamentId);
  if (ownerAlreadyRegistered) {
    return {
      hidden: false,
      selectable: false,
      label: "Đã có đăng ký",
      reason: "Bạn đã có một ngựa đang đăng ký hoặc đã được duyệt cho giải này.",
    };
  }
  return { hidden: false, selectable: true, label: "Đủ điều kiện", reason: "" };
};

// Row color semantics (Owner Tournament Register polish):
//   pass    = satisfied — green
//   fail    = actually blocking right now — red
//   caution = pending / informational / not the applicable blocker — gold
//   neutral = nothing selected yet, so the row can't be evaluated — muted gray
const DUPLICATE_ACTIVE_REGISTRATION_REASON =
  "Bạn đã có một ngựa đang đăng ký hoặc đã được duyệt cho giải này.";

export const buildTournamentRequirementItems = ({ tournament, selectedHorseState, hasHorse }) => {
  const hasTournament = Boolean(tournament);
  const isOpen = Boolean(tournament?.registerable);
  const registrationKey = tournament?.registrationKey ?? "";

  const items = [
    {
      // Overall gate: this is the summary check, so any non-open state is a hard fail.
      label: "Giải đấu đang mở đăng ký",
      tone: !hasTournament ? "neutral" : isOpen ? "pass" : "fail",
      detail: tournament?.registrationLabel || "Chưa chọn giải đấu",
    },
    {
      // Sub-check: only "fail" when the deadline itself is the reason registration is closed;
      // otherwise caution (closed for a different reason, so this row is informational).
      label: "Còn hạn đăng ký",
      tone: !hasTournament ? "neutral" : registrationKey === "closed" ? "fail" : isOpen ? "pass" : "caution",
      detail: tournament?.registrationDeadline ? "Đã thiết lập hạn đăng ký" : "Chưa có hạn đăng ký",
    },
    {
      // Sub-check: only "fail" when capacity itself is the reason registration is closed.
      label: "Còn suất tham gia",
      tone: !hasTournament ? "neutral" : registrationKey === "full" ? "fail" : isOpen ? "pass" : "caution",
      detail: tournament?.capacityMessage || "Theo số lượng đã duyệt",
    },
    {
      // Business requirement on the Horse: Pending is a caution (still waiting, not rejected),
      // Rejected/unknown is a fail (blocked), Approved is a pass.
      label: "Ngựa phải được phê duyệt",
      tone: !hasHorse
        ? "neutral"
        : selectedHorseState?.selectable
          ? "pass"
          : selectedHorseState?.label === "Chờ duyệt"
            ? "caution"
            : "fail",
      detail: !hasHorse ? "Chưa chọn ngựa" : selectedHorseState?.label || "Chưa xác định",
    },
  ];

  const duplicateBlocked = selectedHorseState?.reason === DUPLICATE_ACTIVE_REGISTRATION_REASON;
  items.push({
    // Also re-validated server-side on submit — shown as a pass until we already know it's blocked.
    label: "Không có đăng ký đang hoạt động khác",
    tone: !hasHorse || !hasTournament ? "neutral" : duplicateBlocked ? "fail" : "pass",
    detail: duplicateBlocked ? selectedHorseState.reason : "Không phát hiện đăng ký nào khác đang hoạt động.",
  });

  return items;
};

// OWNER-DEMO-POLISH-V1.3 §2: the single source of truth for whether the Owner Tournament
// Register CTA may actually submit — extracted so the disabled condition is testable without a
// DOM render. The button was already wired to `disabled={!canSubmit}` (so no request could ever
// reach the backend when this is false), but the CTA still LOOKED clickable with no visual/label
// cue — that visual bug is fixed separately in the page's CSS/label, not here. Backend remains the
// authoritative gate regardless of what this returns.
export const canSubmitTournamentRegistration = ({
  selectedHorse,
  selectedHorseState,
  selectedTournament,
  isSubmitting,
  hasExistingRegistration,
  hasOwnerActiveRegistration,
}) =>
  Boolean(
    selectedHorse &&
    selectedHorseState?.selectable &&
    selectedTournament?.registerable &&
    !isSubmitting &&
    !hasExistingRegistration &&
    !hasOwnerActiveRegistration,
  );

export const getFinalRaceRanking = ({ registration, entries, raceResults, tournament }) => {
  const tournamentId = registration?.tournamentId ?? registration?.TournamentId;
  const maxRounds = Number(tournament?.maxRounds ?? tournament?.MaxRounds);
  if (!tournamentId || !Number.isFinite(maxRounds) || maxRounds <= 0) return null;

  const finalEntries = (Array.isArray(entries) ? entries : []).filter(
    (entry) =>
      String(entry.tournamentId ?? entry.TournamentId) === String(tournamentId) &&
      Number(entry.roundNumber ?? entry.RoundNumber) === maxRounds,
  );

  const finalEntry = finalEntries.find((entry) => {
    const raceId = entry.raceId ?? entry.RaceId;
    return isOfficialResult(raceResults?.[raceId]);
  });

  if (!finalEntry) return null;
  const raceId = finalEntry.raceId ?? finalEntry.RaceId;
  return {
    title: OWNER_FINAL_RANKING_TITLE,
    subtitle: OWNER_FINAL_RANKING_SUBTITLE,
    entry: finalEntry,
    result: raceResults?.[raceId],
  };
};

export const formatAffectsResult = (value) => {
  if (value === true) return "Có ảnh hưởng kết quả";
  if (value === false) return "Không ảnh hưởng kết quả";
  return "Chưa xác định";
};
