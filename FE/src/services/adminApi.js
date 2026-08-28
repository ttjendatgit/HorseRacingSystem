import { request } from "./apiClient";

const unwrap = (response) => response?.data ?? response?.Data ?? response;

/** [ROLE: Admin] Lấy thông tin thống kê tổng quan Bảng điều khiển Quản trị viên */
export const getAdminDashboard = async () =>
  unwrap(await request("/api/admin/dashboard"));

/** [ROLE: Admin] Lấy danh sách tất cả người dùng trong hệ thống */
export const getAdminUsers = async () =>
  unwrap(await request("/api/admin/users"));

/** [ROLE: Admin] Lấy thông tin chi tiết một người dùng theo mã GUID */
export const getAdminUser = async (id) =>
  unwrap(await request(`/api/admin/users/${id}`));

/** [ROLE: Admin] Lấy danh sách các con ngựa thuộc sở hữu của một chủ ngựa */
export const getOwnerHorses = async (userId) =>
  unwrap(await request(`/api/admin/users/${userId}/horses`));

/** [ROLE: Admin] Lấy chi tiết thông tin một con ngựa của chủ sở hữu */
export const getOwnerHorse = async (userId, horseId) =>
  unwrap(await request(`/api/admin/users/${userId}/horses/${horseId}`));

/** [ROLE: Admin] Cập nhật trạng thái phê duyệt của ngựa thuộc chủ sở hữu */
export const updateOwnerHorseStatus = (userId, horseId, payload) =>
  request(`/api/admin/users/${userId}/horses/${horseId}/status`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

/** [ROLE: Admin] Kích hoạt hoặc vô hiệu hóa (Khóa) tài khoản người dùng */
export const setUserActive = (id, isActive) =>
  request(`/api/admin/users/${id}/${isActive ? "reactivate" : "deactivate"}`, {
    method: "POST",
  });

/** [ROLE: Admin] Lấy chi tiết hồ sơ nài ngựa (Jockey) phục vụ xét duyệt cấp phép */
export const getJockeyAdminDetail = async (id) =>
  unwrap(await request(`/api/admin/jockeys/${id}`));

/** [ROLE: Admin] Phê duyệt bằng cấp và cấp phép cho kỵ sĩ tham gia thi đấu */
export const approveJockey = (id) =>
  request(`/api/admin/jockeys/${id}/approve`, {
    method: "POST",
  });

/** [ROLE: Admin] Từ chối hồ sơ đăng ký kỵ sĩ kèm theo lý do cụ thể */
export const rejectJockey = (id, reason) =>
  request(`/api/admin/jockeys/${id}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });

/** [ROLE: Admin] Lấy danh sách tất cả các giải đấu trong hệ thống */
export const getAdminTournaments = async () =>
  unwrap(await request("/api/tournaments"));

/** [ROLE: Admin] Khởi tạo một giải đấu đua ngựa mới */
export const createTournament = (payload) =>
  request("/api/tournaments", {
    method: "POST",
    body: JSON.stringify(payload),
  });

/** [ROLE: Admin] Cập nhật thông tin thông số của giải đấu */
export const updateTournament = (id, payload) =>
  request(`/api/tournaments/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

/** [ROLE: Admin] Xóa một giải đấu chưa khởi tranh khỏi hệ thống */
export const deleteTournament = (id) =>
  request(`/api/tournaments/${id}`, { method: "DELETE" });

/** [ROLE: Admin] Lấy danh sách các vòng đấu thuộc một giải đấu */
export const getTournamentRounds = async (tournamentId) =>
  unwrap(await request(`/api/tournaments/${tournamentId}/rounds`));

/** [ROLE: Admin] Khởi tạo thêm một vòng thi đấu mới cho giải đấu */
export const createRound = (tournamentId, payload) =>
  request(`/api/tournaments/${tournamentId}/rounds`, {
    method: "POST",
    body: JSON.stringify({ ...payload, tournamentId }),
  });

/** [ROLE: Admin] Cập nhật thông tin của vòng thi đấu */
export const updateRound = (roundId, payload) =>
  request(`/api/tournaments/rounds/${roundId}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

/** [ROLE: Admin] Tự động đẩy các ngựa thi đấu đạt thành tích sang vòng kế tiếp */
export const generateNextRound = (roundId, confirmShortfall = false) =>
  request(
    `/api/tournaments/rounds/${roundId}/generate-next${confirmShortfall ? "?confirmShortfall=true" : ""}`,
    { method: "POST" }
  );

/** [ROLE: Admin] Lấy danh sách các trận đua thuộc về một giải đấu */
export const getTournamentRaces = async (tournamentId) =>
  unwrap(await request(`/api/races/management/tournament/${tournamentId}`));

/** [ROLE: Admin] Tạo mới một trận đua ngựa thi đấu */
export const createRace = (payload) =>
  request("/api/races/management", {
    method: "POST",
    body: JSON.stringify(payload),
  });

/** [ROLE: Admin] Phân công lượt thi đấu cho ngựa vào trận đua */
export const assignHorseToRace = (raceId, payload) =>
  request(`/api/races/management/${raceId}/assign-horse`, {
    method: "POST",
    body: JSON.stringify(payload),
  });

/** [ROLE: Admin] Bắt đầu chính thức cuộc đua trên đường đua */
export const startRace = (raceId) =>
  request(`/api/races/management/${raceId}/start`, { method: "POST" });

/** [ROLE: Admin] Mở cổng đăng ký tham gia trận đua */
export const openRaceRegistration = (raceId) =>
  request(`/api/races/management/${raceId}/open-registration`, { method: "POST" });

/** [ROLE: Admin] Đóng cổng đăng ký tham gia trận đua */
export const closeRaceRegistration = (raceId) =>
  request(`/api/races/management/${raceId}/close-registration`, { method: "POST" });

/** [ROLE: Admin] Kết thúc trận đua sau khi các ngựa về đích */
export const endRace = (raceId) =>
  request(`/api/races/management/${raceId}/end`, { method: "POST" });

/** [ROLE: Admin] Hủy bỏ trận đua do sự cố ngoài ý muốn */
export const cancelRace = (raceId) =>
  request(`/api/races/management/${raceId}/cancel`, { method: "POST" });

/** [ROLE: Admin] Lấy danh sách đơn đăng ký tài khoản đang chờ phê duyệt */
export const getPendingRegistrations = async () =>
  unwrap(await request("/api/admin/registrations/pending"));

/** [ROLE: Admin] Phê duyệt đơn đăng ký tài khoản người dùng mới */
export const approveRegistration = (id) =>
  request(`/api/admin/registrations/${id}/approve`, { method: "POST" });

/** [ROLE: Admin] Từ chối đơn đăng ký tài khoản người dùng kèm lý do */
export const rejectRegistration = (id, reason) =>
  request(`/api/admin/registrations/${id}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });

/** [ROLE: Admin] Lấy tất cả các đơn đăng ký tài khoản người dùng */
export const getAllRegistrations = async () =>
  unwrap(await request("/api/admin/registrations"));

/** [ROLE: Admin] Lấy chi tiết đơn đăng ký tài khoản người dùng */
export const getRegistrationDetail = async (id) =>
  unwrap(await request(`/api/admin/registrations/${id}`));

/** [ROLE: Admin] Lấy danh sách các trọng tài đang hoạt động */
export const getActiveReferees = async () =>
  unwrap(await request("/api/referees/active"));

/** [ROLE: Admin] Lấy lịch phân công trọng tài theo từng trận đua */
export const getRaceRefereeAssignments = async (raceId) =>
  unwrap(await request(`/api/referees/race/${raceId}/assignments`));

/** [ROLE: Admin] Phân công trọng tài giám sát một trận đua cụ thể */
export const assignRefereeToRace = (payload) =>
  request("/api/referees/assign", {
    method: "POST",
    body: JSON.stringify(payload),
  });

/** [ROLE: Admin] Lấy danh sách các lượt thi đấu của ngựa đang chờ duyệt */
export const getPendingRaceEntries = async () =>
  unwrap(await request("/api/admin/race-entries/pending"));

/** [ROLE: Admin] Phê duyệt lượt thi đấu của ngựa vào trận đua */
export const approveRaceEntry = (entryId) =>
  request(`/api/admin/race-entries/${entryId}/approve`, { method: "POST" });

/** [ROLE: Admin] Từ chối lượt thi đấu của ngựa vào trận đua kèm lý do */
export const rejectRaceEntry = (entryId, reason) =>
  request(`/api/admin/race-entries/${entryId}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });

/** [ROLE: Admin] Lấy danh sách đơn đăng ký ngựa tham gia giải đấu đang chờ duyệt */
export const getPendingTournamentRegistrations = async () =>
  unwrap(await request("/api/tournament-registrations/pending"));

/** [ROLE: Admin] Lấy bảng tổng hợp tình hình đăng ký ngựa của một giải đấu */
export const getTournamentRegistrationSummary = async (tournamentId) =>
  unwrap(await request(`/api/tournament-registrations/tournament/${tournamentId}/summary`));

/** [ROLE: Admin] Lấy danh sách các con ngựa đã được duyệt tham gia giải đấu */
export const getTournamentApprovedHorses = async (tournamentId) =>
  unwrap(await request(`/api/tournament-registrations/tournament/${tournamentId}/approved-horses`));

/** [ROLE: Admin] Phê duyệt đơn đăng ký con ngựa tham gia giải đấu */
export const approveTournamentRegistration = (id) =>
  request(`/api/tournament-registrations/${id}/approve`, { method: "POST" });

/** [ROLE: Admin] Từ chối đơn đăng ký con ngựa tham gia giải đấu kèm lý do */
export const rejectTournamentRegistration = (id, reason) =>
  request(`/api/tournament-registrations/${id}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });

/** [ROLE: Admin] Phê duyệt kết quả thi đấu do Trọng tài báo cáo */
export const approveRaceResult = (raceId) =>
  request(`/api/admin/races/${raceId}/approve-result`, { method: "POST" });

/** [ROLE: Admin] Từ chối kết quả thi đấu do Trọng tài báo cáo kèm lý do */
export const rejectRaceResult = (raceId, reason) =>
  request(`/api/admin/races/${raceId}/reject-result`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });
