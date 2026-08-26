// J-REG-VALIDATION: pure Jockey registration identity/age validators. Mirrors
// BE/Services/JockeyIdentityValidator.cs exactly (same two ASCII-only patterns, same age formula
// and leap-day clamp) so FE and backend never silently drift apart. This is a UX convenience
// only — the backend (AuthService.RegisterAsync) independently enforces the same rules and is
// authoritative; a direct API call bypassing this file is still rejected server-side.

// Explicit [0-9] character classes only — never \d. JS's \d IS ASCII-only by default (unlike
// .NET's \d, which matches every Unicode category-Nd digit unless RegexOptions.ECMAScript is
// set), but [0-9] is used here anyway to keep the two implementations textually identical and
// leave no doubt.
const PHONE_PATTERN = /^[0-9]+$/;
const ID_CARD_NUMBER_PATTERN = /^([0-9]{9}|[0-9]{12})$/;

// Both fields are REQUIRED — null/undefined/empty/whitespace-only all rejected, matching
// AuthService.RegisterAsync's string.IsNullOrWhiteSpace(...) gate.
export function isValidJockeyPhone(phone) {
  if (typeof phone !== "string" || phone.trim() === "") return false;
  return PHONE_PATTERN.test(phone);
}

export function isValidJockeyIdCardNumber(idCardNumber) {
  if (typeof idCardNumber !== "string" || idCardNumber.trim() === "") return false;
  return ID_CARD_NUMBER_PATTERN.test(idCardNumber);
}

function isLeapYear(year) {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

function toUtcYmd(value) {
  if (value === null || value === undefined || value === "") return null;
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  // UTC getters deliberately used (not local getters) so a "YYYY-MM-DD" input — parsed by the
  // Date constructor as UTC midnight — is never shifted a day by the browser's local timezone.
  return { year: date.getUTCFullYear(), month: date.getUTCMonth(), day: date.getUTCDate() };
}

// Mirrors .NET's DateTime.AddYears(18) leap-day clamp exactly: adding 18 years to a Feb-29 date
// lands on Feb 28 whenever the resulting year isn't itself a leap year, rather than letting JS's
// Date.UTC silently overflow Feb 29 (non-leap year) forward into March 1.
function addYearsClampedUtc({ year, month, day }, yearsToAdd) {
  const targetYear = year + yearsToAdd;
  if (month === 1 && day === 29 && !isLeapYear(targetYear)) {
    return Date.UTC(targetYear, 1, 28);
  }
  return Date.UTC(targetYear, month, day);
}

// Age > 18, strictly: find the actual 18th-birthday DATE from the DOB
// (dateOfBirth.AddYears(18)), then require referenceDate > eighteenthBirthday (never >=). Never
// TotalDays/365 (wrong around leap years).
//
// IMPORTANT: the 18-year shift is applied to the DOB, never to the reference date. Shifting the
// REFERENCE date back 18 years instead looks equivalent but is not: on a leap-day reference date
// (e.g. 2028-02-29), that lands in a non-leap year (2010) and clamps to 2010-02-28 — colliding
// with a DOB of exactly 2010-02-28, even though that person is genuinely 18 years + 1 day old on
// 2028-02-29 and must pass. Computing the 18th birthday from the DOB instead (2010-02-28 + 18
// years = 2028-02-28, no clamp needed since Feb 28 always exists) avoids that false rejection.
// Missing/unparseable input fails safely (false), never throws. referenceDate must be passed
// explicitly by the caller — never reads Date.now() itself — so this stays pure and testable.
export function isJockeyOlderThan18(dateOfBirth, referenceDate) {
  const dobYmd = toUtcYmd(dateOfBirth);
  if (dobYmd === null) return false;

  const refYmd = toUtcYmd(referenceDate);
  if (refYmd === null) return false;

  const eighteenthBirthdayUtc = addYearsClampedUtc(dobYmd, 18);
  const refUtc = Date.UTC(refYmd.year, refYmd.month, refYmd.day);

  return refUtc > eighteenthBirthdayUtc;
}

export const JOCKEY_REGISTRATION_MESSAGES = {
  phone: "Số điện thoại chỉ được chứa chữ số.",
  idCardNumber: "CCCD/CMND phải gồm 9 hoặc 12 chữ số.",
  age: "Kỵ sĩ phải trên 18 tuổi.",
};

// Validates the Jockey-only registration fields as a set. Returns a field -> message map
// containing only the fields that failed (an empty object means fully valid).
export function validateJockeyRegistration({ phone, idCardNumber, dateOfBirth }, referenceDate = new Date()) {
  const errors = {};

  if (!isValidJockeyPhone(phone)) {
    errors.phone = JOCKEY_REGISTRATION_MESSAGES.phone;
  }
  if (!isValidJockeyIdCardNumber(idCardNumber)) {
    errors.idCardNumber = JOCKEY_REGISTRATION_MESSAGES.idCardNumber;
  }
  if (!isJockeyOlderThan18(dateOfBirth, referenceDate)) {
    errors.age = JOCKEY_REGISTRATION_MESSAGES.age;
  }

  return errors;
}
