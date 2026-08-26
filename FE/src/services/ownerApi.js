import { request } from "./apiClient";

const unwrapResponseData = (response) => response?.data ?? response;

/** Lấy thông tin hồ sơ của chủ ngựa đang đăng nhập */
export const getOwnerProfile = async () =>
  unwrapResponseData(await request("/api/auth/me"));

/** Lấy danh sách giải đấu dành cho chủ ngựa */
export const getOwnerTournaments = async () =>
  unwrapResponseData(await request("/api/tournaments"));

/** Lấy danh sách các lượt đăng ký ngựa tham gia giải đấu của chủ ngựa */
export const getOwnerEntries = async () =>
  unwrapResponseData(await request("/api/horses/my-entries"));

/** Lấy thống kê hiệu suất thành tích các ngựa thuộc sở hữu của chủ ngựa */
export const getOwnerPerformance = async () =>
  unwrapResponseData(await request("/api/owner/performance"));

/** Lấy danh sách các lịch trình cuộc đua sắp tới của chủ ngựa */
export const getOwnerUpcoming = async () =>
  unwrapResponseData(await request("/api/owner/upcoming"));
