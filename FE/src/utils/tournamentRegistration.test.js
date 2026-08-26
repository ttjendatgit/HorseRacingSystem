import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  canEditTournamentMetadata,
  canEditTournamentStructure,
  canHardDeleteTournament,
  canRegisterTournament,
  filterTournamentsByStatusTab,
  getCapacityFullMessage,
  getTournamentCardActions,
  getTournamentLifecycleLabel,
  getTournamentRegistrationState,
  getTournamentRegistrationTone,
  getTournamentStatusTabCounts,
  getTournamentThumbnailUrl,
  isFinalRound,
  isTournamentCardReadOnly,
  resolveTournamentPageView,
  selectUpcomingTournament,
} from "./tournamentRegistration.js";

describe("tournament registration state", () => {
  test("maps lifecycle status from Tournament.Status", () => {
    assert.equal(getTournamentLifecycleLabel({ status: 0 }), "Bản nháp");
    assert.equal(getTournamentLifecycleLabel({ statusName: "Published" }), "Đã công bố");
    assert.equal(getTournamentLifecycleLabel({ Status: "Ongoing" }), "Đang diễn ra");
    assert.equal(getTournamentLifecycleLabel({ StatusName: "Finished" }), "Đã kết thúc");
    assert.equal(getTournamentLifecycleLabel({ status: 4 }), "Đã hủy");
  });

  test("never reports Draft as open for registration", () => {
    const state = getTournamentRegistrationState({ status: 0, registrationDeadline: "2099-01-01T00:00:00Z" });

    assert.equal(state.label, "Chưa công bố");
    assert.equal(state.canRegister, false);
    assert.equal(canRegisterTournament({ statusName: "Draft", registrationDeadline: "2099-01-01T00:00:00Z" }), false);
  });

  test("separates Published registration window from lifecycle", () => {
    const now = new Date("2026-08-19T00:00:00Z");

    assert.equal(
      getTournamentRegistrationState({ statusName: "Published", registrationDeadline: "2026-08-20T00:00:00Z" }, now).label,
      "Mở đăng ký",
    );
    assert.equal(
      getTournamentRegistrationState({ statusName: "Published", registrationDeadline: "2026-08-18T00:00:00Z" }, now).label,
      "Đã đóng đăng ký",
    );
  });

  test("uses explicit non-registration labels for non-published statuses", () => {
    assert.equal(getTournamentRegistrationState({ statusName: "Ongoing" }).label, "Đã kết thúc đăng ký");
    assert.equal(getTournamentRegistrationState({ statusName: "Finished" }).label, "Giải đã kết thúc");
    assert.equal(getTournamentRegistrationState({ statusName: "Cancelled" }).label, "Giải đã hủy");
  });
});

// ── OWNER-DEMO-POLISH-V1.2 §8: Owner Tournament List badge color system ──
describe("tournament registration tone", () => {
  test("open and finished are both 'success' (green) — active or positively completed", () => {
    assert.equal(getTournamentRegistrationTone("open"), "success");
    assert.equal(getTournamentRegistrationTone("finished"), "success");
  });

  test("full is 'gold' (caution) — closed for a good reason, not a rejection", () => {
    assert.equal(getTournamentRegistrationTone("full"), "gold");
  });

  test("cancelled is 'danger' (red) — the only truly blocked/bad state", () => {
    assert.equal(getTournamentRegistrationTone("cancelled"), "danger");
  });

  test("closed / registration-ended / unpublished / unknown are all 'neutral' — informational, not a failure", () => {
    assert.equal(getTournamentRegistrationTone("closed"), "neutral");
    assert.equal(getTournamentRegistrationTone("registration-ended"), "neutral");
    assert.equal(getTournamentRegistrationTone("unpublished"), "neutral");
    assert.equal(getTournamentRegistrationTone("unknown"), "neutral");
  });

  test("an unrecognized key falls back to neutral rather than throwing", () => {
    assert.equal(getTournamentRegistrationTone("something-new"), "neutral");
    assert.equal(getTournamentRegistrationTone(undefined), "neutral");
  });

  test("tone always matches the key that produced the label — never derived from the label text", () => {
    // Every registration-state key the FE actually produces maps to a defined tone bucket, so a
    // card's badge color can never disagree with its own badge text.
    const now = new Date("2026-08-19T00:00:00Z");
    const openState = getTournamentRegistrationState({ statusName: "Published", registrationDeadline: "2026-08-20T00:00:00Z" }, now);
    const closedState = getTournamentRegistrationState({ statusName: "Published", registrationDeadline: "2026-08-18T00:00:00Z" }, now);
    const cancelledState = getTournamentRegistrationState({ statusName: "Cancelled" });

    assert.equal(getTournamentRegistrationTone(openState.key), "success");
    assert.equal(getTournamentRegistrationTone(closedState.key), "neutral");
    assert.equal(getTournamentRegistrationTone(cancelledState.key), "danger");
  });
});

// ── OWNER-DEMO-POLISH-V1.2 §7/§9: /owner/tournaments status filter must only ever expose labels
// that a real Tournament can actually produce via getTournamentRegistrationState — no invented
// wording, no drift between the filter dropdown and the actual business states it filters by. ──
describe("owner tournament list status filter labels", () => {
  // Mirrors the literal array in OwnerTournamentListPage.jsx — kept here as its own fixture (not
  // imported) so this test still catches drift if that array is ever hand-edited independently.
  const OWNER_TOURNAMENT_STATUS_FILTERS = [
    "Tất cả",
    "Mở đăng ký",
    "Đã đủ số lượng tham gia",
    "Đã đóng đăng ký",
    "Đã kết thúc đăng ký",
    "Giải đã kết thúc",
    "Giải đã hủy",
  ];

  const now = new Date("2026-08-19T00:00:00Z");
  const knownLabels = new Set([
    getTournamentRegistrationState({ statusName: "Published", registrationDeadline: "2026-08-20T00:00:00Z" }, now).label,
    getTournamentRegistrationState({ statusName: "Published", registrationDeadline: "2026-08-18T00:00:00Z" }, now).label,
    getTournamentRegistrationState(
      { statusName: "Published", registrationDeadline: "2026-08-20T00:00:00Z", maxParticipants: 2, approvedRegistrationCount: 2 },
      now,
    ).label,
    getTournamentRegistrationState({ statusName: "Ongoing" }).label,
    getTournamentRegistrationState({ statusName: "Finished" }).label,
    getTournamentRegistrationState({ statusName: "Cancelled" }).label,
  ]);

  test("every non-'Tất cả' filter option is a real registration-state label, not an invented string", () => {
    for (const filter of OWNER_TOURNAMENT_STATUS_FILTERS) {
      if (filter === "Tất cả") continue;
      assert.ok(knownLabels.has(filter), `"${filter}" must be a label getTournamentRegistrationState can actually produce`);
    }
  });

  test("the capacity-full state ('Đã đủ số lượng tham gia') is also a real derivable label", () => {
    const fullState = getTournamentRegistrationState(
      { statusName: "Published", registrationDeadline: "2026-08-20T00:00:00Z", maxParticipants: 2, approvedRegistrationCount: 2 },
      now,
    );
    assert.equal(fullState.label, "Đã đủ số lượng tham gia");
    assert.ok(OWNER_TOURNAMENT_STATUS_FILTERS.includes(fullState.label));
  });
});

describe("tournament capacity gate", () => {
  const now = new Date("2026-08-19T00:00:00Z");
  const base = { statusName: "Published", registrationDeadline: "2026-08-20T00:00:00Z" };

  test("Published + before deadline + capacity available => Mở đăng ký", () => {
    const state = getTournamentRegistrationState(
      { ...base, maxParticipants: 4, approvedRegistrationCount: 3 },
      now,
    );
    assert.equal(state.key, "open");
    assert.equal(state.label, "Mở đăng ký");
    assert.equal(state.canRegister, true);
    assert.equal(
      canRegisterTournament({ ...base, maxParticipants: 4, approvedRegistrationCount: 3 }, now),
      true,
    );
  });

  test("Published + before deadline + full => Đã đủ số lượng tham gia", () => {
    const state = getTournamentRegistrationState(
      { ...base, maxParticipants: 4, approvedRegistrationCount: 4 },
      now,
    );
    assert.equal(state.key, "full");
    assert.equal(state.label, "Đã đủ số lượng tham gia");
    assert.equal(state.canRegister, false);
  });

  test("over capacity (approvedCount > max, e.g. stale data) is still reported full, not open", () => {
    const state = getTournamentRegistrationState(
      { ...base, maxParticipants: 4, approvedRegistrationCount: 5 },
      now,
    );
    assert.equal(state.key, "full");
    assert.equal(state.canRegister, false);
  });

  test("full state is NOT reported as Đã đóng đăng ký — capacity and deadline are distinct reasons", () => {
    const state = getTournamentRegistrationState(
      { ...base, maxParticipants: 4, approvedRegistrationCount: 4 },
      now,
    );
    assert.notEqual(state.label, "Đã đóng đăng ký");
    assert.notEqual(state.key, "closed");
  });

  test("a Tournament with no MaxParticipants set is never reported full", () => {
    const state = getTournamentRegistrationState(
      { ...base, approvedRegistrationCount: 999 },
      now,
    );
    assert.equal(state.key, "open");
    assert.equal(state.canRegister, true);
  });

  test("getCapacityFullMessage reports the exact X/Y counts only when full, null otherwise", () => {
    assert.equal(
      getCapacityFullMessage({ ...base, maxParticipants: 4, approvedRegistrationCount: 4 }, now),
      "Giải đấu đã đủ 4/4 ngựa tham gia.\nHẹn bạn ở giải đấu tiếp theo.",
    );
    assert.equal(
      getCapacityFullMessage({ ...base, maxParticipants: 4, approvedRegistrationCount: 3 }, now),
      null,
    );
    // Past-deadline "closed" is a different reason than capacity — no capacity message either.
    assert.equal(
      getCapacityFullMessage(
        { ...base, registrationDeadline: "2026-08-18T00:00:00Z", maxParticipants: 4, approvedRegistrationCount: 4 },
        now,
      ),
      null,
    );
  });
});

describe("tournament hard-delete affordance (T-D1)", () => {
  // Backend (TournamentAndRoundService.DeleteTournamentAsync) is authoritative and rejects with
  // 409 for anything except Draft or Cancelled (T-D2) — this only governs whether the FE shows
  // the "Xóa" button.
  test("Draft shows the hard-delete affordance", () => {
    assert.equal(canHardDeleteTournament({ status: 0 }), true);
    assert.equal(canHardDeleteTournament({ statusName: "Draft" }), true);
  });

  test("Cancelled shows the hard-delete affordance (T-D2)", () => {
    assert.equal(canHardDeleteTournament({ status: 4 }), true);
    assert.equal(canHardDeleteTournament({ statusName: "Cancelled" }), true);
  });

  test("Published/Ongoing/Finished do not show the hard-delete affordance", () => {
    assert.equal(canHardDeleteTournament({ statusName: "Published" }), false);
    assert.equal(canHardDeleteTournament({ statusName: "Ongoing" }), false);
    assert.equal(canHardDeleteTournament({ statusName: "Finished" }), false);
    assert.equal(canHardDeleteTournament({ status: 1 }), false);
    assert.equal(canHardDeleteTournament({ status: 2 }), false);
    assert.equal(canHardDeleteTournament({ status: 3 }), false);
  });
});

describe("canEditTournamentStructure (V0.1 micro-fix)", () => {
  // Required rule: ONLY Draft may change structural fields (MaxRounds etc.) — every other
  // status must lock them alike, not just Published.
  test("Draft is editable, every other status is locked", () => {
    assert.equal(canEditTournamentStructure(0), true); // Draft
    assert.equal(canEditTournamentStructure(1), false); // Published
    assert.equal(canEditTournamentStructure(2), false); // Ongoing
    assert.equal(canEditTournamentStructure(3), false); // Finished
    assert.equal(canEditTournamentStructure(4), false); // Cancelled
  });

  test("accepts the status name string form too", () => {
    assert.equal(canEditTournamentStructure("Draft"), true);
    assert.equal(canEditTournamentStructure("Published"), false);
    assert.equal(canEditTournamentStructure("Ongoing"), false);
    assert.equal(canEditTournamentStructure("Finished"), false);
    assert.equal(canEditTournamentStructure("Cancelled"), false);
  });

  test("null/undefined status (no Tournament being edited yet) is locked, not editable", () => {
    assert.equal(canEditTournamentStructure(null), false);
    assert.equal(canEditTournamentStructure(undefined), false);
  });
});

describe("canEditTournamentMetadata (ADMIN-TOURNAMENTS-UI-POLISH)", () => {
  // Backend gate: TournamentAndRoundService.UpdateTournamentAsync rejects (400) any update once
  // Status is neither Draft nor Published — this only governs whether the FE offers "Sửa" at all.
  test("Draft and Published may open the edit form", () => {
    assert.equal(canEditTournamentMetadata({ statusName: "Draft" }), true);
    assert.equal(canEditTournamentMetadata({ statusName: "Published" }), true);
  });

  test("Ongoing/Finished/Cancelled must not offer the edit form — backend rejects the whole update", () => {
    assert.equal(canEditTournamentMetadata({ statusName: "Ongoing" }), false);
    assert.equal(canEditTournamentMetadata({ statusName: "Finished" }), false);
    assert.equal(canEditTournamentMetadata({ statusName: "Cancelled" }), false);
  });

  test("accepts numeric TournamentStatus too", () => {
    assert.equal(canEditTournamentMetadata({ status: 0 }), true); // Draft
    assert.equal(canEditTournamentMetadata({ status: 1 }), true); // Published
    assert.equal(canEditTournamentMetadata({ status: 2 }), false); // Ongoing
    assert.equal(canEditTournamentMetadata({ status: 3 }), false); // Finished
    assert.equal(canEditTournamentMetadata({ status: 4 }), false); // Cancelled
  });
});

describe("Tournament status tabs (ADMIN-TOURNAMENTS-UI-POLISH)", () => {
  const tournaments = [
    { id: "t1", statusName: "Draft" },
    { id: "t2", statusName: "Draft" },
    { id: "t3", statusName: "Published" },
    { id: "t4", statusName: "Ongoing" },
    { id: "t5", statusName: "Finished" },
    { id: "t6", statusName: "Finished" },
    { id: "t7", statusName: "Finished" },
    { id: "t8", statusName: "Cancelled" },
  ];

  test("counts every real TournamentStatus value plus an all total", () => {
    assert.deepEqual(getTournamentStatusTabCounts(tournaments), {
      all: 8,
      draft: 2,
      published: 1,
      ongoing: 1,
      finished: 3,
      cancelled: 1,
    });
  });

  test("counts default to zero for an empty/non-array list, never crash", () => {
    assert.deepEqual(getTournamentStatusTabCounts([]), { all: 0, draft: 0, published: 0, ongoing: 0, finished: 0, cancelled: 0 });
    assert.deepEqual(getTournamentStatusTabCounts(null), { all: 0, draft: 0, published: 0, ongoing: 0, finished: 0, cancelled: 0 });
  });

  test("filtering by a status tab returns only that group; 'all' returns everything unfiltered", () => {
    assert.deepEqual(filterTournamentsByStatusTab(tournaments, "finished").map((t) => t.id), ["t5", "t6", "t7"]);
    assert.deepEqual(filterTournamentsByStatusTab(tournaments, "cancelled").map((t) => t.id), ["t8"]);
    assert.deepEqual(filterTournamentsByStatusTab(tournaments, "all"), tournaments);
    assert.deepEqual(filterTournamentsByStatusTab(tournaments, null), tournaments);
  });

  test("no group ever silently hides a Tournament — every filtered group's total sums back to all", () => {
    const total = ["draft", "published", "ongoing", "finished", "cancelled"]
      .reduce((sum, tab) => sum + filterTournamentsByStatusTab(tournaments, tab).length, 0);
    assert.equal(total, tournaments.length);
  });
});

describe("getTournamentCardActions (ADMIN-TOURNAMENTS-UI-POLISH)", () => {
  // Lifecycle buttons must come straight from the backend's own NextTransitions — never invented.
  const draftTransitions = [{ status: 1, label: "Công bố giải", isPrimary: true }, { status: 4, label: "Hủy giải", isPrimary: false }];
  const finishedTransitions = [];

  test("Draft: editable, hard-deletable, and exposes exactly the backend's own transitions", () => {
    const actions = getTournamentCardActions({ statusName: "Draft", nextTransitions: draftTransitions });
    assert.equal(actions.canEdit, true);
    assert.equal(actions.canDelete, true);
    assert.deepEqual(actions.transitions, draftTransitions);
  });

  test("Published: editable, not hard-deletable", () => {
    const actions = getTournamentCardActions({ statusName: "Published", nextTransitions: [] });
    assert.equal(actions.canEdit, true);
    assert.equal(actions.canDelete, false);
  });

  test("Finished: read-only — no edit, no delete, no lifecycle transitions", () => {
    const actions = getTournamentCardActions({ statusName: "Finished", nextTransitions: finishedTransitions });
    assert.equal(actions.canEdit, false);
    assert.equal(actions.canDelete, false);
    assert.deepEqual(actions.transitions, []);
  });

  test("Cancelled: not editable, but hard-delete remains available (T-D1/T-D2), no transitions", () => {
    const actions = getTournamentCardActions({ statusName: "Cancelled", nextTransitions: [] });
    assert.equal(actions.canEdit, false);
    assert.equal(actions.canDelete, true);
    assert.deepEqual(actions.transitions, []);
  });

  test("a missing/non-array NextTransitions never crashes — degrades to no lifecycle buttons", () => {
    assert.deepEqual(getTournamentCardActions({ statusName: "Draft" }).transitions, []);
    assert.deepEqual(getTournamentCardActions({ statusName: "Draft", nextTransitions: null }).transitions, []);
  });
});

describe("isTournamentCardReadOnly (ADMIN-TOURNAMENTS-REGRESSION-FIX #10/#12)", () => {
  test("Finished is operationally read-only — no edit, no lifecycle transition", () => {
    assert.equal(isTournamentCardReadOnly({ statusName: "Finished", nextTransitions: [] }), true);
  });

  test("Cancelled is read-only too, even though hard-delete (a non-card-action) remains available", () => {
    assert.equal(isTournamentCardReadOnly({ statusName: "Cancelled", nextTransitions: [] }), true);
  });

  test("Draft/Published/Ongoing are NOT read-only — each has a real edit or transition affordance", () => {
    assert.equal(isTournamentCardReadOnly({ statusName: "Draft", nextTransitions: [{ status: 1, label: "Công bố giải", isPrimary: true }] }), false);
    assert.equal(isTournamentCardReadOnly({ statusName: "Published", nextTransitions: [] }), false); // canEdit=true alone is enough
    assert.equal(isTournamentCardReadOnly({ statusName: "Ongoing", nextTransitions: [{ status: 3, label: "Kết thúc giải", isPrimary: true }] }), false);
  });
});

describe("getTournamentThumbnailUrl (ADMIN-TOURNAMENTS-REGRESSION-FIX #6)", () => {
  test("uses ImageUrl/imageUrl when present", () => {
    assert.equal(getTournamentThumbnailUrl({ imageUrl: "https://cdn.example/a.png" }), "https://cdn.example/a.png");
    assert.equal(getTournamentThumbnailUrl({ ImageUrl: "https://cdn.example/b.png" }), "https://cdn.example/b.png");
  });

  test("missing/empty image resolves to null, never '' or undefined, so callers get one safe fallback branch", () => {
    assert.equal(getTournamentThumbnailUrl({}), null);
    assert.equal(getTournamentThumbnailUrl({ imageUrl: null }), null);
    assert.equal(getTournamentThumbnailUrl(null), null);
  });
});

describe("resolveTournamentPageView (ADMIN-TOURNAMENTS-REGRESSION-FIX #4/#9)", () => {
  test("selecting a Tournament (Xem chi tiết) enters the detail workspace", () => {
    assert.equal(resolveTournamentPageView({ id: "t1", name: "Giải A" }), "detail");
  });

  test("no selection (after ← Quay lại) shows the list — and the list is never rendered alongside detail", () => {
    assert.equal(resolveTournamentPageView(null), "list");
    assert.equal(resolveTournamentPageView(undefined), "list");
  });
});

describe("isFinalRound (V0/V0.1)", () => {
  // MaxRounds=2: only Round2 (RoundNumber === MaxRounds) is Final — never derived from AdvanceCount.
  test("MaxRounds 2: Round1 is not Final, Round2 is Final", () => {
    const tournament = { maxRounds: 2 };
    assert.equal(isFinalRound({ roundNumber: 1 }, tournament), false);
    assert.equal(isFinalRound({ roundNumber: 2 }, tournament), true);
  });

  test("MaxRounds=2 / Round1 AdvanceCount=3 / Round2 AdvanceCount=0 -> Round2 is Final", () => {
    const tournament = { maxRounds: 2 };
    assert.equal(isFinalRound({ roundNumber: 1, advanceCount: 3 }, tournament), false);
    assert.equal(isFinalRound({ roundNumber: 2, advanceCount: 0 }, tournament), true);
  });

  test("AdvanceCount === 0 on a non-final Round must NOT be treated as Final", () => {
    // The exact V0.1 regression: MaxRounds silently stayed 1 while Round1+Round2 both exist.
    // Round1 must never show as Final just because its AdvanceCount happens to be 0.
    const tournament = { maxRounds: 2 };
    assert.equal(isFinalRound({ roundNumber: 1, advanceCount: 0 }, tournament), false);
  });

  test("single-round Tournament: MaxRounds=1, Round1 is Final", () => {
    assert.equal(isFinalRound({ roundNumber: 1 }, { maxRounds: 1 }), true);
  });

  test("handles PascalCase API shape (RoundNumber / MaxRounds)", () => {
    assert.equal(isFinalRound({ RoundNumber: 2 }, { MaxRounds: 2 }), true);
    assert.equal(isFinalRound({ RoundNumber: 1 }, { MaxRounds: 2 }), false);
  });

  test("string RoundNumber/MaxRounds (raw form/API state) still compares numerically", () => {
    assert.equal(isFinalRound({ roundNumber: "2" }, { maxRounds: "2" }), true);
    assert.equal(isFinalRound({ roundNumber: "1" }, { maxRounds: "2" }), false);
  });

  test("missing MaxRounds or missing round/tournament never reports Final", () => {
    assert.equal(isFinalRound({ roundNumber: 1 }, {}), false);
    assert.equal(isFinalRound({ roundNumber: 1 }, null), false);
    assert.equal(isFinalRound(null, { maxRounds: 1 }), false);
  });
});

// ── OWNER-DEMO-POLISH-V1.3 §1: /owner/tournaments "Giải đấu sắp tới" sidebar must never select a
// Finished or Cancelled Tournament, and must prefer the nearest legitimate upcoming one. ──
describe("selectUpcomingTournament", () => {
  const now = new Date("2026-08-19T00:00:00Z");

  test("a Finished Tournament is excluded even if it would otherwise be 'nearest'", () => {
    const tournaments = [
      { id: "finished-soon", statusKey: "finished", startDate: "2026-08-20T00:00:00Z" },
      { id: "open-later", statusKey: "open", startDate: "2026-09-01T00:00:00Z" },
    ];
    const result = selectUpcomingTournament(tournaments, now);
    assert.equal(result.id, "open-later");
  });

  test("a Cancelled Tournament is excluded even if it would otherwise be 'nearest'", () => {
    const tournaments = [
      { id: "cancelled-soon", statusKey: "cancelled", startDate: "2026-08-20T00:00:00Z" },
      { id: "open-later", statusKey: "open", startDate: "2026-09-01T00:00:00Z" },
    ];
    const result = selectUpcomingTournament(tournaments, now);
    assert.equal(result.id, "open-later");
  });

  test("picks the nearest valid upcoming Tournament by StartDate, not array order", () => {
    const tournaments = [
      { id: "far", statusKey: "open", startDate: "2026-12-01T00:00:00Z" },
      { id: "nearest", statusKey: "open", startDate: "2026-08-25T00:00:00Z" },
      { id: "middle", statusKey: "full", startDate: "2026-09-15T00:00:00Z" },
    ];
    const result = selectUpcomingTournament(tournaments, now);
    assert.equal(result.id, "nearest");
  });

  test("falls back to the nearest-by-date eligible Tournament when none have a future StartDate (e.g. only Ongoing left)", () => {
    const tournaments = [
      { id: "started-long-ago", statusKey: "registration-ended", startDate: "2026-01-01T00:00:00Z" },
      { id: "started-recently", statusKey: "registration-ended", startDate: "2026-08-15T00:00:00Z" },
    ];
    const result = selectUpcomingTournament(tournaments, now);
    assert.equal(result.id, "started-recently");
  });

  test("no valid Tournament (all Finished/Cancelled) => null, so the caller can render the empty state", () => {
    const tournaments = [
      { id: "a", statusKey: "finished", startDate: "2026-09-01T00:00:00Z" },
      { id: "b", statusKey: "cancelled", startDate: "2026-09-05T00:00:00Z" },
    ];
    assert.equal(selectUpcomingTournament(tournaments, now), null);
  });

  test("an empty or non-array input also resolves to null, never throws", () => {
    assert.equal(selectUpcomingTournament([], now), null);
    assert.equal(selectUpcomingTournament(null, now), null);
    assert.equal(selectUpcomingTournament(undefined, now), null);
  });

  test("never falls back to a Finished Tournament even when it is the only one with a parsable StartDate", () => {
    const tournaments = [
      { id: "finished", statusKey: "finished", startDate: "2026-08-18T00:00:00Z" },
      { id: "open-no-date", statusKey: "open", startDate: null },
    ];
    const result = selectUpcomingTournament(tournaments, now);
    assert.equal(result.id, "open-no-date");
  });
});
