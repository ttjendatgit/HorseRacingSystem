import { request } from "./apiClient";

const unwrap = (r) => r?.data ?? r?.Data ?? r;

// ── Prizes ──
export const getPrizes = async () => unwrap(await request("/api/management/prizes"));
export const getPrizesByTournament = async (id) => unwrap(await request(`/api/management/prizes/tournament/${id}`));
export const createPrize = (p) => request("/api/management/prizes", { method: "POST", body: JSON.stringify(p) });
export const updatePrize = (id, p) => request(`/api/management/prizes/${id}`, { method: "PUT", body: JSON.stringify(p) });
export const deletePrize = (id) => request(`/api/management/prizes/${id}`, { method: "DELETE" });

// ── Protests ──
export const getProtests = async () => unwrap(await request("/api/management/protests"));
export const getPendingProtests = async () => unwrap(await request("/api/management/protests/pending"));
export const getMyProtests = async () => unwrap(await request("/api/management/protests/mine"));
export const createProtest = (p) => request("/api/management/protests", { method: "POST", body: JSON.stringify(p) });
export const markProtestUnderReview = (id) => request(`/api/management/protests/${id}/under-review`, { method: "POST" });
export const ruleProtest = (id, p) => request(`/api/management/protests/${id}/rule`, { method: "POST", body: JSON.stringify(p) });
export const withdrawProtest = (id) => request(`/api/management/protests/${id}/withdraw`, { method: "POST" });

// ── Race Complaints ──
export const getRaceComplaints = async (status) => unwrap(await request(`/api/management/race-complaints${status ? `?status=${status}` : ""}`));
export const getMyRaceComplaints = async () => unwrap(await request("/api/management/race-complaints/mine"));
export const getRefereeRaceComplaints = async () => unwrap(await request("/api/management/race-complaints/referee"));
export const getEligibleRaceComplaintRaces = async () => unwrap(await request("/api/management/race-complaints/eligible-races"));
export const createRaceComplaint = (p) => request("/api/management/race-complaints", { method: "POST", body: JSON.stringify(p) });
export const routeRaceComplaint = (id, p) => request(`/api/management/race-complaints/${id}/route`, { method: "POST", body: JSON.stringify(p) });
export const respondRaceComplaint = (id, p) => request(`/api/management/race-complaints/${id}/respond`, { method: "POST", body: JSON.stringify(p) });
export const ruleRaceComplaint = (id, p) => request(`/api/management/race-complaints/${id}/rule`, { method: "POST", body: JSON.stringify(p) });
export const withdrawRaceComplaint = (id) => request(`/api/management/race-complaints/${id}/withdraw`, { method: "POST" });
export const uploadRaceComplaintEvidence = (id, file) => {
  const formData = new FormData();
  formData.append("file", file);
  return request(`/api/management/race-complaints/${id}/evidence`, { method: "POST", body: formData });
};

export const deleteRaceComplaintEvidence = (id, evidenceId) =>
  request(`/api/management/race-complaints/${id}/evidence/${evidenceId}`, { method: "DELETE" });

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
