import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  isValidJockeyPhone,
  isValidJockeyIdCardNumber,
  isJockeyOlderThan18,
  validateJockeyRegistration,
} from "./jockeyRegistrationValidation.js";

describe("isValidJockeyPhone", () => {
  test("digits-only passes", () => {
    assert.equal(isValidJockeyPhone("0353545355"), true);
  });

  test("letters fail", () => {
    assert.equal(isValidJockeyPhone("abc123"), false);
    assert.equal(isValidJockeyPhone("èeertt"), false);
  });

  test("punctuation fails", () => {
    assert.equal(isValidJockeyPhone("035-354-5355"), false);
    assert.equal(isValidJockeyPhone("+84353545355"), false);
    assert.equal(isValidJockeyPhone("(035)3545355"), false);
  });

  test("whitespace fails", () => {
    assert.equal(isValidJockeyPhone("035 354 5355"), false);
    assert.equal(isValidJockeyPhone(" 0353545355"), false);
    assert.equal(isValidJockeyPhone("0353545355 "), false);
  });

  test("invalid input is never silently sanitized/transformed", () => {
    // "abc123" must be rejected outright, never coerced down to "123".
    assert.equal(isValidJockeyPhone("abc123"), false);
  });

  test("required: null/undefined/empty/whitespace-only all rejected", () => {
    assert.equal(isValidJockeyPhone(""), false);
    assert.equal(isValidJockeyPhone(null), false);
    assert.equal(isValidJockeyPhone(undefined), false);
    assert.equal(isValidJockeyPhone("   "), false);
    assert.equal(isValidJockeyPhone("\t\n"), false);
  });

  test("Unicode-numeric digits are rejected (ASCII 0-9 only)", () => {
    assert.equal(isValidJockeyPhone("０３５３５４５３５５"), false); // fullwidth digits
    assert.equal(isValidJockeyPhone("٠٣٥٣٥٤٥٣٥٥"), false); // Arabic-Indic digits
  });
});

describe("isValidJockeyIdCardNumber", () => {
  test("9-digit CMND passes", () => {
    assert.equal(isValidJockeyIdCardNumber("123456789"), true);
  });

  test("12-digit CCCD passes, leading zero preserved", () => {
    assert.equal(isValidJockeyIdCardNumber("012345678901"), true);
  });

  test("wrong lengths fail", () => {
    assert.equal(isValidJockeyIdCardNumber("12345678"), false); // 8
    assert.equal(isValidJockeyIdCardNumber("1234567890"), false); // 10
    assert.equal(isValidJockeyIdCardNumber("12345678901"), false); // 11
    assert.equal(isValidJockeyIdCardNumber("1234567890123"), false); // 13
  });

  test("letters fail", () => {
    assert.equal(isValidJockeyIdCardNumber("ABC123456"), false);
  });

  test("punctuation/whitespace fail", () => {
    assert.equal(isValidJockeyIdCardNumber("123-456-789"), false);
    assert.equal(isValidJockeyIdCardNumber("123 456 789"), false);
    assert.equal(isValidJockeyIdCardNumber("!@#$%"), false);
  });

  test("treated as string — never numeric-parsed (no leading-zero loss)", () => {
    const withLeadingZero = "012345678901";
    assert.equal(isValidJockeyIdCardNumber(withLeadingZero), true);
    // Sanity: Number() would have silently dropped the leading zero — assert the helper never does that.
    assert.notEqual(String(Number(withLeadingZero)), withLeadingZero);
  });

  test("required: null/undefined/empty/whitespace-only all rejected", () => {
    assert.equal(isValidJockeyIdCardNumber(""), false);
    assert.equal(isValidJockeyIdCardNumber(null), false);
    assert.equal(isValidJockeyIdCardNumber(undefined), false);
    assert.equal(isValidJockeyIdCardNumber("   "), false);
    assert.equal(isValidJockeyIdCardNumber("\t\n"), false);
  });

  test("Unicode-numeric digits are rejected (ASCII 0-9 only)", () => {
    assert.equal(isValidJockeyIdCardNumber("０１２３４５６７８"), false); // fullwidth, 9 chars
    assert.equal(isValidJockeyIdCardNumber("٠١٢٣٤٥٦٧٨"), false); // Arabic-Indic, 9 chars
  });
});

describe("isJockeyOlderThan18 (reference date 2026-08-23)", () => {
  const REF = new Date("2026-08-23T00:00:00Z");

  test("one day older than 18 passes", () => {
    assert.equal(isJockeyOlderThan18("2008-08-22", REF), true);
  });

  test("exactly 18 fails (strictly older required)", () => {
    assert.equal(isJockeyOlderThan18("2008-08-23", REF), false);
  });

  test("one day under 18 fails", () => {
    assert.equal(isJockeyOlderThan18("2008-08-24", REF), false);
  });

  test("future date of birth fails", () => {
    assert.equal(isJockeyOlderThan18("2027-01-01", REF), false);
  });

  test("leap-year DOB (2000-02-29) is handled correctly", () => {
    assert.equal(isJockeyOlderThan18("2000-02-29", REF), true);
  });

  test("invalid/null/undefined DOB fails safely, does not throw", () => {
    assert.equal(isJockeyOlderThan18(null, REF), false);
    assert.equal(isJockeyOlderThan18(undefined, REF), false);
    assert.equal(isJockeyOlderThan18("", REF), false);
    assert.equal(isJockeyOlderThan18("not-a-date", REF), false);
  });

  test("accepts Date objects as well as strings for both arguments", () => {
    assert.equal(isJockeyOlderThan18(new Date("2008-08-22T00:00:00Z"), REF), true);
    assert.equal(isJockeyOlderThan18("2008-08-22", "2026-08-23T00:00:00Z"), true);
  });
});

describe("isJockeyOlderThan18 — leap-day REFERENCE date (regression: AddYears(-18)-on-reference bug)", () => {
  // Counter-example proving that shifting the REFERENCE date back 18 years (and clamping) is
  // WRONG, not just an equivalent restatement of "18th birthday from DOB". DOB 2010-02-28 turns
  // exactly 18 on 2028-02-28 (no ambiguity — Feb 28 exists every year). On 2028-02-29 (one day
  // later) they are 18 years + 1 day old and MUST pass. The old (buggy) formula computed
  // referenceDate(2028-02-29).AddYears(-18) = clamp(2010-02-29) = 2010-02-28, then compared
  // "DOB < that", which collided with this exact DOB and wrongly rejected it. The correct formula
  // computes the 18th birthday from the DOB (2010-02-28 + 18y = 2028-02-28, no clamp needed) and
  // compares referenceDate > that fixed calendar date instead.
  test("reference = 2028-02-29, DOB = 2010-02-28 => PASS (genuinely 18 years + 1 day old)", () => {
    assert.equal(isJockeyOlderThan18("2010-02-28", new Date("2028-02-29T00:00:00Z")), true);
  });

  test("reference = 2028-02-28, DOB = 2010-02-28 => FAIL (turns exactly 18 that day)", () => {
    assert.equal(isJockeyOlderThan18("2010-02-28", new Date("2028-02-28T00:00:00Z")), false);
  });
});

describe("isJockeyOlderThan18 — leap-day DOB (2008-02-29)", () => {
  // 2008-02-29 + 18 years lands in 2026, not a leap year, so the 18th birthday clamps to
  // 2026-02-28 — mirrors .NET's DateTime.AddYears(18) on a Feb-29 source date.
  test("reference = the computed 18th birthday (2026-02-28) => FAIL", () => {
    assert.equal(isJockeyOlderThan18("2008-02-29", new Date("2026-02-28T00:00:00Z")), false);
  });

  test("reference = the following calendar day (2026-03-01) => PASS", () => {
    assert.equal(isJockeyOlderThan18("2008-02-29", new Date("2026-03-01T00:00:00Z")), true);
  });
});

describe("validateJockeyRegistration", () => {
  const REF = new Date("2026-08-23T00:00:00Z");

  test("fully valid input returns no errors", () => {
    const errors = validateJockeyRegistration(
      { phone: "0353545355", idCardNumber: "123456789", dateOfBirth: "2008-08-22" },
      REF,
    );
    assert.deepEqual(errors, {});
  });

  test("invalid phone is reported with the exact expected message", () => {
    const errors = validateJockeyRegistration(
      { phone: "abcxyz", idCardNumber: "123456789", dateOfBirth: "2008-08-22" },
      REF,
    );
    assert.equal(errors.phone, "Số điện thoại chỉ được chứa chữ số.");
    assert.equal(errors.idCardNumber, undefined);
    assert.equal(errors.age, undefined);
  });

  test("missing phone/idCardNumber are reported, not silently accepted", () => {
    const errors = validateJockeyRegistration(
      { phone: "", idCardNumber: "", dateOfBirth: "2008-08-22" },
      REF,
    );
    assert.equal(errors.phone, "Số điện thoại chỉ được chứa chữ số.");
    assert.equal(errors.idCardNumber, "CCCD/CMND phải gồm 9 hoặc 12 chữ số.");
  });

  test("invalid idCardNumber is reported with the exact expected message", () => {
    const errors = validateJockeyRegistration(
      { phone: "0353545355", idCardNumber: "abc123", dateOfBirth: "2008-08-22" },
      REF,
    );
    assert.equal(errors.idCardNumber, "CCCD/CMND phải gồm 9 hoặc 12 chữ số.");
  });

  test("exactly-18 DOB is reported with the exact expected age message", () => {
    const errors = validateJockeyRegistration(
      { phone: "0353545355", idCardNumber: "123456789", dateOfBirth: "2008-08-23" },
      REF,
    );
    assert.equal(errors.age, "Kỵ sĩ phải trên 18 tuổi.");
  });

  test("multiple simultaneous errors are all reported at once", () => {
    const errors = validateJockeyRegistration(
      { phone: "abcxyz", idCardNumber: "abc123", dateOfBirth: "2020-01-01" },
      REF,
    );
    assert.equal(Object.keys(errors).length, 3);
  });
});
