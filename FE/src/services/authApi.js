import { request } from "./apiClient";

/**
 * Gửi yêu cầu đăng nhập tài khoản hệ thống
 * @param {Object} payload - { email, password }
 */
export function login(payload) {
  return request("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * Gửi yêu cầu đăng ký tài khoản mới (Chủ ngựa / Nài ngựa / Khán giả)
 * @param {Object} payload - Thông tin đăng ký tài khoản
 */
export function register(payload) {
  return request("/api/auth/register", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * Tải lên tài liệu hoặc chứng chỉ đăng ký nài ngựa
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
 * Cập nhật thông tin hồ sơ người dùng
 * @param {Object} payload - { fullName, phoneNumber, ... }
 */
export function updateProfile(payload) {
  return request("/api/auth/profile", {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}

/**
 * Đổi mật khẩu tài khoản
 * @param {Object} payload - { currentPassword, newPassword }
 */
export function changePassword(payload) {
  return request("/api/auth/change-password", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/** Lấy thông tin hồ sơ tài khoản hiện tại */
export function getProfile() {
  return request("/api/auth/profile");
}

/**
 * Tạo giao dịch nạp tiền qua cổng SePay
 * @param {number} amount - Số tiền VNĐ muốn nạp
 */
export function createDeposit(amount) {
  return request("/api/sepay/deposit", {
    method: "POST",
    body: JSON.stringify({ amount }),
  });
}

/**
 * Kiểm tra trạng thái hoàn thành của giao dịch nạp tiền
 * @param {string} transactionId - ID giao dịch
 */
export function checkDeposit(transactionId) {
  return request(`/api/sepay/check?transactionId=${transactionId}`);
}

/** Lấy danh sách lịch sử nạp tiền của người dùng */
export function getDepositHistory() {
  return request("/api/sepay/history");
}

/**
 * Gửi yêu cầu quên mật khẩu
 * @param {string} email - Địa chỉ email đăng ký tài khoản
 */
export function forgotPassword(email) {
  return request("/api/auth/forgot-password", {
    method: "POST",
    body: JSON.stringify({ email }),
  });
}

/**
 * Đặt lại mật khẩu mới bằng mã resetToken
 * @param {Object} payload - { email, token, newPassword }
 */
export function resetPassword(payload) {
  return request("/api/auth/reset-password", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}
