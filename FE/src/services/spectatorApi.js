import { request } from "./apiClient";

const unwrap = (response) => response?.data ?? response?.Data ?? response;

/** Lấy danh sách tất cả các giải đấu công khai */
export const getTournaments = async () => unwrap(await request("/api/tournaments"));

/** Lấy danh sách các giải đấu đang diễn ra/hoạt động */
export const getActiveTournaments = async () =>
  unwrap(await request("/api/tournaments/active"));

/** Lấy thông tin chi tiết của một giải đấu theo ID */
export const getTournament = async (id) =>
  unwrap(await request(`/api/tournaments/${id}`));

/** Lấy danh sách các vòng đấu thuộc một giải đấu */
export const getRoundsByTournament = (tournamentId) =>
  request(`/api/tournaments/${tournamentId}/rounds`).then(unwrap);

/** Lấy danh sách tất cả các cuộc đua */
export const getRaces = async () => unwrap(await request("/api/races"));

/** Lấy chi tiết thông tin một cuộc đua theo ID */
export const getRace = async (id) => unwrap(await request(`/api/races/${id}`));

/** Lấy kết quả chính thức của một cuộc đua */
export const getRaceResult = async (id) =>
  unwrap(await request(`/api/races/${id}/result`));

/** Lấy kết quả trực tiếp thời gian thực của cuộc đua */
export const getLiveRaceResult = (raceId) =>
  request(`/api/live-results/race/${raceId}`).then(unwrap);

/** Lấy vị trí trực tiếp các ngựa trong cuộc đua */
export const getLivePositions = (raceId) =>
  request(`/api/live-results/race/${raceId}/positions`).then(unwrap);

/** Lấy bảng xếp hạng trực tiếp theo thời gian thực */
export const getLiveRanking = (raceId) =>
  request(`/api/live-results/race/${raceId}/ranking`).then(unwrap);

/**
 * Gửi dự đoán / cược cho cuộc đua
 * @param {Object} payload - { raceId, predictedHorseId, betAmount }
 */
export const createPrediction = (payload) =>
  request("/api/predictions", {
    method: "POST",
    body: JSON.stringify(payload),
  });

/** Lấy lịch sử phiếu dự đoán của người dùng hiện tại */
export const getMyPredictions = () => request("/api/predictions/mine");

/** Lấy danh sách ngựa & nài ngựa đăng ký tham gia cuộc đua */
export const getRaceEntries = async (raceId) =>
  unwrap(await request(`/api/referees/race/${raceId}/entries`));
