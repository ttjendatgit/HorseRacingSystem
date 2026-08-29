import { request } from "./apiClient";

const unwrapResponseData = (response) => response?.data ?? response?.Data ?? response;

const asArray = (payload) => {
  const data = unwrapResponseData(payload);
  if (Array.isArray(data)) return data;
  if (Array.isArray(data?.items)) return data.items;
  if (Array.isArray(data?.Items)) return data.Items;
  if (Array.isArray(data?.races)) return data.races;
  if (Array.isArray(data?.Races)) return data.Races;
  return [];
};

const read = (source, ...keys) => {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== undefined && value !== null && value !== "") {
      return value;
    }
  }
  return undefined;
};

export const formatJockeyDate = (value, fallback = "TBD") => {
  if (!value) return fallback;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return new Intl.DateTimeFormat("vi-VN", {
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
};

const normalizeJockey = (jockey) => ({
  id: jockey.id ?? jockey.Id,
  userId: jockey.userId ?? jockey.UserId,
  fullName: jockey.fullName ?? jockey.FullName ?? "Unnamed jockey",
  email: jockey.email ?? jockey.Email ?? "",
  licenseNumber: jockey.licenseNumber ?? jockey.LicenseNumber ?? "",
  nationality: jockey.nationality ?? jockey.Nationality ?? "",
  experienceYears: jockey.experienceYears ?? jockey.ExperienceYears ?? 0,
  totalRaces: jockey.totalRaces ?? jockey.TotalRaces ?? 0,
  totalWins: jockey.totalWins ?? jockey.TotalWins ?? 0,
  winRate: jockey.winRate ?? jockey.WinRate ?? 0,
  rank: jockey.rank ?? jockey.Rank ?? null,
  status: jockey.status ?? jockey.Status ?? "",
  approvalStatus: jockey.approvalStatus ?? jockey.ApprovalStatus ?? null,
  approvalStatusName:
    jockey.approvalStatusName ?? jockey.ApprovalStatusName ?? "Unknown",
});

/** Lấy danh sách nài ngựa khả dụng trong hệ thống */
export const getAvailableJockeys = async () =>
  (unwrapResponseData(await request("/api/jockeys")) ?? []).map(normalizeJockey);

/**
 * Chuẩn hóa chuỗi/mã trạng thái lời mời cho nài ngựa
 * @param {string|number} rawStatus - Trạng thái thô từ API
 */
export const normalizeInvitationStatus = (rawStatus) => {
  if (rawStatus === undefined || rawStatus === null || rawStatus === "") {
    return "Pending";
  }

  if (typeof rawStatus === "number") {
    return ["Pending", "Accepted", "Declined", "Withdrawn"][rawStatus - 1] ?? "Pending";
  }

  if (typeof rawStatus === "string") {
    const normalized = rawStatus.trim().toLowerCase();
    if (["pending", "1", "wait", "waiting", "chờ", "chờ duyệt", "chờ xác nhận"].includes(normalized)) return "Pending";
    if (["accepted", "accept", "2", "đã chấp nhận", "da chap nhan", "confirmed", "confirm"].includes(normalized)) return "Accepted";
    if (["declined", "decline", "reject", "rejected", "3", "đã từ chối", "da tu choi"].includes(normalized)) return "Declined";
  }

  if (String(rawStatus).trim().toLowerCase() === "withdrawn" || String(rawStatus).trim() === "4") {
    return "Withdrawn";
  }
  return String(rawStatus);
};

/**
 * Chuẩn hóa cấu trúc lời mời tham gia cuộc đua cho nài ngựa
 * @param {Object} invitation - Đối tượng lời mời thô từ API
 */
export const normalizeInvitation = (invitation) => {
  const horse = read(invitation, "horse", "Horse") ?? {};
  const owner = read(horse, "owner", "Owner") ?? {};
  const ownerUser = read(owner, "user", "User") ?? {};
  const race = read(invitation, "race", "Race") ?? {};
  const tournament = read(race, "tournament", "Tournament") ?? {};
  const track = read(race, "track", "Track") ?? {};
  const rawStatus = read(invitation, "status", "Status", "statusName", "StatusName");
  const status = normalizeInvitationStatus(rawStatus);

  return {
    id: read(invitation, "id", "Id"),
    status,
    createdAt: read(invitation, "createdAt", "CreatedAt"),
    message: read(invitation, "message", "Message"),
    responseNote: read(invitation, "responseNote", "ResponseNote"),
    raceId: read(invitation, "raceId", "RaceId") ?? read(race, "id", "Id"),
    raceName: read(
      invitation,
      "raceName",
      "RaceName",
      "title",
      "Title",
    ) ?? read(race, "name", "Name") ?? "Lời mời tham gia cuộc đua",
    scheduledAt:
      read(invitation, "scheduledAt", "ScheduledAt", "date", "Date") ??
      read(race, "scheduledAt", "ScheduledAt"),
    location:
      read(invitation, "track", "Track", "location", "Location") ??
      read(track, "name", "Name") ??
      read(race, "location", "Location") ??
      "Chưa xác định",
    distance: read(race, "distance", "Distance"),
    maxParticipants: read(race, "maxParticipants", "MaxParticipants"),
    raceStatus: read(race, "status", "Status"),
    tournamentName: read(tournament, "name", "Name") ?? "Chưa xác định",
    horseId: read(invitation, "horseId", "HorseId") ?? read(horse, "id", "Id"),
    horseName:
      read(invitation, "horseName", "HorseName") ??
      read(horse, "name", "Name") ??
      "Unknown horse",
    ownerName:
      read(invitation, "ownerName", "OwnerName") ??
      read(ownerUser, "fullName", "FullName") ??
      read(owner, "organizationName", "OrganizationName") ??
      "Chưa xác định",
    horseBreed: read(horse, "breed", "Breed"),
    horseAge: read(horse, "age", "Age"),
    horseWeight: read(horse, "weight", "Weight"),
    horseColor: read(horse, "color", "Color"),
    horseTotalRaces: read(horse, "totalRaces", "TotalRaces"),
    horseTotalWins: read(horse, "totalWins", "TotalWins"),
  };
};

/**
 * Chuẩn hóa thông tin cuộc đua được phân công cho nài ngựa
 * @param {Object} entry - Lượt phân công/đăng ký
 */
export const normalizeAssignedRace = (entry) => {
  const race = read(entry, "race", "Race") ?? entry ?? {};
  const horse = read(entry, "horse", "Horse") ?? {};
  const tournament = read(race, "tournament", "Tournament") ?? {};
  const rawStatus =
    read(entry, "status", "Status", "statusName", "StatusName") ??
    read(race, "status", "Status");

  return {
    id: read(entry, "id", "Id") ?? read(race, "id", "Id"),
    entryId: read(entry, "id", "Id"),
    raceId: read(entry, "raceId", "RaceId") ?? read(race, "id", "Id"),
    title:
      read(entry, "title", "Title", "raceName", "RaceName") ??
      read(race, "name", "Name") ??
      "Assigned race",
    scheduledAt:
      read(entry, "scheduledAt", "ScheduledAt", "time", "Time") ??
      read(race, "scheduledAt", "ScheduledAt"),
    location:
      read(entry, "track", "Track", "location", "Location") ??
      read(race, "location", "Location") ??
      "TBD Track",
    distance: read(race, "distance", "Distance"),
    maxParticipants: read(race, "maxParticipants", "MaxParticipants"),
    description: read(race, "description", "Description"),
    tournamentName: read(tournament, "name", "Name") ?? "Tournament TBD",
    status: typeof rawStatus === "number" ? `Status ${rawStatus}` : rawStatus ?? "Assigned",
    ownerConfirmed: Boolean(read(entry, "ownerConfirmed", "OwnerConfirmed")),
    jockeyConfirmed: Boolean(read(entry, "jockeyConfirmed", "JockeyConfirmed")),
    horseName: read(horse, "name", "Name") ?? "Unknown horse",
    horseBreed: read(horse, "breed", "Breed"),
    horseGender: read(horse, "gender", "Gender"),
    horseAge: read(horse, "age", "Age"),
    horseWeight: read(horse, "weight", "Weight"),
    horseHeight: read(horse, "height", "Height"),
    horseColor: read(horse, "color", "Color"),
    horseTotalRaces: read(horse, "totalRaces", "TotalRaces") ?? 0,
    horseTotalWins: read(horse, "totalWins", "TotalWins") ?? 0,
  };
};

/** Lấy danh sách tất cả các lời mời đua dành cho nài ngựa */
export const getJockeyInvitations = async () =>
  asArray(await request("/api/jockeys/invitations")).map(normalizeInvitation);

/**
 * Phản hồi lời mời tham gia cuộc đua (Đồng ý / Từ chối)
 * @param {string} invitationId - ID lời mời
 * @param {boolean} accept - true nếu đồng ý, false nếu từ chối
 */
export const respondJockeyInvitation = async (invitationId, accept) =>
  request(`/api/jockeys/invitations/${invitationId}/respond`, {
    method: "POST",
    body: JSON.stringify({ accept }),
  });

/**
 * Rút khỏi lời mời tham gia cuộc đua
 * @param {string} invitationId - ID lời mời
 * @param {string} reason - Lý do rút
 */
export const withdrawJockeyInvitation = async (invitationId, reason) =>
  request(`/api/jockeys/invitations/${invitationId}/withdraw`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });

/** Lấy danh sách cuộc đua nài ngựa đã được phân công tham gia */
export const getJockeyAssignedRaces = async () =>
  asArray(await request("/api/jockeys/races")).map(normalizeAssignedRace);

/** Lấy danh sách các lượt phân công đang chờ nài ngựa xác nhận */
export const getJockeyPendingEntries = async () =>
  asArray(await request("/api/jockeys/entries/pending"));

/** Lấy thông tin hồ sơ của nài ngựa đang đăng nhập */
export const getMyJockeyProfile = async () =>
  unwrapResponseData(await request("/api/jockeys/me"));

/**
 * Xác nhận tham gia lượt thi đấu
 * @param {string} entryId - ID lượt tham gia cuộc đua
 */
export const confirmJockeyEntry = async (entryId) =>
  request(`/api/jockeys/entries/${entryId}/confirm`, { method: "POST" });

/**
 * Từ chối tham gia lượt thi đấu
 * @param {string} entryId - ID lượt tham gia cuộc đua
 */
export const declineJockeyEntry = async (entryId) =>
  request(`/api/jockeys/entries/${entryId}/decline`, { method: "POST" });

export const updateJockeyProfile = async (payload) =>
  request("/api/jockeys/profile", {
    method: "PUT",
    body: JSON.stringify(payload),
  });
