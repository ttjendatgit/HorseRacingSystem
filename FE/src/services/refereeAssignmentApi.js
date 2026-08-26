import { request } from "./apiClient";

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy danh sách phân công nhiệm vụ trận đua dành cho trọng tài đang đăng nhập.
 * @param {string} [status] - Bộ lọc trạng thái tùy chọn: "Assigned", "Accepted", "Rejected"
 * @returns {Promise<Array>} Danh sách nhiệm vụ phân công
 */
export function getMyAssignments(status) {
  const qs = status ? `?status=${encodeURIComponent(status)}` : "";
  return request(`/api/referees/my-assignments${qs}`).then(
    (d) => d?.data ?? d?.Data ?? d
  );
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy danh sách nhiệm vụ phân công trận đua đang chờ trọng tài phản hồi nhận/từ chối.
 * @returns {Promise<Array>} Danh sách nhiệm vụ đang chờ
 */
export function getPendingRefereeAssignments() {
  return getMyAssignments("Assigned");
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Lấy toàn bộ lịch sử phân công nhiệm vụ trận đua (Đã nhận, Đã từ chối, Đang chờ).
 * @returns {Promise<Array>} Toàn bộ danh sách phân công
 */
export function getAllRefereeAssignments() {
  return getMyAssignments();
}

/**
 * [ROLE: Trọng Tài / Referee]
 * Phản hồi Đồng ý (Accept) hoặc Từ chối (Reject) nhiệm vụ trọng tài trận đua.
 * @param {string} assignmentId - Mã GUID đơn phân công trọng tài
 * @param {string} response - Trạng thái "Accept" hoặc "Reject"
 * @returns {Promise<Object>} Kết quả xử lý từ API
 */
export function respondToRefereeAssignment(assignmentId, response) {
  return request(`/api/referees/assignments/${assignmentId}/respond`, {
    method: "POST",
    body: JSON.stringify({
      response,
      notes: `Referee ${response.toLowerCase()}ed the assignment.`,
    }),
  });
}
