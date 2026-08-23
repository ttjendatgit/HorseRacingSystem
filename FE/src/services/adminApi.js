import { request } from "./apiClient";

const unwrap = (response) => response?.data ?? response?.Data ?? response;

export const getAdminDashboard = async () =>
  unwrap(await request("/api/admin/dashboard"));

export const getAdminUsers = async () =>
  unwrap(await request("/api/admin/users"));

export const getAdminUser = async (id) =>
  unwrap(await request(`/api/admin/users/${id}`));

export const getOwnerHorses = async (userId) =>
  unwrap(await request(`/api/admin/users/${userId}/horses`));

export const getOwnerHorse = async (userId, horseId) =>
  unwrap(await request(`/api/admin/users/${userId}/horses/${horseId}`));

export const updateOwnerHorseStatus = (userId, horseId, payload) =>
  request(`/api/admin/users/${userId}/horses/${horseId}/status`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

export const setUserActive = (id, isActive) =>
  request(`/api/admin/users/${id}/${isActive ? "reactivate" : "deactivate"}`, {
    method: "POST",
  });

// J-ADMIN-REVIEW: Admin-only Jockey verification detail (Phone/Address/DateOfBirth/Height/Weight/
// IdCardNumber/LicenseFile/ApprovalNote/CreatedAt) — distinct from getAvailableJockeys(), which
// intentionally never exposes those fields to non-Admin callers.
export const getJockeyAdminDetail = async (id) =>
  unwrap(await request(`/api/admin/jockeys/${id}`));

export const approveJockey = (id) =>
  request(`/api/admin/jockeys/${id}/approve`, {
    method: "POST",
  });

export const rejectJockey = (id, reason) =>
  request(`/api/admin/jockeys/${id}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });

export const getAdminTournaments = async () =>
  unwrap(await request("/api/tournaments"));

export const createTournament = (payload) =>
  request("/api/tournaments", {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const updateTournament = (id, payload) =>
  request(`/api/tournaments/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

export const deleteTournament = (id) =>
  request(`/api/tournaments/${id}`, { method: "DELETE" });

export const getTournamentRounds = async (tournamentId) =>
  unwrap(await request(`/api/tournaments/${tournamentId}/rounds`));

export const createRound = (tournamentId, payload) =>
  request(`/api/tournaments/${tournamentId}/rounds`, {
    method: "POST",
    body: JSON.stringify({ ...payload, tournamentId }),
  });

export const updateRound = (roundId, payload) =>
  request(`/api/tournaments/rounds/${roundId}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

// Q1: server derives qualifiers/round-robin/Jockey carry-forward entirely — this call passes
// only the current Round's identity, nothing else.
export const generateNextRound = (roundId) =>
  request(`/api/tournaments/rounds/${roundId}/generate-next`, {
    method: "POST",
  });

export const getTournamentRaces = async (tournamentId) =>
  unwrap(await request(`/api/races/management/tournament/${tournamentId}`));

export const createRace = (payload) =>
  request("/api/races/management", {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const assignHorseToRace = (raceId, payload) =>
  request(`/api/races/management/${raceId}/assign-horse`, {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const startRace = (raceId) =>
  request(`/api/races/management/${raceId}/start`, { method: "POST" });

export const openRaceRegistration = (raceId) =>
  request(`/api/races/management/${raceId}/open-registration`, { method: "POST" });

export const closeRaceRegistration = (raceId) =>
  request(`/api/races/management/${raceId}/close-registration`, { method: "POST" });

export const endRace = (raceId) =>
  request(`/api/races/management/${raceId}/end`, { method: "POST" });

export const cancelRace = (raceId) =>
  request(`/api/races/management/${raceId}/cancel`, { method: "POST" });

export const getPendingRegistrations = async () =>
  unwrap(await request("/api/admin/registrations/pending"));

export const approveRegistration = (id) =>
  request(`/api/admin/registrations/${id}/approve`, { method: "POST" });

export const rejectRegistration = (id, reason) =>
  request(`/api/admin/registrations/${id}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });

export const getAllRegistrations = async () =>
  unwrap(await request("/api/admin/registrations"));

export const getRegistrationDetail = async (id) =>
  unwrap(await request(`/api/admin/registrations/${id}`));

// Referee management
export const getActiveReferees = async () =>
  unwrap(await request("/api/referees/active"));

export const getRaceRefereeAssignments = async (raceId) =>
  unwrap(await request(`/api/referees/race/${raceId}/assignments`));

export const assignRefereeToRace = (payload) =>
  request("/api/referees/assign", {
    method: "POST",
    body: JSON.stringify(payload),
  });

// Tournament race entry management
export const getPendingRaceEntries = async () =>
  unwrap(await request("/api/admin/race-entries/pending"));

export const approveRaceEntry = (entryId) =>
  request(`/api/admin/race-entries/${entryId}/approve`, { method: "POST" });

export const rejectRaceEntry = (entryId, reason) =>
  request(`/api/admin/race-entries/${entryId}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });

// Tournament horse registrations
export const getPendingTournamentRegistrations = async () =>
  unwrap(await request("/api/tournament-registrations/pending"));

export const getTournamentRegistrationSummary = async (tournamentId) =>
  unwrap(await request(`/api/tournament-registrations/tournament/${tournamentId}/summary`));

// Task B Correction 2 §5: Horses with an Approved TournamentHorseRegistration for this
// Tournament — the correct source for the Race-assignment "Chọn ngựa đã được phê duyệt"
// dropdown. Pending/Rejected/Withdrawn registrations, and Horses with no registration at all
// for this Tournament, never appear here.
export const getTournamentApprovedHorses = async (tournamentId) =>
  unwrap(await request(`/api/tournament-registrations/tournament/${tournamentId}/approved-horses`));

export const approveTournamentRegistration = (id) =>
  request(`/api/tournament-registrations/${id}/approve`, { method: "POST" });

export const rejectTournamentRegistration = (id, reason) =>
  request(`/api/tournament-registrations/${id}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });

export const approveRaceResult = (raceId) =>
  request(`/api/admin/races/${raceId}/approve-result`, { method: "POST" });

export const rejectRaceResult = (raceId, reason) =>
  request(`/api/admin/races/${raceId}/reject-result`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });
