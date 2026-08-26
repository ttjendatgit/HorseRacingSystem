import { request } from "./apiClient";

// ── Health Checks (Kiểm Trực Sức Khỏe Ngựa Đua) ──

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy danh sách phiếu kiểm tra sức khỏe ngựa theo mã GUID trận đua.
 * Endpoint: GET /api/referees/race/{raceId}/health-checks
 * @param {string} raceId - Mã GUID trận đua
 */
export function getRaceHealthChecks(raceId) {
  return request(`/api/referees/race/${raceId}/health-checks`);
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy thông tin chi tiết một phiếu kiểm tra sức khỏe ngựa theo ID.
 * Endpoint: GET /api/referees/health-checks/{healthCheckId}
 * @param {string} healthCheckId - Mã GUID phiếu kiểm khám
 */
export function getHealthCheck(healthCheckId) {
  return request(`/api/referees/health-checks/${healthCheckId}`);
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy toàn bộ lịch sử kiểm khám sức khỏe trước đây của con ngựa.
 * Endpoint: GET /api/referees/horse/{horseId}/health-history
 * @param {string} horseId - Mã GUID con ngựa
 */
export function getHorseHealthHistory(horseId) {
  return request(`/api/referees/horse/${horseId}/health-history`);
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Khởi tạo phiếu yêu cầu kiểm tra sức khỏe cho con ngựa thi đấu.
 * Endpoint: POST /api/referees/health-checks
 * @param {Object} payload - { raceId: string, horseId: string, notes: string }
 */
export function createHealthCheck(payload) {
  return request("/api/referees/health-checks", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Hoàn tất quá trình kiểm khám và ghi nhận kết luận sức khỏe ngựa.
 * Endpoint: POST /api/referees/health-checks/{healthCheckId}/complete
 * @param {string} healthCheckId - Mã GUID phiếu kiểm khám
 * @param {Object} payload - Kết quả khám sức khỏe
 */
export function completeHealthCheck(healthCheckId, payload) {
  return request(`/api/referees/health-checks/${healthCheckId}/complete`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Đạt yêu cầu và phê duyệt con ngựa Đủ điều kiện thi đấu trong trận đua.
 * Endpoint: POST /api/referees/health-checks/{healthCheckId}/approve
 * @param {string} healthCheckId - Mã GUID phiếu kiểm khám
 */
export function approveHorseForRace(healthCheckId) {
  return request(`/api/referees/health-checks/${healthCheckId}/approve`, {
    method: "POST",
  });
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Từ chối không cho phép con ngựa tham gia thi đấu do không đạt tiêu chuẩn sức khỏe.
 * Endpoint: POST /api/referees/health-checks/{healthCheckId}/reject
 * @param {string} healthCheckId - Mã GUID phiếu kiểm khám
 * @param {string} reason - Lý do từ chối cụ thể
 */
export function rejectHorseForRace(healthCheckId, reason) {
  return request(`/api/referees/health-checks/${healthCheckId}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });
}

// ── Violations (Ghi Nhận & Xử Lý Vi Phạm Thi Đấu) ──

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy danh sách tất cả các biên bản vi phạm thi đấu trong trận đua.
 * Endpoint: GET /api/referees/race/{raceId}/violations
 * @param {string} raceId - Mã GUID trận đua
 */
export function getRaceViolations(raceId) {
  return request(`/api/referees/race/${raceId}/violations`);
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy chi tiết một biên bản vi phạm thi đấu theo ID.
 * Endpoint: GET /api/referees/violations/{id}
 * @param {string} id - Mã GUID biên bản vi phạm
 */
export function getViolation(id) {
  return request(`/api/referees/violations/${id}`);
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy lịch sử tất cả các lần vi phạm thi đấu trước đây của con ngựa.
 * Endpoint: GET /api/referees/horse/{horseId}/violations
 * @param {string} horseId - Mã GUID con ngựa
 */
export function getHorseViolations(horseId) {
  return request(`/api/referees/horse/${horseId}/violations`);
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Ghi nhận biên bản vi phạm mới của kỵ sĩ / con ngựa trong trận đua.
 * Endpoint: POST /api/referees/violations
 * @param {Object} payload - { raceId, horseId, jockeyId, violationType, description }
 */
export function recordViolation(payload) {
  return request("/api/referees/violations", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

// ── Race Reports (Báo Cáo Băng Sân & Tổng Kết Trận Đua) ──

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy báo cáo tổng hợp kết quả trận đua do Trọng tài lập.
 * Endpoint: GET /api/referees/race/{raceId}/report
 * @param {string} raceId - Mã GUID trận đua
 */
export function getRaceReport(raceId) {
  return request(`/api/referees/race/${raceId}/report`);
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy thông tin chi tiết một báo cáo tổng kết theo ID.
 * Endpoint: GET /api/referees/reports/{id}
 * @param {string} id - Mã GUID báo cáo
 */
export function getReport(id) {
  return request(`/api/referees/reports/${id}`);
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Tạo mới báo cáo tổng kết trận đua sau khi cuộc đua hoàn thành.
 * Endpoint: POST /api/referees/reports
 * @param {Object} payload - Thông tin báo cáo trận đua
 */
export function createReport(payload) {
  return request("/api/referees/reports", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Cập nhật chỉnh sửa nội dung báo cáo tổng kết trận đua.
 * Endpoint: PUT /api/referees/reports/{id}
 * @param {string} id - Mã GUID báo cáo
 * @param {Object} payload - Nội dung cập nhật
 */
export function updateReport(id, payload) {
  return request(`/api/referees/reports/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}

// ── Race Entries (Danh Sách Ngựa & Kỵ Sĩ Tham Gia Trận Đua) ──

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy danh sách lượt đua công khai chính thức của trận đua.
 * Endpoint: GET /api/referees/race/{raceId}/entries
 * @param {string} raceId - Mã GUID trận đua
 */
export function getRaceEntries(raceId) {
  return request(`/api/referees/race/${raceId}/entries`);
}

// ── Gate Assignment (Phân Công Cổng Xuất Phát) ──

/**
 * [ROLE: Trọng Tài / Referee]
 * Phân công số cổng xuất phát (Starting Gate Number) cho con ngựa trước giờ đua.
 * Endpoint: PUT /api/referees/race/{raceId}/entries/{entryId}/gate
 * @param {string} raceId - Mã GUID trận đua
 * @param {string} entryId - Mã GUID lượt đăng ký thi đấu
 * @param {number} gateNumber - Số cổng xuất phát (1, 2, 3...)
 */
export function assignGateNumber(raceId, entryId, gateNumber) {
  return request(`/api/referees/race/${raceId}/entries/${entryId}/gate`, {
    method: "PUT",
    body: JSON.stringify({ gateNumber }),
  });
}

// ── Submit Race Result (Gửi Kết Quả Thi Đấu Chính Thức) ──

/**
 * [ROLE: Trọng Tài / Referee]
 * Gửi bảng kết quả thi đấu thứ hạng chính thức do Trọng tài công nhận.
 * Endpoint: POST /api/referees/race/{raceId}/submit-result
 * @param {string} raceId - Mã GUID trận đua
 * @param {Object} payload - Bảng thứ hạng kết quả trận đua
 */
export function submitRaceResult(raceId, payload) {
  return request(`/api/referees/race/${raceId}/submit-result`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}
