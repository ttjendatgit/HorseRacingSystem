import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { getFinishPosition, getPlacementLabel, getRankedEntries } from "./raceResultDisplay.js";

describe("getFinishPosition", () => {
  test("reads camelCase position", () => {
    assert.equal(getFinishPosition({ position: 2 }), 2);
  });

  test("reads PascalCase Position fallback", () => {
    assert.equal(getFinishPosition({ Position: 3 }), 3);
  });

  test("also reads finishPosition/FinishPosition (legacy RaceEntry shape)", () => {
    assert.equal(getFinishPosition({ finishPosition: 1 }), 1);
    assert.equal(getFinishPosition({ FinishPosition: 4 }), 4);
  });

  test("null/undefined/empty => null", () => {
    assert.equal(getFinishPosition({ position: null }), null);
    assert.equal(getFinishPosition({ position: undefined }), null);
    assert.equal(getFinishPosition({ position: "" }), null);
    assert.equal(getFinishPosition({}), null);
  });

  test("only positive integers are valid", () => {
    assert.equal(getFinishPosition({ position: 0 }), null);
    assert.equal(getFinishPosition({ position: -1 }), null);
    assert.equal(getFinishPosition({ position: 1.5 }), null);
  });

  test("numeric strings are accepted (raw API/form state)", () => {
    assert.equal(getFinishPosition({ position: "2" }), 2);
  });
});

describe("getRankedEntries", () => {
  test("sorts 3,1,2 -> 1,2,3", () => {
    const entries = [{ position: 3, horseId: "c" }, { position: 1, horseId: "a" }, { position: 2, horseId: "b" }];
    const ranked = getRankedEntries(entries);
    assert.deepEqual(ranked.map((e) => e.horseId), ["a", "b", "c"]);
  });

  test("entries with missing/invalid FinishPosition are excluded", () => {
    const entries = [
      { position: 2, horseId: "b" },
      { position: null, horseId: "x" },
      { horseId: "y" },
      { position: 1, horseId: "a" },
    ];
    const ranked = getRankedEntries(entries);
    assert.deepEqual(ranked.map((e) => e.horseId), ["a", "b"]);
  });

  test("does not mutate the input array", () => {
    const entries = [{ position: 2 }, { position: 1 }];
    const original = [...entries];
    getRankedEntries(entries);
    assert.deepEqual(entries, original);
  });

  test("non-array input returns empty array", () => {
    assert.deepEqual(getRankedEntries(null), []);
    assert.deepEqual(getRankedEntries(undefined), []);
  });
});

describe("getPlacementLabel", () => {
  test("non-final: position <= slots => Đi tiếp", () => {
    assert.equal(getPlacementLabel({ position: 1, isFinal: false, qualificationSlots: 3 }), "Đi tiếp");
    assert.equal(getPlacementLabel({ position: 3, isFinal: false, qualificationSlots: 3 }), "Đi tiếp");
  });

  test("non-final: position > slots => Bị loại", () => {
    assert.equal(getPlacementLabel({ position: 4, isFinal: false, qualificationSlots: 3 }), "Bị loại");
  });

  test("final: position 1 => Vô địch", () => {
    assert.equal(getPlacementLabel({ position: 1, isFinal: true, qualificationSlots: 0 }), "Vô địch");
  });

  test("final: position 2 => Hạng 2", () => {
    assert.equal(getPlacementLabel({ position: 2, isFinal: true, qualificationSlots: 0 }), "Hạng 2");
  });

  test("final never returns Đi tiếp/Bị loại regardless of qualificationSlots", () => {
    assert.equal(getPlacementLabel({ position: 4, isFinal: true, qualificationSlots: 3 }), "Hạng 4");
  });

  test("invalid/missing qualificationSlots on a non-final race does not falsely mark qualified/eliminated", () => {
    assert.equal(getPlacementLabel({ position: 1, isFinal: false, qualificationSlots: null }), "Hạng 1");
    assert.equal(getPlacementLabel({ position: 1, isFinal: false, qualificationSlots: undefined }), "Hạng 1");
    assert.equal(getPlacementLabel({ position: 1, isFinal: false, qualificationSlots: "not-a-number" }), "Hạng 1");
    assert.equal(getPlacementLabel({ position: 1, isFinal: false, qualificationSlots: -1 }), "Hạng 1");
  });

  test("missing position returns empty string", () => {
    assert.equal(getPlacementLabel({ isFinal: false, qualificationSlots: 3 }), "");
  });
});
