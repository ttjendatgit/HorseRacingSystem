import test from "node:test";
import assert from "node:assert/strict";
import {
  hasHorseMeasurementErrors,
  isPositiveIntegerInput,
  preventInvalidIntegerKey,
  sanitizeDigitsOnly,
  validateHorseMeasurements,
} from "./horseValidation.js";

test("sanitizeDigitsOnly strips letters, signs, decimal points, and exponent notation", () => {
  assert.equal(sanitizeDigitsOnly("48kg0"), "480");
  assert.equal(sanitizeDigitsOnly("-480"), "480");
  assert.equal(sanitizeDigitsOnly("+165"), "165");
  assert.equal(sanitizeDigitsOnly("1e2"), "12");
  assert.equal(sanitizeDigitsOnly("160.5"), "1605");
});

test("positive horse measurement validation returns exact Vietnamese messages", () => {
  assert.deepEqual(validateHorseMeasurements({ weight: "", height: "" }), {
    weight: "Cân nặng phải lớn hơn 0",
    height: "Chiều cao phải lớn hơn 0",
  });
  assert.deepEqual(validateHorseMeasurements({ weight: "0", height: "0" }), {
    weight: "Cân nặng phải lớn hơn 0",
    height: "Chiều cao phải lớn hơn 0",
  });
  assert.deepEqual(validateHorseMeasurements({ weight: "480", height: "165" }), {});
});

test("isPositiveIntegerInput accepts digits greater than zero only", () => {
  assert.equal(isPositiveIntegerInput("1"), true);
  assert.equal(isPositiveIntegerInput("001"), true);
  assert.equal(isPositiveIntegerInput("0"), false);
  assert.equal(isPositiveIntegerInput("1e2"), false);
  assert.equal(isPositiveIntegerInput("-1"), false);
  assert.equal(isPositiveIntegerInput("12.5"), false);
});

test("hasHorseMeasurementErrors detects weight or height errors", () => {
  assert.equal(hasHorseMeasurementErrors({}), false);
  assert.equal(hasHorseMeasurementErrors({ weight: "Cân nặng phải lớn hơn 0" }), true);
  assert.equal(hasHorseMeasurementErrors({ height: "Chiều cao phải lớn hơn 0" }), true);
});

test("preventInvalidIntegerKey blocks e, signs, decimals, and letters", () => {
  for (const key of ["e", "E", "-", "+", ".", "a"]) {
    let prevented = false;
    preventInvalidIntegerKey({
      key,
      ctrlKey: false,
      metaKey: false,
      preventDefault: () => {
        prevented = true;
      },
    });
    assert.equal(prevented, true, `${key} should be blocked`);
  }
});

test("preventInvalidIntegerKey allows digits, navigation keys, and shortcuts", () => {
  for (const key of ["0", "9", "Backspace", "ArrowLeft", "Tab"]) {
    let prevented = false;
    preventInvalidIntegerKey({
      key,
      ctrlKey: false,
      metaKey: false,
      preventDefault: () => {
        prevented = true;
      },
    });
    assert.equal(prevented, false, `${key} should be allowed`);
  }

  let shortcutPrevented = false;
  preventInvalidIntegerKey({
    key: "v",
    ctrlKey: true,
    metaKey: false,
    preventDefault: () => {
      shortcutPrevented = true;
    },
  });
  assert.equal(shortcutPrevented, false);
});
