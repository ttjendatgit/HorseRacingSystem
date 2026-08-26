import { request } from "./apiClient";

const unwrap = (r) => r?.data ?? r?.Data ?? r;

// ── Prizes ──
export const getPrizes = async () => unwrap(await request("/api/management/prizes"));
export const getPrizesByTournament = async (id) => unwrap(await request(`/api/management/prizes/tournament/${id}`));
export const createPrize = (p) => request("/api/management/prizes", { method: "POST", body: JSON.stringify(p) });
export const deletePrize = (id) => request(`/api/management/prizes/${id}`, { method: "DELETE" });

// ── Protests ──
export const getProtests = async () => unwrap(await request("/api/management/protests"));
export const getPendingProtests = async () => unwrap(await request("/api/management/protests/pending"));
export const createProtest = (p) => request("/api/management/protests", { method: "POST", body: JSON.stringify(p) });
export const ruleProtest = (id, p) => request(`/api/management/protests/${id}/rule`, { method: "POST", body: JSON.stringify(p) });

// ── Horse Transfers ──
export const getTransfers = async () => unwrap(await request("/api/management/transfers"));
export const getPendingTransfers = async () => unwrap(await request("/api/management/transfers/pending"));
export const createTransfer = (p) => request("/api/management/transfers", { method: "POST", body: JSON.stringify(p) });
export const approveTransfer = (id, n) => request(`/api/management/transfers/${id}/approve`, { method: "POST", body: JSON.stringify(n || {}) });
export const rejectTransfer = (id, reason) => request(`/api/management/transfers/${id}/reject`, { method: "POST", body: JSON.stringify({ reason }) });

// ── Contracts ──
export const getContracts = async () => unwrap(await request("/api/management/contracts"));
export const createContract = (p) => request("/api/management/contracts", { method: "POST", body: JSON.stringify(p) });
export const signContractOwner = (id) => request(`/api/management/contracts/${id}/sign-owner`, { method: "POST" });
export const signContractJockey = (id) => request(`/api/management/contracts/${id}/sign-jockey`, { method: "POST" });

// ── Injury Records ──
export const getInjuries = async () => unwrap(await request("/api/management/injuries"));
export const getInjuriesByHorse = async (id) => unwrap(await request(`/api/management/injuries/horse/${id}`));
export const createInjury = (p) => request("/api/management/injuries", { method: "POST", body: JSON.stringify(p) });
export const markRecovered = (id) => request(`/api/management/injuries/${id}/recover`, { method: "POST" });
export const clearToRace = (id) => request(`/api/management/injuries/${id}/clear`, { method: "POST" });
