import { request } from "./apiClient";

export function getBalance() {
  return request("/api/wallet/balance");
}
