import { request } from "./apiClient";

export function login(payload) {
  return request("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function register(payload) {
  return request("/api/auth/register", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function uploadDocument(file) {
  const formData = new FormData();
  formData.append("file", file);
  return request("/api/auth/upload-document", {
    method: "POST",
    body: formData,
  });
}

export function updateProfile(payload) {
  return request("/api/auth/profile", {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}

export function changePassword(payload) {
  return request("/api/auth/change-password", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function getProfile() {
  return request("/api/auth/profile");
}

export function createDeposit(amount) {
  return request("/api/sepay/deposit", {
    method: "POST",
    body: JSON.stringify({ amount }),
  });
}

export function checkDeposit(transactionId) {
  return request(`/api/sepay/check?transactionId=${transactionId}`);
}

export function getDepositHistory() {
  return request("/api/sepay/history");
}

export function forgotPassword(email) {
  return request("/api/auth/forgot-password", {
    method: "POST",
    body: JSON.stringify({ email }),
  });
}

export function resetPassword(payload) {
  return request("/api/auth/reset-password", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}
