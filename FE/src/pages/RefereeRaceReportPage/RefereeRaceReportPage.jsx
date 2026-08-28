import { useEffect, useMemo, useState } from "react";
import { getMyAssignments } from "../../services/refereeAssignmentApi";
import { createReport, getRaceReport, getRaceEntries, getRaceHealthChecks, getRaceViolations, submitRaceResult } from "../../services/refereeApi";
import { getLatestHealthCheck } from "../../utils/healthCheckDisplay";
import { request } from "../../services/apiClient"; 
import "./RefereeRaceReportPage.css";

/* ── SVG: empty state (no race selected) ── */
function ReportEmptySVG() {
  return (
    <svg className="rr-empty-svg" viewBox="0 0 240 150" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="70" y="20" width="100" height="110" rx="8" stroke="currentColor" strokeWidth="2" opacity="0.3" fill="none" />
      <line x1="90" y1="45" x2="150" y2="45" stroke="currentColor" strokeWidth="2" opacity="0.3" strokeLinecap="round" />
      <line x1="90" y1="65" x2="130" y2="65" stroke="currentColor" strokeWidth="2" opacity="0.3" strokeLinecap="round" />
      <line x1="90" y1="85" x2="140" y2="85" stroke="currentColor" strokeWidth="2" opacity="0.3" strokeLinecap="round" />
      <path d="M140 100 L160 120 M160 100 L140 120" stroke="#f2d28b" strokeWidth="3" opacity="0.8" strokeLinecap="round" />
      <circle cx="150" cy="110" r="24" stroke="#f2d28b" strokeWidth="2" opacity="0.4" fill="none" />
    </svg>
  );
}

const REPORT_TYPES = [
  { id: "post-race", label: "Báo cáo sau cuộc đua", sub: "Chi tiết diễn biến và kết quả", icon: "🏁" },
  { id: "health", label: "Báo cáo sức khỏe", sub: "Tổng hợp tình trạng ngựa", icon: "🏥" },
  { id: "violation", label: "Báo cáo vi phạm", sub: "Các vi phạm trong cuộc đua", icon: "⚠️" },
];

const MONTH_LABELS = ["T1", "T2", "T3", "T4", "T5", "T6"];
const MONTH_FULL = ["Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6"];

const RACE_STATUS_LABEL = {
  Scheduled: "Đã lên lịch",
  RegistrationOpen: "Chuẩn bị",
  RegistrationClosed: "Chuẩn bị",
  InProgress: "Đang đua",
  Finished: "Đã kết thúc",
  Cancelled: "Đã hủy",
};

export default function RefereeRaceReportPage() {
  const [assignments, setAssignments] = useState([]);
  const [selectedRaceId, setSelectedRaceId] = useState("");
  const [existingReport, setExistingReport] = useState(null);
  const [form, setForm] = useState({ content: "", notes: "" });
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [msg, setMsg] = useState("");
  const [reportType, setReportType] = useState("post-race");

  const [recentReports, setRecentReports] = useState([]);
  const [resultEntries, setResultEntries] = useState([]);
  const [healthChecks, setHealthChecks] = useState([]);
  
  const [timeData, setTimeData] = useState({});
  const [resultSubmitting, setResultSubmitting] = useState(false);
  const [resultMsg, setResultMsg] = useState("");

  const loadAssignments = async (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    else setLoading(true);
    try {
      const d = await getMyAssignments();
      const list = Array.isArray(d?.data) ? d.data : Array.isArray(d) ? d : [];
      const confirmedAssignments = list.filter((a) => {
        const status = (a.status || a.Status || "").toLowerCase();
        return status === "confirmed";
      });
      setAssignments(confirmedAssignments);
      if (!isRefresh && confirmedAssignments.length > 0) {
        setSelectedRaceId(confirmedAssignments[0].raceId || confirmedAssignments[0].RaceId || "");
      }
    } catch {
      // keep existing list on refresh failure
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    loadAssignments();
  }, []);

  useEffect(() => {
    if (!selectedRaceId) {
      setExistingReport(null);
      return;
    }
    getRaceReport(selectedRaceId)
      .then((d) => {
        setExistingReport(d);
        if (d) {
          setForm({ content: d.details || d.Details || "", notes: d.incidents || d.Incidents || "" });
          if (!recentReports.find((r) => r.raceId === selectedRaceId)) {
            setRecentReports((prev) => [{ ...d, raceId: selectedRaceId, createdAt: new Date().toISOString() }, ...prev].slice(0, 10));
          }
        } else {
          setForm({ content: "", notes: "" });
        }
      })
      .catch(() => { setExistingReport(null); setForm({ content: "", notes: "" }); });
  }, [selectedRaceId]);

  // Load race entries + health checks + violations + EXISTING RESULTS cho Auto-sync
  useEffect(() => {
    setTimeData({});
    if (!selectedRaceId) {
      setResultEntries([]);
      setHealthChecks([]);
      return;
    }
    Promise.all([
      getRaceEntries(selectedRaceId).catch(() => []),
      getRaceHealthChecks(selectedRaceId).catch(() => []),
      getRaceViolations(selectedRaceId).catch(() => []),
      request(`/api/races/${selectedRaceId}/result`).catch(() => null), // Kéo KQ cũ về
    ]).then(([entriesData, hcData, violsData, resData]) => {
      const entriesList = Array.isArray(entriesData?.data) ? entriesData.data : Array.isArray(entriesData) ? entriesData : [];
      setResultEntries(entriesList);
      setHealthChecks(Array.isArray(hcData?.data) ? hcData.data : Array.isArray(hcData) ? hcData : []);
      
      const violsList = Array.isArray(violsData?.data) ? violsData.data : Array.isArray(violsData) ? violsData : [];
      const autoPenaltyMap = {};
      const autoStatusMap = {};

      violsList.forEach(v => {
        const hId = v.horseId || v.HorseId;
        const pType = v.penaltyType || v.PenaltyType;
        const pSec = v.penaltyTimeSeconds || v.PenaltyTimeSeconds || 0;

        if (pType === "DSQ") {
          autoStatusMap[hId] = "DSQ";
        } else if (pType === "TimePenalty") {
          autoPenaltyMap[hId] = (autoPenaltyMap[hId] || 0) + Number(pSec);
        }
      });

      // Parse JSON Kết quả cũ
      const fetchedResult = resData?.data || resData;
      const previousRankings = fetchedResult?.rankings || fetchedResult?.Rankings || [];

      const initialData = {};
      entriesList.forEach(e => {
        const hId = e.horseId || e.HorseId;
        const syncPenalty = autoPenaltyMap[hId] ? String(autoPenaltyMap[hId]) : "0";
        
        // Tìm xem ngựa này đã được nhập KQ lần trước chưa
        const prevRank = previousRankings.find(r => (r.horseId || r.HorseId) === hId);

        if (prevRank) {
          const status = prevRank.status || prevRank.Status || "Completed";
          let mm = "", ss = "", ms = "";
          const t = prevRank.timeTaken || prevRank.TimeTaken; // Thời gian này ĐÃ BỊ CỘNG PHẠT ở backend

        if (status === "Completed" && t != null) {
            let baseTimeMs = Math.round(t * 1000);

            mm = Math.floor(baseTimeMs / 60000).toString();
            ss = Math.floor((baseTimeMs % 60000) / 1000).toString();
            ms = (baseTimeMs % 1000).toString();
          }

          let finalStatus = status;
          let hasDSQViolation = false;

          if (autoStatusMap[hId] === "DSQ") {
             finalStatus = "DSQ"; 
             hasDSQViolation = true;
          } else if (finalStatus === "DSQ") {
             finalStatus = "Completed"; 
          }

          initialData[hId] = { status: finalStatus, mm, ss, ms, penalty: syncPenalty, isAutoDSQ: hasDSQViolation };
        } else {
          // Chưa có KQ cũ
          const syncStatus = autoStatusMap[hId] || "Completed";
          initialData[hId] = { status: syncStatus, mm: "", ss: "", ms: "", penalty: syncPenalty };
        }
      });
      setTimeData(initialData);
    });
  }, [selectedRaceId]);

  const failedHealthCheckHorseIds = useMemo(() => {
    const byHorse = new Map();
    healthChecks.forEach((c) => {
      const horseId = c.horseId || c.HorseId;
      const list = byHorse.get(horseId) || [];
      list.push(c);
      byHorse.set(horseId, list);
    });
    const failed = new Set();
    byHorse.forEach((checks, horseId) => {
      const latest = getLatestHealthCheck(checks);
      if (latest && (latest.status || latest.Status) === "Failed") failed.add(horseId);
    });
    return failed;
  }, [healthChecks]);

    const rankings = useMemo(() => {
    const arr = Object.keys(timeData).map(horseId => {
      const data = timeData[horseId];
      let totalMs = Infinity;
      let baseMs = null;
      let displayTime = "-";

      if (data.status === "Completed") {
        const m = parseInt(data.mm || 0);
        const s = parseInt(data.ss || 0);
        const ms = parseInt(data.ms || 0);
        const p = parseInt(data.penalty || 0);
        
        if (m > 0 || s > 0 || ms > 0) {
          baseMs = (m * 60 * 1000) + (s * 1000) + ms;
          totalMs = baseMs + (p * 1000); 
          const finalM = Math.floor(totalMs / 60000);
          const finalS = Math.floor((totalMs % 60000) / 1000);
          const finalMs = totalMs % 1000;
          displayTime = `${finalM.toString().padStart(2, '0')}:${finalS.toString().padStart(2, '0')}.${finalMs.toString().padStart(3, '0')}`;
        }
      }
      return { horseId, status: data.status, totalMs, baseMs, displayTime }; 
    });

    arr.sort((a, b) => {
      if (a.status === "Completed" && b.status === "Completed") return a.totalMs - b.totalMs;
      if (a.status === "Completed") return -1;
      if (b.status === "Completed") return 1;
      if (a.status === "DNF" && b.status === "DSQ") return -1;
      if (a.status === "DSQ" && b.status === "DNF") return 1;
      return 0;
    });

    const result = {};
    let currentRank = 1;
    for (let i = 0; i < arr.length; i++) {
      if (arr[i].status !== "Completed" || arr[i].totalMs === Infinity) {     
        let textRank = "-";
        if (arr[i].status === "DNF") textRank = "Bỏ cuộc";
        else if (arr[i].status === "DSQ") textRank = "Bị loại";
        result[arr[i].horseId] = { rank: textRank, displayTime: "-", totalMs: null, baseMs: null };
      } else {
        if (i > 0 && arr[i].totalMs === arr[i-1].totalMs && arr[i].status === "Completed") {
          result[arr[i].horseId] = { rank: currentRank, displayTime: arr[i].displayTime, totalMs: arr[i].totalMs, baseMs: arr[i].baseMs };
        } else {
          currentRank = i + 1;
          result[arr[i].horseId] = { rank: currentRank, displayTime: arr[i].displayTime, totalMs: arr[i].totalMs, baseMs: arr[i].baseMs };
        }
      }
    }
    return result;
  }, [timeData]);

  const currentAssignment = assignments.find((a) => (a.raceId || a.RaceId) === selectedRaceId);
  const currentRaceStatus = currentAssignment?.raceStatus || currentAssignment?.RaceStatus || "";
  const currentResultStatus = (currentAssignment?.resultStatus || currentAssignment?.ResultStatus || "").toLowerCase();
  const currentRejectedReason = currentAssignment?.rejectedReason || currentAssignment?.RejectedReason || "";
  const isPendingApproval = currentResultStatus === "provisional" && !currentRejectedReason;
  const isOfficial = currentResultStatus === "official";
  const isReportOfficial = existingReport && (existingReport.isOfficialReport || !existingReport.isDraft);
  const canEditRanking = currentRaceStatus === "Finished" && !isPendingApproval && !isOfficial;

  const handleTimeChange = (horseId, field, value) => {
    if (!canEditRanking) return; // Khóa
    if (value !== "" && !/^\d+$/.test(value)) return;
    if (field === 'ss' && parseInt(value) >= 60) return;
    if (field === 'ms' && value.length > 3) return;
    setTimeData(prev => ({ ...prev, [horseId]: { ...prev[horseId], [field]: value } }));
  };

  const handleStatusChange = (horseId, value) => {
    if (!canEditRanking) return;
    setTimeData(prev => {
      const currentHorseData = prev[horseId] || {};
      return { 
        ...prev, 
        [horseId]: { 
          ...currentHorseData, 
          status: value
        } 
      };
    });
  };

  const [chartData, setChartData] = useState(() => {
    const now = new Date();
    return Array.from({ length: 6 }, (_, i) => {
      return { month: MONTH_LABELS[i], label: MONTH_FULL[i], count: Math.floor(Math.random() * 8) + 1 };
    });
  });

  useEffect(() => {
    if (recentReports.length === 0) return;
    const counts = [0, 0, 0, 0, 0, 0];
    const now = new Date();
    recentReports.forEach((r) => {
      const d = r.createdAt ? new Date(r.createdAt) : new Date();
      const monthDiff = (now.getFullYear() - d.getFullYear()) * 12 + now.getMonth() - d.getMonth();
      const idx = 5 - Math.min(monthDiff, 5);
      if (idx >= 0 && idx < 6) counts[idx]++;
    });
    setChartData((prev) => prev.map((p, i) => ({ ...p, count: Math.max(p.count, counts[i]) })));
  }, [recentReports]);

  const maxCount = Math.max(...chartData.map((d) => d.count), 1);

  const handleSubmitReport = async (isDraft) => {
    if (!form.content.trim()) {
      setMsg("Vui lòng nhập nội dung báo cáo.");
      return;
    }
    if (!isDraft && !window.confirm("Sau khi chốt, báo cáo sẽ không thể sửa đổi. Bạn có chắc chắn?")) return;

    setSubmitting(true);
    setMsg("");
    try {
      await createReport({
        raceId: selectedRaceId,
        details: form.content,
        incidents: form.notes,
        isDraft: isDraft 
      });
      setMsg(isDraft ? "Đã lưu nháp thành công!" : "Đã chốt báo cáo thành công!");
      
      const newReport = { raceId: selectedRaceId, details: form.content, incidents: form.notes, createdAt: new Date().toISOString() };
      setRecentReports((prev) => [newReport, ...prev].slice(0, 10));
      
      if (!isDraft) {
        getRaceReport(selectedRaceId).then((d) => setExistingReport(d)).catch(() => {});
      }
    } catch (e) {
      setMsg("Lỗi: " + (e.message || ""));
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmitResult = async (e) => {
    e.preventDefault();
    if (!canEditRanking) return;
    
    const invalid = Object.values(timeData).some(d => d.status === "Completed" && (!d.mm && !d.ss && !d.ms));
    if (invalid) {
      setResultMsg("Vui lòng nhập thời gian cho tất cả ngựa hoàn thành cuộc đua.");
      return;
    }

    if (!window.confirm("Xác nhận chốt kết quả thời gian? Hệ thống sẽ tự động xếp hạng và gửi lên Admin.")) return;

    setResultSubmitting(true);
    setResultMsg("");
    try {
      const payloadRankings = resultEntries.map((entry) => {
        const hId = entry.horseId || entry.HorseId;
        const rankInfo = rankings[hId];
        return {
          horseId: hId,
          position: typeof rankInfo.rank === "number" ? rankInfo.rank : 99,
          timeTaken: rankInfo.baseMs ? (rankInfo.baseMs / 1000) : null,
          status: timeData[hId].status
        };
      });

      await submitRaceResult(selectedRaceId, { rankings: payloadRankings });
      setResultMsg("Kết quả đã được gửi thành công!");
      loadAssignments(true);
    } catch (err) {
      setResultMsg("Lỗi: " + (err.message || ""));
    } finally {
      setResultSubmitting(false);
    }
  };

  const currentRaceName =
    assignments.find((a) => (a.raceId || a.RaceId) === selectedRaceId)?.raceName ||
    assignments.find((a) => (a.raceId || a.RaceId) === selectedRaceId)?.RaceName ||
    "Cuộc đua đã chọn";

  return (
    <div className="rr-wrap">
      <div className="rr-header">
        <div>
          <h1 className="rr-title">Báo cáo & Nhập kết quả</h1>
          <p className="rr-sub">Nhập thời gian về đích, hệ thống sẽ tự động xếp hạng cuộc đua.</p>
        </div>
        <button className="rr-btn rr-btn--outline" onClick={() => loadAssignments(true)} disabled={refreshing}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={refreshing ? "rr-spin" : ""}>
            <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.92-10.26l5.58 3.69" />
          </svg>
          {refreshing ? "Đang tải..." : "Làm mới"}
        </button>
      </div>

      <div className="rr-type-grid">
        {REPORT_TYPES.map((rt) => (
          <div key={rt.id} className={`rr-type-card${reportType === rt.id ? " rr-type-card--active" : ""}`} onClick={() => setReportType(rt.id)}>
            <span className="rr-type-icon">{rt.icon}</span>
            <div>
              <span className="rr-type-label">{rt.label}</span>
              <span className="rr-type-sub">{rt.sub}</span>
            </div>
          </div>
        ))}
      </div>

      <div className="rr-grid">
        <div className="rr-left">
          <div className="rr-card rr-card-dark">
            <h3 className="rr-card-title">Chọn cuộc đua</h3>
            {loading ? (
              <p className="rr-muted">Đang tải...</p>
            ) : assignments.length === 0 ? (
              <p className="rr-muted">Chưa có phân công nào.</p>
            ) : (
              <select value={selectedRaceId} onChange={(e) => setSelectedRaceId(e.target.value)} className="rr-select">
                <option value="">Chọn cuộc đua</option>
                {assignments.map((a) => (
                  <option key={a.id || a.Id || a.raceId} value={a.raceId || a.RaceId}>
                    {a.raceName || a.RaceName || a.matchName || "Cuộc đua"}
                  </option>
                ))}
              </select>
            )}
          </div>

          {!selectedRaceId && (
            <div className="rr-card rr-card-dark">
              <div className="rr-empty-state">
                <ReportEmptySVG />
                <p>Vui lòng chọn một cuộc đua ở trên để bắt đầu viết báo cáo và chốt kết quả.</p>
              </div>
            </div>
          )}

          {/* ── AUTO RANKING TABLE ── */}
          {selectedRaceId && (
            <form className="rr-card rr-card-dark rr-form" onSubmit={handleSubmitResult} style={{ marginTop: 16, borderTop: "3px solid var(--rr-gold-dim)" }}>
              <h3 className="rr-card-title" style={{ color: "var(--rr-gold)" }}>Nhập kết quả cuộc đua</h3>
              {resultMsg && <div className={`rr-msg ${resultMsg.includes("Lỗi") ? "rr-msg--err" : "rr-msg--ok"}`}>{resultMsg}</div>}
              
              <div style={{ background: "rgba(255,255,255,0.05)", padding: "12px", borderRadius: "8px", marginBottom: "12px" }}>
                <p className="rr-muted" style={{ fontWeight: 600, color: canEditRanking ? "var(--rr-green)" : "var(--rr-amber)", margin: 0 }}>
                  Trạng thái: {RACE_STATUS_LABEL[currentRaceStatus] ?? currentRaceStatus} 
                  {currentResultStatus && ` · Kết quả: ${currentResultStatus === "official" ? "Chính thức" : currentResultStatus === "provisional" ? "Tạm thời" : currentResultStatus}`}
                </p>
                {currentRejectedReason && !isOfficial && (
                  <p className="rr-muted" style={{ marginTop: "8px", color: "var(--rr-red)", margin: "8px 0 0 0" }}>Bị từ chối: {currentRejectedReason}</p>
                )}
              </div>

              {resultEntries.length === 0 ? (
                <p className="rr-muted">Chưa có ngựa tham gia.</p>
              ) : (
                <div style={{ overflowX: "auto" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13, textAlign: "left", whiteSpace: "nowrap" }}>
                    <thead>
                      <tr style={{ borderBottom: "1px solid rgba(255,255,255,0.1)", color: "var(--rr-muted)" }}>
                        <th style={{ padding: "10px" }}>Ngựa</th>
                        <th style={{ padding: "10px" }}>Trạng thái</th>
                        <th style={{ padding: "10px" }}>Thời gian hoàn thành</th>
                        <th style={{ padding: "10px" }}>Phạt (+giây)</th>
                        <th style={{ padding: "10px", textAlign: "center" }}>Thành tích</th>
                        <th style={{ padding: "10px", textAlign: "center", color: "var(--rr-gold)" }}>Hạng</th>
                      </tr>
                    </thead>
                    <tbody>
                      {resultEntries.map((entry) => {
                        const hId = entry.horseId || entry.HorseId;
                        const data = timeData[hId] || {};
                        const rankInfo = rankings[hId] || {};
                        const isDisabledLine = data.status !== "Completed";
                        const isHealthFailed = failedHealthCheckHorseIds.has(hId);

                        return (
                          <tr key={hId} style={{ borderBottom: "1px solid rgba(255,255,255,0.05)" }}>
                            <td style={{ padding: "10px", fontWeight: 500 }}>
                              {entry.horseName || entry.HorseName}
                              {isHealthFailed && <span style={{ display: "block", color: "var(--rr-red)", fontSize: 11 }}>Không đạt yêu cầu sức khỏe</span>}
                            </td>
                            <td style={{ padding: "10px" }}>
                              {(() => {
                                const isAutoDSQ = data.isAutoDSQ === true;
                                return (
                                  <select 
                                    className="rr-select" 
                                    style={{ 
                                      padding: "6px", fontSize: 12, minWidth: "100px", 
                                      opacity: (!canEditRanking || isAutoDSQ) ? 0.8 : 1,
                                      background: isAutoDSQ ? "rgba(201,105,90,0.15)" : "var(--rr-surface-2)",
                                      color: isAutoDSQ ? "var(--rr-danger)" : "inherit",
                                      cursor: (!canEditRanking || isAutoDSQ) ? "not-allowed" : "pointer"
                                    }}
                                    value={data.status} 
                                    onChange={(e) => handleStatusChange(hId, e.target.value)} 
                                    disabled={!canEditRanking || isAutoDSQ}
                                    title={isAutoDSQ ? "Ngựa bị loại do vi phạm." : ""}
                                  >
                                    <option value="Completed">Hoàn thành</option>
                                    <option value="DNF">Bỏ cuộc</option>
                                    
                                    {isAutoDSQ && (
                                      <option value="DSQ">Bị loại</option>
                                    )}
                                  </select>
                                );
                              })()}
                            </td>
                            <td style={{ padding: "10px" }}>
                              <div style={{ display: "flex", gap: "4px", alignItems: "center", opacity: isDisabledLine || !canEditRanking ? 0.4 : 1, pointerEvents: isDisabledLine || !canEditRanking ? "none" : "auto" }}>
                                <input className="rr-select" style={{ width: 40, padding: 6, textAlign: "center" }} placeholder="MM" value={data.mm} onChange={(e) => handleTimeChange(hId, 'mm', e.target.value)} disabled={!canEditRanking} />:
                                <input className="rr-select" style={{ width: 40, padding: 6, textAlign: "center" }} placeholder="SS" value={data.ss} onChange={(e) => handleTimeChange(hId, 'ss', e.target.value)} disabled={!canEditRanking} />.
                                <input className="rr-select" style={{ width: 50, padding: 6, textAlign: "center" }} placeholder="ms" value={data.ms} onChange={(e) => handleTimeChange(hId, 'ms', e.target.value)} disabled={!canEditRanking} />
                              </div>
                            </td>
                            <td style={{ padding: "10px" }}>
                              <input 
                                className="rr-select" 
                                style={{ 
                                  width: 60, padding: 6, 
                                  opacity: isDisabledLine ? 0.3 : !canEditRanking ? 0.6 : 1,
                                  background: Number(data.penalty) > 0 ? "rgba(201,105,90,0.2)" : "var(--rr-surface-2)",
                                  cursor: "not-allowed"
                                }} 
                                placeholder="Giây" 
                                value={data.penalty} 
                                readOnly
                                title={Number(data.penalty) > 0 ? "Đã đồng bộ từ biên bản Vi phạm" : ""}
                              />
                            </td>
                            <td style={{ padding: "10px", textAlign: "center", fontWeight: 600, color: "var(--rr-text)" }}>
                              {rankInfo.displayTime}
                            </td>
                            <td style={{ padding: "10px", textAlign: "center", fontWeight: 700, fontSize: 16 }}>
                              {isHealthFailed && rankInfo.rank === 1 ? (
                                <span style={{ color: "var(--rr-red)", fontSize: 12 }}>Cấm hạng 1</span>
                              ) : (
                                <span style={{ color: typeof rankInfo.rank === 'number' ? "var(--rr-gold)" : "var(--rr-muted)" }}>
                                  {rankInfo.rank}
                                </span>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
              
              {isPendingApproval ? (
                <button type="button" className="rr-submit-btn" disabled style={{ background: "var(--rr-surface-2)", color: "var(--rr-muted)", marginTop: 12 }}>
                  Đang chờ Admin duyệt kết quả
                </button>
              ) : isOfficial ? (
                <button type="button" className="rr-submit-btn" disabled style={{ background: "rgba(112,139,104,0.15)", color: "var(--hr-success)", border: "1px solid rgba(112,139,104,0.4)", marginTop: 12 }}>
                  Kết quả đã được công bố
                </button>
              ) : (
                <button type="submit" className="rr-submit-btn" disabled={resultSubmitting || !canEditRanking} style={{ background: "var(--rr-green)", color: "#fff", marginTop: 12 }}>
                  {resultSubmitting ? "Đang gửi..." : "Chốt kết quả"}
                </button>
              )}
            </form>
          )}

          {/* ── DRAFT REPORT ── */}
          {selectedRaceId && (
            <div className="rr-card rr-card-dark rr-form" style={{ marginTop: 16 }}>
              <h3 className="rr-card-title">Báo cáo cuộc đua {isReportOfficial ? "(Đã chốt)" : "(Nháp)"}</h3>
              {msg && <div className={`rr-msg ${msg.includes("Lỗi") ? "rr-msg--err" : "rr-msg--ok"}`}>{msg}</div>}
              
              <div className="rr-field">
                <label>Nội dung chi tiết</label>
                <textarea 
                  value={form.content} 
                  onChange={(e) => setForm((p) => ({ ...p, content: e.target.value }))} 
                  placeholder="Mô tả diễn biến, lý do phạt..." 
                  disabled={isReportOfficial}
                  rows={5}
                />
              </div>
              <div className="rr-field">
                <label>Sự cố / Ghi chú</label>
                <textarea 
                  value={form.notes} 
                  onChange={(e) => setForm((p) => ({ ...p, notes: e.target.value }))} 
                  style={{ minHeight: "60px" }} 
                  disabled={isReportOfficial}
                  rows={3}
                />
              </div>

              {!isReportOfficial && (
                <div style={{ display: "flex", gap: "10px", marginTop: "12px" }}>
                  <button type="button" className="rr-btn rr-btn--outline" style={{ flex: 1 }} onClick={() => handleSubmitReport(true)} disabled={submitting}>
                    Lưu nháp
                  </button>
                  <button type="button" className="rr-submit-btn" style={{ flex: 1, margin: 0 }} onClick={() => handleSubmitReport(false)} disabled={submitting}>
                    Chốt báo cáo
                  </button>
                </div>
              )}
            </div>
          )}
        </div>

        {/* RIGHT — Chart + Recent Reports */}
        <div className="rr-right">
          <div className="rr-card rr-card-dark">
            <h3 className="rr-card-title">Phân bố báo cáo</h3>
            <p className="rr-card-sub">6 tháng gần nhất</p>
            <div className="rr-chart">
              {chartData.map((item) => (
                <div key={item.month} className="rr-chart-col">
                  <span className="rr-chart-val">{item.count}</span>
                  <div className="rr-chart-bar-wrap">
                    <div className="rr-chart-bar" style={{ height: `${(item.count / maxCount) * 100}%` }} />
                  </div>
                  <span className="rr-chart-label">{item.month}</span>
                </div>
              ))}
            </div>
          </div>

          <div className="rr-card rr-card-dark">
            <h3 className="rr-card-title">Báo cáo gần đây</h3>
            {recentReports.length === 0 ? (
              <p className="rr-muted">Chưa có báo cáo nào được gửi.</p>
            ) : (
              <div className="rr-recent-list">
                {recentReports.map((r, idx) => (
                  <div key={idx} className="rr-recent-item">
                    <div className="rr-recent-head">
                      <span className="rr-recent-race">{r.raceName || `Cuộc đua`}</span>
                      <span className="rr-recent-date">
                        {r.createdAt ? new Date(r.createdAt).toLocaleDateString("vi-VN") : "-"}
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