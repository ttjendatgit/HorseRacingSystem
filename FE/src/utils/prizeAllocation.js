// PRIZE-V1: pure helpers for the Admin Tournament Prize Allocation UI. Prize.Position is an
// Admin-configured allocation slot against the Tournament's FINAL ranking — never a Race result,
// never a payout instruction. These helpers only compute totals/validate shape; they never touch
// IsDistributed/DistributedAt/RaceId/PercentageOfPool, which are legacy fields this V1 workflow
// does not expose.

const STATUS_BY_NUMBER = { 0: "Draft", 1: "Published", 2: "Ongoing", 3: "Finished", 4: "Cancelled" };

// Mirrors the numeric-enum/string-enum/numeric-as-string normalization already used by
// jockeyApproval.js's normalizeJockeyApprovalStatus, for the same reason: the backend sends
// Tournament.Status as a number but some payload shapes carry a string StatusName instead.
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
export function isTournamentDraftEditable(tournament) {
  const raw = tournament?.status ?? tournament?.Status ?? tournament?.statusName ?? tournament?.StatusName ?? null;
  return normalizeTournamentStatus(raw) === "Draft";
}

const toAmount = (prize) => Number(prize?.amount ?? prize?.Amount ?? 0) || 0;
const toPosition = (prize) => Number(prize?.position ?? prize?.Position ?? 0) || 0;

export function computeAllocatedTotal(prizes) {
  if (!Array.isArray(prizes)) return 0;
  return prizes.reduce((sum, p) => sum + toAmount(p), 0);
}

export function computeRemainingBudget(prizePool, prizes) {
  const pool = Number(prizePool) || 0;
  return pool - computeAllocatedTotal(prizes);
}

export function sortPrizesByPosition(prizes) {
  if (!Array.isArray(prizes)) return [];
  return [...prizes].sort((a, b) => toPosition(a) - toPosition(b));
}

export function hasPrizeAllocations(prizes) {
  return Array.isArray(prizes) && prizes.length > 0;
}

// Publish-readiness mirror of the backend's ValidatePrizeReadinessAsync total check (Part 5/6):
// zero pool + zero rows is complete; positive pool requires the allocated sum to exactly match.
export function isAllocationComplete(prizePool, prizes) {
  const pool = Number(prizePool) || 0;
  const allocated = computeAllocatedTotal(prizes);
  if (pool === 0) return allocated === 0;
  return allocated === pool;
}

// Admin-only display formatting for this new page — deliberately "10.000.000 ₫" (vi-VN grouping +
// đồng sign), a different convention than TournamentDetailPage.jsx's existing "N VNĐ" suffix,
// since this file has no pre-existing convention of its own to preserve.
export function formatVndCurrency(amount) {
  const value = Number(amount) || 0;
  return `${value.toLocaleString("vi-VN")} ₫`;
}

// Validates one Create/Edit form submission against the same rules PrizeService enforces
// server-side (Position >= 1, Amount > 0, unique Position, sum <= PrizePool), returning the exact
// Vietnamese messages the backend uses so the UI can show inline errors before the round-trip.
// `existingPrizes` should exclude nothing — pass the full current list; `editingPrizeId` excludes
// the row being edited from both the duplicate-position and sum checks, mirroring
// PrizeService.UpdateAsync's `excludePrizeId` semantics.
export function validatePrizeForm({ position, amount, prizePool, existingPrizes, editingPrizeId }) {
  const errors = {};
  const pos = Number(position);
  const amt = Number(amount);
  const pool = Number(prizePool) || 0;
  const others = (Array.isArray(existingPrizes) ? existingPrizes : []).filter((p) => {
    const id = p?.id ?? p?.Id;
    return editingPrizeId == null || id !== editingPrizeId;
  });

  if (!Number.isFinite(pos) || pos < 1) {
    errors.position = "Hạng thưởng phải lớn hơn hoặc bằng 1.";
  } else if (others.some((p) => toPosition(p) === pos)) {
    errors.position = "Hạng thưởng này đã được cấu hình.";
  }

  if (!Number.isFinite(amt) || amt <= 0) {
    errors.amount = "Tiền thưởng phải lớn hơn 0.";
  } else {
    const allocatedByOthers = computeAllocatedTotal(others);
    if (allocatedByOthers + amt > pool) {
      errors.amount = "Tổng tiền thưởng không được vượt quá quỹ thưởng của giải đấu.";
    }
  }

  return errors;
}
