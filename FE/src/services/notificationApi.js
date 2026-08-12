import { request } from "./apiClient";

const unwrap = (r) => r?.data ?? r?.Data ?? r;

export const getNotifications = async () => unwrap(await request("/api/notifications/user"));
export const getUnreadNotifications = async () => unwrap(await request("/api/notifications/unread"));
export const getUnreadCount = async () => unwrap(await request("/api/notifications/count/unread"));
export const markNotificationRead = async (id) => await request(`/api/notifications/${id}/mark-read`, { method: "PUT" });
export const markMultipleRead = async (ids) => await request("/api/notifications/mark-multiple-read", { method: "POST", body: JSON.stringify({ ids }) });
export const deleteNotification = async (id) => await request(`/api/notifications/${id}`, { method: "DELETE" });
export const deleteAllNotifications = async () => await request("/api/notifications/all", { method: "DELETE" });
