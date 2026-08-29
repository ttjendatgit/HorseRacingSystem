import { request } from "./apiClient";

/**
 * [ROLE: Người Dùng / All Roles]
 * Truy vấn số dư điểm khả dụng hiện tại trong ví của người dùng đang đăng nhập.
 * Endpoint: GET /api/wallet/balance
 * @returns {Promise<Object>} Thông tin số dư ví ({ balance })
 */
export function getBalance() {
  return request("/api/wallet/balance");
}

/**
 * [ROLE: Chủ ngựa]
 * Truy vấn lịch sử nhận thưởng (các lần trao thưởng đã thực sự thành công) của Chủ ngựa đang đăng nhập.
 * Endpoint: GET /api/wallet/my-prize-history
 * @returns {Promise<Object>} Danh sách lịch sử nhận thưởng, mới nhất trước.
 */
export function getMyPrizeHistory() {
  return request("/api/wallet/my-prize-history");
}
