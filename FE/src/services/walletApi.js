import { request } from "./apiClient";

/**
 * Lấy số dư ví hiện tại của người dùng đang đăng nhập
 * @returns {Promise<Object>} Thông tin số dư ví ({ balance })
 */
export function getBalance() {
  return request("/api/wallet/balance");
}
