import { request } from "./apiClient";

// ── Health Checks ──

/** Lấy danh sách kiểm tra sức khỏe ngựa theo cuộc đua */
export function getRaceHealthChecks(raceId) {
  return request(`/api/referees/race/${raceId}/health-checks`);
}

/** Lấy chi tiết một phiếu kiểm tra sức khỏe */
export function getHealthCheck(healthCheckId) {
  return request(`/api/referees/health-checks/${healthCheckId}`);
}

/** Lấy lịch sử kiểm tra sức khỏe của ngựa */
export function getHorseHealthHistory(horseId) {
  return request(`/api/referees/horse/${horseId}/health-history`);
}

/** Tạo mới yêu cầu/phiếu kiểm tra sức khỏe ngựa */
export function createHealthCheck(payload) {
  return request("/api/referees/health-checks", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/** Hoàn tất việc kiểm tra sức khỏe và lưu kết luận */
export function completeHealthCheck(healthCheckId, payload) {
  return request(`/api/referees/health-checks/${healthCheckId}/complete`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/** Phê duyệt ngựa đủ điều kiện tham gia cuộc đua */
export function approveHorseForRace(healthCheckId) {
  return request(`/api/referees/health-checks/${healthCheckId}/approve`, {
    method: "POST",
  });
}

/** Từ chối ngựa tham gia cuộc đua do không đủ sức khỏe */
export function rejectHorseForRace(healthCheckId, reason) {
  return request(`/api/referees/health-checks/${healthCheckId}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });
}

// ── Violations ──

/** Lấy danh sách các vi phạm trong cuộc đua */
export function getRaceViolations(raceId) {
  return request(`/api/referees/race/${raceId}/violations`);
}

/** Lấy chi tiết một vi phạm theo ID */
export function getViolation(id) {
  return request(`/api/referees/violations/${id}`);
}

/** Lấy lịch sử vi phạm của một con ngựa */
export function getHorseViolations(horseId) {
  return request(`/api/referees/horse/${horseId}/violations`);
}

/** Ghi nhận một vi phạm mới của nài ngựa/ngựa */
export function recordViolation(payload) {
  return request("/api/referees/violations", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

// ── Race Reports ──

/** Lấy báo cáo tổng hợp cuộc đua của trọng tài */
export function getRaceReport(raceId) {
  return request(`/api/referees/race/${raceId}/report`);
}

/** Lấy thông tin chi tiết một báo cáo theo ID */
export function getReport(id) {
  return request(`/api/referees/reports/${id}`);
}

/** Tạo báo cáo tổng kết cuộc đua */
export function createReport(payload) {
  return request("/api/referees/reports", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/** Cập nhật báo cáo tổng kết cuộc đua */
export function updateReport(id, payload) {
  return request(`/api/referees/reports/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}

// ── Race Entries (for dropdowns) ──

/** Lấy danh sách lượt tham gia cuộc đua */
export function getRaceEntries(raceId) {
  return request(`/api/referees/race/${raceId}/entries`);
}

// ── Gate Assignment (GATE-V1) ──

/** Phân công số cổng xuất phát (Gate Number) cho ngựa */
export function assignGateNumber(raceId, entryId, gateNumber) {
  return request(`/api/referees/race/${raceId}/entries/${entryId}/gate`, {
    method: "PUT",
    body: JSON.stringify({ gateNumber }),
  });
}

// ── Submit Race Result ──

/** Gửi kết quả cuộc đua chính thức từ trọng tài */
export function submitRaceResult(raceId, payload) {
  return request(`/api/referees/race/${raceId}/submit-result`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}
