import { request } from "./apiClient";

const unwrapResponseData = (response) => response?.data ?? response;

export const getMyHorses = async () =>
  unwrapResponseData(await request("/api/horses"));

export const getHorse = async (id) =>
  unwrapResponseData(await request(`/api/horses/${id}`));

export const createHorse = async (payload) =>
  unwrapResponseData(
    await request("/api/horses", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  );

export const updateHorse = async (id, payload) =>
  unwrapResponseData(
    await request(`/api/horses/${id}`, {
      method: "PUT",
      body: JSON.stringify(payload),
    }),
  );

export const deleteHorse = async (id) =>
  unwrapResponseData(
    await request(`/api/horses/${id}`, {
      method: "DELETE",
    }),
  );

export const inviteJockeyToHorse = async (horseId, payload) =>
  unwrapResponseData(
    await request(`/api/horses/${horseId}/jockey-invitations`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  );

export const removeJockeyFromHorse = async (horseId, raceId, reason) =>
  unwrapResponseData(
    await request(`/api/horses/${horseId}/races/${raceId}/jockeys`, {
      method: "DELETE",
      body: JSON.stringify({ reason }),
    }),
  );

// Legacy Race-level self-registration (RaceStatus.RegistrationOpen-gated) — retained for
// RaceStatus/StartRaceAsync lifecycle compatibility only. Not the canonical Owner registration
// flow; see registerHorseForTournament below.
export const registerHorseForRace = async (horseId, raceId, payload) =>
  unwrapResponseData(
    await request(`/api/horses/${horseId}/races/${raceId}/registrations`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  );

// Canonical Owner registration flow (Task B) — Tournament-level, not Race-level. Backend gate:
// Tournament.Status == Published && now < RegistrationDeadline.
export const registerHorseForTournament = async (tournamentId, horseId) =>
  unwrapResponseData(
    await request("/api/tournament-registrations", {
      method: "POST",
      body: JSON.stringify({ tournamentId, horseId }),
    }),
  );

export const getMyTournamentRegistrations = async () =>
  unwrapResponseData(await request("/api/tournament-registrations/my"));

export const confirmRaceEntry = async (raceId, entryId) =>
  unwrapResponseData(
    await request(`/api/horses/races/${raceId}/entries/${entryId}/owner-confirm`, {
      method: "POST",
    }),
  );

export const getMyRaceEntries = async () =>
  unwrapResponseData(await request("/api/horses/my-entries"));
