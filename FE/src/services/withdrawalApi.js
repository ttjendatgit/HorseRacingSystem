import { request } from "./apiClient";

export function saveBankAccount(payload) {
  return request("/api/withdrawal/bank-account", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function getBankAccounts() {
  return request("/api/withdrawal/bank-accounts");
}

export function createWithdrawal(payload) {
  return request("/api/withdrawal/create", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function getWithdrawalHistory() {
  return request("/api/withdrawal/history");
}
