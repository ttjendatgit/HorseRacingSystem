import { request } from "./apiClient";

const unwrap = (response) => response?.data ?? response?.Data ?? response;

export const getTournaments = async () => unwrap(await request("/api/tournaments"));
export const getActiveTournaments = async () =>
  unwrap(await request("/api/tournaments/active"));
export const getTournament = async (id) =>
  unwrap(await request(`/api/tournaments/${id}`));
export const getRoundsByTournament = (tournamentId) =>
  request(`/api/tournaments/${tournamentId}/rounds`).then(unwrap);

export const getRaces = async () => unwrap(await request("/api/races"));
export const getRace = async (id) => unwrap(await request(`/api/races/${id}`));
export const getRaceResult = async (id) =>
  unwrap(await request(`/api/races/${id}/result`));

export const getLiveRaceResult = (raceId) =>
  request(`/api/live-results/race/${raceId}`).then(unwrap);
export const getLivePositions = (raceId) =>
  request(`/api/live-results/race/${raceId}/positions`).then(unwrap);
export const getLiveRanking = (raceId) =>
  request(`/api/live-results/race/${raceId}/ranking`).then(unwrap);

export const createPrediction = (payload) =>
  request("/api/predictions", {
    method: "POST",
    body: JSON.stringify(payload),
  });
export const getMyPredictions = () => request("/api/predictions/mine");

export const getRaceEntries = async (raceId) =>
  unwrap(await request(`/api/referees/race/${raceId}/entries`));
