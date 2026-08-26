import test from "node:test";
import assert from "node:assert/strict";
import {
  buildRuleProtestPayload,
  filterProtestsByTab,
  getAvailableProtestActions,
  getDefaultProtestTab,
  getProtestStatusDetails,
  getProtestTabCounts,
} from "./protestDisplay.js";

test("protest status labels preserve result-protest lifecycle meanings", () => {
  assert.deepEqual(getProtestStatusDetails("Pending"), {
    status: "Pending",
    label: "Chờ xử lý",
    variant: "pending",
    group: "pending",
  });
  assert.deepEqual(getProtestStatusDetails("UnderReview"), {
    status: "UnderReview",
    label: "Đang xem xét",
    variant: "active",
    group: "underReview",
  });
  assert.deepEqual(getProtestStatusDetails("Upheld"), {
    status: "Upheld",
    label: "Chấp nhận",
    variant: "approved",
    group: "resolved",
  });
  assert.deepEqual(getProtestStatusDetails("Rejected"), {
    status: "Rejected",
    label: "Bác khiếu nại",
    variant: "rejected",
    group: "resolved",
  });
  assert.deepEqual(getProtestStatusDetails("Withdrawn"), {
    status: "Withdrawn",
    label: "Đã rút",
    variant: "inactive",
    group: "resolved",
  });
});

test("protest tabs count and filter open vs terminal statuses", () => {
  const protests = [
    { id: "p1", status: "Pending" },
    { id: "p2", Status: "UnderReview" },
    { id: "p3", status: "Upheld" },
    { id: "p4", status: "Rejected" },
    { id: "p5", status: "Withdrawn" },
  ];

  assert.deepEqual(getProtestTabCounts(protests), {
    pending: 1,
    underReview: 1,
    resolved: 3,
    all: 5,
  });
  assert.deepEqual(filterProtestsByTab(protests, "pending"), [protests[0]]);
  assert.deepEqual(filterProtestsByTab(protests, "underReview"), [protests[1]]);
  assert.deepEqual(filterProtestsByTab(protests, "resolved"), protests.slice(2));
  assert.deepEqual(filterProtestsByTab(protests, "all"), protests);
});

test("default protest tab prefers the active queue", () => {
  assert.equal(getDefaultProtestTab({ pending: 1, underReview: 0, resolved: 0, all: 1 }), "pending");
  assert.equal(getDefaultProtestTab({ pending: 0, underReview: 2, resolved: 0, all: 2 }), "underReview");
  assert.equal(getDefaultProtestTab({ pending: 0, underReview: 0, resolved: 3, all: 3 }), "all");
});

test("buildRuleProtestPayload sends explicit final outcome", () => {
  assert.deepEqual(buildRuleProtestPayload("Upheld", "Chấp nhận vì có bằng chứng"), {
    outcome: "Upheld",
    ruling: "Chấp nhận vì có bằng chứng",
    resolution: "Chấp nhận vì có bằng chứng",
  });
  assert.deepEqual(buildRuleProtestPayload("Rejected", "Không đủ bằng chứng"), {
    outcome: "Rejected",
    ruling: "Không đủ bằng chứng",
    resolution: "Không đủ bằng chứng",
  });
  assert.throws(() => buildRuleProtestPayload("Pending"), /Outcome must be Upheld or Rejected/);
});

test("available action mapping locks terminal protests", () => {
  assert.deepEqual(getAvailableProtestActions("Pending"), ["underReview", "upheld", "rejected"]);
  assert.deepEqual(getAvailableProtestActions("UnderReview"), ["upheld", "rejected"]);
  assert.deepEqual(getAvailableProtestActions("Upheld"), []);
  assert.deepEqual(getAvailableProtestActions("Rejected"), []);
  assert.deepEqual(getAvailableProtestActions("Withdrawn"), []);
});
