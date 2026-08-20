import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  canHardDeleteTournament,
  canRegisterTournament,
  getCapacityFullMessage,
  getTournamentLifecycleLabel,
  getTournamentRegistrationState,
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
  // 409 for anything past Draft — this only governs whether the FE shows the "Xóa" button.
  test("Draft shows the hard-delete affordance", () => {
    assert.equal(canHardDeleteTournament({ status: 0 }), true);
    assert.equal(canHardDeleteTournament({ statusName: "Draft" }), true);
  });

  test("Published/Ongoing/Finished/Cancelled do not show the hard-delete affordance", () => {
    assert.equal(canHardDeleteTournament({ statusName: "Published" }), false);
    assert.equal(canHardDeleteTournament({ statusName: "Ongoing" }), false);
    assert.equal(canHardDeleteTournament({ statusName: "Finished" }), false);
    assert.equal(canHardDeleteTournament({ statusName: "Cancelled" }), false);
    assert.equal(canHardDeleteTournament({ status: 1 }), false);
    assert.equal(canHardDeleteTournament({ status: 2 }), false);
    assert.equal(canHardDeleteTournament({ status: 3 }), false);
    assert.equal(canHardDeleteTournament({ status: 4 }), false);
  });
});
