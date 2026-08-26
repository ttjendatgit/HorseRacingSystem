import { request } from "./apiClient";

/**
 * [ROLE: Người Dùng / User]
 * Lưu mới hoặc cập nhật thông tin tài khoản ngân hàng nhận tiền rút.
 * Endpoint: POST /api/withdrawal/bank-account
 * @param {Object} payload - { bankName: string, accountNumber: string, accountHolder: string }
 */
export function saveBankAccount(payload) {
  return request("/api/withdrawal/bank-account", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * [ROLE: Người Dùng / User]
 * Lấy danh sách các tài khoản ngân hàng thụ hưởng đã liên kết của người dùng.
 * Endpoint: GET /api/withdrawal/bank-accounts
 */
export function getBankAccounts() {
  return request("/api/withdrawal/bank-accounts");
}

/**
 * [ROLE: Người Dùng / User]
 * Tạo yêu cầu rút tiền từ số dư ví về tài khoản ngân hàng thụ hưởng.
 * Endpoint: POST /api/withdrawal/create
 * @param {Object} payload - { bankAccountId: string, amount: number }
 */
export function createWithdrawal(payload) {
  return request("/api/withdrawal/create", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * [ROLE: Người Dùng / User]
 * Lấy lịch sử danh sách tất cả các đơn yêu cầu rút tiền của người dùng.
 * Endpoint: GET /api/withdrawal/history
 */
export function getWithdrawalHistory() {
  return request("/api/withdrawal/history");
}
