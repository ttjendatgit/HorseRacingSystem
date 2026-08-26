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
