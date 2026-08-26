import { useCallback, useEffect, useMemo, useState } from "react";
import {
  RaceButton,
  RaceDataRow,
  RaceEmptyState,
  RacePanel,
  RaceStatusBadge,
  RaceTabs,
} from "../../components/ui/RaceUi";
import { getRefereeRaceComplaints, respondRaceComplaint } from "../../services/managementApi";
import {
  canRefereeRespond,
  getRaceComplaintStatusDetails,
  getRaceComplaintTypeLabel,
} from "../../utils/raceComplaintDisplay";
import "../RefereeAssignmentPage/RefereeAssignmentPage.css";

const fDate = (v) => (v ? new Date(v).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" }) : "Chưa xác định");

const TABS = [
  { value: "pending", label: "Cần phản hồi" },
  { value: "answered", label: "Đã phản hồi" },
];

export default function RefereeComplaintsPage() {
  const [complaints, setComplaints] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [activeTab, setActiveTab] = useState("pending");
  const [drafts, setDrafts] = useState({});
  const [submitting, setSubmitting] = useState(null);
  const [toast, setToast] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getRefereeRaceComplaints();
      setComplaints(Array.isArray(data) ? data : []);
      setError("");
    } catch (e) {
      setError(e?.message || "Lỗi không xác định");
      setComplaints([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const showToast = useCallback((message) => {
    setToast(message);
    window.setTimeout(() => setToast(null), 3500);
  }, []);

  const counts = useMemo(() => {
    const pending = complaints.filter((c) => canRefereeRespond(c.status)).length;
    return { pending, answered: complaints.length - pending };
  }, [complaints]);

  const visible = useMemo(
    () => complaints.filter((c) => (activeTab === "pending" ? canRefereeRespond(c.status) : !canRefereeRespond(c.status))),
    [complaints, activeTab]
  );

  const submitResponse = async (complaint) => {
    const text = String(drafts[complaint.id] || "").trim();
    if (!text) { showToast("Vui lòng nhập nội dung giải trình."); return; }
    setSubmitting(complaint.id);
    try {
      await respondRaceComplaint(complaint.id, { response: text });
      showToast("Đã gửi giải trình.");
      setDrafts((prev) => ({ ...prev, [complaint.id]: "" }));
      load();
    } catch (e) {
      showToast(e?.message || "Gửi giải trình thất bại.");
    } finally {
      setSubmitting(null);
    }
  };

  const renderRow = (complaint) => {
    const statusDetails = getRaceComplaintStatusDetails(complaint.status);
    const respondable = canRefereeRespond(complaint.status);
    const result = complaint.currentResult;

    return (
      <RaceDataRow
        key={complaint.id}
        title={complaint.raceName || complaint.raceId}
        subtitle={complaint.tournamentName || "Không xác định giải đấu"}
        badge={<RaceStatusBadge variant={statusDetails.variant}>{statusDetails.label}</RaceStatusBadge>}
        meta={[
          { label: "Loại khiếu nại", value: getRaceComplaintTypeLabel(complaint.type) },
          { label: "Yêu cầu lúc", value: fDate(complaint.responseRequestedAt) },
        ]}
        secondaryMeta={[
          result ? { label: "Kết quả tạm thời", value: result.winningHorseName ? `Thắng: ${result.winningHorseName}` : result.resultStatus } : null,
          complaint.evidenceDescription ? { label: "Bằng chứng", value: complaint.evidenceDescription } : null,
        ].filter(Boolean)}
      >
        <p className="rm-data-row__reason"><strong>Nội dung khiếu nại:</strong> {complaint.reason}</p>
        {respondable ? (
          <div className="rm-field" style={{ marginTop: 8 }}>
            <label className="rm-field__label" htmlFor={`rc-response-${complaint.id}`}>Giải trình của bạn</label>
            <textarea
              id={`rc-response-${complaint.id}`}
              className="rm-control"
              rows={3}
              value={drafts[complaint.id] || ""}
              onChange={(e) => setDrafts((prev) => ({ ...prev, [complaint.id]: e.target.value }))}
              placeholder="Mô tả diễn biến và giải thích quyết định của bạn..."
            />
            <div style={{ marginTop: 8 }}>
              <RaceButton
                size="compact"
                loading={submitting === complaint.id}
                disabled={submitting === complaint.id}
                onClick={() => submitResponse(complaint)}
              >
                Gửi giải trình
              </RaceButton>
            </div>
          </div>
        ) : complaint.refereeResponse ? (
          <p className="rm-data-row__reason" style={{ marginTop: 8 }}>
            <strong>Giải trình đã gửi:</strong> {complaint.refereeResponse}
          </p>
        ) : null}
      </RaceDataRow>
    );
  };

  return (
    <main className="ra-page">
      <div className="ra-shell">
        <header className="ra-header">
          <h1>Khiếu nại cần giải trình</h1>
          <p>Các khiếu nại cuộc đua được Admin chuyển cho bạn để giải trình.</p>
        </header>

        <div className="ra-toolbar">
          <RaceTabs
            tabs={TABS.map((t) => ({ ...t, count: counts[t.value] }))}
            activeValue={activeTab}
            onChange={setActiveTab}
            ariaLabel="Lọc khiếu nại cần giải trình"
            idPrefix="rc-tab"
            panelId="rc-panel"
          />
        </div>

        {error && (
          <div className="ra-alert" role="alert">
            Không thể tải dữ liệu từ máy chủ: {error}
          </div>
        )}

        <RacePanel id="rc-panel" role="tabpanel" className="ra-assignment-panel">
          {loading ? (
            <div className="ra-loading" aria-label="Đang tải khiếu nại">
              <div className="ra-skeleton" />
              <div className="ra-skeleton" />
            </div>
          ) : visible.length === 0 ? (
            <RaceEmptyState
              title={activeTab === "pending" ? "Không có khiếu nại cần giải trình" : "Chưa có khiếu nại đã phản hồi"}
              description="Danh sách sẽ cập nhật khi Admin chuyển khiếu nại mới cho bạn."
            />
          ) : (
            <div className="ra-list">{visible.map(renderRow)}</div>
          )}
        </RacePanel>

        {toast && (
          <div className="ra-toast" role="status" aria-live="polite">
            {toast}
          </div>
        )}
      </div>
    </main>
  );
}
