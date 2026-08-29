import { request } from "./apiClient";

/**
 * [ROLE: All Roles]
 * Gửi yêu cầu đăng nhập tài khoản hệ thống (Nhận JWT Token và thông tin vai trò).
 * Endpoint: POST /api/auth/login
 * @param {Object} payload - { email, password }
 */
export function login(payload) {
  return request("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * [ROLE: Khán Giả / Chủ Ngựa / Kỵ Sĩ]
 * Gửi yêu cầu đăng ký tài khoản mới vào hệ thống.
 * Endpoint: POST /api/auth/register
 * @param {Object} payload - Thông tin đăng ký tài khoản
 */
export function register(payload) {
  return request("/api/auth/register", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * [ROLE: Kỵ Sĩ / Jockey]
 * Tải lên tệp tài liệu bằng cấp hoặc chứng chỉ đăng ký nài ngựa.
 * Endpoint: POST /api/auth/upload-document
 * @param {File} file - Tệp tài liệu (PDF, JPG, PNG)
 */
export function uploadDocument(file) {
  const formData = new FormData();
  formData.append("file", file);
  return request("/api/auth/upload-document", {
    method: "POST",
    body: formData,
  });
}

/**
 * [ROLE: All Roles]
 * Cập nhật thông tin chi tiết hồ sơ cá nhân của người dùng.
 * Endpoint: PUT /api/auth/profile
 * @param {Object} payload - { fullName, phoneNumber, ... }
 */
export function updateProfile(payload) {
  return request("/api/auth/profile", {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}

/**
 * [ROLE: All Roles]
 * Thay đổi mật khẩu tài khoản người dùng đang đăng nhập.
 * Endpoint: POST /api/auth/change-password
 * @param {Object} payload - { currentPassword, newPassword }
 */
export function changePassword(payload) {
  return request("/api/auth/change-password", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * [ROLE: All Roles]
 * Truy vấn thông tin hồ sơ tài khoản cá nhân hiện tại.
 * Endpoint: GET /api/auth/profile
 */
export function getProfile() {
  return request("/api/auth/profile");
}

/**
 * [ROLE: Khán Giả / User]
 * Khởi tạo mã QR chuyển khoản và tạo đơn nạp tiền qua cổng SePay.
 * Endpoint: POST /api/sepay/deposit
 * @param {number} amount - Số tiền VNĐ muốn nạp
 */
export function createDeposit(amount) {
  return request("/api/sepay/deposit", {
    method: "POST",
    body: JSON.stringify({ amount }),
  });
}


/**
 * [ROLE: Khán Giả / User]
 * Tra cứu kiểm tra trạng thái xử lý của một đơn nạp tiền SePay.
 * Endpoint: GET /api/sepay/check
 * @param {string} transactionId - Mã GUID giao dịch
 */

export function checkDeposit(transactionId) {
  return request(`/api/sepay/check?transactionId=${transactionId}`);
}

/**
 * [ROLE: Khán Giả / User]
 * Lấy lịch sử tất cả các đơn nạp tiền SePay của người dùng.
 * Endpoint: GET /api/sepay/history
 */
export function getDepositHistory() {
  return request("/api/sepay/history");
}

/**
 * [ROLE: All Roles]
 * Gửi yêu cầu nhận Email chứa liên kết khôi phục mật khẩu.
 * Endpoint: POST /api/auth/forgot-password
 * @param {string} email - Địa chỉ email đăng ký tài khoản
 */
export function forgotPassword(email) {
  return request("/api/auth/forgot-password", {
    method: "POST",
    body: JSON.stringify({ email }),
  });
}

/**
 * [ROLE: All Roles]
 * Đặt lại mật khẩu mới thông qua mã resetToken nhận được qua Email.
 * Endpoint: POST /api/auth/reset-password
 * @param {Object} payload - { email, token, newPassword }
 */
export function resetPassword(payload) {
  return request("/api/auth/reset-password", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}
