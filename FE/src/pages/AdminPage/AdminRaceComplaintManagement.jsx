import { useEffect, useMemo, useState } from "react";
import {
  RaceButton,
  RaceDataRow,
  RaceEmptyState,
  RaceModalShell,
  RacePanel,
  RaceSelect,
  RaceStatusBadge,
  RaceTabs,
} from "../../components/ui/RaceUi";
import { getRaceComplaints, routeRaceComplaint, ruleRaceComplaint } from "../../services/managementApi";
import ComplaintEvidenceGallery from "../../components/ComplaintEvidenceGallery";
import {
  ADMIN_RACE_COMPLAINT_TABS,
  buildRuleRaceComplaintPayload,
  filterRaceComplaintsByTab,
  getAvailableAdminRaceComplaintActions,
  getDefaultRaceComplaintTab,
  getRaceComplaintStatusDetails,
  getRaceComplaintTabCounts,
  getRaceComplaintTypeLabel,
} from "../../utils/raceComplaintDisplay";

const fDate = (v) => (v ? new Date(v).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" }) : "-");

const tabCopy = {
  intake: { description: "Khiếu nại mới nộp, chưa được xử lý." },
  awaiting: { description: "Đã yêu cầu trọng tài giải trình, đang chờ phản hồi." },
  underReview: { description: "Trọng tài đã giải trình, chờ Admin ra quyết định cuối cùng." },
  resolved: { description: "Đã chấp nhận, bác bỏ, hoặc đã được người nộp rút lại." },
};

export function AdminRaceComplaintManagement() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState("");
  const [activeTab, setActiveTab] = useState(null);
  const [routeModal, setRouteModal] = useState(null); // { complaint, assignmentId }
  const [ruleModal, setRuleModal] = useState(null); // { complaint, outcome, ruling, affectsResult }

  const load = async () => {
    setLoading(true);
    try {
      const data = await getRaceComplaints();
      const nextItems = Array.isArray(data) ? data : [];
      setItems(nextItems);
      setActiveTab((current) => current || getDefaultRaceComplaintTab(getRaceComplaintTabCounts(nextItems)));
    } catch (e) {
      setMsg(e.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const counts = useMemo(() => getRaceComplaintTabCounts(items), [items]);
  const selectedTab = activeTab || "intake";
  const visibleItems = useMemo(() => filterRaceComplaintsByTab(items, selectedTab), [items, selectedTab]);
  const tabs = ADMIN_RACE_COMPLAINT_TABS.map((tab) => ({ value: tab.value, label: tab.label, count: counts[tab.value] }));

  const openRouteModal = (complaint) => setRouteModal({ complaint, assignmentId: "" });
  const openRejectModal = (complaint) => setRuleModal({ complaint, outcome: "Rejected", ruling: "", affectsResult: null });
  const openUpheldModal = (complaint) => setRuleModal({ complaint, outcome: "Upheld", ruling: "", affectsResult: null });
  const openRejectUnderReviewModal = (complaint) => setRuleModal({ complaint, outcome: "Rejected", ruling: "", affectsResult: null });

  const submitRoute = async () => {
    if (!routeModal?.assignmentId) { setMsg("Vui lòng chọn một trọng tài đã xác nhận."); return; }
    try {
      await routeRaceComplaint(routeModal.complaint.id, { refereeAssignmentId: routeModal.assignmentId });
      setMsg("Đã chuyển khiếu nại cho trọng tài giải trình.");
      setRouteModal(null);
      load();
    } catch (e) { setMsg(e.message); }
  };

  const submitRule = async () => {
    if (!ruleModal) return;
    try {
      const payload = buildRuleRaceComplaintPayload(ruleModal.outcome, ruleModal.ruling, ruleModal.affectsResult);
      await ruleRaceComplaint(ruleModal.complaint.id, payload);
      setMsg(
        ruleModal.outcome === "Upheld" && ruleModal.affectsResult === true
          ? "Đã chấp nhận khiếu nại. Kết quả hiện tại cần được chỉnh sửa và gửi lại trước khi xác nhận chính thức."
          : ruleModal.outcome === "Upheld"
            ? "Đã chấp nhận khiếu nại."
            : "Đã từ chối khiếu nại."
      );
      setRuleModal(null);
      load();
    } catch (e) { setMsg(e.message); }
  };

  const renderActions = (complaint) => {
    const status = getRaceComplaintStatusDetails(complaint.status).status;
    const actions = getAvailableAdminRaceComplaintActions(status);
    if (actions.length === 0) return null;
    return (
      <>
        {actions.includes("reject") && (
          <RaceButton size="compact" variant="danger" onClick={() => openRejectModal(complaint)}>Từ chối tiếp nhận</RaceButton>
        )}
        {actions.includes("route") && (
          <RaceButton size="compact" onClick={() => openRouteModal(complaint)}>Yêu cầu trọng tài giải trình</RaceButton>
        )}
        {actions.includes("upheld") && (
          <RaceButton size="compact" onClick={() => openUpheldModal(complaint)}>Chấp nhận khiếu nại</RaceButton>
        )}
        {actions.includes("rejected") && (
          <RaceButton size="compact" variant="danger" onClick={() => openRejectUnderReviewModal(complaint)}>Bác khiếu nại</RaceButton>
        )}
      </>
    );
  };

  const renderRow = (complaint) => {
    const statusDetails = getRaceComplaintStatusDetails(complaint.status);
    const meta = [
      { label: "Loại khiếu nại", value: getRaceComplaintTypeLabel(complaint.type) },
      { label: "Người nộp", value: complaint.filedByName || "-" },
      { label: "Ngày nộp", value: fDate(complaint.createdAt) },
    ];
    const secondaryMeta = [
      complaint.currentResult
        ? { label: "Kết quả hiện tại", value: complaint.currentResult.resultStatus === "Provisional" ? "Tạm thời" : complaint.currentResult.resultStatus }
        : null,
      complaint.assignedRefereeName ? { label: "Trọng tài phụ trách", value: `${complaint.assignedRefereeName} (${complaint.assignedRefereeRole || "-"})` } : null,
      complaint.refereeResponse ? { label: "Giải trình của trọng tài", value: complaint.refereeResponse } : null,
      complaint.ruling ? { label: "Quyết định", value: complaint.ruling } : null,
    ].filter(Boolean);

    return (
      <RaceDataRow
        key={complaint.id}
        title={complaint.raceName || complaint.raceId}
        subtitle={complaint.tournamentName || "Không xác định giải đấu"}
        badge={<RaceStatusBadge variant={statusDetails.variant}>{statusDetails.label}</RaceStatusBadge>}
        meta={meta}
        secondaryMeta={secondaryMeta}
        actions={renderActions(complaint)}
      >
        <p className="rm-data-row__reason">{complaint.reason}</p>
        <ComplaintEvidenceGallery evidence={complaint.evidence} filedByUserId={complaint.filedByUserId} />
      </RaceDataRow>
    );
  };

  return (
    <div>
      <h2>Khiếu nại cuộc đua</h2>
      <p style={{ color: "var(--hr-muted)", marginBottom: 16 }}>
        Tiếp nhận, chuyển cho trọng tài giải trình, và ra quyết định cuối cùng cho khiếu nại cuộc đua.
      </p>
      {msg && <p className="admin-notice">{msg}</p>}

      <RaceTabs
        tabs={tabs}
        activeValue={selectedTab}
        onChange={setActiveTab}
        ariaLabel="Lọc khiếu nại cuộc đua"
        idPrefix="admin-rc-tab"
        panelId="admin-rc-panel"
      />

      <RacePanel
        id="admin-rc-panel"
        role="tabpanel"
        description={tabCopy[selectedTab]?.description}
        style={{ marginTop: 12 }}
      >
        {loading ? (
          <p className="muted">Đang tải khiếu nại...</p>
        ) : visibleItems.length === 0 ? (
          <RaceEmptyState title="Không có khiếu nại trong nhóm này" description="Danh sách sẽ cập nhật khi có khiếu nại mới." />
        ) : (
          <div className="rm-list">{visibleItems.map(renderRow)}</div>
        )}
      </RacePanel>

      {routeModal && (
        <RaceModalShell
          title="Yêu cầu trọng tài giải trình"
          description={`Cuộc đua: ${routeModal.complaint.raceName || routeModal.complaint.raceId}`}
          onClose={() => setRouteModal(null)}
          footer={(
            <>
              <RaceButton variant="ghost" onClick={() => setRouteModal(null)}>Hủy</RaceButton>
              <RaceButton onClick={submitRoute}>Gửi yêu cầu</RaceButton>
            </>
          )}
        >
          {(routeModal.complaint.confirmedRefereeAssignments || []).length === 0 ? (
            <p className="muted">Cuộc đua này chưa có trọng tài nào được xác nhận (Confirmed).</p>
          ) : (
            <RaceSelect
              label="Chọn trọng tài đã xác nhận"
              value={routeModal.assignmentId}
              onChange={(e) => setRouteModal((prev) => ({ ...prev, assignmentId: e.target.value }))}
            >
              <option value="">Chọn trọng tài</option>
              {routeModal.complaint.confirmedRefereeAssignments.map((a) => (
                <option key={a.id} value={a.id}>{a.refereeName || a.refereeId} — {a.role}</option>
              ))}
            </RaceSelect>
          )}
        </RaceModalShell>
      )}

      {ruleModal && (
        <RaceModalShell
          title={ruleModal.outcome === "Upheld" ? "Chấp nhận khiếu nại" : "Từ chối khiếu nại"}
          description={`Cuộc đua: ${ruleModal.complaint.raceName || ruleModal.complaint.raceId}`}
          onClose={() => setRuleModal(null)}
          footer={(
            <>
              <RaceButton variant="ghost" onClick={() => setRuleModal(null)}>Hủy</RaceButton>
              <RaceButton onClick={submitRule}>{ruleModal.outcome === "Upheld" ? "Chấp nhận" : "Từ chối"}</RaceButton>
            </>
          )}
        >
          <div className="rm-field">
            <label className="rm-field__label" htmlFor="admin-rc-ruling">Ghi chú quyết định</label>
            <textarea
              id="admin-rc-ruling"
              className="rm-control"
              rows={4}
              value={ruleModal.ruling}
              onChange={(e) => setRuleModal((prev) => ({ ...prev, ruling: e.target.value }))}
              placeholder={ruleModal.outcome === "Upheld" ? "Mô tả lý do chấp nhận..." : "Lý do từ chối khiếu nại..."}
            />
          </div>
          {ruleModal.outcome === "Upheld" && (
            <div className="rm-field">
              <label className="rm-field__label">Ảnh hưởng kết quả?</label>
              <div style={{ display: "flex", gap: 8 }}>
                <RaceButton
                  type="button"
                  variant={ruleModal.affectsResult === false ? "primary" : "ghost"}
                  size="compact"
                  onClick={() => setRuleModal((prev) => ({ ...prev, affectsResult: false }))}
                >
                  Không
                </RaceButton>
                <RaceButton
                  type="button"
                  variant={ruleModal.affectsResult === true ? "danger" : "ghost"}
                  size="compact"
                  onClick={() => setRuleModal((prev) => ({ ...prev, affectsResult: true }))}
                >
                  Có
                </RaceButton>
              </div>
              {ruleModal.affectsResult === true && (
                <p className="rm-field__message" style={{ marginTop: 8 }}>
                  Kết quả hiện tại sẽ cần được chỉnh sửa và gửi lại trước khi xác nhận chính thức.
                </p>
              )}
            </div>
          )}
        </RaceModalShell>
      )}
    </div>
  );
}
