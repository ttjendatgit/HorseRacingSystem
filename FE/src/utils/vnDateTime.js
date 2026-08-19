// Single Vietnam-timezone (Asia/Ho_Chi_Minh, fixed UTC+07:00, no DST) datetime policy for the
// Admin Tournament/Round/Race screens.
//
// Backend contract (see BE/Program.cs "Npgsql.EnableLegacyTimestampBehavior"): every
// Tournament/Round/Race DateTime is persisted as PostgreSQL "timestamp without time zone" and
// read back with Kind=Unspecified, so System.Text.Json serializes it WITHOUT a trailing "Z"/offset
// even though the value represents a UTC instant by convention. A naive API string must therefore
// be parsed as UTC here, never as the browser's local time — new Date("...") on a string with no
// zone suffix is parsed as LOCAL time per the JS spec, which silently breaks for any Admin whose
// machine isn't set to UTC+7.

const VN_OFFSET_MS = 7 * 60 * 60 * 1000; // Asia/Ho_Chi_Minh: fixed UTC+7, Vietnam observes no DST.
const HAS_ZONE = /[zZ]$|[+-]\d{2}:\d{2}$/;

function pad(n) {
  return String(n).padStart(2, "0");
}

// API datetime string -> real UTC Date instant. Respects an explicit Z/offset if present;
// otherwise treats the naive string as UTC (matches this backend's actual serialized behavior).
// Exported so callers can compare two API instants directly (e.g. client-side containment
// validation) without re-deriving this parsing rule themselves.
export function apiToUtcDate(value) {
  if (!value) return null;
  const iso = HAS_ZONE.test(value) ? value : `${value}Z`;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? null : d;
}
const parseApiUtc = apiToUtcDate;

// API datetime string -> <input type="datetime-local"> value showing Vietnam wall-clock time.
export function apiToVNInput(value) {
  const utc = parseApiUtc(value);
  if (!utc) return "";
  const vn = new Date(utc.getTime() + VN_OFFSET_MS);
  return `${vn.getUTCFullYear()}-${pad(vn.getUTCMonth() + 1)}-${pad(vn.getUTCDate())}T${pad(vn.getUTCHours())}:${pad(vn.getUTCMinutes())}`;
}

// API datetime string -> Vietnamese display "dd/MM/yyyy HH:mm".
export function apiToVNDisplay(value) {
  const utc = parseApiUtc(value);
  if (!utc) return "";
  const vn = new Date(utc.getTime() + VN_OFFSET_MS);
  return `${pad(vn.getUTCDate())}/${pad(vn.getUTCMonth() + 1)}/${vn.getUTCFullYear()} ${pad(vn.getUTCHours())}:${pad(vn.getUTCMinutes())}`;
}

// API datetime string -> Vietnamese date-only display "dd/MM/yyyy" (for summary/report lists).
export function apiToVNDate(value) {
  const full = apiToVNDisplay(value);
  return full ? full.slice(0, 10) : "";
}

// <input type="datetime-local"> value (interpreted as Vietnam wall-clock time) -> UTC ISO string
// (with trailing Z) ready to send to the API. Never round-trips through the browser's own zone.
export function vnInputToApiUtc(value) {
  if (!value) return null;
  const [datePart, timePart] = value.split("T");
  if (!datePart || !timePart) return null;
  const [year, month, day] = datePart.split("-").map(Number);
  const [hour, minute] = timePart.split(":").map(Number);
  if ([year, month, day, hour, minute].some((n) => Number.isNaN(n))) return null;
  const utcMs = Date.UTC(year, month - 1, day, hour, minute) - VN_OFFSET_MS;
  return new Date(utcMs).toISOString();
}

// Current Vietnam wall-clock time (+ optional day offset) as a <input type="datetime-local">
// value — for form defaults ("N days from now" in Vietnam time), never the browser's own timezone.
export function vnNowInput(daysOffset = 0) {
  const nowUtcMs = Date.now() + daysOffset * 24 * 60 * 60 * 1000;
  const vn = new Date(nowUtcMs + VN_OFFSET_MS);
  return `${vn.getUTCFullYear()}-${pad(vn.getUTCMonth() + 1)}-${pad(vn.getUTCDate())}T${pad(vn.getUTCHours())}:${pad(vn.getUTCMinutes())}`;
}
