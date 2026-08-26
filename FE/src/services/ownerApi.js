import { request } from "./apiClient";

const unwrapResponseData = (response) => response?.data ?? response;

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Lấy thông tin chi tiết hồ sơ của chủ ngựa đang đăng nhập.
 * Endpoint: GET /api/auth/me
 */
export const getOwnerProfile = async () =>
  unwrapResponseData(await request("/api/auth/me"));

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Lấy danh sách các giải đấu mở đăng ký dành cho chủ ngựa.
 * Endpoint: GET /api/tournaments
 */
export const getOwnerTournaments = async () =>
  unwrapResponseData(await request("/api/tournaments"));

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Lấy danh sách các lượt đăng ký ngựa tham gia trận đua của chủ sở hữu.
 * Endpoint: GET /api/horses/my-entries
 */
export const getOwnerEntries = async () =>
  unwrapResponseData(await request("/api/horses/my-entries"));

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Lấy số liệu thống kê hiệu suất thi đấu và thành tích chiến thắng của các ngựa đua.
 * Endpoint: GET /api/owner/performance
 */
export const getOwnerPerformance = async () =>
  unwrapResponseData(await request("/api/owner/performance"));

/**
 * [ROLE: Chủ Ngựa / Owner]
 * Lấy danh sách các trận đua ngựa sắp diễn ra của chủ sở hữu.
 * Endpoint: GET /api/owner/upcoming
 */
export const getOwnerUpcoming = async () =>
  unwrapResponseData(await request("/api/owner/upcoming"));
