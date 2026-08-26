import { request } from "./apiClient";

const unwrap = (r) => r?.data ?? r?.Data ?? r;

/** [ROLE: All Roles] Lấy tất cả thông báo của người dùng đang đăng nhập */
export const getNotifications = async () => unwrap(await request("/api/notifications/user"));

/** [ROLE: All Roles] Lấy danh sách các thông báo chưa đọc */
export const getUnreadNotifications = async () => unwrap(await request("/api/notifications/unread"));

/** [ROLE: All Roles] Lấy tổng số lượng thông báo chưa đọc */
export const getUnreadCount = async () => unwrap(await request("/api/notifications/count/unread"));

/** [ROLE: All Roles] Đánh dấu một thông báo là Đã đọc theo ID */
export const markNotificationRead = async (id) => await request(`/api/notifications/${id}/mark-read`, { method: "PUT" });

/** [ROLE: All Roles] Đánh dấu nhiều thông báo cùng lúc là Đã đọc */
export const markMultipleRead = async (ids) => await request("/api/notifications/mark-multiple-read", { method: "POST", body: JSON.stringify({ ids }) });

/** [ROLE: All Roles] Xóa một thông báo theo ID */
export const deleteNotification = async (id) => await request(`/api/notifications/${id}`, { method: "DELETE" });

/** [ROLE: All Roles] Xóa toàn bộ tất cả thông báo của người dùng */
export const deleteAllNotifications = async () => await request("/api/notifications/all", { method: "DELETE" });
