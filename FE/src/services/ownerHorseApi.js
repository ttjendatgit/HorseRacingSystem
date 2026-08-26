import { request } from "./apiClient";

const unwrapResponseData = (response) => response?.data ?? response;

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Lấy danh sách tất cả các con ngựa đua thuộc sở hữu của chủ ngựa đang đăng nhập.
 * Endpoint: GET /api/horses
 */
export const getMyHorses = async () =>
  unwrapResponseData(await request("/api/horses"));

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Lấy thông tin chi tiết một con ngựa đua theo mã GUID.
 * Endpoint: GET /api/horses/{id}
 * @param {string} id - Mã GUID con ngựa
 */
export const getHorse = async (id) =>
  unwrapResponseData(await request(`/api/horses/${id}`));

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Đăng ký thêm một con ngựa đua mới vào hệ thống (Chờ Admin duyệt).
 * Endpoint: POST /api/horses
 * @param {Object} payload - Thông tin con ngựa (name, breed, age, weight, height, color...)
 */
export const createHorse = async (payload) =>
  unwrapResponseData(
    await request("/api/horses", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  );

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Cập nhật thông tin chỉ số cá nhân và hình ảnh của con ngựa đua.
 * Endpoint: PUT /api/horses/{id}
 * @param {string} id - Mã GUID con ngựa
 * @param {Object} payload - Thông tin chỉnh sửa
 */
export const updateHorse = async (id, payload) =>
  unwrapResponseData(
    await request(`/api/horses/${id}`, {
      method: "PUT",
      body: JSON.stringify(payload),
    }),
  );

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Xóa hoặc lưu trữ thông tin một con ngựa khỏi danh sách.
 * Endpoint: DELETE /api/horses/{id}
 * @param {string} id - Mã GUID con ngựa
 */
export const deleteHorse = async (id) =>
  unwrapResponseData(
    await request(`/api/horses/${id}`, {
      method: "DELETE",
    }),
  );

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Gửi lời mời kỵ sĩ (Jockey) điều khiển con ngựa đua trong một trận đua cụ thể.
 * Endpoint: POST /api/horses/{horseId}/jockey-invitations
 * @param {string} horseId - Mã GUID con ngựa
 * @param {Object} payload - { jockeyId, raceId, message }
 */
export const inviteJockeyToHorse = async (horseId, payload) =>
  unwrapResponseData(
    await request(`/api/horses/${horseId}/jockey-invitations`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  );

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Hủy phân công hoặc gỡ bỏ kỵ sĩ khỏi lượt đua của con ngựa.
 * Endpoint: DELETE /api/horses/{horseId}/races/{raceId}/jockeys
 * @param {string} horseId - Mã GUID con ngựa
 * @param {string} raceId - Mã GUID trận đua
 * @param {string} invitationId - Mã GUID lời mời
 * @param {string} reason - Lý do gỡ kỵ sĩ
 */
export const removeJockeyFromHorse = async (horseId, raceId, invitationId, reason) =>
  unwrapResponseData(
    await request(`/api/horses/${horseId}/races/${raceId}/jockeys`, {
      method: "DELETE",
      body: JSON.stringify({ invitationId, reason }),
    }),
  );

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Chủ ngựa chốt chọn chính thức 1 kỵ sĩ đã đồng ý lời mời để điều khiển ngựa trong trận đua.
 * Endpoint: POST /api/horses/{horseId}/races/{raceId}/jockeys/final-confirm
 * @param {string} horseId - Mã GUID con ngựa
 * @param {string} raceId - Mã GUID trận đua
 * @param {string} invitationId - Mã GUID lời mời đã được đồng ý
 */
export const finalConfirmJockey = async (horseId, raceId, invitationId) =>
  unwrapResponseData(
    await request(`/api/horses/${horseId}/races/${raceId}/jockeys/final-confirm`, {
      method: "POST",
      body: JSON.stringify({ invitationId }),
    }),
  );

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Đăng ký con ngựa vào trận đua cấp độ cuộc đua công khai.
 * Endpoint: POST /api/horses/{horseId}/races/{raceId}/registrations
 * @param {string} horseId - Mã GUID con ngựa
 * @param {string} raceId - Mã GUID trận đua
 * @param {Object} payload - Dữ liệu đăng ký
 */
export const registerHorseForRace = async (horseId, raceId, payload) =>
  unwrapResponseData(
    await request(`/api/horses/${horseId}/races/${raceId}/registrations`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  );

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Đăng ký chính thức con ngựa tham gia giải đấu cấp độ Tournament.
 * Endpoint: POST /api/tournament-registrations
 * @param {string} tournamentId - Mã GUID giải đấu
 * @param {string} horseId - Mã GUID con ngựa
 */
export const registerHorseForTournament = async (tournamentId, horseId) =>
  unwrapResponseData(
    await request("/api/tournament-registrations", {
      method: "POST",
      body: JSON.stringify({ tournamentId, horseId }),
    }),
  );

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Lấy danh sách tất cả các đơn đăng ký tham gia giải đấu của chủ ngựa.
 * Endpoint: GET /api/tournament-registrations/my
 */
export const getMyTournamentRegistrations = async () =>
  unwrapResponseData(await request("/api/tournament-registrations/my"));

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Rút đơn đăng ký tham gia giải đấu của con ngựa trước hạn chót.
 * Endpoint: POST /api/tournament-registrations/{registrationId}/withdraw
 * @param {string} registrationId - Mã GUID đơn đăng ký giải đấu
 */
export const withdrawTournamentRegistration = async (registrationId) =>
  unwrapResponseData(
    await request(`/api/tournament-registrations/${registrationId}/withdraw`, {
      method: "POST",
    }),
  );

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Chủ ngựa xác nhận lượt tham gia thi đấu chính thức của con ngựa trong trận đua.
 * Endpoint: POST /api/horses/races/{raceId}/entries/{entryId}/owner-confirm
 * @param {string} raceId - Mã GUID trận đua
 * @param {string} entryId - Mã GUID lượt đăng ký thi đấu
 */
export const confirmRaceEntry = async (raceId, entryId) =>
  unwrapResponseData(
    await request(`/api/horses/races/${raceId}/entries/${entryId}/owner-confirm`, {
      method: "POST",
    }),
  );

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Lấy tất cả các lượt đua (Race Entries) của các con ngựa thuộc sở hữu chủ ngựa.
 * Endpoint: GET /api/horses/my-entries
 */
export const getMyRaceEntries = async () =>
  unwrapResponseData(await request("/api/horses/my-entries"));
