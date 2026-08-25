// ADMIN-TOURNAMENTS-REGRESSION-FIX #3/#7: RaceStatusBadge (rm-status) is display:inline-flex,
// so it only stretches full-width when a flex-column ancestor's default `align-items: stretch`
// forces it to. No DOM renderer exists in this project (see other *.test.js files — all pure
// node:test, no jsdom/RTL) — reading the actual CSS source is the smallest honest way to pin
// the specific regression (and its fix) without inventing rendering infrastructure for one rule.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import test from "node:test";

const cssPath = path.join(path.dirname(fileURLToPath(import.meta.url)), "AdminPage.css");
const css = readFileSync(cssPath, "utf8");

function ruleBodyFor(selector) {
  const marker = `${selector} {`;
  const start = css.indexOf(marker);
  assert.notEqual(start, -1, `expected to find a "${selector}" rule in AdminPage.css`);
  const end = css.indexOf("}", start);
  return css.slice(start + marker.length, end);
}

test("tm-card__header does not stretch its children full-width (the status-badge-as-progress-bar bug)", () => {
  const body = ruleBodyFor(".tm-card__header");
  assert.match(body, /align-items:\s*flex-start/, "must explicitly override flex column's default stretch");
  assert.doesNotMatch(body, /align-items:\s*stretch/);
});

test("tm-card__media has a fixed height so cards stay a stable height with or without a real image", () => {
  const body = ruleBodyFor(".tm-card__media");
  assert.match(body, /height:\s*\d/);
});

test("tm-card__media img uses object-fit: cover, never stretching/distorting the thumbnail", () => {
  const body = ruleBodyFor(".tm-card__media img");
  assert.match(body, /object-fit:\s*cover/);
});
