import { request } from "./apiClient";

const unwrapResponseData = (response) => response?.data ?? response;

export const getOwnerProfile = async () =>
  unwrapResponseData(await request("/api/auth/me"));

export const getOwnerTournaments = async () =>
  unwrapResponseData(await request("/api/tournaments"));

export const getOwnerEntries = async () =>
  unwrapResponseData(await request("/api/horses/my-entries"));

export const getOwnerPerformance = async () =>
  unwrapResponseData(await request("/api/owner/performance"));

export const getOwnerUpcoming = async () =>
  unwrapResponseData(await request("/api/owner/upcoming"));
