import { request } from "./apiClient";

const unwrap = (response) => response?.data ?? response?.Data ?? response;

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy danh sách tất cả các giải đấu công khai trong hệ thống.
 * Endpoint: GET /api/tournaments
 */
export const getTournaments = async () => unwrap(await request("/api/tournaments"));

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy danh sách các giải đấu đang diễn ra hoặc mở đăng ký cược.
 * Endpoint: GET /api/tournaments/active
 */
export const getActiveTournaments = async () =>
  unwrap(await request("/api/tournaments/active"));

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy thông tin chi tiết của một giải đấu theo mã GUID.
 * Endpoint: GET /api/tournaments/{id}
 * @param {string} id - Mã GUID giải đấu
 */
export const getTournament = async (id) =>
  unwrap(await request(`/api/tournaments/${id}`));

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy danh sách các vòng thi đấu thuộc về một giải đấu.
 * Endpoint: GET /api/tournaments/{tournamentId}/rounds
 * @param {string} tournamentId - Mã GUID giải đấu
 */
export const getRoundsByTournament = (tournamentId) =>
  request(`/api/tournaments/${tournamentId}/rounds`).then(unwrap);

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy danh sách tất cả các trận đua ngựa công khai.
 * Endpoint: GET /api/races
 */
export const getRaces = async () => unwrap(await request("/api/races"));

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy thông tin chi tiết một trận đua ngựa theo mã GUID.
 * Endpoint: GET /api/races/{id}
 * @param {string} id - Mã GUID trận đua
 */
export const getRace = async (id) => unwrap(await request(`/api/races/${id}`));

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy kết quả thi đấu chính thức và bảng thứ hạng sau khi trận đua kết thúc.
 * Endpoint: GET /api/races/{id}/result
 * @param {string} id - Mã GUID trận đua
 */
export const getRaceResult = async (id) =>
  unwrap(await request(`/api/races/${id}/result`));

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy kết quả trực tiếp thời gian thực của trận đua đang diễn ra.
 * Endpoint: GET /api/live-results/race/{raceId}
 * @param {string} raceId - Mã GUID trận đua
 */
export const getLiveRaceResult = (raceId) =>
  request(`/api/live-results/race/${raceId}`).then(unwrap);

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy tọa độ vị trí các con ngựa trên đường đua thời gian thực.
 * Endpoint: GET /api/live-results/race/{raceId}/positions
 * @param {string} raceId - Mã GUID trận đua
 */
export const getLivePositions = (raceId) =>
  request(`/api/live-results/race/${raceId}/positions`).then(unwrap);

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy bảng xếp hạng thời gian thực của trận đua đang diễn ra.
 * Endpoint: GET /api/live-results/race/{raceId}/ranking
 * @param {string} raceId - Mã GUID trận đua
 */
export const getLiveRanking = (raceId) =>
  request(`/api/live-results/race/${raceId}/ranking`).then(unwrap);

/**
 * [ROLE: Khán Giả / Spectator]
 * Đặt phiếu dự đoán / cược cho con ngựa trong trận đua.
 * Endpoint: POST /api/predictions
 * @param {Object} payload - { raceId: string, predictedHorseId: string, betAmount: number }
 */
export const createPrediction = (payload) =>
  request("/api/predictions", {
    method: "POST",
    body: JSON.stringify(payload),
  });

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy lịch sử tất cả các phiếu dự đoán / cược của khán giả đang đăng nhập.
 * Endpoint: GET /api/predictions/mine
 */
export const getMyPredictions = () => request("/api/predictions/mine");

/**
 * [ROLE: Khán Giả / Spectator]
 * Lấy danh sách danh sách các con ngựa & kỵ sĩ tham gia trận đua.
 * Endpoint: GET /api/referees/race/{raceId}/entries
 * @param {string} raceId - Mã GUID trận đua
 */
export const getRaceEntries = async (raceId) =>
  unwrap(await request(`/api/referees/race/${raceId}/entries`));

