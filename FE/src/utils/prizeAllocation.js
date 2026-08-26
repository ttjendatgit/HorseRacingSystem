// PRIZE-V1.2: pure helpers for the Admin Tournament Prize Allocation UI. Admin configures
// Prize.PercentageOfPool — Amount is entirely backend-derived (Tournament.PrizePool *
// PercentageOfPool / 100) and never submitted by the client. Percentage is now the
// source-of-truth allocation-completeness figure (SUM == 100% at Publish), not the Amount sum.
// These helpers never touch IsDistributed/DistributedAt/RaceId, which stay hidden — no
// payout/distribution workflow exists in this product.

const STATUS_BY_NUMBER = { 0: "Draft", 1: "Published", 2: "Ongoing", 3: "Finished", 4: "Cancelled" };

// Mirrors the numeric-enum/string-enum/numeric-as-string normalization already used by
// jockeyApproval.js's normalizeJockeyApprovalStatus, for the same reason: the backend sends
// Tournament.Status as a number but some payload shapes carry a string StatusName instead.
/**
 * Chuẩn hóa trạng thái giải đấu từ định dạng số enum hoặc chuỗi về tên chuẩn.
 * @param {number|string} rawStatus - Trạng thái thô nhận từ API backend.
 * @returns {string|null} Tên trạng thái chuẩn ("Draft", "Published", "Ongoing", "Finished", "Cancelled")
 */
export function normalizeTournamentStatus(rawStatus) {
  if (rawStatus === undefined || rawStatus === null || rawStatus === "") return null;

  if (typeof rawStatus === "number") {
    return STATUS_BY_NUMBER[rawStatus] ?? null;
  }

  const normalized = String(rawStatus).trim().toLowerCase();
  for (const [num, name] of Object.entries(STATUS_BY_NUMBER)) {
    if (normalized === name.toLowerCase() || normalized === num) return name;
  }
  return null;
}

// Accepts a Tournament payload (camelCase or PascalCase) and reports whether its Prize allocation
// is currently mutable — true only in Draft, per the V1 lock rule (Part 3).
/**
 * Kiểm tra giải đấu có ở trạng thái Bản nháp (Draft) để cho phép chỉnh sửa giải thưởng hay không.
 * @param {Object} tournament - Thông tin đối tượng giải đấu.
 * @returns {boolean} True nếu giải đấu ở trạng thái Bản nháp.
 */
export function isTournamentDraftEditable(tournament) {
  const raw = tournament?.status ?? tournament?.Status ?? tournament?.statusName ?? tournament?.StatusName ?? null;
  return normalizeTournamentStatus(raw) === "Draft";
}

const toAmount = (prize) => Number(prize?.amount ?? prize?.Amount ?? 0) || 0;
const toPercentage = (prize) => Number(prize?.percentageOfPool ?? prize?.PercentageOfPool ?? 0) || 0;
const toPosition = (prize) => Number(prize?.position ?? prize?.Position ?? 0) || 0;

// PRIZE-V1.1 PART 1/13: PlannedFinalParticipants — the maximum valid Prize.Position. Mirrors
// BE/Services/PlannedFinalParticipantsHelper.cs exactly: single-round (MaxRounds <= 1) uses
// Tournament.MaxParticipants directly; multi-round uses the AdvanceCount of the single Round with
// RoundNumber == MaxRounds - 1 (the pre-Final round). Deliberately never derived from actual
// registrations, RaceEntry counts, Race.MaxParticipants, or Track.Capacity — those answer
// different questions. Returns null when indeterminate (MaxParticipants unset, or the pre-Final
// Round doesn't exist yet / is duplicated) — never guessed.
export function computePlannedFinalParticipants(tournament, rounds) {
  const maxRoundsRaw = tournament?.maxRounds ?? tournament?.MaxRounds;
  const maxRounds = maxRoundsRaw === undefined || maxRoundsRaw === null ? 1 : Number(maxRoundsRaw);
  const maxParticipantsRaw = tournament?.maxParticipants ?? tournament?.MaxParticipants;
  const maxParticipants = maxParticipantsRaw === undefined || maxParticipantsRaw === null ? null : Number(maxParticipantsRaw);

  if (maxRounds <= 1) return maxParticipants;

  const preFinalRoundNumber = maxRounds - 1;
  const candidates = (Array.isArray(rounds) ? rounds : []).filter(
    (r) => Number(r?.roundNumber ?? r?.RoundNumber) === preFinalRoundNumber
  );
  if (candidates.length !== 1) return null;

  const advanceCountRaw = candidates[0]?.advanceCount ?? candidates[0]?.AdvanceCount;
  return advanceCountRaw === undefined || advanceCountRaw === null ? null : Number(advanceCountRaw);
}

// PART 20: avoids wording that implies every Final participant must be rewarded — pairs with a
// "Hạng {N}" label rather than a bare count.
export function getMaxRankLabel(maxRank) {
  return maxRank == null ? null : `Hạng ${maxRank}`;
}

export function computeAllocatedTotal(prizes) {
  if (!Array.isArray(prizes)) return 0;
  return prizes.reduce((sum, p) => sum + toAmount(p), 0);
}

export function computeRemainingBudget(prizePool, prizes) {
  const pool = Number(prizePool) || 0;
  return pool - computeAllocatedTotal(prizes);
}

// PRIZE-V1.2 PART 2: percentage sum is the source-of-truth allocation figure — independent of
// PrizePool's actual value (a Prize row's PercentageOfPool validity never depends on PrizePool).
export function computeAllocatedPercentage(prizes) {
  if (!Array.isArray(prizes)) return 0;
  return Math.round(prizes.reduce((sum, p) => sum + toPercentage(p), 0) * 100) / 100;
}

export function computeRemainingPercentage(prizes) {
  return Math.round((100 - computeAllocatedPercentage(prizes)) * 100) / 100;
}

export function sortPrizesByPosition(prizes) {
  if (!Array.isArray(prizes)) return [];
  return [...prizes].sort((a, b) => toPosition(a) - toPosition(b));
}

export function hasPrizeAllocations(prizes) {
  return Array.isArray(prizes) && prizes.length > 0;
}

// Publish-readiness mirror of the backend's ValidatePrizeReadinessAsync total check (Part 2/10):
// SUM(PercentageOfPool) must equal exactly 100% — the Amount sum is a derived consequence, not
// the completeness source of truth.
export function isAllocationComplete(prizes) {
  if (!Array.isArray(prizes) || prizes.length === 0) return false;
  return computeAllocatedPercentage(prizes) === 100;
}

// Admin-only display formatting for this page — "10.000.000 ₫" (vi-VN grouping + đồng sign).
export function formatVndCurrency(amount) {
  const value = Number(amount) || 0;
  return `${value.toLocaleString("vi-VN")} ₫`;
}

// PART 17: compact percentage formatting — "30%" / "27.5%" / "33.33%", never "30.0000%".
// decimal(5,2) storage means at most 2 fractional digits, so toFixed(2) round-trip is safe.
export function formatPercentage(value) {
  const n = Number(value);
  if (!Number.isFinite(n)) return "0%";
  return `${Number(n.toFixed(2))}%`;
}

// PRIZE-V1.2 FINAL HARDENING Part 2: historical Published/Ongoing/Finished Prize rows may
// legitimately carry PercentageOfPool == 0 (unused before PRIZE-V1.2) — those rows are never
// migrated/mutated, so the display must fall back gracefully instead of showing a misleading
// "0% · amount" or inventing a fake historical percentage from Amount. PercentageOfPool > 0 shows
// both figures; <= 0 (or missing) shows the amount alone.
export function formatPrizeFigure(percentage, amount) {
  const pct = Number(percentage) || 0;
  const money = formatVndCurrency(amount);
  return pct > 0 ? `${formatPercentage(pct)} · ${money}` : money;
}

// PART 16: live, client-side-only preview of what Amount a percentage would derive to — the
// backend remains authoritative and recalculates on save using the full multi-row
// rounding-remainder rule (PrizeAmountCalculator); this single-row estimate exists purely so
// Admin sees an immediate number while typing, not to replicate that algorithm exactly.
export function computePreviewAmount(prizePool, percentage) {
  const pool = Number(prizePool) || 0;
  const pct = Number(percentage);
  if (!Number.isFinite(pct) || pct <= 0) return 0;
  return Math.round((pool * pct) / 100);
}

// PART 15: quick-fill percentage shortcuts — suggestions only, manual entry is never restricted.
export const PRESET_PERCENTAGES = [10, 20, 25, 30, 50];

const DEFAULT_NAME_BY_POSITION = { 1: "Vô địch", 2: "Á quân", 3: "Quý quân" };

// PART 21: mirrors PrizeService's own DefaultPrizeName exactly (Vô địch/Á quân/Quý quân/Hạng N) —
// used only as a placeholder/preview; never overwrites an Admin-entered Name.
export function getDefaultPrizeName(position) {
  const pos = Number(position);
  return DEFAULT_NAME_BY_POSITION[pos] ?? `Hạng ${Number.isFinite(pos) ? pos : "?"}`;
}

export function isValidPercentage(value) {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 && n <= 100;
}

// Validates one Create/Edit form submission against the same rules PrizeService enforces
// server-side (Position >= 1, Position <= PlannedFinalParticipants, PercentageOfPool in (0, 100],
// unique Position, total percentage <= 100), returning the exact Vietnamese messages the backend
// uses so the UI can show inline errors before the round-trip. `existingPrizes` should exclude
// nothing — pass the full current list; `editingPrizeId` excludes the row being edited from both
// the duplicate-position and total-percentage checks, mirroring PrizeService.UpdateAsync's
// `excludePrizeId` semantics. `maxRank` is the Tournament's PlannedFinalParticipants — pass
// null/undefined to skip that check (e.g. while it's still indeterminate).
export function validatePrizeForm({ position, percentage, prizePool, existingPrizes, editingPrizeId, maxRank }) {
  const errors = {};
  const pos = Number(position);
  const pct = Number(percentage);
  const others = (Array.isArray(existingPrizes) ? existingPrizes : []).filter((p) => {
    const id = p?.id ?? p?.Id;
    return editingPrizeId == null || id !== editingPrizeId;
  });

  if (!Number.isFinite(pos) || pos < 1) {
    errors.position = "Hạng thưởng phải lớn hơn hoặc bằng 1.";
  } else if (maxRank != null && pos > Number(maxRank)) {
    errors.position = `Hạng thưởng chỉ được từ 1 đến ${maxRank}.`;
  } else if (others.some((p) => toPosition(p) === pos)) {
    errors.position = "Hạng thưởng này đã được cấu hình.";
  }

  if (!isValidPercentage(pct)) {
    errors.percentage = pct > 100 ? "Tỷ lệ phân bổ không được vượt quá 100%." : "Tỷ lệ phân bổ phải lớn hơn 0.";
  } else {
    const allocatedByOthers = computeAllocatedPercentage(others);
    if (allocatedByOthers + pct > 100) {
      errors.percentage = "Tổng tỷ lệ phân bổ không được vượt quá 100%.";
    }
  }

  return errors;
}
