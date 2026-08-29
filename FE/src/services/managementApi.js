import { request } from "./apiClient";

const unwrap = (r) => r?.data ?? r?.Data ?? r;

// ── Prizes (Giải Thưởng Giải Đấu) ──

/** [ROLE: Admin / Management] Lấy danh sách cơ cấu giải thưởng trong hệ thống */
export const getPrizes = async () => unwrap(await request("/api/management/prizes"));

/** [ROLE: Admin / Spectator] Lấy danh sách giải thưởng theo ID giải đấu */
export const getPrizesByTournament = async (id) => unwrap(await request(`/api/management/prizes/tournament/${id}`));

/** [ROLE: Admin / Spectator] Lấy bảng xếp hạng chung cuộc theo ID giải đấu */
export const getFinalStandingsByTournament = async (id) =>
  unwrap(await request(`/api/management/standings/tournament/${id}`));

/** [ROLE: Admin] Tạo mới giải thưởng cho giải đấu */
export const createPrize = (p) => request("/api/management/prizes", { method: "POST", body: JSON.stringify(p) });

/** [ROLE: Admin] Cập nhật mức giải thưởng giải đấu */
export const updatePrize = (id, p) => request(`/api/management/prizes/${id}`, { method: "PUT", body: JSON.stringify(p) });

/** [ROLE: Admin] Xóa giải thưởng giải đấu */
export const deletePrize = (id) => request(`/api/management/prizes/${id}`, { method: "DELETE" });

/** [ROLE: Admin] Trao thưởng thủ công — cộng tiền thật vào ví các Chủ ngựa đoạt giải theo thứ hạng chung cuộc */
export const distributePrizes = async (tournamentId) =>
  unwrap(await request(`/api/management/prizes/tournament/${tournamentId}/distribute`, { method: "POST" }));

// ── Protests (Kháng Nghị Thi Đấu) ──

/** [ROLE: Admin] Lấy tất cả các kháng nghị thi đấu */
export const getProtests = async () => unwrap(await request("/api/management/protests"));

/** [ROLE: Admin] Lấy danh sách kháng nghị thi đấu đang chờ xử lý */
export const getPendingProtests = async () => unwrap(await request("/api/management/protests/pending"));

/** [ROLE: Người Dùng] Lấy danh sách các kháng nghị thi đấu cá nhân */
export const getMyProtests = async () => unwrap(await request("/api/management/protests/mine"));

/** [ROLE: Chủ Ngựa / Kỵ Sĩ] Tạo mới một đơn kháng nghị kết quả thi đấu */
export const createProtest = (p) => request("/api/management/protests", { method: "POST", body: JSON.stringify(p) });

/** [ROLE: Admin] Chuyển kháng nghị sang trạng thái Đang xem xét */
export const markProtestUnderReview = (id) => request(`/api/management/protests/${id}/under-review`, { method: "POST" });

/** [ROLE: Admin] Phán quyết và đưa ra kết luận kháng nghị thi đấu */
export const ruleProtest = (id, p) => request(`/api/management/protests/${id}/rule`, { method: "POST", body: JSON.stringify(p) });

/** [ROLE: Người Dùng] Rút lại đơn kháng nghị thi đấu đã gửi */
export const withdrawProtest = (id) => request(`/api/management/protests/${id}/withdraw`, { method: "POST" });

// ── Race Complaints (KHIẾU NẠI TRẬN ĐUA & BẰNG CHỨNG) ──

/** [ROLE: Admin / Trọng Tài] Lấy danh sách khiếu nại trận đua (Lọc theo trạng thái) */
export const getRaceComplaints = async (status) => unwrap(await request(`/api/management/race-complaints${status ? `?status=${status}` : ""}`));

/** [ROLE: Người Dùng] Lấy danh sách khiếu nại trận đua của bản thân */
export const getMyRaceComplaints = async () => unwrap(await request("/api/management/race-complaints/mine"));

/** [ROLE: Trọng Tài] Lấy danh sách khiếu nại trận đua được phân công xử lý */
export const getRefereeRaceComplaints = async () => unwrap(await request("/api/management/race-complaints/referee"));

/** [ROLE: Người Dùng] Lấy danh sách các trận đua đủ điều kiện nộp khiếu nại */
export const getEligibleRaceComplaintRaces = async () => unwrap(await request("/api/management/race-complaints/eligible-races"));

/** [ROLE: Người Dùng] Tạo mới đơn khiếu nại kết quả trận đua */
export const createRaceComplaint = (p) => request("/api/management/race-complaints", { method: "POST", body: JSON.stringify(p) });

/** [ROLE: Admin] Điều hướng khiếu nại cho Trọng tài xử lý */
export const routeRaceComplaint = (id, p) => request(`/api/management/race-complaints/${id}/route`, { method: "POST", body: JSON.stringify(p) });

/** [ROLE: Trọng Tài] Gửi giải trình/báo cáo về khiếu nại trận đua */
export const respondRaceComplaint = (id, p) => request(`/api/management/race-complaints/${id}/respond`, { method: "POST", body: JSON.stringify(p) });

/** [ROLE: Admin] Phán quyết và chốt kết quả khiếu nại trận đua */
export const ruleRaceComplaint = (id, p) => request(`/api/management/race-complaints/${id}/rule`, { method: "POST", body: JSON.stringify(p) });

/** [ROLE: Người Dùng] Rút đơn khiếu nại trận đua */
export const withdrawRaceComplaint = (id) => request(`/api/management/race-complaints/${id}/withdraw`, { method: "POST" });

/** [ROLE: Người Dùng] Tải lên tập tin hình ảnh/video bằng chứng khiếu nại */
export const uploadRaceComplaintEvidence = (id, file) => {
  const formData = new FormData();
  formData.append("file", file);
  return request(`/api/management/race-complaints/${id}/evidence`, { method: "POST", body: formData });
};

/** [ROLE: Người Dùng] Xóa tập tin bằng chứng khiếu nại */
export const deleteRaceComplaintEvidence = (id, evidenceId) =>
  request(`/api/management/race-complaints/${id}/evidence/${evidenceId}`, { method: "DELETE" });

// ── Horse Transfers (Chuyển Nhượng Ngựa Đua) ──

/** [ROLE: Admin] Lấy tất cả danh sách chuyển nhượng ngựa */
export const getTransfers = async () => unwrap(await request("/api/management/transfers"));

/** [ROLE: Admin] Lấy danh sách chuyển nhượng ngựa đang chờ duyệt */
export const getPendingTransfers = async () => unwrap(await request("/api/management/transfers/pending"));

/** [ROLE: Chủ Ngựa] Yêu cầu chuyển nhượng quyền sở hữu ngựa đua */
export const createTransfer = (p) => request("/api/management/transfers", { method: "POST", body: JSON.stringify(p) });

/** [ROLE: Admin] Phê duyệt yêu cầu chuyển nhượng quyền sở hữu ngựa */
export const approveTransfer = (id, n) => request(`/api/management/transfers/${id}/approve`, { method: "POST", body: JSON.stringify(n || {}) });

/** [ROLE: Admin] Từ chối yêu cầu chuyển nhượng quyền sở hữu ngựa */
export const rejectTransfer = (id, reason) => request(`/api/management/transfers/${id}/reject`, { method: "POST", body: JSON.stringify({ reason }) });

// ── Contracts (Hợp Đồng Chủ Ngựa & Kỵ Sĩ) ──

/** [ROLE: Management] Lấy danh sách hợp đồng hợp tác thi đấu */
export const getContracts = async () => unwrap(await request("/api/management/contracts"));

/** [ROLE: Owner / Jockey] Khởi tạo hợp đồng thuê kỵ sĩ lái ngựa */
export const createContract = (p) => request("/api/management/contracts", { method: "POST", body: JSON.stringify(p) });

/** [ROLE: Chủ Ngựa] Ký tên xác nhận hợp đồng hợp tác */
export const signContractOwner = (id) => request(`/api/management/contracts/${id}/sign-owner`, { method: "POST" });

/** [ROLE: Kỵ Sĩ] Ký tên xác nhận hợp đồng hợp tác */
export const signContractJockey = (id) => request(`/api/management/contracts/${id}/sign-jockey`, { method: "POST" });

// ── Injury Records (Hồ Sơ Chấn Thương Ngựa Đua) ──

/** [ROLE: Trọng Tài / Bác Sĩ Thú Y] Lấy danh sách hồ sơ chấn thương ngựa */
export const getInjuries = async () => unwrap(await request("/api/management/injuries"));

/** [ROLE: Trọng Tài / Chủ Ngựa] Lấy lịch sử chấn thương của một con ngựa */
export const getInjuriesByHorse = async (id) => unwrap(await request(`/api/management/injuries/horse/${id}`));

/** [ROLE: Trọng Tài] Ghi nhận trường hợp chấn thương của con ngựa */
export const createInjury = (p) => request("/api/management/injuries", { method: "POST", body: JSON.stringify(p) });

/** [ROLE: Trọng Tài / Bác Sĩ] Đánh dấu con ngựa đã bình phục chấn thương */
export const markRecovered = (id) => request(`/api/management/injuries/${id}/recover`, { method: "POST" });

/** [ROLE: Trọng Tài] Đạt điều kiện và cấp phép cho ngựa trở lại thi đấu */
export const clearToRace = (id) => request(`/api/management/injuries/${id}/clear`, { method: "POST" });
