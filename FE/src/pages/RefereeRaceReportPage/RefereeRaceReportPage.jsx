import { useEffect, useState } from "react";
import { getMyAssignments } from "../../services/refereeAssignmentApi";
import { createReport, getRaceReport, getRaceEntries, submitRaceResult } from "../../services/refereeApi";
import "./RefereeRaceReportPage.css";

const REPORT_TYPES = [
  {
    id: "post-race",
    label: "Báo cáo sau cuộc đua",
    sub: "Chi tiết diễn biến và kết quả",
    icon: "🏁",
  },
  {
    id: "health",
    label: "Báo cáo sức khỏe",
    sub: "Tổng hợp tình trạng ngựa",
    icon: "🏥",
  },
  {
    id: "violation",
    label: "Báo cáo vi phạm",
    sub: "Các vi phạm trong cuộc đua",
    icon: "⚠️",
  },
];

const MONTH_LABELS = [
  "T1",
  "T2",
  "T3",
  "T4",
  "T5",
  "T6",
];

const MONTH_FULL = [
  "Tháng 1",
  "Tháng 2",
  "Tháng 3",
  "Tháng 4",
  "Tháng 5",
  "Tháng 6",
];

// Race progress (RaceStatus) — event lifecycle only.
const RACE_STATUS_LABEL = {
  Scheduled: "Đã lên lịch",
  RegistrationOpen: "Chuẩn bị",
  RegistrationClosed: "Chuẩn bị",
  InProgress: "Đang đua",
  Finished: "Đã kết thúc",
  Cancelled: "Đã hủy",
};

// Result status (RaceResultStatus) — separate concern from race progress.
const RESULT_STATUS_LABEL = {
  Provisional: "Tạm thời (chờ duyệt)",
  Official: "Chính thức",
};

export default function RefereeRaceReportPage() {
  const [assignments, setAssignments] = useState([]);
  const [selectedRaceId, setSelectedRaceId] = useState("");
  const [existingReport, setExistingReport] = useState(null);
  const [form, setForm] = useState({ content: "", notes: "" });
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [msg, setMsg] = useState("");
  const [reportType, setReportType] = useState("post-race");

  // Recent reports history (built from submissions during session + loaded)
  const [recentReports, setRecentReports] = useState([]);
  // Submit result states
  const [resultEntries, setResultEntries] = useState([]);
  const [resultWinningHorseId, setResultWinningHorseId] = useState("");
  const [resultSubmitting, setResultSubmitting] = useState(false);
  const [resultMsg, setResultMsg] = useState("");

  useEffect(() => {
    getMyAssignments()
      .then((d) => {
        const list = Array.isArray(d) ? d : [];
        console.log("🔍 All assignments:", list);
        // Filter only confirmed assignments (referee đã chấp nhận lời mời)
        const confirmedAssignments = list.filter((a) => {
          const status = (a.status || a.Status || "").toLowerCase();
          console.log(`Assignment ${a.raceId || a.RaceId}: status=${status}`);
          return status === "confirmed";
        });
        console.log("✅ Confirmed assignments:", confirmedAssignments);
        setAssignments(confirmedAssignments);
        if (confirmedAssignments.length > 0)
          setSelectedRaceId(
            confirmedAssignments[0].raceId || confirmedAssignments[0].RaceId || ""
          );
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!selectedRaceId) {
      setExistingReport(null);
      return;
    }
    getRaceReport(selectedRaceId)
      .then((d) => {
        setExistingReport(d);
        if (d && !recentReports.find((r) => r.raceId === selectedRaceId)) {
          setRecentReports((prev) => [
            { ...d, raceId: selectedRaceId, createdAt: new Date().toISOString() },
            ...prev,
          ].slice(0, 10));
        }
      })
      .catch(() => setExistingReport(null));
  }, [selectedRaceId]);

  // Load race entries for result submission
  useEffect(() => {
    if (!selectedRaceId) {
      setResultEntries([]);
      return;
    }
    getRaceEntries(selectedRaceId)
      .then((d) => {
        const list = Array.isArray(d) ? d : [];
        setResultEntries(list);
      })
      .catch(() => setResultEntries([]));
  }, [selectedRaceId]);

  // Chart data — monthly count (simulated from recent reports)
  const [chartData, setChartData] = useState(() => {
    const now = new Date();
    return Array.from({ length: 6 }, (_, i) => {
      // eslint-disable-next-line no-unused-vars
      const m = new Date(now.getFullYear(), now.getMonth() - 5 + i, 1);
      return {
        month: MONTH_LABELS[i],
        label: MONTH_FULL[i],
        count: Math.floor(Math.random() * 8) + 1,
      };
    });
  });

  // Update chart when reports change
  useEffect(() => {
    if (recentReports.length === 0) return;
    const counts = [0, 0, 0, 0, 0, 0];
    const now = new Date();
    recentReports.forEach((r) => {
      const d = r.createdAt ? new Date(r.createdAt) : new Date();
      const monthDiff =
        (now.getFullYear() - d.getFullYear()) * 12 +
        now.getMonth() -
        d.getMonth();
      const idx = 5 - Math.min(monthDiff, 5);
      if (idx >= 0 && idx < 6) counts[idx]++;
    });
    setChartData((prev) =>
      prev.map((p, i) => ({ ...p, count: Math.max(p.count, counts[i]) }))
    );
  }, [recentReports]);

  const maxCount = Math.max(...chartData.map((d) => d.count), 1);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.content.trim()) {
      setMsg("Vui lòng nhập nội dung báo cáo.");
      return;
    }
    setSubmitting(true);
    setMsg("");
    try {
      await createReport({
        raceId: selectedRaceId,
        details: form.content,
        incidents: form.notes,
      });
      setMsg("Đã gửi báo cáo thành công!");
      const newReport = {
        raceId: selectedRaceId,
        details: form.content,
        incidents: form.notes,
        createdAt: new Date().toISOString(),
      };
      setRecentReports((prev) => [newReport, ...prev].slice(0, 10));
      setForm({ content: "", notes: "" });
      getRaceReport(selectedRaceId)
        .then((d) => setExistingReport(d))
        .catch(() => {});
    } catch (e) {
      setMsg("Lỗi: " + (e.message || ""));
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmitResult = async (e) => {
    e.preventDefault();
    if (!resultWinningHorseId) {
      setResultMsg("Vui lòng chọn ngựa thắng cuộc.");
      return;
    }
    setResultSubmitting(true);
    setResultMsg("");
    try {
      await submitRaceResult(selectedRaceId, { winningHorseId: resultWinningHorseId });
      setResultMsg("✅ Kết quả đã được gửi thành công! Admin sẽ duyệt sau.");
      setResultWinningHorseId("");
    } catch (err) {
      setResultMsg("❌ Lỗi: " + (err.message || ""));
    } finally {
      setResultSubmitting(false);
    }
  };

  const currentRaceName =
    assignments.find(
      (a) => (a.raceId || a.RaceId) === selectedRaceId
    )?.raceName ||
    assignments.find(
      (a) => (a.raceId || a.RaceId) === selectedRaceId
    )?.RaceName ||
    "Cuộc đua đã chọn";

  const currentAssignment = assignments.find(
    (a) => (a.raceId || a.RaceId) === selectedRaceId
  );
  const currentRaceStatus =
    currentAssignment?.raceStatus || currentAssignment?.RaceStatus || "";
  const currentResultStatus =
    currentAssignment?.resultStatus || currentAssignment?.ResultStatus || "";
  const currentRejectedReason =
    currentAssignment?.rejectedReason || currentAssignment?.RejectedReason || "";
  // Submission only happens after the race has Finished (per the locked
  // lifecycle, the event concludes before any result is submitted), and only
  // while no result exists yet or the existing one is still Provisional —
  // an Official result cannot be resubmitted through this form.
  const canSubmitResult =
    currentRaceStatus === "Finished" && currentResultStatus !== "Official";

  return (
    <div className="rr-wrap">
      {/* ── Header ── */}
      <div className="rr-header">
        <div>
          <h1 className="rr-title">Báo cáo cuộc đua</h1>
          <p className="rr-sub">Tạo và theo dõi báo cáo cho các cuộc đua được phân công</p>
        </div>
      </div>

      {/* ── Report Type Selector ── */}
      <div className="rr-type-grid">
        {REPORT_TYPES.map((rt) => (
          <div
            key={rt.id}
            className={`rr-type-card${reportType === rt.id ? " rr-type-card--active" : ""}`}
            onClick={() => setReportType(rt.id)}
          >
            <span className="rr-type-icon">{rt.icon}</span>
            <div>
              <span className="rr-type-label">{rt.label}</span>
              <span className="rr-type-sub">{rt.sub}</span>
            </div>
          </div>
        ))}
      </div>

      {/* ── Two-column Layout ── */}
      <div className="rr-grid">
        {/* LEFT — Form */}
        <div className="rr-left">
          {/* Race Selector */}
          <div className="rr-card rr-card-dark">
            <h3 className="rr-card-title">Chọn cuộc đua</h3>
            {loading ? (
              <p className="rr-muted">Đang tải...</p>
            ) : assignments.length === 0 ? (
              <p className="rr-muted">Chưa có phân công nào.</p>
            ) : (
              <select
                value={selectedRaceId}
                onChange={(e) => setSelectedRaceId(e.target.value)}
                className="rr-select"
              >
                <option value="">-- Chọn --</option>
                {assignments.map((a) => (
                  <option
                    key={a.id || a.Id || a.raceId}
                    value={a.raceId || a.RaceId}
                  >
                    {a.raceName ||
                      a.RaceName ||
                      a.matchName ||
                      a.MatchName ||
                      "Cuộc đua"}
                  </option>
                ))}
              </select>
            )}
          </div>

          {existingReport ? (
            <div className="rr-card rr-card-dark">
              <h3 className="rr-card-title">Báo cáo đã tồn tại</h3>
              <div className="rr-existing">
                <div className="rr-existing-row">
                  <span className="rr-existing-label">Nội dung</span>
                  <span>{existingReport.details || existingReport.Details || "-"}</span>
                </div>
                {(existingReport.incidents || existingReport.Incidents) && (
                  <div className="rr-existing-row">
                    <span className="rr-existing-label">Sự cố</span>
                    <span>{existingReport.incidents || existingReport.Incidents}</span>
                  </div>
                )}
              </div>
            </div>
          ) : selectedRaceId ? (
            <form className="rr-card rr-card-dark rr-form" onSubmit={handleSubmit}>
              <h3 className="rr-card-title">
                Tạo báo cáo mới — {currentRaceName}
              </h3>
              {msg && (
                <div className={`rr-msg ${msg.includes("Lỗi") ? "rr-msg--err" : "rr-msg--ok"}`}>
                  {msg}
                </div>
              )}
              <div className="rr-field">
                <label>Nội dung báo cáo</label>
                <textarea
                  value={form.content}
                  onChange={(e) =>
                    setForm((p) => ({ ...p, content: e.target.value }))
                  }
                  placeholder="Mô tả diễn biến cuộc đua, kết quả, các sự kiện đáng chú ý..."
                  rows={5}
                />
              </div>
              <div className="rr-field">
                <label>Sự cố / ghi chú thêm</label>
                <textarea
                  value={form.notes}
                  onChange={(e) =>
                    setForm((p) => ({ ...p, notes: e.target.value }))
                  }
                  placeholder="Các sự cố xảy ra trong cuộc đua (không bắt buộc)..."
                  rows={3}
                />
              </div>
              <button
                type="submit"
                className="rr-submit-btn"
                disabled={submitting}
              >
                {submitting ? "Đang gửi..." : "Gửi báo cáo"}
              </button>
            </form>
          ) : null}

          {/* ── Submit Race Result ── */}
          {selectedRaceId && (
            <form className="rr-card rr-card-dark rr-form" onSubmit={handleSubmitResult} style={{ marginTop: 16, borderTop: "3px solid #e6a54a" }}>
              <h3 className="rr-card-title" style={{ color: "#7C2D12" }}>
                🏁 Chốt kết quả cuộc đua
              </h3>
              {resultMsg && (
                <div className={`rr-msg ${resultMsg.includes("❌") ? "rr-msg--err" : "rr-msg--ok"}`}>
                  {resultMsg}
                </div>
              )}
              <p className="rr-muted" style={{ marginBottom: 12 }}>
                Chọn ngựa thắng cuộc. Kết quả sẽ được gửi lên admin duyệt.
              </p>
              {currentRaceStatus && (
                <p className="rr-muted" style={{ marginBottom: 4, fontWeight: 600, color: canSubmitResult ? "#166534" : "#b45309" }}>
                  Trạng thái cuộc đua: {RACE_STATUS_LABEL[currentRaceStatus] ?? currentRaceStatus}
                  {currentResultStatus && ` · Kết quả: ${RESULT_STATUS_LABEL[currentResultStatus] ?? currentResultStatus}`}
                  {!canSubmitResult && " — chưa thể nộp kết quả lúc này"}
                </p>
              )}
              {currentRejectedReason && currentResultStatus !== "Official" && (
                <p className="rr-muted" style={{ marginBottom: 12, color: "#b45309" }}>
                  ⚠️ Kết quả trước đã bị từ chối: {currentRejectedReason} — vui lòng nộp lại.
                </p>
              )}
              <div className="rr-field">
                <label>Ngựa thắng cuộc</label>
                <select
                  value={resultWinningHorseId}
                  onChange={(e) => setResultWinningHorseId(e.target.value)}
                  className="rr-select"
                  style={{ width: "100%", padding: 10, borderRadius: 8, fontSize: 14 }}
                  disabled={!canSubmitResult}
                >
                  <option value="">-- Chọn ngựa thắng --</option>
                  {resultEntries.map((entry) => (
                    <option key={entry.horseId || entry.HorseId} value={entry.horseId || entry.HorseId}>
                      🐎 {entry.horseName || entry.HorseName} — Tỉ lệ: {(entry.odds || entry.Odds || 1).toFixed(2)}x
                    </option>
                  ))}
                </select>
              </div>
              <button
                type="submit"
                className="rr-submit-btn"
                disabled={resultSubmitting || !resultWinningHorseId || !canSubmitResult}
                style={{ background: resultWinningHorseId ? "#e6a54a" : undefined }}
              >
                {resultSubmitting ? "Đang gửi..." : "📨 Gửi kết quả"}
              </button>
            </form>
          )}
        </div>

        {/* RIGHT — Chart + Recent Reports */}
        <div className="rr-right">
          {/* Monthly Chart */}
          <div className="rr-card rr-card-dark">
            <h3 className="rr-card-title">Phân bố báo cáo</h3>
            <p className="rr-card-sub">6 tháng gần nhất</p>
            <div className="rr-chart">
              {chartData.map((item) => (
                <div key={item.month} className="rr-chart-col">
                  <span className="rr-chart-val">{item.count}</span>
                  <div className="rr-chart-bar-wrap">
                    <div
                      className="rr-chart-bar"
                      style={{
                        height: `${(item.count / maxCount) * 100}%`,
                      }}
                    />
                  </div>
                  <span className="rr-chart-label">{item.month}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Recent Reports */}
          <div className="rr-card rr-card-dark">
            <h3 className="rr-card-title">Báo cáo gần đây</h3>
            {recentReports.length === 0 ? (
              <p className="rr-muted">Chưa có báo cáo nào được gửi.</p>
            ) : (
              <div className="rr-recent-list">
                {recentReports.map((r, idx) => (
                  <div key={idx} className="rr-recent-item">
                    <div className="rr-recent-head">
                      <span className="rr-recent-race">
                        {r.raceName || `Cuộc đua`}
                      </span>
                      <span className="rr-recent-date">
                        {r.createdAt
                          ? new Date(r.createdAt).toLocaleDateString("vi-VN")
                          : "-"}
                      </span>
                    </div>
                    <p className="rr-recent-excerpt">
                      {r.details?.slice(0, 100)}
                      {(r.details?.length || 0) > 100 ? "..." : ""}
                    </p>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
