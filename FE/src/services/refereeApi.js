import { request } from "./apiClient";

// ── Health Checks ──

export function getRaceHealthChecks(raceId) {
  return request(`/api/referees/race/${raceId}/health-checks`);
}

export function getHealthCheck(healthCheckId) {
  return request(`/api/referees/health-checks/${healthCheckId}`);
}

export function getHorseHealthHistory(horseId) {
  return request(`/api/referees/horse/${horseId}/health-history`);
}

export function createHealthCheck(payload) {
  return request("/api/referees/health-checks", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function completeHealthCheck(healthCheckId, payload) {
  return request(`/api/referees/health-checks/${healthCheckId}/complete`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function approveHorseForRace(healthCheckId) {
  return request(`/api/referees/health-checks/${healthCheckId}/approve`, {
    method: "POST",
  });
}

export function rejectHorseForRace(healthCheckId, reason) {
  return request(`/api/referees/health-checks/${healthCheckId}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });
}

// ── Violations ──

export function getRaceViolations(raceId) {
  return request(`/api/referees/race/${raceId}/violations`);
}

export function getViolation(id) {
  return request(`/api/referees/violations/${id}`);
}

export function getHorseViolations(horseId) {
  return request(`/api/referees/horse/${horseId}/violations`);
}

export function recordViolation(payload) {
  return request("/api/referees/violations", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

// ── Race Reports ──

export function getRaceReport(raceId) {
  return request(`/api/referees/race/${raceId}/report`);
}

export function getReport(id) {
  return request(`/api/referees/reports/${id}`);
}

export function createReport(payload) {
  return request("/api/referees/reports", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function updateReport(id, payload) {
  return request(`/api/referees/reports/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}

// ── Race Entries (for dropdowns) ──

export function getRaceEntries(raceId) {
  return request(`/api/referees/race/${raceId}/entries`);
}

// ── Gate Assignment (GATE-V1) ──

export function assignGateNumber(raceId, entryId, gateNumber) {
  return request(`/api/referees/race/${raceId}/entries/${entryId}/gate`, {
    method: "PUT",
    body: JSON.stringify({ gateNumber }),
  });
}

// ── Submit Race Result ──

export function submitRaceResult(raceId, payload) {
  return request(`/api/referees/race/${raceId}/submit-result`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}
